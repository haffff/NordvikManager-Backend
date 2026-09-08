using DndOnePlaceManager.Application.Commands.Properties;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties.CustomLayers
{
    public class CustomLayerLayoutTests
    {
        [Fact]
        public void Renormalize_EmptyList_ReturnsEmpty()
        {
            var result = CustomLayerLayout.Renormalize(new List<(string, int?)>(), (0, 100));

            Assert.Empty(result);
        }

        [Fact]
        public void Renormalize_SingleNewItem_PlacesAtMidpoint()
        {
            var result = CustomLayerLayout.Renormalize(new List<(string, int?)> { ("a", null) }, (0, 100));

            Assert.Single(result);
            Assert.Equal(("a", 50), result[0]);
        }

        [Fact]
        public void Renormalize_TwoNewItems_EvenlySpacedInOrder()
        {
            var result = CustomLayerLayout.Renormalize(new List<(string, int?)> { ("a", null), ("b", null) }, (0, 100));

            Assert.Equal(2, result.Count);
            Assert.Equal(("a", 33), result[0]);
            Assert.Equal(("b", 66), result[1]);
        }

        [Fact]
        public void Renormalize_AlreadyEvenlySpaced_ReturnsNoChanges()
        {
            // Same values Renormalize itself would have produced for these two slots —
            // re-running against an unchanged membership must be a no-op (no needless
            // element-reassignment churn on layers nobody touched).
            var result = CustomLayerLayout.Renormalize(new List<(string, int?)> { ("a", 33), ("b", 66) }, (0, 100));

            Assert.Empty(result);
        }

        [Fact]
        public void Renormalize_OneValueDrifted_OnlyReturnsTheDriftedRow()
        {
            var result = CustomLayerLayout.Renormalize(new List<(string, int?)> { ("a", 33), ("b", 999) }, (0, 100));

            Assert.Single(result);
            Assert.Equal(("b", 66), result[0]);
        }

        [Theory]
        [InlineData(CustomLayerLayout.MapLayerId, CustomLayerLayout.MapLayerId, CustomLayerLayout.GridLayerId)]
        [InlineData(CustomLayerLayout.GridLayerId, CustomLayerLayout.GridLayerId, CustomLayerLayout.TokenLayerId)]
        [InlineData(CustomLayerLayout.TokenLayerId, CustomLayerLayout.TokenUiLayerId, CustomLayerLayout.TopBandCeiling)]
        [InlineData(CustomLayerLayout.TokenUiLayerId, CustomLayerLayout.TokenUiLayerId, CustomLayerLayout.TopBandCeiling)]
        [InlineData(250, CustomLayerLayout.TokenUiLayerId, CustomLayerLayout.TopBandCeiling)]
        public void BandFor_ResolvesExpectedBand(int reference, int expectedLo, int expectedHi)
        {
            var band = CustomLayerLayout.BandFor(reference);

            Assert.Equal((expectedLo, expectedHi), band);
        }

        [Theory]
        // TokenLayerId and TokenUiLayerId are the two cases that deliberately differ
        // from BandFor: "immediately below Token" must land inside Grid-Token (its
        // Hi), and "immediately below TokenUi" (leaving the top band entirely) must
        // also land inside Grid-Token — skipping the dead 100-110 gap — rather than
        // jumping (or bouncing straight back) to the top band the way "insert above
        // X" does for Add.
        [InlineData(CustomLayerLayout.GridLayerId, CustomLayerLayout.MapLayerId, CustomLayerLayout.GridLayerId)]
        [InlineData(CustomLayerLayout.TokenLayerId, CustomLayerLayout.GridLayerId, CustomLayerLayout.TokenLayerId)]
        [InlineData(CustomLayerLayout.TokenUiLayerId, CustomLayerLayout.GridLayerId, CustomLayerLayout.TokenLayerId)]
        [InlineData(CustomLayerLayout.MapLayerId, CustomLayerLayout.MapLayerId, CustomLayerLayout.GridLayerId)]
        public void BandBelow_ResolvesExpectedBand(int upperNeighbor, int expectedLo, int expectedHi)
        {
            var band = CustomLayerLayout.BandBelow(upperNeighbor);

            Assert.Equal((expectedLo, expectedHi), band);
        }
    }
}
