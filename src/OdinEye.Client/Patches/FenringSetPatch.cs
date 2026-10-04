namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Counters;

    // Dapper Fenrisian / Fenrisian Warlord (VALSER-96): "equip the full
    // Fenring set" is naturally an EQUIP-TIME event, not a periodic sample
    // like Boat/Swamp/Oven/Encumbrance -- Humanoid.EquipItem (public,
    // confirmed via IL) is the one real entry point for every equip
    // change, and it always finishes by calling Humanoid.SetupEquipment()
    // before returning, so a postfix here sees the FULLY settled
    // equipment state, not a half-updated one. Unlike Raw Dog's
    // weapon-at-postfix-time read, there's no race here: EquipItem has
    // already completed by the time this postfix runs, so there's nothing
    // left to be mid-swap.
    //
    // Inventory.GetEquippedItems() (public, confirmed via IL) and each
    // ItemData.m_dropPrefab/m_quality (both public) are all this needs --
    // no reflection into any private field anywhere, unlike some of this
    // codebase's earlier patches.
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    public static class FenringSetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance) =>
            ClientRuntime.Guard("fenring set equip", () =>
            {
                if (ClientRuntime.Counters == null || __instance == null)
                {
                    return;
                }

                var byLocalPlayer = ReferenceEquals(__instance, Player.m_localPlayer);
                var equipped = __instance.GetInventory()?.GetEquippedItems();

                string helmetPrefabName = null;
                string chestPrefabName = null;
                string legsPrefabName = null;
                var helmetQuality = 0;
                var chestQuality = 0;
                var legsQuality = 0;

                if (equipped != null)
                {
                    foreach (var item in equipped)
                    {
                        var prefabName = Utils.GetPrefabName(item.m_dropPrefab);
                        if (prefabName == CounterRules.HelmetFenringItemName)
                        {
                            helmetPrefabName = prefabName;
                            helmetQuality = item.m_quality;
                        }
                        else if (prefabName == CounterRules.ArmorFenringChestItemName)
                        {
                            chestPrefabName = prefabName;
                            chestQuality = item.m_quality;
                        }
                        else if (prefabName == CounterRules.ArmorFenringLegsItemName)
                        {
                            legsPrefabName = prefabName;
                            legsQuality = item.m_quality;
                        }
                    }
                }

                if (CounterRules.CountsAsDapperFenrisianEquip(byLocalPlayer, helmetPrefabName, chestPrefabName, legsPrefabName))
                {
                    ClientRuntime.Counters.Increment(CounterRules.DapperFenrisianEquippedKey);
                }

                if (CounterRules.CountsAsFenrisianWarlordEquip(byLocalPlayer, helmetPrefabName, helmetQuality, chestPrefabName, chestQuality, legsPrefabName, legsQuality))
                {
                    ClientRuntime.Counters.Increment(CounterRules.FenrisianWarlordEquippedKey);
                }
            });
    }
}
