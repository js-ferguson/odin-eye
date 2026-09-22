namespace OdinEye.Client.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using OdinEye.Client.Stats;

    // Gilligan's Island: the local-only landmass-anchor list. Same
    // atomic-write/corrupt-file shape as CustomCounterStoreTests, since
    // OutpostAnchorStore mirrors CustomCounterStore's persistence.
    [TestFixture]
    public class OutpostAnchorStoreTests
    {
        private string dir;
        private string file;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "odineye-outposts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            file = Path.Combine(dir, "outposts.json");
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
        public void Add_ThenAnchors_ReadsThemBack()
        {
            var store = new OutpostAnchorStore(file);
            store.Add(100f, -200f);
            store.Add(300f, 400f);

            Assert.That(store.Anchors, Is.EquivalentTo(new[] { (100f, -200f), (300f, 400f) }));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void Add_IgnoresNonsenseCoordinates(float x, float z)
        {
            var store = new OutpostAnchorStore(file);
            store.Add(x, z);

            Assert.That(store.Anchors, Is.Empty);
        }

        [Test]
        public void Flush_ThenANewStoreOnTheSameFile_ReadsTheSameAnchors()
        {
            var store = new OutpostAnchorStore(file);
            store.Add(50f, 60f);
            Assert.That(store.Flush(), Is.True);

            var reopened = new OutpostAnchorStore(file);

            Assert.That(reopened.Anchors, Is.EquivalentTo(new[] { (50f, 60f) }));
        }

        [Test]
        public void Flush_WithNothingToWrite_SucceedsAndCreatesNoFile()
        {
            Assert.That(new OutpostAnchorStore(file).Flush(), Is.True);
            Assert.That(File.Exists(file), Is.False);
        }

        [Test]
        public void Flush_LeavesNoTempFileBehind()
        {
            var store = new OutpostAnchorStore(file);
            store.Add(1f, 2f);
            store.Flush();
            store.Add(3f, 4f);
            store.Flush();

            Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EquivalentTo(new[] { "outposts.json" }));
        }

        [Test]
        public void ACorruptFile_IsKeptAsBad_AndTheStoreStartsEmpty()
        {
            File.WriteAllText(file, "{ this is not json");
            string warning = null;

            var store = new OutpostAnchorStore(file, w => warning = w);

            Assert.That(store.Anchors, Is.Empty);
            Assert.That(File.Exists(file + ".bad"), Is.True);
            Assert.That(warning, Does.Contain("unreadable"));
        }

        [Test]
        public void ACrashBetweenDeleteAndMove_LeavesATempFileThatIsAdopted()
        {
            File.WriteAllText(file + ".tmp", "[[10.0, 20.0]]");

            var store = new OutpostAnchorStore(file);

            Assert.That(store.Anchors, Is.EquivalentTo(new[] { (10f, 20f) }));
        }

        [Test]
        public void AFailedFlush_KeepsItDirtyAndNeverThrows()
        {
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "x");
            string warning = null;
            var store = new OutpostAnchorStore(Path.Combine(blocker, "outposts.json"), w => warning = w);
            store.Add(1f, 1f);

            Assert.That(store.Flush(), Is.False);
            Assert.That(warning, Does.Contain("could not save"));
        }
    }
}
