namespace OdinEye.Client.Stats
{
    using OdinEye.Client.Counters;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ODINEYE-36/38: adds what this mod keeps to what the game reports --
    // the "Custom:" counters and the "Derived:" totals -- so they travel in
    // the same submission. Pure apart from the inner source, so it is
    // testable with a fake one.
    public sealed class CounterAugmentedStatsSource : IPlayerStatsSource
    {
        private readonly IPlayerStatsSource inner;
        private readonly CustomCounterStore store;
        private readonly Func<ISet<string>> stationNames;

        public CounterAugmentedStatsSource(IPlayerStatsSource inner, CustomCounterStore store, Func<ISet<string>> stationNames)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.stationNames = stationNames ?? (() => null);
        }

        public IReadOnlyDictionary<string, float> GetStats()
        {
            var stats = new Dictionary<string, float>();
            foreach (var kv in inner.GetStats())
            {
                stats[kv.Key] = kv.Value;
            }

            if (stats.Count == 0)
            {
                return stats; // no profile loaded: send nothing rather than only our counters
            }

            // The total of crafting stations and their upgrade pieces placed,
            // kept as a high-water mark: the set of station names is found by
            // scanning the game's prefabs, and a total that could shrink (say
            // the scan came back short one day) must never be sent lower than
            // before -- the server rejects a submission that lowers a value.
            var placed = stats
                .Where(kv => kv.Key.StartsWith(PieceStats.PiecePlacedPrefix, StringComparison.Ordinal))
                .Select(kv => new KeyValuePair<string, float>(kv.Key.Substring(PieceStats.PiecePlacedPrefix.Length), kv.Value));
            var total = PieceStats.StationTotal(placed, stationNames());
            if (total > 0f)
            {
                store.RaiseTo(PieceStats.StationTotalKey, total);
            }

            foreach (var kv in store.Snapshot())
            {
                stats[kv.Key] = kv.Value;
            }

            return stats;
        }
    }
}
