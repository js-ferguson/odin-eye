namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // Mike Tyson (fists) and Mushashi Master of Blades (swords): both need
    // "did I land the killing blow, with this specific weapon skill" --
    // same hook, so one patch class covers both rather than patching
    // Character.ApplyDamage twice. Raw Dog (butcher knife + wolf) lives
    // here too, for the same reason -- it's still "my killing blow, with
    // the right weapon", just gated on the specific item as well as skill.
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
    //
    // Raw Dog is item-specific, not just skill-category, so it needs the
    // attacker's CURRENTLY EQUIPPED weapon read separately -- HitData itself
    // carries no weapon-item identity. Humanoid.GetCurrentWeapon() (public,
    // confirmed via IL, returns m_rightItem) -> ItemData.m_dropPrefab
    // (public) -> Utils.GetPrefabName, the same prefab-name helper this
    // codebase already established (TameablePatch.cs, IncomingHitPatch.cs).
    // Every step null-chains -- an unarmed attacker, a non-Humanoid
    // Character, or no weapon equipped all correctly yield null here, not a
    // NullReferenceException. ACCEPTED RISK, not fixed: this reads the
    // weapon at POSTFIX time (after the hit already applied), not captured
    // at the moment the hit was dealt -- a same-frame weapon swap between
    // dealing the blow and this postfix running could theoretically
    // misattribute. The "Knives" skill-type gate in CountsAsRawDogKill
    // mitigates the common case (swapped to a non-knife) but can't catch a
    // knife-to-knife swap in the same frame.
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

                var attackerHumanoid = hit.GetAttacker() as Humanoid;
                var weaponPrefab = attackerHumanoid?.GetCurrentWeapon()?.m_dropPrefab;
                var weaponPrefabName = weaponPrefab == null ? null : Utils.GetPrefabName(weaponPrefab);
                var targetPrefabName = targetIsCharacter ? Utils.GetPrefabName(__instance.gameObject) : null;

                if (CounterRules.CountsAsRawDogKill(byLocalPlayer, skillName, weaponPrefabName, targetPrefabName, targetIsCharacter, targetIsPlayer, targetIsTamed, targetIsDeadNow))
                {
                    ClientRuntime.Counters.Increment(CounterRules.RawDogWolfKillsKey);
                }
            });
    }
}
