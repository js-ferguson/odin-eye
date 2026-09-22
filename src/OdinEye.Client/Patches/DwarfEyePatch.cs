namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using System;
    using UnityEngine;

    // ODINEYE-38: Dead-Eye Dick. Counts Greydwarf eyes the local player
    // picks up. Humanoid.Pickup(GameObject, ...) is where the game actually
    // adds an item to an inventory -- ItemDrop.Pickup (the interact target)
    // just forwards into it, and it is public.
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
    // "GreydwarfEye" -- so this achievement could never have fired either.
    // Switched to itemData.m_dropPrefab.name (confirmed
    // "GreydwarfEye.prefab" in the game's own asset manifest), the same
    // GameObject.name convention CookingStationPatches.cs already uses.
    [HarmonyPatch(typeof(Humanoid), "Pickup")]
    public static class DwarfEyePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Humanoid __instance, GameObject go, out object __state)
        {
            __state = null;
            var captured = default(object);
            ClientRuntime.Guard("dwarf eye pickup check", () =>
            {
                if (ClientRuntime.Counters == null || !ReferenceEquals(__instance, Player.m_localPlayer) || go == null)
                {
                    return;
                }

                var itemData = go.GetComponent<ItemDrop>()?.m_itemData;
                if (itemData?.m_dropPrefab != null)
                {
                    captured = (itemData.m_dropPrefab.name, itemData.m_stack);
                }
            });
            __state = captured;
        }

        [HarmonyPostfix]
        private static void Postfix(bool __result, object __state) =>
            ClientRuntime.Guard("dwarf eye pickup count", () =>
            {
                if (!__result || !(__state is ValueTuple<string, int> captured))
                {
                    return;
                }

                var toCount = CounterRules.DwarfEyesToCount(true, true, captured.Item1, captured.Item2);
                if (toCount > 0)
                {
                    ClientRuntime.Counters?.Increment(CounterRules.DwarfEyesTouchedKey, toCount);
                }
            });
    }
}
