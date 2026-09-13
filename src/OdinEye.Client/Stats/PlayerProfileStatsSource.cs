namespace OdinEye.Client.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Game-dependent adapter (not unit tested for the same reason the
    // server-side controllers aren't: it's a thin read of live game state).
    // Reads the local player's own PlayerProfile, which is only ever fully
    // populated client-side -- this is the entire reason OdinEye needs a
    // client-side component at all (see CHARACTER-STATS-INVESTIGATION.md).
    //
    // PlayerProfile exposes no public getter for an individual stat (only
    // IncrementStat) or for total playtime, so both are read directly off
    // its public fields instead.
    public sealed class PlayerProfileStatsSource : IPlayerStatsSource
    {
        // Not one of PlayerStatType's values -- Valheim tracks total real
        // playtime separately, as per-world seconds in
        // PlayerProfile.m_knownWorlds (see Decision 1 / ODINEYE-13). Summed
        // here into one lifetime total.
        public const string PlayTimeSecondsKey = "PlayTimeSeconds";

        public IReadOnlyDictionary<string, float> GetStats()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null)
            {
                return new Dictionary<string, float>();
            }

            var stats = new Dictionary<string, float>();
            var statValues = profile.m_playerStats?.m_stats;

            foreach (PlayerStatType statType in Enum.GetValues(typeof(PlayerStatType)))
            {
                stats[statType.ToString()] = statValues != null && statValues.TryGetValue(statType, out var value) ? value : 0f;
            }

            stats[PlayTimeSecondsKey] = profile.m_knownWorlds?.Values.Sum() ?? 0f;

            return stats;
        }
    }
}
