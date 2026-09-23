namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // VALSER-81: Joe dirt. LEAST confirmed patch in this batch -- see
    // CounterRules.MudPilePrefabNames' own header for why the component
    // and exact prefab casing are both inferred, not confirmed, and needs
    // a real live hit before this is trusted.
    //
    // MineRock5.DamageArea(int hitAreaIndex, HitData hit) is confirmed
    // via IL as the method that applies a hit to one of a MineRock5
    // object's hit areas (a mud pile, copper deposit, meteorite, etc. all
    // share this one component) -- private, but Harmony patches by
    // reflection regardless of visibility. Its own IL, read end to end,
    // shows it already checks AllDestroyed() (every hit area's health at
    // 0) right after applying the hit, and when true calls
    // m_nview.Destroy() (removes the object from the scene) and sets
    // m_allDestroyed = true, in that same call -- so DamageArea can never
    // fire again afterward for an object this already fired for, and
    // reading m_allDestroyed in a postfix (via the "___" private-field
    // convention TameablePatch.cs already established) needs no
    // false-to-true transition tracking of its own: by construction, it is
    // only ever true on the ONE DamageArea call that pushed the object
    // over the edge.
    //
    // The same IL also shows the game's own code gates ITS OWN native
    // stat increment (right after this) on hit.GetAttacker() ==
    // Player.m_localPlayer -- matched here for the same "I did it, not
    // someone else farming the same node" reason.
    [HarmonyPatch(typeof(MineRock5), "DamageArea")]
    public static class MudPilePatch
    {
        [HarmonyPostfix]
        private static void Postfix(MineRock5 __instance, HitData hit, bool ___m_allDestroyed) =>
            ClientRuntime.Guard("mud pile count", () =>
            {
                if (ClientRuntime.Counters == null || hit == null || __instance == null || !___m_allDestroyed)
                {
                    return;
                }

                var byLocalPlayer = ReferenceEquals(hit.GetAttacker(), Player.m_localPlayer);
                var prefabName = Utils.GetPrefabName(__instance.gameObject);
                if (CounterRules.CountsAsMuddyScrapPileOpened(byLocalPlayer, true, prefabName))
                {
                    ClientRuntime.Counters.Increment(CounterRules.MuddyScrapPilesOpenedKey);
                }
            });
    }
}
