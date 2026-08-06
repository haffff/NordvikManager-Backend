using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Covers the "Roll Damage"-style follow-up action button added to RollDice's chat
    // broadcast. BuildFollowUpActions is private/static — invoked via reflection rather
    // than exercising the full Execute -> GameLobby.HandleCommand -> chat_push pipeline,
    // since only the args-parsing logic is new/untested here.
    public class RollDiceStepDefinitionTests
    {
        private static ActionItemTemplate[] BuildFollowUpActions(RollDiceStepData stepData)
        {
            var method = typeof(RollDiceStepDefinition).GetMethod(
                "BuildFollowUpActions", BindingFlags.NonPublic | BindingFlags.Static);
            return (ActionItemTemplate[])method.Invoke(null, new object[] { stepData });
        }

        [Fact]
        public void BuildFollowUpActions_NoFollowUpActionName_ReturnsNull()
        {
            var result = BuildFollowUpActions(new RollDiceStepData { DiceString = "1d20" });

            Assert.Null(result);
        }

        [Fact]
        public void BuildFollowUpActions_ParsesArgsFromNameEqualsValueLines()
        {
            var stepData = new RollDiceStepData
            {
                DiceString = "1d20+3",
                FollowUpActionName = "dnd5e.roll_damage",
                FollowUpActionLabel = "Roll Damage",
                FollowUpActionArgs = "damage=1d6+3\ndamageType=Slashing\nname=Longsword",
            };

            var result = BuildFollowUpActions(stepData);

            var action = Assert.Single(result);
            Assert.Equal("dnd5e.roll_damage", action.ActionName);
            Assert.Equal("Roll Damage", action.Label);
            Assert.Equal("1d6+3", action.Args["damage"]);
            Assert.Equal("Slashing", action.Args["damageType"]);
            Assert.Equal("Longsword", action.Args["name"]);
        }

        [Fact]
        public void BuildFollowUpActions_NoLabelGiven_DefaultsToRoll()
        {
            var stepData = new RollDiceStepData
            {
                DiceString = "1d20",
                FollowUpActionName = "dnd5e.roll_damage",
            };

            var result = BuildFollowUpActions(stepData);

            Assert.Equal("Roll", Assert.Single(result).Label);
        }

        [Fact]
        public void BuildFollowUpActions_MalformedOrBlankLines_AreSkipped()
        {
            var stepData = new RollDiceStepData
            {
                DiceString = "1d20",
                FollowUpActionName = "dnd5e.roll_damage",
                FollowUpActionArgs = "\n  \ndamage=1d6\nnotAKeyValuePair\n=noKey",
            };

            var result = BuildFollowUpActions(stepData);

            var args = Assert.Single(result).Args;
            Assert.Single(args);
            Assert.Equal("1d6", args["damage"]);
        }
    }
}
