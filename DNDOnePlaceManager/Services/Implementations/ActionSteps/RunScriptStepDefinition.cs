using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using Jint;
using Jint.Runtime;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
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
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
        private const long MemoryLimitBytes = 16 * 1024 * 1024;
        private const int MaxStatements = 100_000;
        private const int MaxRecursion = 64;
        private const uint MaxArrayLength = 100_000;
        // Timeout/statement limits are only checked between statements, so a single
        // catastrophic regex would otherwise run unbounded inside one built-in call.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        public string Name => "Run Script";
        public string Value => "RunScript";
        public string Category => "Script";
        public string Description => "Runs a short sandboxed JavaScript snippet for calculations, lists, text and JSON. " +
            "Reads variables through `vars`, returns new values. It cannot change the game directly — use the other steps for that.";
        public string? Summary => "Script {Script}[ → {Output}]";
        public Type DataType => typeof(RunScriptStepData);

        // JS source must reach the engine verbatim (`%` is the modulo operator, and variables come in via `vars`).
        private static readonly string[] DeferredArgs = { nameof(RunScriptStepData.Script) };
        public IReadOnlyCollection<string> DeferredArguments => DeferredArgs;

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RunScriptStepData>();
            if (string.IsNullOrWhiteSpace(stepData.Script))
                throw new ActionProcessException("RunScript: 'Script' argument is required.");

            var result = Run(stepData.Script, variables);

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

            return Task.CompletedTask;
        }

        /// <summary>Executes the script and returns its JSON-converted return value (null for undefined/null).</summary>
        private static JToken Run(string script, Dictionary<string, object> variables)
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

            try
            {
                // The prefix shares line 1 with the script so reported line numbers match the user's
                // source; the newline before the suffix stops a trailing `// comment` swallowing it.
                var json = engine.Evaluate(
                    "(function () { const vars = Object.freeze(JSON.parse(__vars)); const __r = (function (vars) { " +
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

        // Primitives come back as .NET values (so %var% and If/Calculate keep working);
        // objects/arrays stay as JTokens, which %v:% paths, ForEach and FilterCollection understand.
        private static object FromJson(JToken token) => token switch
        {
            null => null,
            JValue v => v.Value,
            _ => token,
        };
    }
}
