namespace OdinEye.Client.Counters
{
    using System.Collections.Generic;
    using System.Linq;

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
        public const string ChickenMeatCookedKey = "Custom:ChickenMeatCooked"; // KFC - The Colonel
        public const string LoxPiesCookedKey = "Custom:LoxPiesCooked"; // Baked as Bro
        public const string MeadsMadeKey = "Custom:MeadsMade"; // Punky Brewster
        public const string OutpostLandmassesKey = "Custom:OutpostLandmasses"; // Gilligan's Island

        // VALSER-81 batch (2026-09-23). Rooted (EnemyKill:Abomination) and
        // Darryl Wraithway (EnemyKill:Wraith) need no counter of their own
        // at all -- PlayerProfileStatsSource already submits a native
        // EnemyKill:<prefab> entry for every credited kill (see that
        // class's own ODINEYE-29 header), the same signal Fuck the
        // Police/Mangey Dog already ride for Boar/Wolf. Only the four that
        // need a NEW counter this mod doesn't already track are here.
        public const string PoisonDeathsKey = "Custom:PoisonDeaths"; // Venom
        public const string LeechHitsKey = "Custom:LeechHits"; // Suckie Suckie
        public const string GuckCollectedKey = "Custom:GuckCollected"; // Guck guck 9000
        public const string BloodBagsCollectedKey = "Custom:BloodBagsCollected"; // Vlad the impaler
        public const string MuddyScrapPilesOpenedKey = "Custom:MuddyScrapPilesOpened"; // Joe dirt
        public const string ElderBarkCollectedKey = "Custom:ElderBarkCollected"; // Barking up the wrong tree
        public const string IronProcessedKey = "Custom:IronProcessed"; // Iron maiden

        // VALSER-82 batch (2026-09-23). Got a woody/Cradle snatcher were
        // corrected the same day, per explicit feedback, from an invented
        // "leaderboard" completion threshold to "sole_leader" -- a single
        // transferable title with no finish at all (admin-panel side only;
        // no client-side change from that correction).
        public const string TimeInSwampSecondsKey = "Custom:TimeInSwampSeconds"; // Stink Fish
        public const string WoodCollectedKey = "Custom:WoodCollected"; // Got a woody
        public const string BedsDestroyedKey = "Custom:BedsDestroyed"; // Cradle snatcher

        // VALSER-83 (2026-09-23): Stoned, "same as Got a woody, but stone".
        public const string StoneCollectedKey = "Custom:StoneCollected";

        // VALSER-84 (2026-09-23): Baker's High. Ordinary counter_threshold
        // achievement (NOT a sole-title rule) -- every player who
        // personally reaches the threshold earns it, and the counter
        // keeps growing afterward same as every other counter here (no
        // "stop counting once earned" mechanic exists or is needed).
        public const string TimeInBakerySecondsKey = "Custom:TimeInBakerySeconds";

        // Farmer Joe. One counter per tameable species this live build
        // actually has (confirmed via IL class search: no Asksvin or Moose
        // class exists in this build, so those two are out of scope until
        // the server updates to a build that adds them). Keyed on the
        // ZDO's prefab name -- the same raw token the game's own
        // EnemyKill:<name> stat already uses (Fuck the Police/Mangey Dog
        // key off "Boar"/"Wolf" directly) -- rather than any display name,
        // to sidestep localization entirely.
        public const string TamedBoarKey = "Custom:TamedBoar";
        public const string TamedWolfKey = "Custom:TamedWolf";
        public const string TamedLoxKey = "Custom:TamedLox";

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
        // near them) drop -- this is the item's PREFAB name (confirmed
        // "GreydwarfEye.prefab" in the game's own asset manifest), matched
        // against ItemData.m_dropPrefab.name, NOT ItemData.m_shared.m_name
        // (a localization token, "$item_greydwarfeye" -- see
        // DwarfEyePatch.cs's header, fixed 2026-09-23 alongside the same
        // bug in Vomit Bomb; unconfirmed live until re-tested).
        public const string DwarfEyeItemName = "GreydwarfEye";

        // Vomit Bomb. The brief said "Bukeperries" -- the real item's
        // prefab name is "Pukeberries" (confirmed "Pukeberries.prefab" in
        // the game's own asset manifest). It is not a normal food: eating
        // it attaches the SE_Puke status effect (SharedData.
        // m_consumeStatusEffect), which removes one active food buff per
        // second for its duration -- it never becomes a tracked food
        // itself. The status effect is applied in Player.ConsumeItem, NOT
        // Player.EatFood -- confirmed live 2026-09-23 that the original
        // EatFood hook never fired for Pukeberries at all (see
        // VomitBombPatch.cs's own header for the full story: EatFood only
        // runs when the item's m_food > 0, which Pukeberries never has).
        // SECOND bug, found the same day on re-test: this is matched
        // against ItemData.m_dropPrefab.name, NOT ItemData.m_shared.m_name
        // -- the latter is a localization token ("$item_pukeberries"), so
        // the achievement could never fire even once the hook was fixed
        // (see VomitBombPatch.cs's header for the full IL evidence).
        public const string VomitBombItemName = "Pukeberries";

