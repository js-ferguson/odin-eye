namespace OdinEye.Client.Stats
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // Game-dependent adapter (not unit tested for the same reason the
    // server-side controllers aren't: it's a thin read of live game state).
    // Reads the local player's own PlayerProfile, which is only ever fully
    // populated client-side -- this is the entire reason OdinEye needs a
    // client-side component at all (see CHARACTER-STATS-INVESTIGATION.md).
    //
    // PlayerProfile exposes no public getter for an individual stat (only
    // IncrementStat) or for total playtime, so both are read directly off
    // its fields instead -- via reflection, not a compile-time field access,
    // for a real reason (not caution for its own sake): this project's
    // ValheimGameLibs compile-time reference package (the publicized/
    // stripped game-assembly stubs it builds against) has no version newer
    // than 0.221.4 published anywhere -- confirmed live against nuget.org's
    // full version list -- while the actual live game is 1.0.x. A live
    // player's real BepInEx log caught this exact gap: the original code
    // (profile.m_playerStats?.m_stats, assuming a "PlayerStats" wrapper
    // between PlayerProfile and its real stats dictionary) compiled fine
    // against that stub but threw MissingFieldException against the real,
    // running 1.0.12 assembly on every single submission attempt --
    // confirmed live: no real player's OdinEye.Client has EVER
    // successfully submitted anything, despite loading and being correctly
    // configured. A straight field-name fix (PlayerProfile.m_stats
    // directly, the real 1.0.12 shape, confirmed via IL disassembly of the
    // actual running server's assembly_valheim.dll) turned out not to
    // compile either -- surfacing that this project's own local copy of
    // the 0.221.4 stub no longer agrees with whatever CI's fresh restore
    // of that exact same nominal package version actually contains, an
    // unresolved discrepancy not worth chasing further. Reflection
    // sidesteps the whole question: it resolves against whatever the
    // REAL game assembly loaded at runtime actually looks like, not
    // whatever shape happened to be available to compile against.
    public sealed class PlayerProfileStatsSource : IPlayerStatsSource
    {
        // Not one of PlayerStatType's values -- Valheim tracks total real
        // playtime separately, as per-world seconds in
        // PlayerProfile.m_knownWorlds (see Decision 1 / ODINEYE-13). Summed
        // here into one lifetime total. m_knownWorlds itself has been the
        // same public Dictionary<string, float> field across every game
        // version checked so far, so it's still read directly rather than
        // reflectively -- only the stats dictionary's own location has
        // ever been observed to move.
        public const string PlayTimeSecondsKey = "PlayTimeSeconds";

        // Resolved once, not per call -- GetStats() runs on every
        // SubmissionScheduler tick (every 5 minutes per player, plus once
        // on login), not hot enough to need it, but no reason to redo the
        // same two reflection lookups every time either.
        private static readonly Lazy<FieldInfo> DirectStatsField = new Lazy<FieldInfo>(
            () => typeof(PlayerProfile).GetField("m_stats", BindingFlags.Public | BindingFlags.Instance));
        private static readonly Lazy<FieldInfo> WrapperField = new Lazy<FieldInfo>(
            () => typeof(PlayerProfile).GetField("m_playerStats", BindingFlags.Public | BindingFlags.Instance));

        public IReadOnlyDictionary<string, float> GetStats()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null)
            {
                return new Dictionary<string, float>();
            }

            var stats = new Dictionary<string, float>();
            var statValues = GetStatsDictionary(profile);

            foreach (PlayerStatType statType in Enum.GetValues(typeof(PlayerStatType)))
            {
                float value = 0f;
                if (statValues != null && statValues.Contains(statType))
                {
                    value = Convert.ToSingle(statValues[statType]);
                }
                stats[statType.ToString()] = value;
            }

            stats[PlayTimeSecondsKey] = profile.m_knownWorlds?.Values.Sum() ?? 0f;

            return stats;
        }

        // Tries PlayerProfile.m_stats directly first (confirmed live to be
        // the real 1.0.x shape); falls back to the older
        // PlayerProfile.m_playerStats.m_stats wrapper path (the shape this
        // code originally assumed, and which -- per this class's own
        // comment -- may still be what a stale compile-time reference
        // expects) if the direct field genuinely isn't there. Either way,
        // this is resolved against whatever's ACTUALLY loaded at runtime,
        // not a compile-time guess.
        private static IDictionary GetStatsDictionary(PlayerProfile profile)
        {
            var direct = DirectStatsField.Value;
            if (direct != null)
            {
                return direct.GetValue(profile) as IDictionary;
            }

            var wrapper = WrapperField.Value?.GetValue(profile);
            if (wrapper == null)
            {
                return null;
            }

            var nested = wrapper.GetType().GetField("m_stats", BindingFlags.Public | BindingFlags.Instance);
            return nested?.GetValue(wrapper) as IDictionary;
        }
    }
}
