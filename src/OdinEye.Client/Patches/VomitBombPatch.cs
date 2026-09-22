namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // Vomit Bomb: eating Pukeberries while no other food buff is already
    // active -- purely pointlessly, since Pukeberries' entire purpose is to
    // clear existing food buffs (it attaches the SE_Puke status effect,
    // which removes one active food per second; it never becomes a tracked
    // food itself). That means the "no other food items applied" state has
    // to be captured BEFORE eating, in a prefix -- checking Player.GetFoods()
    // afterward would only ever show it heading toward empty, telling us
    // nothing about whether anything was there to begin with.
    //
    // Player.EatFood(ItemDrop.ItemData) is public and returns whether it
    // actually succeeded (false if CanEat() rejects it); Player.GetFoods()
    // (public) returns the live active-food list.
    [HarmonyPatch(typeof(Player), "EatFood")]
    public static class VomitBombPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player __instance, out object __state)
        {
            __state = null;
            var captured = default(object);
            ClientRuntime.Guard("vomit bomb pre-check", () =>
            {
                if (ReferenceEquals(__instance, Player.m_localPlayer))
                {
                    captured = __instance.GetFoods()?.Count ?? 0;
                }
            });
            __state = captured;
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDrop.ItemData item, bool __result, object __state) =>
            ClientRuntime.Guard("vomit bomb count", () =>
            {
                if (ClientRuntime.Counters == null || !(__state is int foodsBeforeEating))
                {
                    return;
                }

                if (CounterRules.CountsAsVomitBomb(__result, item?.m_shared?.m_name, foodsBeforeEating))
                {
                    ClientRuntime.Counters.Increment(CounterRules.VomitBombsKey);
                }
            });
    }
}
