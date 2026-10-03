namespace OdinEye.Client.Tests
{
    using NUnit.Framework;
    using OdinEye.Client.Counters;

    // Pure decision functions behind the Harmony patches -- see
    // CounterRules.cs's own docstring. Covers the two rules hit by the
    // 2026-09-23 item-identity bug fix (Vomit Bomb, Dead-Eye Dick): both
    // used to compare against ItemData.m_shared.m_name (a localization
    // token) instead of ItemData.m_dropPrefab.name (the item's actual
    // prefab name, which these constants -- CounterRules.VomitBombItemName,
    // CounterRules.DwarfEyeItemName -- are set to), so neither achievement
    // could ever fire. These tests exercise the rule functions with the
    // CORRECT (prefab-name) input, matching what the patches pass in now.
    [TestFixture]
    public class CounterRulesTests
    {
        // --- CountsAsVomitBomb -------------------------------------------------------

        [Test]
        public void VomitBomb_CountsOnAnEmptyStomach()
        {
            Assert.That(CounterRules.CountsAsVomitBomb(true, "Pukeberries", 0), Is.True);
        }

        [Test]
        public void VomitBomb_DoesNotCountWithAnyFoodActive()
        {
            Assert.That(CounterRules.CountsAsVomitBomb(true, "Pukeberries", 1), Is.False);
            Assert.That(CounterRules.CountsAsVomitBomb(true, "Pukeberries", 3), Is.False);
        }

        [Test]
        public void VomitBomb_DoesNotCountWhenConsumeItemRejectedTheEat()
        {
            // e.g. CanConsumeItem's own "already have this status effect or
            // its category" rejection -- Player.ConsumeItem's __result is
            // false, so nothing was actually applied.
            Assert.That(CounterRules.CountsAsVomitBomb(false, "Pukeberries", 0), Is.False);
        }

        [Test]
        public void VomitBomb_DoesNotCountADifferentItem()
        {
            Assert.That(CounterRules.CountsAsVomitBomb(true, "Blueberries", 0), Is.False);
        }

        [Test]
        public void VomitBomb_DoesNotCountTheLocalizationTokenAnymore()
        {
            // The bug this fix removed: m_shared.m_name would have been
            // "$item_pukeberries", never the prefab name -- confirming the
            // rule itself doesn't (and shouldn't) accept that token.
            Assert.That(CounterRules.CountsAsVomitBomb(true, "$item_pukeberries", 0), Is.False);
        }

        [Test]
        public void VomitBomb_DoesNotCountANullItemName()
        {
            Assert.That(CounterRules.CountsAsVomitBomb(true, null, 0), Is.False);
        }

        // --- DwarfEyesToCount ---------------------------------------------------------

        [Test]
        public void DwarfEyes_CountsAStackedPickupByTheLocalPlayer()
        {
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "GreydwarfEye", 3), Is.EqualTo(3));
        }

        [Test]
        public void DwarfEyes_DoesNotCountAnotherPlayersPickup()
        {
            Assert.That(CounterRules.DwarfEyesToCount(false, true, "GreydwarfEye", 1), Is.EqualTo(0));
        }

        [Test]
        public void DwarfEyes_DoesNotCountAFailedPickup()
        {
            Assert.That(CounterRules.DwarfEyesToCount(true, false, "GreydwarfEye", 1), Is.EqualTo(0));
        }

        [Test]
        public void DwarfEyes_DoesNotCountADifferentItem()
        {
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "Wood", 1), Is.EqualTo(0));
        }

        [Test]
        public void DwarfEyes_DoesNotCountTheLocalizationTokenAnymore()
        {
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "$item_greydwarfeye", 1), Is.EqualTo(0));
        }

        [Test]
        public void DwarfEyes_DoesNotCountAZeroOrNegativeStack()
        {
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "GreydwarfEye", 0), Is.EqualTo(0));
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "GreydwarfEye", -1), Is.EqualTo(0));
        }

        // --- VALSER-81 batch (2026-09-23) ---------------------------------------------

        // --- CountsAsPoisonDeath (Venom) ---

        [Test]
        public void PoisonDeath_CountsWhenTheKillingHitWasPoison()
        {
            Assert.That(CounterRules.CountsAsPoisonDeath(true, true, 5f), Is.True);
        }

        [Test]
        public void PoisonDeath_DoesNotCountSomeoneElseDying()
        {
            Assert.That(CounterRules.CountsAsPoisonDeath(false, true, 5f), Is.False);
        }

        [Test]
        public void PoisonDeath_DoesNotCountANonFatalPoisonHit()
        {
            Assert.That(CounterRules.CountsAsPoisonDeath(true, false, 5f), Is.False);
        }

        [Test]
        public void PoisonDeath_DoesNotCountAFatalHitWithNoPoisonInIt()
        {
            Assert.That(CounterRules.CountsAsPoisonDeath(true, true, 0f), Is.False);
        }

        // --- CountsAsLeechHit (Suckie Suckie) ---

        [Test]
        public void LeechHit_CountsFromTheOpenSwampLeech()
        {
            Assert.That(CounterRules.CountsAsLeechHit(true, "Leech"), Is.True);
        }

        [Test]
        public void LeechHit_CountsFromTheCaveLeech()
        {
            Assert.That(CounterRules.CountsAsLeechHit(true, "Leech_cave"), Is.True);
        }

        [Test]
        public void LeechHit_DoesNotCountAHitOnSomeoneElse()
        {
            Assert.That(CounterRules.CountsAsLeechHit(false, "Leech"), Is.False);
        }

        [Test]
        public void LeechHit_DoesNotCountADifferentAttacker()
        {
            Assert.That(CounterRules.CountsAsLeechHit(true, "Boar"), Is.False);
        }

        [Test]
        public void LeechHit_DoesNotCountAnAttackerlessHit()
        {
            // Fall/drowning/environmental damage: GetAttacker() is null,
            // which the patch turns into a null prefab name here.
            Assert.That(CounterRules.CountsAsLeechHit(true, null), Is.False);
        }

        // --- GuckToCount (Guck guck 9000) ---

        [Test]
        public void Guck_CountsAStackedPickupByTheLocalPlayer()
        {
            Assert.That(CounterRules.GuckToCount(true, true, "Guck", 4), Is.EqualTo(4));
        }

        [Test]
        public void Guck_DoesNotCountTheLocalizationTokenOrAnotherItem()
        {
            Assert.That(CounterRules.GuckToCount(true, true, "$item_guck", 1), Is.EqualTo(0));
            Assert.That(CounterRules.GuckToCount(true, true, "GuckSack", 1), Is.EqualTo(0));
        }

        [Test]
        public void Guck_DoesNotCountAnotherPlayersOrFailedPickup()
        {
            Assert.That(CounterRules.GuckToCount(false, true, "Guck", 1), Is.EqualTo(0));
            Assert.That(CounterRules.GuckToCount(true, false, "Guck", 1), Is.EqualTo(0));
        }

        // --- BloodBagToCount (Vlad the impaler) ---

        [Test]
        public void BloodBag_CountsAStackedPickupByTheLocalPlayer()
        {
            Assert.That(CounterRules.BloodBagToCount(true, true, "Bloodbag", 2), Is.EqualTo(2));
        }

        [Test]
        public void BloodBag_DoesNotCountTheLocalizationTokenOrADifferentItem()
        {
            Assert.That(CounterRules.BloodBagToCount(true, true, "$item_bloodbag", 1), Is.EqualTo(0));
            Assert.That(CounterRules.BloodBagToCount(true, true, "Bilebag", 1), Is.EqualTo(0));
        }

        // --- ElderBarkToCount (Barking up the wrong tree) ---

        [Test]
        public void ElderBark_CountsAStackedPickupByTheLocalPlayer()
        {
            Assert.That(CounterRules.ElderBarkToCount(true, true, "ElderBark", 10), Is.EqualTo(10));
        }

        [Test]
        public void ElderBark_DoesNotCountTheLocalizationTokenOrADifferentItem()
        {
            Assert.That(CounterRules.ElderBarkToCount(true, true, "$item_elderbark", 1), Is.EqualTo(0));
            Assert.That(CounterRules.ElderBarkToCount(true, true, "FineWood", 1), Is.EqualTo(0));
        }

        // --- SilverOreToCount (Johnny Silverhand, VALSER-89) ---

        [Test]
        public void SilverOre_CountsAStackedPickupByTheLocalPlayer()
        {
            Assert.That(CounterRules.SilverOreToCount(true, true, "SilverOre", 5), Is.EqualTo(5));
        }

        [Test]
        public void SilverOre_DoesNotCountTheLocalizationTokenOrTheSmeltedBar()
        {
            Assert.That(CounterRules.SilverOreToCount(true, true, "$item_silverore", 1), Is.EqualTo(0));
            // "Silver" is the smelted bar, a different item -- this
            // achievement is about mining ore, not smelting it.
            Assert.That(CounterRules.SilverOreToCount(true, true, "Silver", 1), Is.EqualTo(0));
        }

        [Test]
        public void SilverOre_DoesNotCountAnotherPlayersOrFailedPickup()
        {
            Assert.That(CounterRules.SilverOreToCount(false, true, "SilverOre", 1), Is.EqualTo(0));
            Assert.That(CounterRules.SilverOreToCount(true, false, "SilverOre", 1), Is.EqualTo(0));
        }

        [Test]
        public void SilverOre_DoesNotCountAZeroStackPickup()
        {
            Assert.That(CounterRules.SilverOreToCount(true, true, "SilverOre", 0), Is.EqualTo(0));
        }

        // --- IsMudPileNowFullyDestroyed (Joe dirt, VALSER-87) ---
        // No "byLocalPlayer" cases anymore: RPC_Damage only ever runs its
        // real logic on whichever peer owns the ZDO, not necessarily the
        // attacker, so attribution moved to a broadcast-and-range-check
        // instead (see WithinMudPileBroadcastRange below) rather than
        // gating here on who dealt the hit.

        [Test]
        public void MudPile_CountsWhenFullyDestroyed()
        {
            Assert.That(CounterRules.IsMudPileNowFullyDestroyed(true, "MudPile"), Is.True);
        }

        [Test]
        public void MudPile_CountsTheSecondSceneVariant()
        {
            Assert.That(CounterRules.IsMudPileNowFullyDestroyed(true, "MudPile2"), Is.True);
        }

        [Test]
        public void MudPile_MatchesCaseInsensitively()
        {
            // The manifest only gives a lowercase FILE name for this one
            // (unlike every other prefab in this batch) -- see
            // CounterRules.MudPilePrefabNames' own header.
            Assert.That(CounterRules.IsMudPileNowFullyDestroyed(true, "mudpile"), Is.True);
        }

        [Test]
        public void MudPile_DoesNotCountBeforeItIsFullyDestroyed()
        {
            Assert.That(CounterRules.IsMudPileNowFullyDestroyed(false, "MudPile"), Is.False);
        }

        [Test]
        public void MudPile_DoesNotCountADifferentDestructible()
        {
            Assert.That(CounterRules.IsMudPileNowFullyDestroyed(true, "Beech_Stub"), Is.False);
        }

        [Test]
        public void MudPile_DoesNotCountANullPrefabName()
        {
            Assert.That(CounterRules.IsMudPileNowFullyDestroyed(true, null), Is.False);
        }

        // --- WithinMudPileBroadcastRange (Joe dirt, VALSER-87) ---
        // The receiving half of the cross-client broadcast: every
        // connected player's own client computes its own distance to the
        // destroyed pile's position (a Vector3.Distance call, kept in
        // MudPileRpc.cs so this rule itself stays engine-free) and runs
        // that through this to decide whether it was close enough to
        // credit -- see MudPileRpc.cs.

        [Test]
        public void MudPileRange_CountsAPlayerStandingOnTopOfIt()
        {
            Assert.That(CounterRules.WithinMudPileBroadcastRange(0f), Is.True);
        }

        [Test]
        public void MudPileRange_CountsAPlayerExactlyAtTheRadius()
        {
            Assert.That(CounterRules.WithinMudPileBroadcastRange(CounterRules.MudPileBroadcastRangeMeters), Is.True);
        }

        [Test]
        public void MudPileRange_DoesNotCountAPlayerBeyondTheRadius()
        {
            Assert.That(CounterRules.WithinMudPileBroadcastRange(CounterRules.MudPileBroadcastRangeMeters + 0.01f), Is.False);
        }

        // --- IronToProcess (Iron maiden) ---

        [Test]
        public void Iron_CountsTheQueuedAmountWhenOreIsIronScrap()
        {
            Assert.That(CounterRules.IronToProcess("IronScrap", 5), Is.EqualTo(5));
        }

        [Test]
        public void Iron_DoesNotCountADifferentOre()
        {
            Assert.That(CounterRules.IronToProcess("CopperOre", 5), Is.EqualTo(0));
            Assert.That(CounterRules.IronToProcess("TinOre", 5), Is.EqualTo(0));
        }

        [Test]
        public void Iron_DoesNotCountAnEmptyQueue()
        {
            Assert.That(CounterRules.IronToProcess("IronScrap", 0), Is.EqualTo(0));
            Assert.That(CounterRules.IronToProcess("", 0), Is.EqualTo(0));
        }

        // --- VALSER-82 batch (2026-09-23) -----------------------------------------------

        // --- SwampSecondsToAdd (Stink Fish) ---

        [Test]
        public void SwampSeconds_CreditsTheGapWhileInTheSwamp()
        {
            Assert.That(CounterRules.SwampSecondsToAdd(true, 30f, 300f), Is.EqualTo(30f));
        }

        [Test]
        public void SwampSeconds_CreditsNothingOutsideTheSwamp()
        {
            Assert.That(CounterRules.SwampSecondsToAdd(false, 30f, 300f), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void SwampSeconds_CreditsNothingForAZeroOrNegativeGap(float elapsed)
        {
            Assert.That(CounterRules.SwampSecondsToAdd(true, elapsed, 300f), Is.EqualTo(0f));
        }

        [Test]
        public void SwampSeconds_DropsAGapLongerThanTheSanityCap()
        {
            Assert.That(CounterRules.SwampSecondsToAdd(true, 1200f, 300f), Is.EqualTo(0f));
        }

        // --- WoodToCount (Got a woody) ---

        [TestCase("Wood")]
        [TestCase("RoundLog")]
        [TestCase("FineWood")]
        [TestCase("Blackwood")]
        [TestCase("Frostwood")]
        [TestCase("YggdrasilWood")]
        public void Wood_CountsEveryRecognizedWoodType(string prefabName)
        {
            Assert.That(CounterRules.WoodToCount(true, true, prefabName, 5), Is.EqualTo(5));
        }

        [Test]
        public void Wood_DoesNotCountElderBarkOrAnUnrelatedItem()
        {
            // ElderBark is a real tree material but its own separate
            // achievement's currency (Barking up the wrong tree) -- must
            // not double-count here.
            Assert.That(CounterRules.WoodToCount(true, true, "ElderBark", 1), Is.EqualTo(0));
            Assert.That(CounterRules.WoodToCount(true, true, "Stone", 1), Is.EqualTo(0));
        }

        [Test]
        public void Wood_DoesNotCountAnotherPlayersOrFailedPickup()
        {
            Assert.That(CounterRules.WoodToCount(false, true, "Wood", 1), Is.EqualTo(0));
            Assert.That(CounterRules.WoodToCount(true, false, "Wood", 1), Is.EqualTo(0));
        }

        [Test]
        public void Wood_DoesNotCountAZeroOrNegativeStack()
        {
            Assert.That(CounterRules.WoodToCount(true, true, "Wood", 0), Is.EqualTo(0));
            Assert.That(CounterRules.WoodToCount(true, true, "Wood", -1), Is.EqualTo(0));
        }

        // --- VALSER-83 (2026-09-23) -------------------------------------------------

        // --- StoneToCount (Stoned) ---

        [TestCase("Stone")]
        [TestCase("Flint")]
        public void Stone_CountsEveryRecognizedStoneType(string prefabName)
        {
            Assert.That(CounterRules.StoneToCount(true, true, prefabName, 5), Is.EqualTo(5));
        }

        [Test]
        public void Stone_DoesNotCountARarerNamedMaterialOrAnUnrelatedItem()
        {
            // Obsidian/BlackMarble/etc are their own distinctly-named,
            // rarer materials -- not colloquially "stone" -- same
            // reasoning WoodItemNames excludes ElderBark for.
            Assert.That(CounterRules.StoneToCount(true, true, "Obsidian", 1), Is.EqualTo(0));
            Assert.That(CounterRules.StoneToCount(true, true, "BlackMarble", 1), Is.EqualTo(0));
            Assert.That(CounterRules.StoneToCount(true, true, "Wood", 1), Is.EqualTo(0));
        }

        [Test]
        public void Stone_DoesNotCountAnotherPlayersOrFailedPickup()
        {
            Assert.That(CounterRules.StoneToCount(false, true, "Stone", 1), Is.EqualTo(0));
            Assert.That(CounterRules.StoneToCount(true, false, "Stone", 1), Is.EqualTo(0));
        }

        [Test]
        public void Stone_DoesNotCountAZeroOrNegativeStack()
        {
            Assert.That(CounterRules.StoneToCount(true, true, "Stone", 0), Is.EqualTo(0));
            Assert.That(CounterRules.StoneToCount(true, true, "Stone", -1), Is.EqualTo(0));
        }

        // --- VALSER-84 (2026-09-23) -------------------------------------------------

        // --- BakerySecondsToAdd (Baker's High) ---

        [Test]
        public void BakerySeconds_CreditsTheGapWhileNearAnOven()
        {
            Assert.That(CounterRules.BakerySecondsToAdd(true, 30f, 300f), Is.EqualTo(30f));
        }

        [Test]
        public void BakerySeconds_CreditsNothingAwayFromAnOven()
        {
            Assert.That(CounterRules.BakerySecondsToAdd(false, 30f, 300f), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void BakerySeconds_CreditsNothingForAZeroOrNegativeGap(float elapsed)
        {
            Assert.That(CounterRules.BakerySecondsToAdd(true, elapsed, 300f), Is.EqualTo(0f));
        }

        [Test]
        public void BakerySeconds_DropsAGapLongerThanTheSanityCap()
        {
            Assert.That(CounterRules.BakerySecondsToAdd(true, 1200f, 300f), Is.EqualTo(0f));
        }

        // --- VALSER-85 (2026-09-23) --------------------------------------------------

        // --- ResinToCount (Sticky fingers) ---

        [Test]
        public void Resin_CountsAStackedPickupByTheLocalPlayer()
        {
            Assert.That(CounterRules.ResinToCount(true, true, "Resin", 5), Is.EqualTo(5));
        }

        [Test]
        public void Resin_DoesNotCountCharcoalResinOrAnUnrelatedItem()
        {
            // CharcoalResin is its own distinctly-named, later-game
            // material -- not what a player means by plain "resin".
            Assert.That(CounterRules.ResinToCount(true, true, "CharcoalResin", 1), Is.EqualTo(0));
            Assert.That(CounterRules.ResinToCount(true, true, "Wood", 1), Is.EqualTo(0));
        }

        [Test]
        public void Resin_DoesNotCountAnotherPlayersOrFailedPickup()
        {
            Assert.That(CounterRules.ResinToCount(false, true, "Resin", 1), Is.EqualTo(0));
            Assert.That(CounterRules.ResinToCount(true, false, "Resin", 1), Is.EqualTo(0));
        }

        [Test]
        public void Resin_DoesNotCountAZeroOrNegativeStack()
        {
            Assert.That(CounterRules.ResinToCount(true, true, "Resin", 0), Is.EqualTo(0));
            Assert.That(CounterRules.ResinToCount(true, true, "Resin", -1), Is.EqualTo(0));
        }

        // --- VALSER-90 batch ---------------------------------------------------------

        // --- CountsAsEscapeArtistDeath (Escape Artist) ---

        [Test]
        public void EscapeArtistDeath_CountsWhenKilledByAnEnemyWithTheMenuOpen()
        {
            Assert.That(CounterRules.CountsAsEscapeArtistDeath(true, true, true, true), Is.True);
        }

        [Test]
        public void EscapeArtistDeath_DoesNotCountSomeoneElseDying()
        {
            Assert.That(CounterRules.CountsAsEscapeArtistDeath(false, true, true, true), Is.False);
        }

        [Test]
        public void EscapeArtistDeath_DoesNotCountANonFatalHit()
        {
            Assert.That(CounterRules.CountsAsEscapeArtistDeath(true, false, true, true), Is.False);
        }

        [Test]
        public void EscapeArtistDeath_DoesNotCountAPvpOrEnvironmentalDeath()
        {
            // killedByEnemy is already resolved from HitData.HitType ==
            // EnemyHit by the patch -- false covers PlayerHit, Fall,
            // Drowning, and everything else that isn't an enemy attack.
            Assert.That(CounterRules.CountsAsEscapeArtistDeath(true, true, false, true), Is.False);
        }

        [Test]
        public void EscapeArtistDeath_DoesNotCountWithTheMenuClosed()
        {
            Assert.That(CounterRules.CountsAsEscapeArtistDeath(true, true, true, false), Is.False);
        }

        // --- CountsAsTerriblePalsyDeath (Terrible Palsy) ---

        [Test]
        public void TerriblePalsyDeath_CountsWhenKilledByAnEnemyWhileOverburdenedAndOutOfStamina()
        {
            Assert.That(CounterRules.CountsAsTerriblePalsyDeath(true, true, true, true, true), Is.True);
        }

        [Test]
        public void TerriblePalsyDeath_DoesNotCountSomeoneElseDying()
        {
            Assert.That(CounterRules.CountsAsTerriblePalsyDeath(false, true, true, true, true), Is.False);
        }

        [Test]
        public void TerriblePalsyDeath_DoesNotCountANonFatalHit()
        {
            Assert.That(CounterRules.CountsAsTerriblePalsyDeath(true, false, true, true, true), Is.False);
        }

        [Test]
        public void TerriblePalsyDeath_DoesNotCountAPvpOrEnvironmentalDeath()
        {
            Assert.That(CounterRules.CountsAsTerriblePalsyDeath(true, true, false, true, true), Is.False);
        }

        [Test]
        public void TerriblePalsyDeath_DoesNotCountWithoutBeingOverburdened()
        {
            Assert.That(CounterRules.CountsAsTerriblePalsyDeath(true, true, true, false, true), Is.False);
        }

        [Test]
        public void TerriblePalsyDeath_DoesNotCountWithAnyStaminaRemaining()
        {
            Assert.That(CounterRules.CountsAsTerriblePalsyDeath(true, true, true, true, false), Is.False);
        }

        // --- OverburdenedDistanceToAdd (Palsy) ---

        [Test]
        public void OverburdenedDistance_CreditsTheFullDistanceWhileOverburdened()
        {
            Assert.That(CounterRules.OverburdenedDistanceToAdd(true, 12f, 5f, 300f, 10f), Is.EqualTo(12f));
        }

        [Test]
        public void OverburdenedDistance_CreditsNothingWhenNotOverburdened()
        {
            Assert.That(CounterRules.OverburdenedDistanceToAdd(false, 12f, 5f, 300f, 10f), Is.EqualTo(0f));
        }

        [Test]
        public void OverburdenedDistance_CreditsNothingForZeroOrNegativeDistance()
        {
            Assert.That(CounterRules.OverburdenedDistanceToAdd(true, 0f, 5f, 300f, 10f), Is.EqualTo(0f));
            Assert.That(CounterRules.OverburdenedDistanceToAdd(true, -1f, 5f, 300f, 10f), Is.EqualTo(0f));
        }

        [Test]
        public void OverburdenedDistance_CreditsNothingPastTheMaxPlausibleGap()
        {
            // The game was suspended/the computer slept -- same guard as
            // Boat/Swamp/BakerySecondsToAdd.
            Assert.That(CounterRules.OverburdenedDistanceToAdd(true, 12f, 301f, 300f, 10f), Is.EqualTo(0f));
        }

        [Test]
        public void OverburdenedDistance_CreditsNothingForAnImplausiblySuddenJump()
        {
            // 100m in 5s is 20 m/s -- well past the 10 m/s cap, i.e. a
            // portal hop between two samples, not real walking.
            Assert.That(CounterRules.OverburdenedDistanceToAdd(true, 100f, 5f, 300f, 10f), Is.EqualTo(0f));
        }

        [Test]
        public void OverburdenedDistance_CreditsExactlyAtTheSpeedCapBoundary()
        {
            // 50m in 5s is exactly 10 m/s -- the boundary itself still counts.
            Assert.That(CounterRules.OverburdenedDistanceToAdd(true, 50f, 5f, 300f, 10f), Is.EqualTo(50f));
        }
    }
}
