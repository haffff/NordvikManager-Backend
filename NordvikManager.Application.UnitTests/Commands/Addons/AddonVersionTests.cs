using NordvikManager.Application.Commands.Addons;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // A dependency's "version" is the lowest version that works.
    public class AddonVersionTests
    {
        [Theory]
        [InlineData("0.0.4", "0.0.4")]
        [InlineData("0.0.5", "0.0.4")]
        [InlineData("1.0.0", "0.9.9")]
        [InlineData("1.10.0", "1.9.0")]
        [InlineData("1.0", "1.0.0")]
        [InlineData("1.0.0", "1")]
        [InlineData("2.0.0-beta", "1.9")]
        public void Satisfies_NewEnoughVersion(string installed, string required)
            => Assert.True(AddonVersion.Satisfies(installed, required));

        [Theory]
        [InlineData("0.0.3", "0.0.4")]
        [InlineData("1.9.0", "1.10.0")]
        [InlineData("0.9", "1")]
        public void DoesNotSatisfy_OlderVersion(string installed, string required)
            => Assert.False(AddonVersion.Satisfies(installed, required));

        [Fact]
        public void NoRequiredVersion_AcceptsAnything()
        {
            Assert.True(AddonVersion.Satisfies("0.0.1", null));
            Assert.True(AddonVersion.Satisfies(null, null));
            Assert.True(AddonVersion.Satisfies(null, " "));
        }

        [Fact]
        public void NoInstalledVersion_DoesNotSatisfyARequirement()
            => Assert.False(AddonVersion.Satisfies(null, "1.0"));

        // Versions that aren't numbers can only match exactly.
        [Fact]
        public void NonNumericVersions_MatchOnlyExactly()
        {
            Assert.True(AddonVersion.Satisfies("nightly", "nightly"));
            Assert.False(AddonVersion.Satisfies("nightly", "1.0"));
            Assert.False(AddonVersion.Satisfies("1.0", "nightly"));
        }
    }
}