        // KFC - The Colonel. What a cooking station produces from raw
        // chicken meat. Like BreadItemName, needs live confirmation
        // (ODINEYE-34) before this is trusted -- the same class of bug the
        // Pukeberries fix was about.
        public const string CookedChickenItemName = "CookedChickenMeat";

        // Baked as Bro. What a cooking station produces from an unbaked lox
        // pie. Like CookedChickenItemName, needs live confirmation
        // (ODINEYE-34) before this is trusted.
        public const string LoxMeatPieItemName = "LoxMeatPie";

        // VALSER-81 batch, all matched against ItemData.m_dropPrefab.name
        // (the Vomit Bomb/Dead-Eye Dick lesson, applied from the start this
        // time rather than found the hard way) -- every one confirmed as
        // the item's own prefab file name in the game's own asset manifest,
        // not yet confirmed live via an actual pickup.
        public const string GuckItemName = "Guck"; // Guck guck 9000 ("materials/Guck.prefab")
        public const string BloodBagItemName = "Bloodbag"; // Vlad the impaler ("materials/Bloodbag.prefab")
        // Barking up the wrong tree. The brief said "ancient bark" -- the
        // real dropped material (from Mistlands' Yggdrasil roots) is
        // "Elder Bark" in game, prefab "ElderBark" ("materials/
        // ElderBark.prefab"). "AncientBark" only exists as part of a
        // WEAPON's name (SpearAncientbark/AncientSpear) built from it, not
        // as its own pickup -- there is no standalone "AncientBark" item.
        public const string ElderBarkItemName = "ElderBark";

        // Suckie Suckie. Two Leech prefabs exist in this build's asset
        // manifest -- Leech (open swamp water) and Leech_cave (sunken
        // crypts) -- both should count as "a leech", so this is checked
        // against a set, not a single name.
        public static readonly HashSet<string> LeechPrefabNames = new HashSet<string> { "Leech", "Leech_cave" };

