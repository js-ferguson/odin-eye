namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using System.Reflection;
    using UnityEngine;

    // ODINEYE-38: Marksman. Valheim keeps no count of arrow hits, so count
    // them here: Projectile.OnHit runs on the shooter's own machine when a
    // projectile they fired strikes something.
    //
    // Patched by name with the arguments read from __args, so this does not
    // need a compile-time reference to the physics module for Collider (a
    // Collider is a Component, which lives in the module we do reference).
    [HarmonyPatch]
    public static class ArrowHitPatch
    {
        private static readonly FieldInfo OwnerField = AccessTools.Field(typeof(Projectile), "m_owner");
        private static readonly FieldInfo SkillField = AccessTools.Field(typeof(Projectile), "m_skill");

        [HarmonyPrepare]
        private static bool Prepare() => TargetMethod() != null && OwnerField != null && SkillField != null;

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Projectile), "OnHit");

        [HarmonyPrefix]
        private static void Prefix(Projectile __instance, object[] __args) =>
            ClientRuntime.Guard("arrow hit count", () =>
            {
                var counters = ClientRuntime.Counters;
                if (counters == null || Player.m_localPlayer == null)
                {
                    return;
                }

                var target = (__args[0] as Component)?.GetComponentInParent<Character>();
                var counts = CounterRules.CountsAsArrowHitOnEnemy(
                    firedByLocalPlayer: ReferenceEquals(OwnerField.GetValue(__instance), Player.m_localPlayer),
                    skillName: SkillField.GetValue(__instance)?.ToString(),
                    targetIsCharacter: target != null,
                    targetIsPlayer: target != null && target.IsPlayer(),
                    targetIsTamed: target != null && target.IsTamed(),
                    targetIsDead: target != null && target.IsDead());
                if (counts)
                {
                    counters.Increment(CounterRules.ArrowHitsEnemyKey);
                }
            });
    }
}
