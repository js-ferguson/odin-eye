namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // VALSER-87 (replaces the original VALSER-81 version): Joe dirt. The
    // original patch targeted MineRock5.DamageArea -- a guess, flagged in
    // its own comments as unconfirmed, and wrong: direct extraction of the
    // real game asset bundle (UnityPy against the live mudpile.prefab/
    // mudpile2.prefab objects) confirms both actually carry Destructible,
    // not MineRock5. That's why this never fired for anyone despite real,
    // confirmed mining -- the Harmony patch was never even reachable.
    //
    // Destructible.RPC_Damage(int64 sender, HitData hit) is the real hit-
    // handling method (confirmed via IL, registered in Awake() via
    // ZNetView.Register("RPC_Damage", ...)). Unlike MineRock5.DamageArea,
    // it opens with `if (!m_nview.IsValid() || !m_nview.IsOwner()) return;`
    // -- its real logic only ever runs on whichever peer currently owns
    // this object's ZDO, not necessarily the attacking player. For a
    // dungeon room several players are mining together, every non-owning
    // peer's own copy of this component never sees m_destroyed flip at
    // all (their client only learns the object is gone via ZNetScene's
    // periodic sync sweep, a bare UnityEngine.Object.Destroy with zero
    // game-specific signal -- confirmed via IL, see ZNetScene.RemoveObjects).
    //
    // So this patch doesn't credit the owning client directly -- it
    // broadcasts to every nearby player instead (MudPileRpc.cs), and each
    // receiving client (including whoever actually triggered it) decides
    // independently whether to credit itself, via a flat range check
    // (CounterRules.WithinMudPileBroadcastRange) rather than only the ZDO
    // owner ever getting counted.
    [HarmonyPatch(typeof(Destructible), "RPC_Damage")]
    public static class MudPilePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Destructible __instance, bool ___m_destroyed) =>
            ClientRuntime.Guard("mud pile count", () =>
            {
                if (__instance == null || !___m_destroyed)
                {
                    return;
                }

                var prefabName = Utils.GetPrefabName(__instance.gameObject);
                if (CounterRules.IsMudPileNowFullyDestroyed(___m_destroyed, prefabName))
                {
                    MudPileRpc.Broadcast(__instance.transform.position);
                }
            });
    }
}
