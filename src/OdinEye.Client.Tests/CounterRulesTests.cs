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

        // --- CountsAsMuddyScrapPileOpened (Joe dirt) ---

        [Test]
        public void MudPile_CountsWhenTheLocalPlayersHitFullyDestroysIt()
        {
            Assert.That(CounterRules.CountsAsMuddyScrapPileOpened(true, true, "MudPile"), Is.True);
        }

        [Test]
        public void MudPile_CountsTheSecondSceneVariant()
        {
            Assert.That(CounterRules.CountsAsMuddyScrapPileOpened(true, true, "MudPile2"), Is.True);
        }

        [Test]
        public void MudPile_MatchesCaseInsensitively()
        {
            // The manifest only gives a lowercase FILE name for this one
            // (unlike every other prefab in this batch) -- see
            // CounterRules.MudPilePrefabNames' own header.
            Assert.That(CounterRules.CountsAsMuddyScrapPileOpened(true, true, "mudpile"), Is.True);
        }

        [Test]
        public void MudPile_DoesNotCountBeforeItIsFullyDestroyed()
        {
            Assert.That(CounterRules.CountsAsMuddyScrapPileOpened(true, false, "MudPile"), Is.False);
        }

        [Test]
        public void MudPile_DoesNotCountSomeoneElsesHit()
        {
            Assert.That(CounterRules.CountsAsMuddyScrapPileOpened(false, true, "MudPile"), Is.False);
        }

        [Test]
        public void MudPile_DoesNotCountADifferentMineRock5Deposit()
        {
            Assert.That(CounterRules.CountsAsMuddyScrapPileOpened(true, true, "Copper_deposit"), Is.False);
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
    }
}
