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
    // full version list -- while the actual live game is 1.0.x.
    //
    // The real shape, confirmed via IL disassembly (ikdasm) of the actual
    // running server's assembly_valheim.dll, PlayerProfile class:
    //   .field public initonly class PlayerProfile/PlayerStats[] m_playerStats
    // -- a fixed 10-element array, NOT a single wrapper object. The nested
    // PlayerProfile.PlayerStats class is what actually declares m_stats
    // (Dictionary<PlayerStatType, float>), m_knownWorlds, and the rest of
    // the per-category dictionaries. PlayerProfile's own GetStat/SetStat
    // methods always read/write index 0 for a normal (non-achievement-
    // difficulty-specific) stat -- confirmed by a `c_RawStats = 0` literal
    // constant on PlayerProfile and by GetStat/SetStat's own IL, which
    // falls back to `m_playerStats[0]` whenever achievement tracking is
    // off (Achievements.CanGetAchievements(false)) and always writes index
    // 0 unconditionally in SetStat. Index 0 is therefore the real lifetime
    // "raw stats" slot we want; the other 9 slots are per-achievement-
    // difficulty variants, not per-world or per-session data.
    //
    // Two real, sequentially-discovered bugs shipped and were fixed here
    // before this shape was confirmed (see PRs #17/#18): both assumed
    // m_playerStats was a single object with its own m_stats/m_knownWorlds
    // fields, so GetFieldValue(wrapper, "m_stats") always returned null
    // (arrays have no such field) -- no exception, just every stat silently
    // defaulting to 0. That's a real player's actual confirmed symptom
    // (v1.2.8: submission succeeded, every stat included, every value 0)
    // once the earlier MissingFieldException/missing-dependency bugs were
    // fixed -- fixed for real now by indexing into the array first.
    //
    // A THIRD bug (ODINEYE-27), found investigating a real player whose
    // BossKillMultiplayer/BossKillSolo read 0 despite confirmed boss
    // kills: GetStats() used to iterate Enum.GetValues(typeof(PlayerStatType))
    // -- OUR OWN compiled enum, from the same stale ValheimGameLibs stub
    // described above. Confirmed via IL disassembly that stub's enum has
    // only 106 members while the real, running game's has 207 -- roughly
    // HALF of every real stat the live game tracks (anything added since
    // the stub was published, BossKillMultiplayer/BossKillSolo included)
    // was never even attempted, for every player, always. Fixed by
    // iterating the real, live dictionary's own keys directly instead of
    // our enum's membership -- see GetStats() below.
    //
    // ODINEYE-28: while investigating the above, noticed m_knownWorlds
    // (already read here to compute PlayTimeSecondsKey) also directly
    // answers "how many distinct worlds has this character played on" --
    // its own .Count, surfaced as KnownWorldsCountKey below.
    //
    // ODINEYE-29: boss progression on the admin-panel side (VALSER-48)
    // was inferring "did this character kill boss X" from the Forsaken
    // Power stats (SetPowerX/UsePowerX) -- a proxy, and an unreliable
    // one: a character can kill a boss and never (re-)activate the
    // guardian power afterward, so it under-reports real kills. IL
    // disassembly (ikdasm) of the real running server's
    // assembly_valheim.dll found a much more direct signal already
    // being persisted by the game itself, one level down from m_stats:
    // PlayerProfile.PlayerStats.m_enemyStats is a Dictionary<string,
    // float>[] keyed by enemy prefab name (index 0 = the same "raw
    // stats" RawStatsIndex slot m_stats/m_knownWorlds already use; the
    // other slots are per-achievement-difficulty variants, same split
    // as everywhere else in this class). It's written by exactly one
    // call site, PlayerProfile.IncrementStatEnemy(name, amount,
    // modifiers, cheated), itself only ever called from
    // Game.RPC_RegisterKill -- which Character.OnDeath only sends to a
    // given player's own client when THAT character was one of the
    // flagged credited attackers on the dying creature's ZDO (checked
    // against the local character's own ZDOID before the RPC is ever
    // sent). So m_enemyStats[0][bossPrefabName] >= 1 means,
    // unambiguously, "this specific character was personally credited
    // with a kill of this specific enemy" -- bosses included -- with
    // zero dependency on ever activating the resulting guardian power.
    // Folded into the same stats dictionary GetStats() already
    // returns, under EnemyKillKeyPrefix, so it rides the existing wire
    // format with no server/agent/admin-panel API change needed; the
    // real boss prefab name strings (exact casing) aren't confirmed
    // yet -- that needs a live submission from a character with a
    // confirmed kill, once this ships.
    public sealed class PlayerProfileStatsSource : IPlayerStatsSource
    {
        // Not one of PlayerStatType's values -- Valheim tracks total real
        // playtime separately, as per-world seconds in
        // PlayerProfile.PlayerStats.m_knownWorlds (see Decision 1 /
        // ODINEYE-13). Summed here into one lifetime total.
        public const string PlayTimeSecondsKey = "PlayTimeSeconds";

        // Also derived from m_knownWorlds (ODINEYE-28): its own .Count is
        // "how many distinct worlds this character has ever played on" --
        // the exact same dictionary the game's own in-built stats screen
        // labels "Known worlds:" (confirmed via IL disassembly of
        // TextsDialog.AddStats). Keyed by WORLD NAME, not a server
        // identity Valheim doesn't track at all -- two different servers
        // both running a world named e.g. "seasoned" collide into one
        // entry here, same as they would in the game's own stats screen.
        // Distinct from WorldLoads (a load-EVENT counter, already exposed
        // via GetStats()'s live-dictionary iteration below) -- that one
        // can be far larger than the number of distinct worlds, since
        // reconnecting to the same world repeatedly increments it every
        // time.
        public const string KnownWorldsCountKey = "KnownWorldsCount";

        // ODINEYE-29: prefixes every m_enemyStats[RawStatsIndex] entry
        // (keyed by enemy prefab name -- see this class's header comment)
        // so it can never collide with a real PlayerStatType name in the
        // same flat stats dictionary, and so a consumer can recognize
        // "this is a per-enemy kill count" without a fixed enum of every
        // possible enemy name (new creatures ship over time, same
        // reasoning as ODINEYE-27's live-dictionary-keys fix above).
        public const string EnemyKillKeyPrefix = "EnemyKill:";

        // Matches PlayerProfile's own c_RawStats literal (confirmed via IL
        // disassembly) -- the array slot GetStat/SetStat treat as the real,
        // always-updated lifetime stats when achievement-difficulty
        // tracking isn't in play.
        private const int RawStatsIndex = 0;

        // EVERY PlayerProfile/PlayerStats field this class reads goes
        // through GetFieldValue() below, none accessed directly -- see
        // this file's header comment for why (compile-time stub staleness,
        // not caution for its own sake).
        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(target);
        }

        // PlayerProfile.m_playerStats[RawStatsIndex] -- see this class's
        // header comment for why index 0 specifically. Returned as a plain
        // object (its real type, PlayerProfile.PlayerStats, isn't
        // available to compile against either); reflected into again by
        // callers via GetFieldValue().
        private static object GetRawPlayerStats(object profile)
        {
            if (!(GetFieldValue(profile, "m_playerStats") is Array statsArray) || statsArray.Length <= RawStatsIndex)
            {
                return null;
            }
            return statsArray.GetValue(RawStatsIndex);
        }

        public IReadOnlyDictionary<string, float> GetStats()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null)
            {
                return new Dictionary<string, float>();
            }

            var stats = new Dictionary<string, float>();
            var rawPlayerStats = GetRawPlayerStats(profile);
            var statValues = GetFieldValue(rawPlayerStats, "m_stats") as IDictionary;

            // Iterate the REAL dictionary's own keys (ODINEYE-27), not
            // Enum.GetValues(typeof(PlayerStatType)) -- see this class's
            // header comment for why that silently dropped ~half of every
            // real stat. entry.Key.ToString() resolves against whatever
            // enum the actually-running game defines, regardless of what
            // our own stale compile-time stub knows about.
            if (statValues != null)
            {
                foreach (DictionaryEntry entry in statValues)
                {
                    stats[entry.Key.ToString()] = Convert.ToSingle(entry.Value);
                }
            }

            float totalPlaytime = 0f;
            int knownWorldsCount = 0;
            if (GetFieldValue(rawPlayerStats, "m_knownWorlds") is IDictionary knownWorlds)
            {
                knownWorldsCount = knownWorlds.Count;
                foreach (var value in knownWorlds.Values)
                {
                    totalPlaytime += Convert.ToSingle(value);
                }
            }
            stats[PlayTimeSecondsKey] = totalPlaytime;
            stats[KnownWorldsCountKey] = knownWorldsCount;

            // ODINEYE-29: m_enemyStats is itself an array (raw slot +
            // per-achievement-difficulty variants, same RawStatsIndex
            // convention as m_playerStats) -- index into it the same way
            // GetRawPlayerStats() indexes into m_playerStats, then read
            // its own per-enemy-name dictionary.
            if (GetFieldValue(rawPlayerStats, "m_enemyStats") is Array enemyStatsArray
                && enemyStatsArray.Length > RawStatsIndex
                && enemyStatsArray.GetValue(RawStatsIndex) is IDictionary enemyStats)
            {
                foreach (DictionaryEntry entry in enemyStats)
                {
                    stats[EnemyKillKeyPrefix + entry.Key] = Convert.ToSingle(entry.Value);
                }
            }

            // ODINEYE-38: the game's own exact count of every piece this
            // character has ever placed, keyed by Piece.m_name -- the source
            // of Benched / Fully Benched, and it covers what was built
            // before this mod ever ran. A plain dictionary (not an array of
            // per-difficulty variants like m_enemyStats).
            if (GetFieldValue(rawPlayerStats, "m_piecesPlacedStats") is IDictionary piecesPlaced)
            {
                var placed = new Dictionary<string, float>();
                foreach (DictionaryEntry entry in piecesPlaced)
                {
                    placed[entry.Key.ToString()] = Convert.ToSingle(entry.Value);
                }

                foreach (var kv in PieceStats.Prefix(placed))
                {
                    stats[kv.Key] = kv.Value;
                }
            }

            return stats;
        }
    }
}
