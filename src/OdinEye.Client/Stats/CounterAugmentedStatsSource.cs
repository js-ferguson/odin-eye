namespace OdinEye.Client.Stats
{
    using OdinEye.Client.Counters;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ODINEYE-36/38: adds what this mod keeps to what the game reports --
    // the "Custom:" counters and the "Derived:" totals -- so they travel in
    // the same submission. Pure apart from the inner source and the two
    // live-state readers, all three injected, so it is testable with fakes.
    public sealed class CounterAugmentedStatsSource : IPlayerStatsSource
    {
        // The Admiral: a gap between two GetStats() calls longer than this
        // (the game suspended, a computer slept) is never credited as time
        // on a boat -- see CounterRules.BoatSecondsToAdd.
        private static readonly TimeSpan MaxPlausibleBoatGap = TimeSpan.FromMinutes(5);

        // Stink Fish: same cap, same reasoning, for time in the Swamp --
        // see CounterRules.SwampSecondsToAdd.
        private static readonly TimeSpan MaxPlausibleSwampGap = TimeSpan.FromMinutes(5);

        private readonly IPlayerStatsSource inner;
        private readonly CustomCounterStore store;
        private readonly Func<ISet<string>> stationNames;
        private readonly Func<float?> currentNorthZ;
        private readonly Func<bool> isInDeepNorth;
        private readonly Func<bool> isOnBoat;
        private readonly Func<bool> isInSwamp;
        private readonly Func<DateTime> nowUtc;
        private DateTime? lastBoatSampleUtc;
        private DateTime? lastSwampSampleUtc;

        public CounterAugmentedStatsSource(IPlayerStatsSource inner, CustomCounterStore store, Func<ISet<string>> stationNames,
            Func<float?> currentNorthZ = null, Func<bool> isInDeepNorth = null,
            Func<bool> isOnBoat = null, Func<bool> isInSwamp = null, Func<DateTime> nowUtc = null)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.stationNames = stationNames ?? (() => null);
            this.currentNorthZ = currentNorthZ ?? (() => null);
            this.isInDeepNorth = isInDeepNorth ?? (() => false);
            this.isOnBoat = isOnBoat ?? (() => false);
            this.isInSwamp = isInSwamp ?? (() => false);
            this.nowUtc = nowUtc ?? (() => DateTime.UtcNow);
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

            // Peter North: a high-water mark of how far north this
            // character has ever been, and a once-ever flag for having
            // reached the Deep North biome. Both only ever grow, same
            // reasoning as the station total above.
            var z = currentNorthZ();
            if (z.HasValue)
            {
                store.RaiseTo(CounterRules.FurthestNorthZKey, CounterRules.NorthDistanceToRaise(z.Value));
            }

            if (isInDeepNorth())
            {
                store.RaiseTo(CounterRules.ReachedDeepNorthKey, 1f);
            }

            // The Admiral: credit the real elapsed time since the LAST time
            // this ran (the 30s check + login) if the player is on a boat
            // right now -- the same "sample, don't reconstruct the whole
            // path" approximation the game's own DistanceSail stat uses.
            // Nothing is credited on the first call of a session (nothing
            // to measure the gap from yet).
            var now = nowUtc();
            if (lastBoatSampleUtc.HasValue)
            {
                var elapsed = (float)(now - lastBoatSampleUtc.Value).TotalSeconds;
                var toAdd = CounterRules.BoatSecondsToAdd(isOnBoat(), elapsed, (float)MaxPlausibleBoatGap.TotalSeconds);
                if (toAdd > 0f)
                {
                    store.Increment(CounterRules.TimeOnBoatSecondsKey, toAdd);
                }
            }

            lastBoatSampleUtc = now;

            // Stink Fish: same shape as The Admiral above, against the
            // Swamp biome instead of a boat.
            if (lastSwampSampleUtc.HasValue)
            {
                var elapsed = (float)(now - lastSwampSampleUtc.Value).TotalSeconds;
                var toAdd = CounterRules.SwampSecondsToAdd(isInSwamp(), elapsed, (float)MaxPlausibleSwampGap.TotalSeconds);
                if (toAdd > 0f)
                {
                    store.Increment(CounterRules.TimeInSwampSecondsKey, toAdd);
                }
            }

            lastSwampSampleUtc = now;

            foreach (var kv in store.Snapshot())
            {
                stats[kv.Key] = kv.Value;
            }

            return stats;
        }
    }
}
