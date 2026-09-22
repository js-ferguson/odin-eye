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
    // BUG FIX (confirmed live 2026-09-23: balgore ate Pukeberries with an
    // empty food bar and Custom:VomitBombs never moved): this was
    // originally hooked on Player.EatFood, on the assumption that eating
    // Pukeberries still routes through it. Re-reading Player.EatFood's own
    // IL end to end shows it has NOTHING to do with status effects at all
    // -- no SEMan/StatusEffect reference anywhere in that method. The real
    // entry point is Player.ConsumeItem(Inventory, ItemDrop.ItemData, bool):
    // it applies m_shared.m_consumeStatusEffect via SEMan.AddStatusEffect
    // UNCONDITIONALLY (once CanConsumeItem's own "already have this effect
    // or its category" rejection has passed), and only THEN calls
    // Player.EatFood -- and only if m_shared.m_food > 0. Pukeberries has no
    // food value, so EatFood was never being called for it at all; the old
    // hook could never have fired, regardless of active food count. Moved
    // to ConsumeItem, the actual place the status effect (and therefore
    // this achievement's real trigger) happens.
    //
    // Player.ConsumeItem is public and returns whether it actually
    // succeeded (false if CanConsumeItem() rejects it -- including the
    // "already have this status effect/category" case, so a true result
    // here really does mean the effect was freshly applied); Player.
    // GetFoods() (public) returns the live active-food list.
    //
    // SECOND BUG FIX (confirmed live 2026-09-23: balgore re-tested on the
    // real v1.2.24 build -- confirmed installed via Thunderstore Mod
    // Manager itself, not any in-game/log version string, all of which are
    // hardcoded to 1.0.0.0 -- see ODINEYE-43 -- and Custom:VomitBombs still
    // never moved): item.m_shared.m_name is NOT the item's name -- it is a
    // localization TOKEN ("$item_pukeberries"), confirmed via
    // Character.ShowPickupMessage's own IL, which concatenates
    // "$msg_added " directly with m_shared.m_name and hands the result to
    // Character.Message -- a string only worth localizing if it's built out
    // of tokens. VomitBombItemName ("Pukeberries") could therefore never
    // equal item.m_shared.m_name, regardless of the hook point -- this
    // achievement has never been able to fire, on any build. The correct
    // field is item.m_dropPrefab.name (the item's actual prefab asset,
    // confirmed "Pukeberries.prefab" -> GameObject name "Pukeberries" from
    // the game's own asset manifest) -- the same GameObject.name-based
    // convention CookingStationPatches.cs already uses successfully for
    // Bread/chicken/lox-pie identification. DwarfEyePatch.cs had this same
    // bug -- fixed alongside this one, unconfirmed live until re-tested.
    [HarmonyPatch(typeof(Player), "ConsumeItem")]
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

                if (CounterRules.CountsAsVomitBomb(__result, item?.m_dropPrefab?.name, foodsBeforeEating))
                {
                    ClientRuntime.Counters.Increment(CounterRules.VomitBombsKey);
                }
            });
    }
}
