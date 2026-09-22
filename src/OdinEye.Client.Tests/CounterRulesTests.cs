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
    }
}
