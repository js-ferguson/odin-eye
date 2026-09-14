namespace OdinEye.Client.Stats
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
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
        // here into one lifetime total.
        public const string PlayTimeSecondsKey = "PlayTimeSeconds";

        // EVERY PlayerProfile field this class reads goes through
        // GetFieldValue() below, none accessed directly -- confirmed live
        // that even m_knownWorlds, originally assumed stable enough to
        // read directly, throws the exact same MissingFieldException class
        // of failure the stats dictionary did (see this file's own header
        // comment for the full story). The two don't even agree with each
        // other: IL disassembly of the SERVER's own assembly_valheim.dll
        // shows m_knownWorlds present and public on PlayerProfile, but a
        // real player's client build throws looking for that exact field
        // at runtime -- client and dedicated-server builds evidently don't
        // share one consistent PlayerProfile layout. Reflection resolved
        // fresh against whatever's actually loaded is the only thing that
        // has held up under real testing; no field on this type gets a
        // second exemption from that.
        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(target);
        }

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

            float totalPlaytime = 0f;
            if (GetFieldValue(profile, "m_knownWorlds") is IDictionary knownWorlds)
            {
                foreach (var value in knownWorlds.Values)
                {
                    totalPlaytime += Convert.ToSingle(value);
                }
            }
            stats[PlayTimeSecondsKey] = totalPlaytime;

            return stats;
        }

        // Tries PlayerProfile.m_stats directly first (confirmed live to be
        // the real shape on the SERVER's own assembly, at least); falls
        // back to the older PlayerProfile.m_playerStats.m_stats wrapper
        // path (the shape this code originally assumed) if the direct
        // field genuinely isn't there. Both paths go through
        // GetFieldValue() -- see this class's own field-access comment.
        private static IDictionary GetStatsDictionary(PlayerProfile profile)
        {
            if (GetFieldValue(profile, "m_stats") is IDictionary direct)
            {
                return direct;
            }

            var wrapper = GetFieldValue(profile, "m_playerStats");
            if (wrapper == null)
            {
                return null;
            }

            return GetFieldValue(wrapper, "m_stats") as IDictionary;
        }
    }
}
