namespace OdinEye.Patches
{
    using HarmonyLib;
    using Models.Proto;
    using System.Linq;
    using System.Reflection;

    // ODINEYE-31, stage 1: observe star-level-attributed enemy kills, to
    // eventually power user-built custom achievements ("killed a 2-star
    // troll"). No existing Valheim/OdinEye signal carries star level at
    // all -- PlayerProfile.m_enemyStats (ODINEYE-29's EnemyKill:$enemy_*
    // stat) is keyed only by enemy prefab name, blind to level. This
    // patches Character.OnDeath() directly, the only place level
    // (Character.GetLevel(), a real public method returning m_level --
    // 1-indexed: 1 = 0-star/regular, 2 = 1-star, 3 = 2-star, confirmed
    // present in the old ValheimGameLibs compile-time stub too, no
    // reflection needed for the level read itself) is available at the
    // moment of death, alongside the enemy's name (Character.m_name,
    // public) and attacker attribution (the same ZDOVars.s_attackers +
    // per-player ZDO.GetBool(ZDOVars.s_attackers.ToString() + playerName)
    // flags vanilla's own Game.RPC_RegisterKill dispatch already uses --
    // confirmed via IL disassembly of Character.OnDeath -- readable
    // server-side too, since ZDOs are server-visible, not client-only).
    //
    // Server-side by design (not OdinEye.Client), per the user's own
    // call on ODINEYE-31: this works for every player automatically, no
    // client-mod install needed -- unlike ODINEYE-29's EnemyKill: stat.
    // REAL, UNVERIFIED RISK, flagged rather than assumed away: this only
    // fires if the dedicated server currently OWNS this specific
    // creature's ZDO (ZNetView.IsOwner()) -- Valheim's ownership model
    // can hand nearby zone objects to a connected client instead of the
    // server. Needs live verification against a real kill (tail
    // whitey's BepInEx log for "EnemyKilled", or the /activity
    // WebSocket) before trusting this never misses one. If it does miss
    // kills, the documented fallback is this exact same patch logic
    // moved into OdinEye.Client instead -- same reliability guarantee
    // ODINEYE-29 already accepts (fires on whichever peer is
    // authoritative), at the cost of the per-player-client-install
    // dependency.
    //
    // Deliberately just an observable GameEvent for now (logged +
    // WebSocket-broadcast, same convention as every other patch in this
    // folder) -- no persistence/aggregation/REST API until this is
    // confirmed to fire reliably. See ODINEYE-31's staged plan.
    //
    // m_nview is `protected` on Character, so it isn't reachable as
    // __instance.m_nview from this external patch class -- read via
    // Harmony's own AccessTools the same way any Harmony mod reaches a
    // non-public field, cached once rather than re-reflected per death.
    //
    // ZDOVars.s_attackers itself is ALSO absent from this project's
    // ValheimGameLibs compile-time stub (confirmed: CS0117 at build
    // time) -- same "added to the real game after the stub was
    // published" class of gap as Achievements/m_enemyStats elsewhere in
    // this project, not a typo. Its runtime value (a stable string hash,
    // assigned once in ZDOVars' own static constructor) is read via
    // plain reflection instead, same pattern as CheatStatusReader's
    // out-of-stub Achievements lookup.
    [HarmonyPatch(typeof(Character))]
    public class CharacterDeathPatch
    {
        private static readonly FieldInfo NViewField = AccessTools.Field(typeof(Character), "m_nview");

        private static readonly int? AttackersVarId =
            typeof(Character).Assembly.GetType("ZDOVars")
                ?.GetField("s_attackers", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as int?;

        [HarmonyPatch(nameof(Character.OnDeath))]
        [HarmonyPrefix]
        protected static void OnDeath(Character __instance)
        {
            if (__instance.IsPlayer() || AttackersVarId == null)
            {
                return;
            }

            if (!(NViewField.GetValue(__instance) is ZNetView nview) || !nview.IsOwner())
            {
                return;
            }

            var zdo = nview.GetZDO();
            var attackerKeyPrefix = AttackersVarId.Value.ToString();
            var attackerNames = ZNet.instance.GetPlayerList()
                .Where(p => zdo.GetBool(attackerKeyPrefix + p.m_name, false))
                .Select(p => p.m_name)
                .ToList();

            if (attackerNames.Count == 0)
            {
                return; // died to fall/fire/environment/etc, not a credited player kill
            }

            var message = $"{string.Join(", ", attackerNames)} killed {__instance.m_name} " +
                          $"(level {__instance.GetLevel()}, boss: {__instance.IsBoss()})";
            OdinEyePlugin.Instance.EventHandler.Handle(GameEvent.New(EventType.EnemyKilled, message));
        }
    }
}
