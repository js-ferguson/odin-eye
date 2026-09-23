namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using System;
    using UnityEngine;

    // ODINEYE-38's Dead-Eye Dick, plus VALSER-81's Guck guck 9000/Vlad the
    // impaler/Barking up the wrong tree, plus VALSER-82's Got a woody: five
    // achievements, all "MY successful pickup of item X (or, for wood, ANY
    // of a set), by the stack size picked up" -- the same shape
    // CookingStationPatches.cs already uses for its own four achievements
    // sharing one hook (RemoveDoneItemPatch calls BreadToCount/
    // CookedChickenMeatToCount/LoxPieToCount all against the same captured
    // item), so each new one was added here rather than as its own
    // near-duplicate patch class.
    //
    // Humanoid.Pickup(GameObject, ...) is where the game actually adds an
    // item to an inventory -- ItemDrop.Pickup (the interact target) just
    // forwards into it, and it is public.
    //
    // The ground item's prefab name and stack are read in the prefix,
    // before the original method can destroy the GameObject on a full
    // pickup; __state carries them to the postfix, which only counts if the
    // pickup actually succeeded (the method's own bool return).
    //
    // BUG FIX (found alongside the Vomit Bomb item-identity bug, 2026-09-23,
    // unconfirmed live until re-tested): this used to read
    // itemData.m_shared.m_name, which is a localization TOKEN
    // ("$item_greydwarfeye"), never DwarfEyeItemName's literal
    // "GreydwarfEye" -- so Dead-Eye Dick could never have fired either.
    // Switched to Utils.GetPrefabName(itemData.m_dropPrefab) -- the same
    // helper TameablePatch.cs already established as this codebase's
    // correct way to turn a prefab GameObject into its clean prefab name
    // (confirmed via IL to strip a "(Clone)"/space suffix a raw .name read
    // could carry, which the Vomit Bomb fix's own m_dropPrefab.name read
    // didn't guard against -- low risk there since m_dropPrefab is a
    // static asset reference, not a live instantiated clone, but there is
    // no reason to take the smaller risk here when the safer helper is one
    // call away).
    [HarmonyPatch(typeof(Humanoid), "Pickup")]
    public static class ItemPickupPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Humanoid __instance, GameObject go, out object __state)
        {
            __state = null;
            var captured = default(object);
            ClientRuntime.Guard("item pickup check", () =>
            {
                if (ClientRuntime.Counters == null || !ReferenceEquals(__instance, Player.m_localPlayer) || go == null)
                {
                    return;
                }

                var itemData = go.GetComponent<ItemDrop>()?.m_itemData;
                if (itemData?.m_dropPrefab != null)
                {
                    captured = (Utils.GetPrefabName(itemData.m_dropPrefab), itemData.m_stack);
                }
            });
            __state = captured;
        }

        [HarmonyPostfix]
        private static void Postfix(bool __result, object __state) =>
            ClientRuntime.Guard("item pickup count", () =>
            {
                if (!__result || !(__state is ValueTuple<string, int> captured))
                {
                    return;
                }

                var prefabName = captured.Item1;
                var stack = captured.Item2;
                var counters = ClientRuntime.Counters;

                var dwarfEyes = CounterRules.DwarfEyesToCount(true, true, prefabName, stack);
                if (dwarfEyes > 0)
                {
                    counters?.Increment(CounterRules.DwarfEyesTouchedKey, dwarfEyes);
                }

                var guck = CounterRules.GuckToCount(true, true, prefabName, stack);
                if (guck > 0)
                {
                    counters?.Increment(CounterRules.GuckCollectedKey, guck);
                }

                var bloodBags = CounterRules.BloodBagToCount(true, true, prefabName, stack);
                if (bloodBags > 0)
                {
                    counters?.Increment(CounterRules.BloodBagsCollectedKey, bloodBags);
                }

                var elderBark = CounterRules.ElderBarkToCount(true, true, prefabName, stack);
                if (elderBark > 0)
                {
                    counters?.Increment(CounterRules.ElderBarkCollectedKey, elderBark);
                }

                var wood = CounterRules.WoodToCount(true, true, prefabName, stack);
                if (wood > 0)
                {
                    counters?.Increment(CounterRules.WoodCollectedKey, wood);
                }
            });
    }
}
