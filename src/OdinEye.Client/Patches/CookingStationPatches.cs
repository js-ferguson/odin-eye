namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using System;
    using System.Reflection;

    // ODINEYE-38: Baked, This guys cooking up, KFC - The Colonel, and Baked
    // as Bro. "You have to take it out manually when it's finished or it
    // will turn to coal", so all counts are of items the player TAKES OUT
    // of a cooking station, never of food merely finishing or burning
    // unattended.
    //
    // Patched on the CookingStation class itself, not any one prefab -- so
    // this counts a matching item collected from ANY station built on this
    // component (the basic early-game cooking station and the stone oven
    // today, and whatever else is built on it later), not just ovens.
    // Confirmed via CookingStation.Interact's own IL: every prefab's
    // click-to-collect routes through this same OnInteract, with no
    // prefab-specific branch.
    //
    // Taking an item out is CookingStation.OnInteract on the collector's own
    // client: it works out which finished slot comes next -- a Done item
    // (bread, cooked chicken, a lox pie, ...), or a Burnt one turning into
    // coal -- then asks the station's owner to hand it over with the RPC
    // "RPC_RemoveDoneItem". So: OnInteract's prefix works out what the next
    // removal is (and, for a Done item, which one by name), and the RPC
    // call itself carries the exact amount, bonus included.
    public static class CookingStationPatches
    {
        // Set by the OnInteract prefix, consumed by the RPC call that
        // follows it in the same call stack, cleared by the postfix in case
        // the interaction ended without sending one. At most one is ever
        // set: the game's own "next item" logic picks a single slot, either
        // Done (with a product name) or Burnt, never both at once.
        private static string nextRemovalProductName;
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
                    nextRemovalProductName = null;
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
                        nextRemovalProductName = next.Value.ProductName;
                    }
                });

            [HarmonyPostfix]
            private static void Postfix()
            {
                nextRemovalProductName = null;
                nextRemovalIsBurnt = false;
            }
        }

        [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.InvokeRPC), new[] { typeof(string), typeof(object[]) })]
        public static class RemoveDoneItemPatch
        {
            [HarmonyPrefix]
            private static void Prefix(object[] __args)
            {
                if (!(__args[0] is string method) || method != "RPC_RemoveDoneItem" || (nextRemovalProductName == null && !nextRemovalIsBurnt))
                {
                    return;
                }

                var productName = nextRemovalProductName;
                var isBurnt = nextRemovalIsBurnt;
                nextRemovalProductName = null;
                nextRemovalIsBurnt = false;

                ClientRuntime.Guard("cooking station count", () =>
                {
                    var rpcArgs = __args[1] as object[];
                    var amount = rpcArgs != null && rpcArgs.Length > 1 ? Convert.ToInt32(rpcArgs[1]) : 1;

                    if (productName != null)
                    {
                        var breadCount = CounterRules.BreadToCount(true, productName, amount);
                        if (breadCount > 0)
                        {
                            ClientRuntime.Counters?.Increment(CounterRules.BreadCollectedKey, breadCount);
                        }

                        var chickenCount = CounterRules.CookedChickenMeatToCount(true, productName, amount);
                        if (chickenCount > 0)
                        {
                            ClientRuntime.Counters?.Increment(CounterRules.ChickenMeatCookedKey, chickenCount);
                        }

                        var loxPieCount = CounterRules.LoxPieToCount(true, productName, amount);
                        if (loxPieCount > 0)
                        {
                            ClientRuntime.Counters?.Increment(CounterRules.LoxPiesCookedKey, loxPieCount);
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
