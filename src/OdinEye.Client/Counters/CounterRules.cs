namespace OdinEye.Client.Counters
{
    // ODINEYE-38: the decisions behind the counters Valheim does not keep,
    // separated from the Harmony patches that gather the facts so they can be
    // tested without a game. Each takes plain values the patch reads.
    public static class CounterRules
    {
        public const string ArrowHitsEnemyKey = "Custom:ArrowHitsEnemy";
        public const string BreadCollectedKey = "Custom:BreadCollected";
        public const string DwarfEyesTouchedKey = "Custom:DwarfEyesTouched";
        public const string FoodBurntToCoalKey = "Custom:FoodBurntToCoal";

        // The item a stone oven produces from bread dough. Burnt bread is a
        // different item (coal), so it can never match.
        public const string BreadItemName = "Bread";

        // Dead-Eye Dick. The crafting material Greydwarfs (and the bushes
        // near them) drop. Like BreadItemName above, the exact shared item
        // name needs live confirmation (ODINEYE-34) before this is trusted.
        public const string DwarfEyeItemName = "GreydwarfEye";

        // Marksman counts an arrow that this player fired hitting a live
        // enemy. Not other players, not tamed animals, not something already
        // dead, and not an arrow someone else fired.
        public static bool CountsAsArrowHitOnEnemy(bool firedByLocalPlayer, string skillName, bool targetIsCharacter, bool targetIsPlayer, bool targetIsTamed, bool targetIsDead) =>
            firedByLocalPlayer
            && skillName == "Bows"
            && targetIsCharacter
            && !targetIsPlayer
            && !targetIsTamed
            && !targetIsDead;

        // How many loaves to add when the local player takes a finished item
        // out of a cooking station: the amount the game is about to ask for
        // (1, plus any bonus yield) if what comes out is finished bread.
        public static int BreadToCount(bool itemIsDone, string producedItemName, int amountTaken) =>
            itemIsDone && producedItemName == BreadItemName && amountTaken > 0 ? amountTaken : 0;

        // This guys cooking up. A cooking station slot that finishes as
        // Burnt always turns into the station's overcooked item (coal) when
        // collected -- whatever food it started as, so no item-name check is
        // needed here the way BreadToCount needs one. Same "amount taken"
        // shape: the RPC that hands it over carries any bonus too.
        public static int FoodBurntToCoal(bool isBurntStatus, int amountTaken) =>
            isBurntStatus && amountTaken > 0 ? amountTaken : 0;

        // Dead-Eye Dick counts the local player's own successful pickups of
        // a Greydwarf eye, by the stack size actually picked up (a bush or a
        // kill can drop more than one at once).
        public static int DwarfEyesToCount(bool byLocalPlayer, bool pickupSucceeded, string itemSharedName, int stack) =>
            byLocalPlayer && pickupSucceeded && itemSharedName == DwarfEyeItemName && stack > 0 ? stack : 0;
    }
}
