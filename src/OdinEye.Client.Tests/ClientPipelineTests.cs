namespace OdinEye.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using OdinEye.Client.Counters;
    using OdinEye.Client.Events;
    using OdinEye.Client.Stats;
    using OdinEye.Client.Submission;
    using OdinEye.Models.Api;

    // ODINEYE-38/39/40: the decisions and plumbing around the game patches --
    // what counts, what gets reported, what is sent and what survives a
    // failure. The patches themselves need the running game.
    [TestFixture]
    public class ClientPipelineTests
    {
        // --- Marksman ----------------------------------------------------------------

        [Test]
        public void AnArrowFiredByMeAtALiveEnemy_Counts()
        {
            Assert.That(CounterRules.CountsAsArrowHitOnEnemy(true, "Bows", true, false, false, false), Is.True);
        }

        [TestCase(false, "Bows", true, false, false, false, TestName = "someone else's arrow")]
        [TestCase(true, "Crossbows", true, false, false, false, TestName = "not a bow")]
        [TestCase(true, null, true, false, false, false, TestName = "no skill")]
        [TestCase(true, "Bows", false, false, false, false, TestName = "hit the ground")]
        [TestCase(true, "Bows", true, true, false, false, TestName = "hit a player")]
        [TestCase(true, "Bows", true, false, true, false, TestName = "hit a tamed animal")]
        [TestCase(true, "Bows", true, false, false, true, TestName = "hit a corpse")]
        public void AnythingElse_DoesNotCount(bool mine, string skill, bool character, bool player, bool tamed, bool dead)
        {
            Assert.That(CounterRules.CountsAsArrowHitOnEnemy(mine, skill, character, player, tamed, dead), Is.False);
        }

        // --- Baked ---------------------------------------------------------------------

        [Test]
        public void FinishedBread_CountsTheWholeAmountIncludingBonus()
        {
            Assert.That(CounterRules.BreadToCount(true, "Bread", 1), Is.EqualTo(1));
            Assert.That(CounterRules.BreadToCount(true, "Bread", 2), Is.EqualTo(2));
        }

        [TestCase(false, "Bread", 1, TestName = "not finished")]
        [TestCase(true, "Coal", 1, TestName = "burnt bread turns to coal")]
        [TestCase(true, "CookedMeat", 1, TestName = "other food")]
        [TestCase(true, null, 1, TestName = "unknown product")]
        [TestCase(true, "Bread", 0, TestName = "nothing taken")]
        [TestCase(true, "Bread", -3, TestName = "nonsense amount")]
        public void ThingsThatAreNotFinishedBread_CountNothing(bool done, string produced, int amount)
        {
            Assert.That(CounterRules.BreadToCount(done, produced, amount), Is.EqualTo(0));
        }

        // --- This guys cooking up ---------------------------------------------------------

        [Test]
        public void BurntFood_CountsTheWholeAmount_RegardlessOfWhatItWas()
        {
            Assert.That(CounterRules.FoodBurntToCoal(true, 1), Is.EqualTo(1));
            Assert.That(CounterRules.FoodBurntToCoal(true, 3), Is.EqualTo(3));
        }

        [TestCase(false, 1, TestName = "not burnt")]
        [TestCase(true, 0, TestName = "nothing taken")]
        [TestCase(true, -2, TestName = "nonsense amount")]
        public void ThingsThatAreNotBurntFood_CountNothing(bool burnt, int amount)
        {
            Assert.That(CounterRules.FoodBurntToCoal(burnt, amount), Is.EqualTo(0));
        }

        // --- Dead-Eye Dick ------------------------------------------------------------------

        [Test]
        public void PickingUpADwarfEye_CountsTheWholeStack()
        {
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "GreydwarfEye", 1), Is.EqualTo(1));
            Assert.That(CounterRules.DwarfEyesToCount(true, true, "GreydwarfEye", 4), Is.EqualTo(4));
        }

        [TestCase(false, true, "GreydwarfEye", 1, TestName = "someone else's pickup")]
        [TestCase(true, false, "GreydwarfEye", 1, TestName = "the pickup failed (inventory full)")]
        [TestCase(true, true, "Wood", 1, TestName = "a different item")]
        [TestCase(true, true, null, 1, TestName = "no item")]
        [TestCase(true, true, "GreydwarfEye", 0, TestName = "an empty stack")]
        [TestCase(true, true, "GreydwarfEye", -1, TestName = "a nonsense stack")]
        public void AnythingElse_TouchesNoDwarfEyes(bool mine, bool succeeded, string item, int stack)
        {
            Assert.That(CounterRules.DwarfEyesToCount(mine, succeeded, item, stack), Is.EqualTo(0));
        }

        // --- Homeless -------------------------------------------------------------------

        [Test]
        public void RemovingSomeoneElsesBed_IsReported()
        {
            var e = BedEvents.Removed(-1234, 99, 1.5f, 2.5f, 3.5f);

            Assert.That(e.Type, Is.EqualTo("BedRemoved"));
            Assert.That(e.OwnerPlayerId, Is.EqualTo("-1234"));
            Assert.That(e.RemoverPlayerId, Is.EqualTo("99"));
            Assert.That((e.X, e.Y, e.Z), Is.EqualTo((1.5f, 2.5f, 3.5f)));
        }

        [Test]
        public void RemovingYourOwnBed_OrAnUnclaimedOne_IsNotReported()
        {
            Assert.That(BedEvents.Removed(99, 99, 0, 0, 0), Is.Null);
            Assert.That(BedEvents.Removed(0, 99, 0, 0, 0), Is.Null);
        }

        [Test]
        public void ARespawnAtTheCircle_CarriesTheLostSpawnPoint()
        {
            var e = BedEvents.MissingAtRespawn(7, 8, 9);

            Assert.That(e.Type, Is.EqualTo("BedMissingAtRespawn"));
            Assert.That((e.X, e.Y, e.Z), Is.EqualTo((7f, 8f, 9f)));
        }

        // --- the event queue ---------------------------------------------------------------

        private static ClientEvent Ev(int n) => BedEvents.MissingAtRespawn(n, 0, 0);

        [Test]
        public void TheQueueHandsBackBatchesInOrder()
        {
            var queue = new ClientEventQueue();
            for (var i = 1; i <= 5; i++)
            {
                queue.Enqueue(Ev(i));
            }

            Assert.That(queue.TakeBatch(3).Select(e => e.X), Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.TakeBatch(10).Select(e => e.X), Is.EqualTo(new[] { 4f, 5f }));
            Assert.That(queue.TakeBatch(10), Is.Empty);
        }

        [Test]
        public void ARequeuedBatchGoesBackInFront_InItsOriginalOrder()
        {
            var queue = new ClientEventQueue();
            for (var i = 1; i <= 4; i++)
            {
                queue.Enqueue(Ev(i));
            }

            var batch = queue.TakeBatch(2);
            queue.Enqueue(Ev(5));
            queue.Requeue(batch);

            Assert.That(queue.TakeBatch(10).Select(e => e.X), Is.EqualTo(new[] { 1f, 2f, 3f, 4f, 5f }));
        }

        [Test]
        public void ANeverReachableServerCannotGrowTheQueueWithoutBound_TheOldestAreDropped()
        {
            var queue = new ClientEventQueue(capacity: 3);
            for (var i = 1; i <= 5; i++)
            {
                queue.Enqueue(Ev(i));
            }

            Assert.That(queue.TakeBatch(10).Select(e => e.X), Is.EqualTo(new[] { 3f, 4f, 5f }));
        }

        [Test]
        public void TheQueueIgnoresNullsAndNullBatches()
        {
            var queue = new ClientEventQueue();
            queue.Enqueue(null);
            queue.Requeue(null);

            Assert.That(queue.Count, Is.EqualTo(0));
        }

        // --- seeding from the server ---------------------------------------------------------

        [Test]
        public void ParseOwnStats_PicksOurCharactersEntry()
        {
            var me = Guid.NewGuid();
            var json = "{\"" + me + "\":{\"Custom:ArrowHitsEnemy\":412,\"FishCaught\":7},\"" + Guid.NewGuid() + "\":{\"Custom:ArrowHitsEnemy\":9}}";

            var own = StatsSeeder.ParseOwnStats(json, me);

            Assert.That(own["Custom:ArrowHitsEnemy"], Is.EqualTo(412f));
            Assert.That(own["FishCaught"], Is.EqualTo(7f));
            Assert.That(own.Count, Is.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{}")]
        public void ParseOwnStats_IsEmptyForAnythingUnusable(string body)
        {
            Assert.That(StatsSeeder.ParseOwnStats(body, Guid.NewGuid()), Is.Empty);
        }

        // --- what is sent ---------------------------------------------------------------------------

        private sealed class FakeSource : IPlayerStatsSource
        {
            public Dictionary<string, float> Stats = new Dictionary<string, float>();
            public IReadOnlyDictionary<string, float> GetStats() => Stats;
        }

        private string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "odineye-pipeline-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        private CustomCounterStore NewStore() => new CustomCounterStore(Path.Combine(dir, "c.json"));

        [Test]
        public void TheCountersRideAlongWithTheGamesOwnStats()
        {
            var store = NewStore();
            store.Increment("Custom:ArrowHitsEnemy", 12);
            var source = new FakeSource { Stats = { ["FishCaught"] = 5f } };

            var stats = new CounterAugmentedStatsSource(source, store, () => null).GetStats();

            Assert.That(stats["FishCaught"], Is.EqualTo(5f));
            Assert.That(stats["Custom:ArrowHitsEnemy"], Is.EqualTo(12f));
        }

        [Test]
        public void WithNoCharacterLoaded_NothingIsSent_NotEvenTheCounters()
        {
            var store = NewStore();
            store.Increment("Custom:ArrowHitsEnemy");

            Assert.That(new CounterAugmentedStatsSource(new FakeSource(), store, () => null).GetStats(), Is.Empty);
        }

        [Test]
        public void TheStationTotalIsDerivedFromPiecePlacedStats()
        {
            var store = NewStore();
            var source = new FakeSource
            {
                Stats =
                {
                    ["PiecePlaced:$piece_workbench"] = 100f,
                    ["PiecePlaced:$piece_chopblock"] = 40f,
                    ["PiecePlaced:$piece_wood_wall"] = 5000f
                }
            };
            var names = new HashSet<string> { "$piece_workbench", "$piece_chopblock" };

            var stats = new CounterAugmentedStatsSource(source, store, () => names).GetStats();

            Assert.That(stats["Derived:StationOrUpgradePlaced"], Is.EqualTo(140f));
        }

        [Test]
        public void TheStationTotalNeverGoesDown_EvenIfALaterScanFindsFewerStations()
        {
            var store = NewStore();
            var source = new FakeSource { Stats = { ["PiecePlaced:$piece_workbench"] = 100f, ["PiecePlaced:$piece_forge"] = 50f } };
            var both = new HashSet<string> { "$piece_workbench", "$piece_forge" };
            var oneOnly = new HashSet<string> { "$piece_workbench" };

            new CounterAugmentedStatsSource(source, store, () => both).GetStats();
            var later = new CounterAugmentedStatsSource(source, store, () => oneOnly).GetStats();

            Assert.That(later["Derived:StationOrUpgradePlaced"], Is.EqualTo(150f));
        }

        [Test]
        public void BeforeTheGameHasLoadedItsPrefabs_NoStationTotalIsInvented()
        {
            var source = new FakeSource { Stats = { ["PiecePlaced:$piece_workbench"] = 100f } };

            var stats = new CounterAugmentedStatsSource(source, NewStore(), () => null).GetStats();

            Assert.That(stats.ContainsKey("Derived:StationOrUpgradePlaced"), Is.False);
        }

        [Test]
        public void TheMetaRidesInTheSubmission_AndIsOptional()
        {
            var raw = new Dictionary<string, float> { ["FishCaught"] = 1f, ["Bad"] = float.NaN };

            var with = CharacterStatsPayloadBuilder.Build(raw, new SubmissionMeta { PlayerId = "-42", ClientVersion = "1.2.3" });
            var without = CharacterStatsPayloadBuilder.Build(raw);

            Assert.That(with.Meta.PlayerId, Is.EqualTo("-42"));
            Assert.That(with.Stats.Keys, Is.EquivalentTo(new[] { "FishCaught" }));
            Assert.That(without.Meta, Is.Null);
        }
    }
}
