namespace OdinEye.Client.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using OdinEye.Client.Counters;

    // ODINEYE-36: counters that survive restarts, and never send a value lower
    // than the server already holds (it would reject the WHOLE submission).
    [TestFixture]
    public class CustomCounterStoreTests
    {
        private string dir;
        private string file;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "odineye-counters-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            file = Path.Combine(dir, "counters.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Increment_AddsAndGetReadsBack()
        {
            var store = new CustomCounterStore(file);
            store.Increment("Custom:ArrowHitsEnemy");
            store.Increment("Custom:ArrowHitsEnemy", 4);

            Assert.That(store.Get("Custom:ArrowHitsEnemy"), Is.EqualTo(5f));
            Assert.That(store.Get("Custom:Nothing"), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Increment_IgnoresNonsenseAmounts(float amount)
        {
            var store = new CustomCounterStore(file);
            store.Increment("Custom:K", amount);

            Assert.That(store.Get("Custom:K"), Is.EqualTo(0f));
            Assert.That(store.IsDirty, Is.False);
        }

        [Test]
        public void Flush_ThenANewStoreOnTheSameFile_ReadsTheSameValues()
        {
            var store = new CustomCounterStore(file);
            store.Increment("Custom:BreadCollected", 12);
            Assert.That(store.Flush(), Is.True);
            Assert.That(store.IsDirty, Is.False);

            var reopened = new CustomCounterStore(file);

            Assert.That(reopened.Get("Custom:BreadCollected"), Is.EqualTo(12f));
        }

        [Test]
        public void Flush_WithNothingToWrite_SucceedsAndCreatesNoFile()
        {
            Assert.That(new CustomCounterStore(file).Flush(), Is.True);
            Assert.That(File.Exists(file), Is.False);
        }

        [Test]
        public void Flush_LeavesNoTempFileBehind()
        {
            var store = new CustomCounterStore(file);
            store.Increment("Custom:K");
            store.Flush();
            store.Increment("Custom:K");
            store.Flush();

            Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EquivalentTo(new[] { "counters.json" }));
        }

        [Test]
        public void ACorruptFile_IsKeptAsBad_AndTheStoreStartsEmpty()
        {
            File.WriteAllText(file, "{ this is not json");
            string warning = null;

            var store = new CustomCounterStore(file, w => warning = w);

            Assert.That(store.Snapshot(), Is.Empty);
            Assert.That(File.Exists(file + ".bad"), Is.True);
            Assert.That(warning, Does.Contain("unreadable"));
        }

        [Test]
        public void ACrashBetweenDeleteAndMove_LeavesATempFileThatIsAdopted()
        {
            File.WriteAllText(file + ".tmp", "{\"Custom:K\": 7}");

            var store = new CustomCounterStore(file);

            Assert.That(store.Get("Custom:K"), Is.EqualTo(7f));
        }

        [Test]
        public void SeedFrom_TakesTheHigherOfLocalAndServer_ForOurOwnKeysOnly()
        {
            var store = new CustomCounterStore(file);
            store.Increment("Custom:Low", 3);
            store.Increment("Custom:High", 90);

            store.SeedFrom(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, float>("Custom:Low", 50f),     // server higher: adopt
                new System.Collections.Generic.KeyValuePair<string, float>("Custom:High", 10f),    // local higher: keep
                new System.Collections.Generic.KeyValuePair<string, float>("Derived:Total", 200f), // our other family
                new System.Collections.Generic.KeyValuePair<string, float>("FishCaught", 500f),    // the game's own stat: never adopt
                new System.Collections.Generic.KeyValuePair<string, float>("Custom:Bad", float.NaN)
            });

            Assert.That(store.Get("Custom:Low"), Is.EqualTo(50f));
            Assert.That(store.Get("Custom:High"), Is.EqualTo(90f));
            Assert.That(store.Get("Derived:Total"), Is.EqualTo(200f));
            Assert.That(store.Snapshot().ContainsKey("FishCaught"), Is.False);
            Assert.That(store.Snapshot().ContainsKey("Custom:Bad"), Is.False);
        }

        [Test]
        public void AReinstall_ThatLostTheFile_IsRestoredFromTheServerBeforeAnythingIsSent()
        {
            // The scenario the seeding exists for: the file is gone, the server
            // remembers 500. Without seeding, the first hit would send 1 and the
            // server would reject the whole submission.
            var store = new CustomCounterStore(file);
            store.SeedFrom(new[] { new System.Collections.Generic.KeyValuePair<string, float>("Custom:ArrowHitsEnemy", 500f) });
            store.Increment("Custom:ArrowHitsEnemy");

            Assert.That(store.Get("Custom:ArrowHitsEnemy"), Is.EqualTo(501f));
        }

        [Test]
        public void RaiseTo_NeverLowersAHighWaterMark()
        {
            var store = new CustomCounterStore(file);

            Assert.That(store.RaiseTo("Derived:Total", 40f), Is.EqualTo(40f));
            Assert.That(store.RaiseTo("Derived:Total", 25f), Is.EqualTo(40f), "a smaller recomputed total must not be sent");
            Assert.That(store.RaiseTo("Derived:Total", 60f), Is.EqualTo(60f));
        }

        [Test]
        public void RaiseTo_IgnoresNonsenseValues()
        {
            var store = new CustomCounterStore(file);
            store.RaiseTo("Derived:Total", 40f);

            Assert.That(store.RaiseTo("Derived:Total", float.NaN), Is.EqualTo(40f));
            Assert.That(store.RaiseTo("Derived:Total", -1f), Is.EqualTo(40f));
        }

        [Test]
        public void Snapshot_IsACopy()
        {
            var store = new CustomCounterStore(file);
            store.Increment("Custom:K");
            var snapshot = store.Snapshot();

            store.Increment("Custom:K");

            Assert.That(snapshot["Custom:K"], Is.EqualTo(1f));
        }

        [Test]
        public void AFailedFlush_KeepsTheStoreDirtyAndNeverThrows()
        {
            // A path whose "directory" is actually a file: creating it fails.
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "x");
            string warning = null;
            var store = new CustomCounterStore(Path.Combine(blocker, "counters.json"), w => warning = w);
            store.Increment("Custom:K");

            Assert.That(store.Flush(), Is.False);
            Assert.That(store.IsDirty, Is.True);
            Assert.That(warning, Does.Contain("could not save"));
        }
    }
}
