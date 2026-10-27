using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using Jint;
using Jint.Runtime;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Runs a sandboxed JavaScript snippet (Jint) for pure computation — math, collections,
    /// strings, JSON — instead of chaining low-code blocks. The script sees a JSON snapshot of
    /// the action variables as `vars` and has no access to the game, the server or .NET types;
    /// its return value is written back into the variables.
    /// </summary>
    public class RunScriptStepDefinition : IActionStepDefinition, IDeferredArgumentsStep
    {
        // Scripts run inside the game lobby on the server — keep them small and bounded.
        // Game calls (game.*) take wall time too.
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private const int MaxGameCalls = 200;
        private const long MemoryLimitBytes = 16 * 1024 * 1024;
        private const int MaxStatements = 100_000;
        private const int MaxRecursion = 64;
        private const uint MaxArrayLength = 100_000;
        // Timeout/statement limits are only checked between statements, so a single
        // catastrophic regex would otherwise run unbounded inside one built-in call.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        public string Name => "Run Script";
        public string Value => "RunScript";
        public IReadOnlyList<string> Keywords => new[] { "calculate", "math", "code", "javascript", "list", "filter", "sort", "text", "json", "random pick" };
        public string Category => "Script";
        public string Description => "Runs a short sandboxed JavaScript snippet for calculations, lists, text and JSON. " +
            "Reads variables through `vars`, returns new values. It cannot change the game directly — use the other steps for that.";
        public string? Summary => "Script {Script}[ → {Output}]";
        public Type DataType => typeof(RunScriptStepData);

        // JS source must reach the engine verbatim (`%` is the modulo operator, and variables come in via `vars`).
        private static readonly string[] DeferredArgs = { nameof(RunScriptStepData.Script) };
        public IReadOnlyCollection<string> DeferredArguments => DeferredArgs;

        private static readonly JsonSerializer CamelCase = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver
            {
                // Variable names stay as the action named them; only .NET objects' fields are camelCased.
                NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy { ProcessDictionaryKeys = false },
            },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });

        // On one line: the script's own line numbers stay as written.
        private static readonly string GamePrelude = "const game = (function () {   const out = function (vars, key) { return vars == null ? null : vars[key]; };   const upper = function (o) { const r = {}; Object.keys(o || {}).forEach(function (k) { const v = o[k]; r[k.charAt(0).toUpperCase() + k.slice(1)] = v !== null && typeof v === 'object' ? JSON.stringify(v) : v; }); return r; };   const step = function (type, data) { const r = __game_step(String(type), JSON.stringify(data || {})); return r == null ? {} : JSON.parse(r); };   return Object.freeze({     step: step,     find: function (what, options) { return out(step(\"Find\", Object.assign(upper(options), { What: what, Output: \"__r\" })), \"__r\"); },     getProperty: function (parentId, name, fallback) {       return out(step(\"GetPropertyValue\", { ParentId: String(parentId), PropertyName: name, DefaultValue: fallback == null ? \"\" : String(fallback), Output: \"__r\" }), \"__r\");     },     setProperty: function (parentId, name, value) { step(\"SetProperty\", { ParentId: String(parentId), PropertyName: name, PropertyValue: String(value) }); },     roll: function (formula) { return out(step(\"RollDice\", { DiceString: String(formula), OutputVariable: \"__r\" }), \"__r\"); },     chat: function (text, options) {       options = options || {};       step(\"SendChat\", { Template: \"Text\", Message: String(text), Title: options.title || \"\", Player: options.to || \"\" });     },     log: function (message) { step(\"Log\", { Message: String(message) }); },     moveElement: function (id, x, y) { step(\"MoveElement\", { ElementId: String(id), X: String(x), Y: String(y) }); },     deleteElement: function (id) { step(\"DeleteElement\", { ElementId: String(id) }); },     spawnToken: function (options) { return out(step(\"SpawnToken\", Object.assign(upper(options), { Output: \"__r\" })), \"__r\"); },     showMarker: function (options) { return out(step(\"ShowMarker\", Object.assign(upper(options), { Output: \"__r\" })), \"__r\"); },     addElement: function (options) { return out(step(\"AddElement\", Object.assign(upper(options), { Output: \"__r\" })), \"__r\"); },     createCard: function (options) { return out(step(\"CreateCard\", Object.assign(upper(options), { Output: \"__r\" })), \"__r\"); },   }); })();";

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RunScriptStepData>();
            if (string.IsNullOrWhiteSpace(stepData.Script))
                throw new ActionProcessException("RunScript: 'Script' argument is required.");

            var included = await IncludedAsync(mediator, gameLobby, stepData.Include);
            var result = Run(included + stepData.Script, variables, gameLobby);

            if (!string.IsNullOrWhiteSpace(stepData.Output))
            {
                variables[stepData.Output] = FromJson(result);
            }
            else if (result is JObject obj)
            {
                foreach (var prop in obj.Properties())
                    variables[prop.Name] = FromJson(prop.Value);
            }
            else if (result != null)
            {
                throw new ActionProcessException("RunScript: without 'Output' the script must return an object (e.g. `return { total: 5 };`).");
            }
        }

        // The Include files' source, each on its own lines, ending with a line break so the
        // script itself starts on a line of its own.
        private static async Task<string> IncludedAsync(IMediator mediator, GameLobby? gameLobby, string? include)
        {
            if (string.IsNullOrWhiteSpace(include) || gameLobby == null)
                return string.Empty;
            var source = new System.Text.StringBuilder();
            foreach (var key in include.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var (data, _) = await mediator.Send(new NordvikManager.Application.Commands.Resources.GetResourceDataCommand
                {
                    GameID = gameLobby.GameId, Player = gameLobby.SystemPlayer, Key = key,
                    // Runs as the system: never a player's upload under that key.
                    OnlyFromGameEditors = true,
                });
                if (data == null)
                    throw new ActionProcessException($"RunScript: no script file '{key}' to include.");
                source.Append(System.Text.Encoding.UTF8.GetString(data)).Append('\n');
            }
            return source.ToString();
        }

        /// <summary>Executes the script and returns its JSON-converted return value (null for undefined/null).</summary>
        private static JToken Run(string script, Dictionary<string, object> variables, GameLobby? gameLobby = null)
        {
            var engine = new Jint.Engine(options => options
                .Strict()
                .TimeoutInterval(Timeout)
                .LimitMemory(MemoryLimitBytes)
                .MaxStatements(MaxStatements)
                .LimitRecursion(MaxRecursion)
                .MaxArraySize(MaxArrayLength)
                .RegexTimeoutInterval(RegexTimeout)
                .DisableStringCompilation());

            engine.SetValue("__vars", VariableSnapshot.Build(variables).ToString(Formatting.None));

            // game.*: the action's steps, run through the same definitions as the blocks (as the
            // system player, like them). Synchronous here: the script waits for each call.
            var prelude = "";
            if (gameLobby?.ActionProcessingService != null)
            {
                var calls = 0;
                engine.SetValue("__game_step", new Func<string, string, string?>((type, json) =>
                {
                    if (++calls > MaxGameCalls)
                        throw new ActionProcessException($"RunScript: more than {MaxGameCalls} game calls in one script.");
                    var set = gameLobby.ActionProcessingService
                        .RunStepAsync(type, JObject.Parse(json), variables).GetAwaiter().GetResult();
                    return JToken.FromObject(set, CamelCase).ToString(Formatting.None);
                }));
                prelude = GamePrelude;
            }

            try
            {
                // The prefix shares line 1 with the script so reported line numbers match the user's
                // source; the newline before the suffix stops a trailing `// comment` swallowing it.
                var json = engine.Evaluate(
                    "(function () { " + prelude + " const vars = Object.freeze(JSON.parse(__vars)); const __r = (function (vars) { " +
                    script + "\n})(vars); return __r === undefined ? null : JSON.stringify(__r); })()");

                // JSON.stringify yields undefined for e.g. a returned function — treat it like no return.
                return json.IsString() ? JToken.Parse(json.AsString()) : null;
            }
            catch (JavaScriptException e)
            {
                throw new ActionProcessException($"RunScript: {e.Error} (line {e.Location.Start.Line}).");
            }
            catch (Exception e) when (e is TimeoutException || e is MemoryLimitExceededException ||
                                      e is StatementsCountOverflowException || e is RecursionDepthOverflowException ||
                                      e is System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                throw new ActionProcessException($"RunScript: script stopped — {e.Message}");
            }
            catch (Acornima.ParseErrorException e)
            {
                throw new ActionProcessException($"RunScript: SyntaxError: {e.Message}");
            }
        }

        // Primitives come back as .NET values (so %var% and If conditions keep working);
        // objects/arrays stay as JTokens, which %name.path% tokens and ForEach understand.
        private static object FromJson(JToken token) => token switch
        {
            null => null,
            JValue v => v.Value,
            _ => token,
        };
    }
}
