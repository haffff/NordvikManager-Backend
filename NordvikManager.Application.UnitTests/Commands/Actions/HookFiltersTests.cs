using NordvikManager.Enums;

namespace NordvikManager.Application.UnitTests.Commands.Actions
{
    public class HookFiltersTests
    {
        [Theory]
        [InlineData("poll, vote", "poll, vote")]
        [InlineData("  /Poll   VOTE ", "poll, vote")]
        [InlineData("poll,vote,poll", "poll, vote")]
        [InlineData("roll-init my_cmd", "roll-init, my_cmd")]
        public void Normalize_ChatCommand_TidiesTheCommandList(string filter, string expected)
        {
            var (value, error) = HookFilters.Normalize(Hook.ChatCommand, filter);

            Assert.Null(error);
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" , ")]
        public void Normalize_ChatCommand_RequiresTheCommands(string? filter)
        {
            var (_, error) = HookFilters.Normalize(Hook.ChatCommand, filter);

            Assert.NotNull(error);
            Assert.Contains("e.g. poll, vote", error);
        }

        [Theory]
        [InlineData("cast!")]
        [InlineData("café")]
        [InlineData("x23456789012345678901234567890123")]
        [InlineData("//poll")]
        public void Normalize_ChatCommand_RefusesBadNames(string filter)
        {
            var (_, error) = HookFilters.Normalize(Hook.ChatCommand, filter);

            Assert.NotNull(error);
        }

        [Fact]
        public void Normalize_ChatCommand_RefusesMoreThanTwentyCommands()
        {
            var filter = string.Join(",", Enumerable.Range(0, 21).Select(i => "c" + i));

            Assert.NotNull(HookFilters.Normalize(Hook.ChatCommand, filter).Error);
        }

        [Theory]
        [InlineData("r")]
        [InlineData("/roll")]
        [InlineData("poll, gm")]
        [InlineData("HELP")]
        public void Normalize_ChatCommand_RefusesBuiltInCommands(string filter)
        {
            var (_, error) = HookFilters.Normalize(Hook.ChatCommand, filter);

            Assert.NotNull(error);
            Assert.Contains("built in", error);
        }

        [Theory]
        [InlineData(Hook.None)]
        [InlineData(Hook.PropertyUpdate)]
        [InlineData(Hook.ChatMessage)]
        public void Normalize_OtherHooks_TakeNoFilter(Hook hook)
        {
            Assert.Equal((null, null), HookFilters.Normalize(hook, null));
            Assert.Equal((null, null), HookFilters.Normalize(hook, "  "));
            Assert.NotNull(HookFilters.Normalize(hook, "hp").Error);
        }

        [Theory]
        [InlineData("poll, vote", "vote", true)]
        [InlineData("poll, vote", "VOTE", true)]
        [InlineData("poll, vote", "cast", false)]
        [InlineData("", "cast", false)]
        [InlineData(null, "cast", false)]
        public void Accepts_ChatCommand_OnlyListedCommands(string? filter, string command, bool expected)
        {
            Assert.Equal(expected, HookFilters.Accepts(Hook.ChatCommand, filter, command));
        }

        [Fact]
        public void Accepts_OtherHooks_Everything()
        {
            Assert.True(HookFilters.Accepts(Hook.PropertyUpdate, null, ""));
            Assert.True(HookFilters.Accepts(Hook.Load, "", "anything"));
        }

        [Fact]
        public void HookInfo_DescribesTheFilterOfChatCommandOnly()
        {
            var hooks = HookInfo.GetAll();
            var chat = hooks.Single(h => h.Value == (int)Hook.ChatCommand);

            Assert.True(chat.FilterRequired);
            Assert.False(string.IsNullOrWhiteSpace(chat.Filter));
            Assert.All(hooks.Where(h => h.Value != (int)Hook.ChatCommand), h =>
            {
                Assert.True(string.IsNullOrEmpty(h.Filter), h.Key);
                Assert.False(h.FilterRequired, h.Key);
            });
        }

        [Fact]
        public void BuiltInChatCommands_AreTheChatHandlersOwn()
        {
            foreach (var name in new[] { "r", "roll", "gr", "w", "gm", "help" })
                Assert.Contains(name, HookFilters.BuiltInChatCommands);
        }
    }
}
