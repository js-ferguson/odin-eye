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

        // Baker's High: same cap, same reasoning, for time near an oven --
        // see CounterRules.BakerySecondsToAdd.
        private static readonly TimeSpan MaxPlausibleBakeryGap = TimeSpan.FromMinutes(5);

        // Palsy: same elapsed-gap cap as the three above, PLUS a max
        // plausible speed -- a time-based sampler can never be fooled by a
        // teleport (it only ever credits wall-clock time), but a
        // distance-based one can be, if a portal hop happens to land
        // between two samples. See CounterRules.OverburdenedDistanceToAdd.
        // distanceMovedSinceLastSample is a plain float, not two Vector3
        // positions -- all Vector3 math (and its own last-position state)
        // stays inside EncumbranceTracking, since UnityEngine.Vector3's own
        // static constructor throws outside a real Unity process and this
        // class is exactly what the test suite exercises directly.
        private static readonly TimeSpan MaxPlausibleOverburdenedGap = TimeSpan.FromMinutes(5);
        private const float MaxPlausibleOverburdenedSpeedMetersPerSecond = 10f;

        private readonly IPlayerStatsSource inner;
        private readonly CustomCounterStore store;
        private readonly Func<ISet<string>> stationNames;
        private readonly Func<float?> currentNorthZ;
        private readonly Func<bool> isInDeepNorth;
        private readonly Func<bool> isOnBoat;
        private readonly Func<bool> isInSwamp;
        private readonly Func<bool> isNearOven;
        private readonly Func<bool> isOverburdened;
        private readonly Func<float> overburdenedDistanceSinceLastSample;
        private readonly Func<DateTime> nowUtc;
        private DateTime? lastBoatSampleUtc;
        private DateTime? lastSwampSampleUtc;
        private DateTime? lastBakerySampleUtc;
        private DateTime? lastOverburdenedSampleUtc;

        public CounterAugmentedStatsSource(IPlayerStatsSource inner, CustomCounterStore store, Func<ISet<string>> stationNames,
            Func<float?> currentNorthZ = null, Func<bool> isInDeepNorth = null,
            Func<bool> isOnBoat = null, Func<bool> isInSwamp = null, Func<bool> isNearOven = null,
            Func<bool> isOverburdened = null, Func<float> overburdenedDistanceSinceLastSample = null, Func<DateTime> nowUtc = null)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.stationNames = stationNames ?? (() => null);
            this.currentNorthZ = currentNorthZ ?? (() => null);
            this.isInDeepNorth = isInDeepNorth ?? (() => false);
            this.isOnBoat = isOnBoat ?? (() => false);
            this.isInSwamp = isInSwamp ?? (() => false);
            this.isNearOven = isNearOven ?? (() => false);
            this.isOverburdened = isOverburdened ?? (() => false);
            this.overburdenedDistanceSinceLastSample = overburdenedDistanceSinceLastSample ?? (() => 0f);
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

            // Baker's High: same shape as The Admiral/Stink Fish above,
            // against being in the vicinity of an oven instead.
            if (lastBakerySampleUtc.HasValue)
            {
                var elapsed = (float)(now - lastBakerySampleUtc.Value).TotalSeconds;
                var toAdd = CounterRules.BakerySecondsToAdd(isNearOven(), elapsed, (float)MaxPlausibleBakeryGap.TotalSeconds);
                if (toAdd > 0f)
                {
                    store.Increment(CounterRules.TimeInBakerySecondsKey, toAdd);
                }
            }

            lastBakerySampleUtc = now;

            // Palsy: distance moved since the LAST check, credited only if
            // the player was overburdened at sample time -- a position
            // delta, not a time delta. overburdenedDistanceSinceLastSample()
            // is called every time regardless (it has to be, to keep its
            // own internal last-position state in lockstep with
            // lastOverburdenedSampleUtc below), but its result is only
            // ever CREDITED once there's a prior sample to measure a real
            // gap from -- same "nothing on the first call" rule every
            // other sampler here already follows.
            var overburdenedDistance = overburdenedDistanceSinceLastSample();
            if (lastOverburdenedSampleUtc.HasValue)
            {
                var elapsed = (float)(now - lastOverburdenedSampleUtc.Value).TotalSeconds;
                var toAdd = CounterRules.OverburdenedDistanceToAdd(isOverburdened(), overburdenedDistance, elapsed,
                    (float)MaxPlausibleOverburdenedGap.TotalSeconds, MaxPlausibleOverburdenedSpeedMetersPerSecond);
                if (toAdd > 0f)
                {
                    store.Increment(CounterRules.OverburdenedDistanceMetersKey, toAdd);
                }
            }

            lastOverburdenedSampleUtc = now;

            foreach (var kv in store.Snapshot())
            {
                stats[kv.Key] = kv.Value;
            }

            return stats;
        }
    }
}
