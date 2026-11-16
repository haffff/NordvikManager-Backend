using NordvikManager.Application.Commands.Addons;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // A dependency's "version" is the lowest version that works.
    public class AddonVersionTests
    {
        // No requirement accepts anything; a requirement needs an installed version at least as
        // new. Versions that aren't numbers can only match exactly.
        [Theory]
        [InlineData("0.0.4", "0.0.4")]
        [InlineData("0.0.5", "0.0.4")]
        [InlineData("1.0.0", "0.9.9")]
        [InlineData("1.10.0", "1.9.0")]
        [InlineData("1.0", "1.0.0")]
        [InlineData("1.0.0", "1")]
        [InlineData("2.0.0-beta", "1.9")]
        [InlineData("0.0.1", null)]
        [InlineData(null, null)]
        [InlineData(null, " ")]
        [InlineData("nightly", "nightly")]
        public void Satisfies(string? installed, string? required)
            => Assert.True(AddonVersion.Satisfies(installed, required));

        [Theory]
        [InlineData("0.0.3", "0.0.4")]
        [InlineData("1.9.0", "1.10.0")]
        [InlineData("0.9", "1")]
        [InlineData(null, "1.0")]
        [InlineData("nightly", "1.0")]
        [InlineData("1.0", "nightly")]
        public void DoesNotSatisfy(string? installed, string? required)
            => Assert.False(AddonVersion.Satisfies(installed, required));

    }
}
