namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using UnityEngine;

    // VALSER-87: the cross-client half of the Joe dirt fix -- see
    // MudPilePatch.cs's own header for why this exists (only the ZDO
    // owner's client ever sees a mud pile's real destroy logic fire, not
    // necessarily the attacker, and never any other nearby player at all).
    //
    // ZRoutedRpc is Valheim's own peer-to-peer custom-message primitive
    // (confirmed via IL: ZRoutedRpc.instance.Register<T>(name, handler) /
    // InvokeRoutedRPC(name, parameters) broadcasts to every connected
    // peer, each of which independently runs the registered handler --
    // there is no built-in radius-limited delivery, so "nearby" is a
    // plain distance check each receiving client makes for itself against
    // the position carried in the message). Vector3 is a directly
    // ZPackage-serializable parameter type (confirmed via IL,
    // ZPackage::Write(Vector3) is a real overload), so the destroyed
    // pile's own position travels as-is, no encoding needed.
    //
    // Registration has to happen once ZRoutedRpc itself actually exists --
    // confirmed via IL there's no Awake() on this class, only a
    // constructor (.ctor(bool server)), so that's the patch target, the
    // same pattern most Valheim mods use for a custom routed RPC.
    [HarmonyPatch(typeof(ZRoutedRpc))]
    public static class MudPileRpc
    {
        public const string RpcName = "OdinEye_MudPileOpened";

        [HarmonyPatch(MethodType.Constructor, typeof(bool))]
        [HarmonyPostfix]
        private static void RegisterPostfix(ZRoutedRpc __instance) =>
            ClientRuntime.Guard("mud pile rpc register", () =>
            {
                __instance?.Register<Vector3>(RpcName, OnMudPileOpened);
            });

        // Called from MudPilePatch's own postfix once it has confirmed the
        // destroyed object really is a mud pile -- broadcasts to every
        // connected peer, including whichever one is about to receive its
        // own message straight back (deliberate: see OnMudPileOpened,
        // treating the triggering player the same as everyone else rather
        // than special-casing them).
        public static void Broadcast(Vector3 pilePosition) =>
            ClientRuntime.Guard("mud pile rpc broadcast", () =>
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(RpcName, new object[] { pilePosition });
            });

        // The receiving half, run once per connected client (the
        // triggering player's own client included) for every broadcast.
        // senderPeerID (ZRoutedRpc's own convention: the first parameter
        // on every registered handler) is deliberately unused -- credit
        // is decided purely by "am I close enough to where it happened",
        // not by who sent the message.
        private static void OnMudPileOpened(long senderPeerID, Vector3 pilePosition) =>
            ClientRuntime.Guard("mud pile rpc receive", () =>
            {
                if (ClientRuntime.Counters == null || Player.m_localPlayer == null)
                {
                    return;
                }

                var distance = Vector3.Distance(Player.m_localPlayer.transform.position, pilePosition);
                if (CounterRules.WithinMudPileBroadcastRange(distance))
                {
                    ClientRuntime.Counters.Increment(CounterRules.MuddyScrapPilesOpenedKey);
                }
            });
    }
}
