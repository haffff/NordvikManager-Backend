using System.Collections.Concurrent;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    // Pure allocation logic for custom (GM-defined) battle-map layers, shared by
    // AddCustomLayerCommandHandler and MoveCustomLayerCommandHandler. No DB access —
    // kept static/pure so it's directly unit-testable.
    //
    // A custom layer's "layerId" doubles as both its position in the stack and the
    // literal value written to ElementModel.Layer for every element placed on it —
    // exactly like the reserved layers already work. Reordering is therefore just
    // "give this layer a new numeric value, then reassign its elements" — no change
    // to the canvas sort comparator (still plain ascending-by-int) is ever needed.
    internal static class CustomLayerLayout
    {
        // Mirrors NordvikManagerFrontEnd's BattleMap/Constants/layers.js RESERVED_LAYERS.
        // Do not renumber — real per-game data already persists at these exact values.
        public const int MapLayerId = -100;
        public const int GridLayerId = 0;
        public const int TokenLayerId = 100;
        public const int TokenUiLayerId = 110;

        // Mirrors layers.js TOP_BAND_CEILING — a purely internal allocation bound,
        // never a real layer. Must start above TokenUiLayerId, not TokenLayerId: the
        // 100-110 gap is deliberately excluded from all three bands below, so a
        // custom layer inserted "at the top" can never land underneath the token
        // health-bar/name-label overlay.
        public const int TopBandCeiling = 1000;

        // The three insertable ranges. (100, 110) is intentionally not a band.
        public static readonly (int Lo, int Hi)[] Bands =
        {
            (MapLayerId, GridLayerId),      // -100 .. 0
            (GridLayerId, TokenLayerId),    // 0 .. 100
            (TokenUiLayerId, TopBandCeiling), // 110 .. 1000
        };

        // Resolves which band a reference value (a reserved anchor, or an existing
        // custom layer's current value) falls into — i.e. "insert immediately above
        // this reference" lands in whichever band starts at or below it. Anything at
        // or above TokenLayerId with no matching band (e.g. exactly TokenLayerId, or
        // a legacy layer already sitting at 200+) falls through to the top band.
        public static (int Lo, int Hi) BandFor(int referenceValue)
        {
            foreach (var band in Bands)
            {
                if (referenceValue >= band.Lo && referenceValue < band.Hi)
                    return band;
            }
            return Bands[^1];
        }

        // Resolves the band that sits immediately BELOW a reference value — used by
        // Move, where the reference is the item that will become the target's new
        // upper neighbor. This is deliberately NOT the same rule as BandFor: BandFor
        // (used for "insert above X", e.g. Add's AfterLayerId) treats a reference of
        // exactly TokenLayerId, or TokenUiLayerId, as "nowhere valid to land, so jump
        // to the top band" — correct when the GM explicitly chose to insert above
        // Token. But when one of those is the value immediately ABOVE a layer that's
        // moving DOWN into this slot (e.g. the layer was alone in the top band and
        // got nudged down one step, so its new upper neighbor is TokenUi itself), the
        // layer must actually descend — landing in the highest real band that sits
        // below the reference (skipping the dead 100-110 gap entirely, since nothing
        // can ever occupy it) — never bounce back into the band it's leaving.
        public static (int Lo, int Hi) BandBelow(int upperNeighborValue)
        {
            foreach (var band in Bands)
            {
                if (upperNeighborValue > band.Lo && upperNeighborValue <= band.Hi)
                    return band;
            }
            for (var i = Bands.Length - 1; i >= 0; i--)
            {
                if (Bands[i].Hi <= upperNeighborValue)
                    return Bands[i];
            }
            return Bands[0];
        }

        // orderedItemsInBand must already be in the desired final bottom-to-top
        // order (the caller is responsible for splicing a new or moved item into
        // the right position among the band's existing members — this function
        // only spaces them evenly, it does not decide order). CurrentLayerId is
        // used purely to detect whether a value actually changed, so unaffected
        // rows aren't written back or swept; pass null for a brand-new item.
        public static List<(string ItemId, int NewLayerId)> Renormalize(
            IReadOnlyList<(string ItemId, int? CurrentLayerId)> orderedItemsInBand,
            (int Lo, int Hi) band)
        {
            var n = orderedItemsInBand.Count;
            var result = new List<(string, int)>();
            for (var k = 0; k < n; k++)
            {
                var newValue = band.Lo + (band.Hi - band.Lo) * (k + 1) / (n + 1);
                var (itemId, currentLayerId) = orderedItemsInBand[k];
                if (currentLayerId != newValue)
                    result.Add((itemId, newValue));
            }
            return result;
        }

        // Serializes Add/Move/Remove against the same game's "customLayers" property —
        // all three read-then-write the same JSON row, so two concurrent callers
        // (two GMs, or a double-click) could otherwise both read stale state before
        // either commits. Same failure mode and fix as ConnectTreeEntriesCommandHandler.
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> GameLocks = new();

        public static SemaphoreSlim LockFor(Guid gameId) =>
            GameLocks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
    }
}
