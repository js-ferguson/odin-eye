namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // Vomit Bomb: eating Blueberries while no other food buff is active.
    // Player.EatFood(ItemDrop.ItemData) is public and returns whether it
    // actually succeeded (false if CanEat() rejects it -- e.g. already full
    // on food memory); Player.GetFoods() (public) returns the live active-
    // food list, so checking it right after a successful eat tells us
    // whether anything else was already active.
    [HarmonyPatch(typeof(Player), "EatFood")]
    public static class VomitBombPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result) =>
            ClientRuntime.Guard("vomit bomb check", () =>
            {
                if (ClientRuntime.Counters == null || !ReferenceEquals(__instance, Player.m_localPlayer))
                {
                    return;
                }

                var activeFoodCount = __instance.GetFoods()?.Count ?? 0;
                if (CounterRules.CountsAsVomitBomb(__result, item?.m_shared?.m_name, activeFoodCount))
                {
                    ClientRuntime.Counters.Increment(CounterRules.VomitBombsKey);
                }
            });
    }
}
