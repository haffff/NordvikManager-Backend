using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
{
    // RunScript replaces the planned math/collection/string/JSON blocks with a sandboxed
    // Jint snippet. These pin down the variable round-trip and, above all, the sandbox limits
    // — scripts run on the server inside the game lobby.
    public class RunScriptStepDefinitionTests
    {
        private readonly IMediator _mediator = Mock.Of<IMediator>();
        private readonly RunScriptStepDefinition _step = new();

        private Task Run(Dictionary<string, object> vars, string script, string output = null) =>
            _step.Execute(_mediator, vars, null, new ActionStep
            {
                Type = "RunScript",
                Data = JObject.FromObject(new { Script = script, Output = output }),
            });

        [Fact]
        public async Task ReturnedObject_KeysBecomeVariables()
        {
            var vars = new Dictionary<string, object> { ["hp"] = 10, ["damage"] = 13 };

            await Run(vars, "return { hp: Math.max(0, vars.hp - vars.damage), downed: vars.damage >= vars.hp };");

            Assert.Equal(0L, vars["hp"]);
            Assert.Equal(true, vars["downed"]);
        }

        [Fact]
        public async Task Output_StoresWholeReturnValue_ArraysStayCollections()
        {
            var vars = new Dictionary<string, object>
            {
                ["cards"] = new List<object> { new { Name = "Orc", Hp = 0 }, new { Name = "Elf", Hp = 4 } },
            };

            await Run(vars, "return vars.cards.filter(c => c.Hp > 0).map(c => c.Name);", output: "alive");

            var alive = Assert.IsAssignableFrom<IEnumerable<object>>(vars["alive"]);
            Assert.Equal(new[] { "Elf" }, alive.Select(x => x.ToString()));
        }

        [Fact]
        public async Task ModuloOperator_AndJsonStringVariables_Work()
        {
            var vars = new Dictionary<string, object> { ["body"] = "{\"results\":[1,2,3,4]}" };

            await Run(vars, "const r = JSON.parse(vars.body).results;\nreturn { evens: r.filter(n => n % 2 === 0).length };");

            Assert.Equal(2L, vars["evens"]);
        }

        [Fact]
        public async Task NoReturn_LeavesVariablesUnchanged()
        {
            var vars = new Dictionary<string, object> { ["x"] = 1 };

            await Run(vars, "const y = vars.x + 1; // no return");

            Assert.Single(vars);
        }

        [Fact]
        public async Task VarsAreReadOnly()
        {
            var vars = new Dictionary<string, object> { ["x"] = 1 };

            await Assert.ThrowsAsync<ActionProcessException>(() => Run(vars, "vars.x = 5;"));
        }

        // Scripts that fail, and what the error says (when it matters): a bad return, the
        // sandbox limits (time, recursion, memory, no eval), and errors in the script itself.
        [Theory]
        [InlineData("NonObjectReturnWithoutOutput", "return 5;", "")]
        [InlineData("InfiniteLoop_IsStopped", "while (true) {}", "stopped")]
        [InlineData("DeepRecursion_IsStopped", "function f(n) { return f(n + 1); } return { v: f(0) };", "")]
        [InlineData("HugeArray_IsRejected", "return { n: new Array(1e7).fill(0).length };", "")]
        [InlineData("Eval_IsDisabled", "return { v: eval('1 + 1') };", "")]
        [InlineData("RuntimeError_ReportsUserLineNumber", "const a = 1;\nundefinedThing.x;", "line 2")]
        [InlineData("SyntaxError_IsReported", "return { ;", "SyntaxError")]
        public async Task FailingScript_Throws(string because, string script, string messageHas)
        {
            _ = because;
            var ex = await Assert.ThrowsAsync<ActionProcessException>(() => Run(new Dictionary<string, object>(), script));
            Assert.Contains(messageHas, ex.Message);
        }

        [Fact]
        public async Task CatastrophicRegex_IsStopped()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new Dictionary<string, object>(), "return { m: /^(a+)+$/.test('a'.repeat(40) + '!') };"));
            Assert.True(sw.Elapsed.TotalSeconds < 10);
        }

        [Fact]
        public async Task ReturningFunction_WithOutput_StoresNull()
        {
            var vars = new Dictionary<string, object>();

            await Run(vars, "return () => 1;", output: "f");

            Assert.Null(vars["f"]);
        }

        [Fact]
        public async Task NoClrAccess()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new Dictionary<string, object>(), "return { t: System.IO.File.ReadAllText('x') };"));
            await Assert.ThrowsAsync<ActionProcessException>(() =>
                Run(new Dictionary<string, object>(), "return { t: importNamespace('System') };"));
        }

        [Fact]
        public void Script_IsDeferredFromVariableSubstitution()
        {
            Assert.Contains("Script", _step.DeferredArguments);
        }
    }
}
