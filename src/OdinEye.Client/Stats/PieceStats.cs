namespace OdinEye.Client.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ODINEYE-38: the game already keeps an exact, cumulative, per-character
    // count of every piece placed (PlayerProfile.PlayerStats.
    // m_piecesPlacedStats, keyed by Piece.m_name, incremented on every
    // successful placement in Player.TryPlacePiece). These helpers turn that
    // into submission keys and the derived "stations and upgrades" total.
    // Pure, so they can be tested without a game.
    public static class PieceStats
    {
        public const string PiecePlacedPrefix = "PiecePlaced:";
        public const string StationTotalKey = "Derived:StationOrUpgradePlaced";

        // "PiecePlaced:<Piece.m_name>" for every piece with a sane count.
        public static Dictionary<string, float> Prefix(IEnumerable<KeyValuePair<string, float>> placed)
        {
            var result = new Dictionary<string, float>();
            foreach (var kv in placed ?? Enumerable.Empty<KeyValuePair<string, float>>())
            {
                if (!string.IsNullOrEmpty(kv.Key) && !float.IsNaN(kv.Value) && !float.IsInfinity(kv.Value) && kv.Value >= 0f)
                {
                    result[PiecePlacedPrefix + kv.Key] = kv.Value;
                }
            }

            return result;
        }

        // The sum of placements over every piece that is a crafting station or
        // a station upgrade (chopping block, tanning rack, forge extensions,
        // ...). The set of names comes from a component scan of the game's
        // prefabs, so pieces added by future Valheim updates count without
        // anyone maintaining a list.
        public static float StationTotal(IEnumerable<KeyValuePair<string, float>> placed, ISet<string> stationNames)
        {
            if (placed == null || stationNames == null || stationNames.Count == 0)
            {
                return 0f;
            }

            return placed
                .Where(kv => stationNames.Contains(kv.Key) && !float.IsNaN(kv.Value) && !float.IsInfinity(kv.Value) && kv.Value > 0f)
                .Sum(kv => kv.Value);
        }
    }
}
