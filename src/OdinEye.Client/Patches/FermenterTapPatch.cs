namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using System.Linq;
    using System.Reflection;

    // ODINEYE-38: Punky Brewster. Fermenter.Interact (public) runs on the
    // COLLECTING player's own client, not the fermenter's owner: it checks
    // GetStatus() == Ready then invokes RPC_Tap with no amount argument.
    // RPC_Tap (private, owner-gated) schedules the actual item spawn via
    // Unity's Invoke() on the owner's machine only -- confirmed via IL that
    // this is never broadcast back to the collector. So the collector has
    // to read what it is ABOUT to get BEFORE calling the real Interact,
    // the same "read data pre-RPC" shape CookingStationPatches already
    // uses for Bread/Grilled/KFC/Baked as Bro.
    //
    // GetStatus() and GetContent() are private, so they are invoked via
    // reflection like CookingStation's GetSlot/GetItemConversion already
    // are. GetContent() returns a content hash read straight off the
    // fermenter's own ZDO (confirmed via IL: ZNetView.GetZDO().GetInt(...)),
    // which is genuinely readable from any client since ZDO state is
    // synced -- Fermenter.m_conversion (the recipe table) is a PUBLIC
    // field, so matching that hash against each entry's input item name
    // (via assembly_utils' StringExtensionMethods.GetStableHashCode, the
    // same hash function Fermenter.GetItemConversion itself uses) needs no
    // further reflection at all.
    [HarmonyPatch(typeof(Fermenter), "Interact")]
    public static class FermenterTapPatch
    {
        private static readonly MethodInfo GetStatus = AccessTools.Method(typeof(Fermenter), "GetStatus");
        private static readonly MethodInfo GetContent = AccessTools.Method(typeof(Fermenter), "GetContent");

        private static bool Available => GetStatus != null && GetContent != null;

        [HarmonyPrepare]
        private static bool Prepare() => Available;

        [HarmonyPrefix]
        private static void Prefix(Fermenter __instance, Humanoid user) =>
            ClientRuntime.Guard("fermenter tap check", () =>
            {
                if (ClientRuntime.Counters == null || !ReferenceEquals(user, Player.m_localPlayer))
                {
                    return;
                }

                var isReady = GetStatus.Invoke(__instance, null)?.ToString() == "Ready";
                var contentHash = isReady ? (int)GetContent.Invoke(__instance, null) : 0;
                var conversion = contentHash != 0
                    ? __instance.m_conversion?.FirstOrDefault(c =>
                        c?.m_from != null && StringExtensionMethods.GetStableHashCode(c.m_from.gameObject.name) == contentHash)
                    : null;

                var toCount = CounterRules.MeadsToCount(isReady, conversion?.m_producedItems ?? 0);
                if (toCount > 0)
                {
                    ClientRuntime.Counters.Increment(CounterRules.MeadsMadeKey, toCount);
                }
            });
    }
}
