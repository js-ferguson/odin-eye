namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // Mike Tyson (fists) and Mushashi Master of Blades (swords): both need
    // "did I land the killing blow, with this specific weapon skill" --
    // same hook, so one patch class covers both rather than patching
    // Character.ApplyDamage twice.
    //
    // HitData.GetAttacker() (public) resolves the attacking Character
    // directly -- no need to compare raw ZDOIDs -- and HitData.m_skill
    // (public field, Skills.SkillType) names the weapon skill used. Both
    // confirmed present and public via IL disassembly of the live game
    // assembly; unlike Projectile.m_owner (ArrowHitPatch), no reflection is
    // needed here.
    //
    // A postfix on ApplyDamage: by the time it returns, the hit has already
    // been applied, so __instance.IsDead() reflects whether THIS hit was
    // the killing blow, not just any hit landed with the weapon.
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    public static class WeaponKillPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Character __instance, HitData hit) =>
            ClientRuntime.Guard("weapon kill count", () =>
            {
                if (ClientRuntime.Counters == null || hit == null)
                {
                    return;
                }

                var byLocalPlayer = ReferenceEquals(hit.GetAttacker(), Player.m_localPlayer);
                var skillName = hit.m_skill.ToString();
                var targetIsCharacter = __instance != null;
                var targetIsPlayer = targetIsCharacter && __instance.IsPlayer();
                var targetIsTamed = targetIsCharacter && __instance.IsTamed();
                var targetIsDeadNow = targetIsCharacter && __instance.IsDead();

                if (CounterRules.CountsAsWeaponKill(byLocalPlayer, skillName, "Unarmed", targetIsCharacter, targetIsPlayer, targetIsTamed, targetIsDeadNow))
                {
                    ClientRuntime.Counters.Increment(CounterRules.FistKillsKey);
                }

                if (CounterRules.CountsAsWeaponKill(byLocalPlayer, skillName, "Swords", targetIsCharacter, targetIsPlayer, targetIsTamed, targetIsDeadNow))
                {
                    ClientRuntime.Counters.Increment(CounterRules.SwordKillsKey);
                }
            });
    }
}
