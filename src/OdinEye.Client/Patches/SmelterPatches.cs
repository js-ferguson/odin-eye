namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // VALSER-81: Iron maiden. Smelter.OnEmpty(Switch, Humanoid, ItemDrop.
    // ItemData) is the local player's own click on the smelter's "empty"
    // switch -- confirmed via IL: it checks GetProcessedQueueSize() > 0,
    // then invokes "RPC_EmptyProcessed" with no arguments at all (the RPC
    // itself carries neither an amount nor an item name), so -- the same
    // "capture in a prefix before the RPC clears it" shape
    // CookingStationPatches.cs already uses for its own RPC -- what's
    // about to be collected has to be read here, before OnEmpty runs.
    //
    // Confirmed via IL: the ZDO already holds exactly what SpawnProcessed()
    // itself reads to do the actual spawn -- ZDOVars.s_spawnOre (a string,
    // the queued ORE's prefab name) and ZDOVars.s_spawnAmount (an int, how
    // many are queued) -- both public ZDOVars fields, read via ZDO's own
    // public GetString/GetInt. One ore always yields exactly one bar in
    // vanilla Valheim, so the queued ore amount IS the bar count that's
    // about to be produced; see CounterRules.IronToProcess's own header
    // for why this reads the ore name rather than resolving the produced
    // bar's name through Smelter's ItemConversion table.
    //
    // Smelter.m_nview is private -- read via the "___" convention
    // TameablePatch.cs already established, same as MudPilePatch.cs's own
    // ___m_allDestroyed read.
    [HarmonyPatch(typeof(Smelter), "OnEmpty")]
    public static class SmelterEmptyPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Humanoid user, ZNetView ___m_nview) =>
            ClientRuntime.Guard("smelter empty count", () =>
            {
                if (ClientRuntime.Counters == null || ___m_nview == null || !ReferenceEquals(user, Player.m_localPlayer))
                {
                    return;
                }

                var zdo = ___m_nview.GetZDO();
                if (zdo == null)
                {
                    return;
                }

                var queuedOre = zdo.GetString(ZDOVars.s_spawnOre, "");
                var queuedAmount = zdo.GetInt(ZDOVars.s_spawnAmount, 0);
                var toProcess = CounterRules.IronToProcess(queuedOre, queuedAmount);
                if (toProcess > 0)
                {
                    ClientRuntime.Counters.Increment(CounterRules.IronProcessedKey, toProcess);
                }
            });
    }
}
