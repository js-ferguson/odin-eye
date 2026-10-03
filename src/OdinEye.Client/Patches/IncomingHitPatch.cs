namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using OdinEye.Client.Stats;

    // VALSER-81 batch: Venom and Suckie Suckie both need "a hit landed ON
    // me", the mirror image of WeaponKillPatch's "a hit I dealt" -- a
    // second, independent patch on the same method (Harmony chains
    // multiple patches on one target fine; WeaponKillPatch's own postfix is
    // untouched).
    //
    // Character.ApplyDamage(HitData hit) is confirmed public via IL
    // disassembly of the live game assembly (same method WeaponKillPatch
    // already patches). A postfix: by the time it returns, the hit has
    // already been applied, so __instance.IsDead() reflects whether THIS
    // hit was the killing blow -- the same postfix-timing WeaponKillPatch's
    // own header already documents.
    //
    // HitData.m_damage is a HitData.DamageTypes instance with one public
    // float per damage type (confirmed via IL, including m_poison) --
    // Venom reads that field directly, no reflection needed. HitData.
    // GetAttacker() (public) resolves the attacking Character; its prefab
    // name is read via assembly_utils' Utils.GetPrefabName(GameObject),
    // the same helper TameablePatch.cs already established as this
    // codebase's correct way to turn a live GameObject into its prefab's
    // string name (strips a "(Clone)"/space suffix a raw .name read could
    // carry -- confirmed via IL of Utils.GetPrefabName itself).
    //
    // VALSER-90 batch: Escape Artist and Terrible Palsy both need "the hit
    // that just killed me came from an enemy", added as two more
    // conditions in this SAME postfix rather than a new patch class.
    // HitData.m_hitType (confirmed via IL: a real HitData.HitType enum
    // field on this same hit object, EnemyHit distinct from PlayerHit) is
    // the death-cause signal; Menu.IsVisible()/Player.IsEncumbered()/
    // GetStamina() are the extra live-state reads each one needs,
    // confirmed real via IL but read by this codebase for the first time.
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    public static class IncomingHitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Character __instance, HitData hit) =>
            ClientRuntime.Guard("incoming hit count", () =>
            {
                if (ClientRuntime.Counters == null || hit == null || __instance == null
                    || !ReferenceEquals(__instance, Player.m_localPlayer))
                {
                    return;
                }

                if (CounterRules.CountsAsPoisonDeath(true, __instance.IsDead(), hit.m_damage.m_poison))
                {
                    ClientRuntime.Counters.Increment(CounterRules.PoisonDeathsKey);
                }

                // GetAttacker() is null for environmental/attacker-less
                // damage (fall, drowning, an area poison cloud with no
                // Character source) -- Utils.GetPrefabName itself does no
                // null check (confirmed via its own IL: a bare .name read),
                // so that has to be guarded here, not assumed away.
                var attacker = hit.GetAttacker();
                var attackerPrefabName = attacker == null ? null : Utils.GetPrefabName(attacker.gameObject);
                if (CounterRules.CountsAsLeechHit(true, attackerPrefabName))
                {
                    ClientRuntime.Counters.Increment(CounterRules.LeechHitsKey);
                }

                // VALSER-90: Escape Artist / Terrible Palsy. "Killed by an
                // enemy" is hit.m_hitType == HitData.HitType.EnemyHit --
                // a real enum value on this same hit object (confirmed via
                // IL), distinct from PlayerHit, so PvP deaths correctly
                // never count toward either achievement.
                var killedByEnemy = hit.m_hitType == HitData.HitType.EnemyHit;

                if (CounterRules.CountsAsEscapeArtistDeath(true, __instance.IsDead(), killedByEnemy, Menu.IsVisible()))
                {
                    ClientRuntime.Counters.Increment(CounterRules.EscapeArtistDeathsKey);
                }

                // Player.m_localPlayer is safe to dereference unchecked here --
                // the guard above already confirmed __instance IS
                // Player.m_localPlayer (ReferenceEquals), so it cannot be null.
                if (CounterRules.CountsAsTerriblePalsyDeath(true, __instance.IsDead(), killedByEnemy,
                    EncumbranceTracking.IsOverburdened(), Player.m_localPlayer.GetStamina() <= 0f))
                {
                    ClientRuntime.Counters.Increment(CounterRules.TerriblePalsyDeathsKey);
                }
            });
    }
}
