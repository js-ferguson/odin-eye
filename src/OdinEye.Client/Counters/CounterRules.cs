namespace OdinEye.Client.Counters
{
    // ODINEYE-38: the decisions behind the counters Valheim does not keep,
    // separated from the Harmony patches that gather the facts so they can be
    // tested without a game. Each takes plain values the patch reads.
    public static class CounterRules
    {
        public const string ArrowHitsEnemyKey = "Custom:ArrowHitsEnemy"; // Robin Hood
        public const string BreadCollectedKey = "Custom:BreadCollected";
        public const string DwarfEyesTouchedKey = "Custom:DwarfEyesTouched";
        public const string FoodBurntToCoalKey = "Custom:FoodBurntToCoal"; // Grilled
        public const string TimeOnBoatSecondsKey = "Custom:TimeOnBoatSeconds"; // The Admiral
        public const string VomitBombsKey = "Custom:VomitBombs"; // Vomit Bomb
        public const string FistKillsKey = "Custom:FistKills"; // Mike Tyson
        public const string SwordKillsKey = "Custom:SwordKills"; // Mushashi Master of Blades

        // Peter North. World +Z is north (confirmed via the live game's own
        // world-generation constants: Ashlands sits at large negative Z,
        // Deep North at large positive Z) -- see NorthTracking for where
        // these are actually read from the local player.
        public const string FurthestNorthZKey = "Derived:FurthestNorthZ";
        public const string ReachedDeepNorthKey = "Custom:ReachedDeepNorth";

        // The item a stone oven produces from bread dough. Burnt bread is a
        // different item (coal), so it can never match.
        public const string BreadItemName = "Bread";

        // Dead-Eye Dick. The crafting material Greydwarfs (and the bushes
        // near them) drop. Like BreadItemName above, the exact shared item
        // name needs live confirmation (ODINEYE-34) before this is trusted.
        public const string DwarfEyeItemName = "GreydwarfEye";

        // Vomit Bomb. The brief said "Bukeperries" -- no such item exists in
        // Valheim. Implemented against "Blueberries" (a real, early-game
        // food) as the closest match, needing the same live confirmation as
        // every other item-name guess here.
        public const string VomitBombItemName = "Blueberries";

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

        // Peter North. A submitted stat can never be negative -- the server
        // rejects the whole submission if one is (CharacterStatsController.
        // IsValidStatValue) -- so a position south of world Z=0 (spawn) is
        // floored at 0 rather than sent as-is. That loses nothing: south of
        // spawn is never a contender for "furthest north" anyway, and 0 is
        // indistinguishable from "never left spawn," which is the correct
        // starting point either way.
        public static float NorthDistanceToRaise(float positionZ) => positionZ > 0f ? positionZ : 0f;

        // The Admiral. How much of the elapsed time since the last check to
        // credit toward time on a boat: all of it if the player was on a
        // boat, none of it otherwise. A gap longer than the sanity cap
        // (e.g. the game was suspended, or a computer slept) is dropped
        // rather than credited in full -- a real player was never actually
        // sailing through that whole gap.
        public static float BoatSecondsToAdd(bool isOnBoat, float elapsedSeconds, float maxPlausibleGapSeconds) =>
            isOnBoat && elapsedSeconds > 0f && elapsedSeconds <= maxPlausibleGapSeconds ? elapsedSeconds : 0f;

        // Vomit Bomb: eating counts only when it actually succeeded, the
        // item eaten was Blueberries, and it left exactly one food buff
        // active -- the brief's "no other food items applied".
        public static bool CountsAsVomitBomb(bool eatenSuccessfully, string itemSharedName, int activeFoodCount) =>
            eatenSuccessfully && itemSharedName == VomitBombItemName && activeFoodCount == 1;

        // Mike Tyson / Mushashi Master of Blades: a kill counts when it was
        // MY hit, with the weapon skill this achievement cares about, on a
        // live non-player, non-tamed Character, and it was the hit that
        // actually killed it (checked by the caller immediately after
        // applying the hit) -- not just any hit landed with that weapon.
        public static bool CountsAsWeaponKill(bool byLocalPlayer, string skillName, string wantSkillName, bool targetIsCharacter, bool targetIsPlayer, bool targetIsTamed, bool targetIsDeadNow) =>
            byLocalPlayer
            && skillName == wantSkillName
            && targetIsCharacter
            && !targetIsPlayer
            && !targetIsTamed
            && targetIsDeadNow;
    }
}
