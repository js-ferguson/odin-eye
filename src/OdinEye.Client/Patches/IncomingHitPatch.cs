namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

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
            });
    }
}
