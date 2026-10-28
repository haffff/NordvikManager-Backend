using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
{
    // If and ForEach hold their steps inline (Then / Else / Do). Those steps are part of the
    // action: they see and change the same variables, an Exit in them stops the action, and
    // the trace reports them with their path.
    public class ActionBlockTests : ActionServiceTestBase
    {
        private static object[] Steps(params object[] steps) => steps;

        private static string Var(JObject message, string name) => message["variables"]?[name]?.ToString();

        [Fact]
        public async Task Then_SetsAVariable_TheStepsAfterTheIfSeeIt()
        {
            AddAction("a",
                Step("if", "If", new { Condition = "1 = 1", Then = Steps(Step("set", "SetVariable", new { Name = "hp", Value = "5" })) }),
                Step("after", "SetVariable", new { Name = "copy", Value = "%hp%" }));

            var msgs = await Run("a");

            Assert.Equal("5", Var(Last(msgs, "after"), "copy"));
            Assert.Equal("Completed", msgs.Last()["state"]?.ToString());
        }

        [Fact]
        public async Task Else_RunsWhenTheConditionIsFalse()
        {
            AddAction("a",
                Step("if", "If", new
                {
                    Condition = "1 = 2",
                    Then = Steps(Step("t", "SetVariable", new { Name = "branch", Value = "then" })),
                    Else = Steps(Step("e", "SetVariable", new { Name = "branch", Value = "else" })),
                }));

            var msgs = await Run("a");

            Assert.Equal("else", Var(Last(msgs, "if"), "branch"));
            Assert.DoesNotContain(msgs, m => m["stepId"]?.ToString() == "t");
        }

        [Fact]
        public async Task ForEach_Do_AccumulatesIntoAVariableOfTheAction()
        {
            AddAction("a",
                Step("init", "RunScript", new { Script = "return { items: [1, 2, 3], total: 0 };" }),
                Step("loop", "ForEach", new
                {
                    Collection = "items", ItemName = "item",
                    Do = Steps(Step("add", "RunScript", new { Script = "return { total: vars.total + vars.item };" })),
                }));

            var msgs = await Run("a");

            Assert.Equal("6", Var(Last(msgs, "loop"), "total"));
        }

        [Fact]
        public async Task TokensInABlock_SeeWhatEarlierStepsOfTheBlockSet()
        {
            AddAction("a",
                Step("if", "If", new
                {
                    Condition = "1 = 1",
                    Then = Steps(
                        Step("s1", "SetVariable", new { Name = "a", Value = "1" }),
                        Step("s2", "SetVariable", new { Name = "b", Value = "%a%x" })),
                }));

            var msgs = await Run("a");

            Assert.Equal("1x", Var(Last(msgs, "s2"), "b"));
        }

        [Fact]
        public async Task Exit_InABlock_StopsTheWholeAction()
        {
            AddAction("a",
                Step("if", "If", new { Condition = "1 = 1", Then = Steps(Step("x", "Exit", new { Message = "bye" })) }),
                Step("never", "Log", new { Message = "no" }));

            var msgs = await Run("a");

            Assert.Equal("exited", Last(msgs, "x")["status"]?.ToString());
            Assert.DoesNotContain(msgs, m => m["stepId"]?.ToString() == "never");
            Assert.Equal("Exited", msgs.Last()["state"]?.ToString());
        }

        [Fact]
        public async Task FailingStepInABlock_FailsTheIfAndTheAction()
        {
            AddAction("a",
                Step("if", "If", new { Condition = "1 = 1", Then = Steps(Step("bad", "RunScript", new { Script = "throw new Error('broken');" })) }),
                Step("never", "Log", new { Message = "no" }));

            var msgs = await Run("a");

            Assert.Equal("failed", Last(msgs, "bad")["status"]?.ToString());
            Assert.Equal("failed", Last(msgs, "if")["status"]?.ToString());
            Assert.DoesNotContain(msgs, m => m["stepId"]?.ToString() == "never");
            Assert.Equal("Faulted", msgs.Last()["state"]?.ToString());
        }

        [Fact]
        public async Task StepsInBlocks_AreTracedWithTheirPath()
        {
            AddAction("a",
                Step("first", "Log", new { Message = "x" }),
                Step("loop", "ForEach", new
                {
                    Collection = "items", ItemName = "item",
                    Do = Steps(Step("inner", "Log", new { Message = "%item%" })),
                }));
            // items comes from the caller here
            var msgs = await Run("a", new System.Collections.Generic.Dictionary<string, object> { ["items"] = new JArray(1, 2) });

            Assert.Equal("1", Last(msgs, "loop")["stepPath"]?.ToString());
            Assert.Equal("1/Do/0", Last(msgs, "inner")["stepPath"]?.ToString());
            Assert.Equal(2, msgs.Count(m => m["stepId"]?.ToString() == "inner" && m["status"]?.ToString() == "done"));
        }
    }
}
