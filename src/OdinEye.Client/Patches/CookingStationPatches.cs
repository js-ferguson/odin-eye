namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using System;
    using System.Reflection;

    // ODINEYE-38: Baked and This guys cooking up. "You have to take it out
    // manually when it's finished or it will turn to coal", so both counts
    // are of items the player TAKES OUT of a cooking station, never of food
    // merely finishing or burning unattended.
    //
    // Patched on the CookingStation class itself, not any one prefab -- so
    // This guys cooking up counts a burnt item collected from ANY station
    // built on this component (the basic early-game cooking station and the
    // stone oven today, and whatever else is built on it later), not just
    // ovens. Confirmed via CookingStation.Interact's own IL: every prefab's
    // click-to-collect routes through this same OnInteract, with no
    // prefab-specific branch.
    //
    // Taking an item out is CookingStation.OnInteract on the collector's own
    // client: it works out which finished slot comes next -- bread (or any
    // other Done item), or a Burnt one turning into coal -- then asks the
    // station's owner to hand it over with the RPC "RPC_RemoveDoneItem". So:
    // OnInteract's prefix works out what the next removal is, and the RPC
    // call itself carries the exact amount, bonus included.
    public static class CookingStationPatches
    {
        // Set by the OnInteract prefix, consumed by the RPC call that
        // follows it in the same call stack, cleared by the postfix in case
        // the interaction ended without sending one. At most one of the two
        // is ever true: the game's own "next item" logic picks a single
        // slot, either Done or Burnt, never both at once.
        private static bool nextRemovalIsBread;
        private static bool nextRemovalIsBurnt;

        private static readonly MethodInfo GetSlot = AccessTools.Method(typeof(CookingStation), "GetSlot");
        private static readonly MethodInfo GetItemConversion = AccessTools.Method(typeof(CookingStation), "GetItemConversion");

        private static bool Available => GetSlot != null && GetItemConversion != null;

        // The status ("Done"/"Burnt") of the FIRST finished-or-burnt slot,
        // and for Done, what it produces -- the slot the game's own "next
        // item" logic takes first. Null if nothing is finished yet.
        private static (string Status, string ProductName)? NextFinishedSlot(CookingStation station)
        {
            for (var i = 0; i < station.m_slots.Length; i++)
            {
                var args = new object[] { i, null, 0f, null, false };
                GetSlot.Invoke(station, args);
                var itemName = args[1] as string;
                var status = args[3]?.ToString();
                if (string.IsNullOrEmpty(itemName) || (status != "Done" && status != "Burnt"))
                {
                    continue;
                }

                if (status != "Done")
                {
                    return ("Burnt", null);
                }

                var conversion = GetItemConversion.Invoke(station, new object[] { itemName });
                var product = conversion?.GetType().GetField("m_to")?.GetValue(conversion) as UnityEngine.Object;
                return ("Done", product?.name);
            }

            return null;
        }

        [HarmonyPatch(typeof(CookingStation), "OnInteract")]
        public static class OnInteractPatch
        {
            [HarmonyPrepare]
            private static bool Prepare() => Available;

            [HarmonyPrefix]
            private static void Prefix(CookingStation __instance, Humanoid user) =>
                ClientRuntime.Guard("cooking station check", () =>
                {
                    nextRemovalIsBread = false;
                    nextRemovalIsBurnt = false;
                    if (ClientRuntime.Counters == null || !ReferenceEquals(user, Player.m_localPlayer))
                    {
                        return;
                    }

                    var next = NextFinishedSlot(__instance);
                    if (next == null)
                    {
                        return;
                    }

                    if (next.Value.Status == "Burnt")
                    {
                        nextRemovalIsBurnt = true;
                    }
                    else
                    {
                        nextRemovalIsBread = next.Value.ProductName == CounterRules.BreadItemName;
                    }
                });

            [HarmonyPostfix]
            private static void Postfix()
            {
                nextRemovalIsBread = false;
                nextRemovalIsBurnt = false;
            }
        }

        [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.InvokeRPC), new[] { typeof(string), typeof(object[]) })]
        public static class RemoveDoneItemPatch
        {
            [HarmonyPrefix]
            private static void Prefix(object[] __args)
            {
                if (!(__args[0] is string method) || method != "RPC_RemoveDoneItem" || (!nextRemovalIsBread && !nextRemovalIsBurnt))
                {
                    return;
                }

                var isBread = nextRemovalIsBread;
                var isBurnt = nextRemovalIsBurnt;
                nextRemovalIsBread = false;
                nextRemovalIsBurnt = false;

                ClientRuntime.Guard("cooking station count", () =>
                {
                    var rpcArgs = __args[1] as object[];
                    var amount = rpcArgs != null && rpcArgs.Length > 1 ? Convert.ToInt32(rpcArgs[1]) : 1;

                    if (isBread)
                    {
                        var toCount = CounterRules.BreadToCount(true, CounterRules.BreadItemName, amount);
                        if (toCount > 0)
                        {
                            ClientRuntime.Counters?.Increment(CounterRules.BreadCollectedKey, toCount);
                        }
                    }

                    if (isBurnt)
                    {
                        var toCount = CounterRules.FoodBurntToCoal(true, amount);
                        if (toCount > 0)
                        {
                            ClientRuntime.Counters?.Increment(CounterRules.FoodBurntToCoalKey, toCount);
                        }
                    }
                });
            }
        }
    }
}