        // Joe dirt. Two scene variants in this build's asset manifest
        // (mudpile/mudpile_frac and mudpile2/mudpile2_frac -- the paired
        // whole-mesh + fracture-mesh files are MineRock5's own signature
        // asset shape, the same technique Copper/Meteorite/Black Marble
        // deposits use). LEAST confirmed of this whole batch: the manifest
        // only gives lowercase FILE names, unlike every other prefab name
        // in this file (which all matched their file name's exact casing)
        // -- so this is matched case-insensitively, and needs a real live
        // hit to confirm both the component (MineRock5) and the casing.
        public static readonly HashSet<string> MudPilePrefabNames =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "MudPile", "MudPile2" };

        // Iron maiden. What a Smelter's own s_spawnOre ZDO var holds while
        // an Iron Scrap is being processed (Smelter.QueueProcessed/
        // SpawnProcessed, confirmed via IL) -- the RAW ORE's prefab name,
        // not the finished bar's. One ore always yields exactly one bar in
        // vanilla Valheim, so counting queued ore amount IS counting bars
        // produced; matching the ore name directly avoids a second
        // ItemConversion lookup for the same result the game already
        // spawns 1:1.
        public const string IronScrapItemName = "IronScrap";

        // Got a woody. "All types of wood" per the brief -- every raw wood
        // material this build's asset manifest has, base Meadows wood
        // through the two Ashlands/Mountain refined woods and Mistlands'
        // Yggdrasil wood (each confirmed as its own "materials/*.prefab"
        // entry). Deliberately excludes ElderBark: a real tree material,
        // but its own separate achievement's currency (Barking up the
        // wrong tree), and colloquially "wood" doesn't mean bark.
        public static readonly HashSet<string> WoodItemNames =
            new HashSet<string> { "Wood", "RoundLog", "FineWood", "Blackwood", "Frostwood", "YggdrasilWood" };

        // Stoned. Deliberately narrower than WoodItemNames above: only
        // Stone and Flint, the two common, unprocessed "pick it straight
        // up" stone-family materials (each confirmed as its own
        // "materials/*.prefab" entry, with its own distinct item icon in
        // the game's own asset manifest, separate from "StoneRock" --
        // unconfirmed whether that is a real second carryable stone item
        // or something else entirely, so left out rather than guessed
        // in). Excludes Obsidian/BlackMarble/the three gemstones/
        // Thunderstone/SulfurStone/SharpeningStone -- all distinctly-
        // named, rarer materials no player calls "stone", the same
        // reasoning WoodItemNames already excludes ElderBark for.
        public static readonly HashSet<string> StoneItemNames = new HashSet<string> { "Stone", "Flint" };

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

        // KFC - The Colonel. Same shape as BreadToCount: only counts when
        // the item taken out is actually cooked chicken meat, by the stack
        // size actually taken (bonus yield included).
        public static int CookedChickenMeatToCount(bool itemIsDone, string producedItemName, int amountTaken) =>
            itemIsDone && producedItemName == CookedChickenItemName && amountTaken > 0 ? amountTaken : 0;

        // Baked as Bro. Same shape as BreadToCount/CookedChickenMeatToCount:
        // only counts when the item taken out is actually a baked lox pie.
        public static int LoxPieToCount(bool itemIsDone, string producedItemName, int amountTaken) =>
            itemIsDone && producedItemName == LoxMeatPieItemName && amountTaken > 0 ? amountTaken : 0;

        // Farmer Joe. Which counter (if any) a freshly-tamed creature's
        // prefab name should credit -- null for anything outside the
        // three species this build supports (including Hen, which is
        // never tamed through Tameable.Tame() to begin with, so it would
        // never reach here anyway).
        public static string TamedSpeciesCounterKey(string prefabName)
        {
            switch (prefabName)
            {
                case "Boar": return TamedBoarKey;
                case "Wolf": return TamedWolfKey;
                case "Lox": return TamedLoxKey;
                default: return null;
            }
        }

        // Punky Brewster. Fermenter.RPC_Tap (owner-gated, private) computes
        // the actual spawned amount only on the ZDO owner's machine and
        // never broadcasts it, so the collecting client credits itself
        // BEFORE tapping instead: producedItems is read off the matching
        // Fermenter.ItemConversion entry's own public m_producedItems field
        // (6 for most mead bases, 3 for Berserkir -- confirmed via web
        // research, and this reads the live recipe table directly rather
        // than hardcoding either number), only when the fermenter's own
        // GetStatus() says Ready -- anything else means there is nothing to
        // credit yet (or the recipe couldn't be resolved).
        public static int MeadsToCount(bool statusIsReady, int producedItems) =>
            statusIsReady && producedItems > 0 ? producedItems : 0;

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

        // Vomit Bomb: Pukeberries clears existing food buffs rather than
        // adding one of its own, so "no other food items applied" has to be
        // checked on the state BEFORE eating, not after (after is always
        // heading toward zero regardless, since that is what it does) --
        // eating it while genuinely nothing was active, i.e. entirely
        // pointlessly, is the joke.
        public static bool CountsAsVomitBomb(bool eatenSuccessfully, string itemSharedName, int activeFoodCountBeforeEating) =>
            eatenSuccessfully && itemSharedName == VomitBombItemName && activeFoodCountBeforeEating == 0;

        // Gilligan's Island. An outpost = a roofed bed with a crafting-
        // station-family piece and a portal both nearby (see
        // OutpostTracking for the radius and how "nearby"/"roofed" are
        // actually read from the live world). The brief said "a covered,
        // bed, crafting bench with a portal in close vicinity" -- read as
        // the BED being the covered one, not the bench or portal needing
        // their own roof; flagged on the ticket as an interpretation to
        // confirm, not a certainty.
        public static bool IsQualifyingOutpost(bool bedIsCovered, bool hasCraftingStationNearby, bool hasPortalNearby) =>
            bedIsCovered && hasCraftingStationNearby && hasPortalNearby;

        // Gilligan's Island. A qualifying outpost counts as a NEW landmass
        // only if it is ocean-separated from EVERY landmass already
        // credited -- an empty list (the very first outpost ever) is
        // vacuously new. This is a heuristic, not real landmass/graph
        // connectivity: OutpostTracking decides "ocean-separated" by
        // sampling WorldGenerator's own biome along the straight line to
        // each already-credited anchor, which can misjudge a roundabout
        // land bridge as separate or clip a third landmass and miss a
        // real separation -- an accepted approximation for a low-stakes
        // achievement, not a target for more precision on its own.
        public static bool IsNewLandmass(IEnumerable<bool> oceanSeparatedFromEachKnownLandmass) =>
            oceanSeparatedFromEachKnownLandmass.All(separated => separated);

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

        // Venom: the hit that just killed ME (not one I dealt) had nonzero
        // poison damage in it. "Just killed me" is IsDead() read AFTER
        // Character.ApplyDamage returns, the same postfix-timing WeaponKillPatch
        // already relies on for its own "was this hit the killing blow" check.
        public static bool CountsAsPoisonDeath(bool targetIsLocalPlayer, bool targetIsDeadNow, float poisonDamageInHit) =>
            targetIsLocalPlayer && targetIsDeadNow && poisonDamageInHit > 0f;

        // Suckie Suckie: a hit landed ON me (any amount, doesn't need to be
        // the killing blow, doesn't need to be poison) by something whose
        // prefab is a leech.
        public static bool CountsAsLeechHit(bool targetIsLocalPlayer, string attackerPrefabName) =>
            targetIsLocalPlayer && attackerPrefabName != null && LeechPrefabNames.Contains(attackerPrefabName);

        // Guck guck 9000: same shape as DwarfEyesToCount -- MY successful
        // pickup of this specific item, by the stack size actually picked
        // up.
        public static int GuckToCount(bool byLocalPlayer, bool pickupSucceeded, string itemPrefabName, int stack) =>
            byLocalPlayer && pickupSucceeded && itemPrefabName == GuckItemName && stack > 0 ? stack : 0;

        // Vlad the impaler: same shape.
        public static int BloodBagToCount(bool byLocalPlayer, bool pickupSucceeded, string itemPrefabName, int stack) =>
            byLocalPlayer && pickupSucceeded && itemPrefabName == BloodBagItemName && stack > 0 ? stack : 0;

        // Barking up the wrong tree: same shape.
        public static int ElderBarkToCount(bool byLocalPlayer, bool pickupSucceeded, string itemPrefabName, int stack) =>
            byLocalPlayer && pickupSucceeded && itemPrefabName == ElderBarkItemName && stack > 0 ? stack : 0;

        // Joe dirt: the hit that just fully destroyed a mud pile (every hit
        // area's health at 0 -- MineRock5.AllDestroyed(), read via its own
        // m_allDestroyed field right after DamageArea applies a hit) was
        // dealt by me, against a prefab this build recognizes as a mud
        // pile. The object is removed from the scene (ZNetView.Destroy())
        // in the same call that sets m_allDestroyed, so DamageArea can never
        // fire again for it afterward -- no double-count risk from reading
        // "is now fully destroyed" rather than "just transitioned".
        public static bool CountsAsMuddyScrapPileOpened(bool byLocalPlayer, bool nowFullyDestroyed, string prefabName) =>
            byLocalPlayer && nowFullyDestroyed && prefabName != null && MudPilePrefabNames.Contains(prefabName);

        // Iron maiden: however much ore a Smelter's OnEmpty is about to
        // collect, but only when it's Iron Scrap -- 0 for any other ore
        // (Copper/Tin/Silver all share the same Smelter component and
        // OnEmpty hook, distinguished only by this queued ore name) or an
        // empty queue.
        public static int IronToProcess(string queuedOreName, int queuedAmount) =>
            queuedOreName == IronScrapItemName && queuedAmount > 0 ? queuedAmount : 0;

        // Stink Fish: real elapsed time since the LAST check to credit, if
        // the player is standing in the Swamp biome right now -- exact same
        // shape as BoatSecondsToAdd (The Admiral), including the sanity cap
        // against a suspended game/slept computer producing one enormous
        // bogus gap.
        public static float SwampSecondsToAdd(bool isInSwamp, float elapsedSeconds, float maxPlausibleGapSeconds) =>
            isInSwamp && elapsedSeconds > 0f && elapsedSeconds <= maxPlausibleGapSeconds ? elapsedSeconds : 0f;

        // Got a woody: MY successful pickup of any wood-family item, by the
        // stack size picked up -- same shape as the other pickup counters,
        // but checks membership in WoodItemNames rather than a single name.
        public static int WoodToCount(bool byLocalPlayer, bool pickupSucceeded, string itemPrefabName, int stack) =>
            byLocalPlayer && pickupSucceeded && stack > 0 && itemPrefabName != null && WoodItemNames.Contains(itemPrefabName)
                ? stack : 0;

        // Stoned: same shape as WoodToCount, against StoneItemNames.
        public static int StoneToCount(bool byLocalPlayer, bool pickupSucceeded, string itemPrefabName, int stack) =>
            byLocalPlayer && pickupSucceeded && stack > 0 && itemPrefabName != null && StoneItemNames.Contains(itemPrefabName)
                ? stack : 0;

        // Baker's High: real elapsed time since the LAST check to credit,
        // if the player is in the vicinity of an oven right now -- exact
        // same shape as BoatSecondsToAdd/SwampSecondsToAdd, including the
        // sanity cap against a suspended game/slept computer.
        public static float BakerySecondsToAdd(bool isNearOven, float elapsedSeconds, float maxPlausibleGapSeconds) =>
            isNearOven && elapsedSeconds > 0f && elapsedSeconds <= maxPlausibleGapSeconds ? elapsedSeconds : 0f;
    }
}
