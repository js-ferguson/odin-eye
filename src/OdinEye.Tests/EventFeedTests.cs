namespace OdinEye.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using OdinEye.Events;
    using OdinEye.Http;
    using OdinEye.Models.Proto;

    // ODINEYE-35: the pull feed the agent persists. These pin down the
    // properties the consumer depends on: monotonic Seq per boot, a BootId
    // that changes on restart, and a Gap flag that tells it when events fell
    // out of the capped buffer before it asked.
    [TestFixture]
    public class EventFeedTests
    {
        private static GameEvent Event(EventType type = EventType.EnemyKilled, string message = "m") =>
            GameEvent.New(type, message);

        [Test]
        public void Append_AssignsIncreasingSequenceNumbersStartingAtOne()
        {
            var feed = new EventFeed();

            Assert.That(feed.Append(Event()), Is.EqualTo(1));
            Assert.That(feed.Append(Event()), Is.EqualTo(2));
            Assert.That(feed.Append(Event()), Is.EqualTo(3));
        }

        [Test]
        public void Read_AfterZeroReturnsEverythingInOrder()
        {
            var feed = new EventFeed();
            feed.Append(Event(message: "a"));
            feed.Append(Event(message: "b"));

            var result = feed.Read(0, 100);

            Assert.That(result.Events.Select(e => e.Message), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(result.Events.Select(e => e.Seq), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(result.NextSeq, Is.EqualTo(3));
            Assert.That(result.Gap, Is.False);
        }

        [Test]
        public void Read_AfterASeqReturnsOnlyNewerEvents()
        {
            var feed = new EventFeed();
            for (var i = 0; i < 5; i++)
            {
                feed.Append(Event());
            }

            Assert.That(feed.Read(3, 100).Events.Select(e => e.Seq), Is.EqualTo(new long[] { 4, 5 }));
            Assert.That(feed.Read(5, 100).Events, Is.Empty);
        }

        [Test]
        public void Read_HonoursTheLimit()
        {
            var feed = new EventFeed();
            for (var i = 0; i < 10; i++)
            {
                feed.Append(Event());
            }

            Assert.That(feed.Read(0, 3).Events.Select(e => e.Seq), Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        [Test]
        public void EmptyFeed_IsEmptyWithNoGap()
        {
            var result = new EventFeed().Read(0, 10);

            Assert.That(result.Events, Is.Empty);
            Assert.That(result.Gap, Is.False);
            Assert.That(result.NextSeq, Is.EqualTo(1));
        }

        [Test]
        public void TheBufferIsCappedAndDropsTheOldestFirst()
        {
            var feed = new EventFeed(capacity: 3);
            for (var i = 0; i < 5; i++)
            {
                feed.Append(Event());
            }

            Assert.That(feed.Read(0, 100).Events.Select(e => e.Seq), Is.EqualTo(new long[] { 3, 4, 5 }));
        }

        [Test]
        public void Gap_IsTrueWhenTheConsumerAsksForEventsThatWereDropped()
        {
            var feed = new EventFeed(capacity: 3);
            for (var i = 0; i < 5; i++)
            {
                feed.Append(Event()); // 1 and 2 are gone
            }

            Assert.That(feed.Read(0, 100).Gap, Is.True, "asking from the start after events were lost");
            Assert.That(feed.Read(1, 100).Gap, Is.True, "seq 2 was wanted but is gone");
            Assert.That(feed.Read(2, 100).Gap, Is.False, "the oldest held (3) directly follows what the consumer has");
            Assert.That(feed.Read(5, 100).Gap, Is.False);
        }

        [Test]
        public void BootId_IsStableForOneFeedAndDiffersBetweenFeeds()
        {
            var a = new EventFeed();
            var b = new EventFeed();

            Assert.That(a.Read(0, 1).BootId, Is.EqualTo(a.BootId));
            Assert.That(a.BootId, Is.Not.EqualTo(b.BootId));
            Assert.That(new EventFeed(bootId: "fixed").BootId, Is.EqualTo("fixed"));
        }

        [Test]
        public void ChatIsNeverRecorded_EverythingElseIs()
        {
            Assert.That(EventFeed.ShouldRecord(EventType.PlayerChat), Is.False);
            foreach (var type in Enum.GetValues(typeof(EventType)).Cast<EventType>().Where(t => t != EventType.PlayerChat))
            {
                Assert.That(EventFeed.ShouldRecord(type), Is.True, type.ToString());
            }
        }

        [Test]
        public void Entry_CarriesTheEventsFieldsAsJsonReadyValues()
        {
            var player = new Player { Id = Guid.NewGuid(), Name = "Astrid", SteamId = "111" };
            var details = new Dictionary<string, object>
            {
                ["Enemy"] = "$enemy_troll",
                ["Level"] = 3,
                ["Attackers"] = new List<string> { "Astrid" }
            };
            var feed = new EventFeed();
            feed.Append(GameEvent.New(EventType.EnemyKilled, "Astrid killed a troll", player, details));

            var entry = feed.Read(0, 1).Events.Single();

            Assert.That(entry.Type, Is.EqualTo("EnemyKilled"));
            Assert.That(entry.Message, Is.EqualTo("Astrid killed a troll"));
            Assert.That(entry.Player.Name, Is.EqualTo("Astrid"));
            Assert.That(entry.Player.SteamId, Is.EqualTo("111"));
            Assert.That(entry.Player.Id, Is.EqualTo(player.Id.ToString()));
            Assert.That(entry.Data["Enemy"], Is.EqualTo("$enemy_troll"));
            Assert.That(entry.Data["Level"], Is.EqualTo(3));
            Assert.That(DateTime.Parse(entry.Ts).Kind, Is.Not.EqualTo(DateTimeKind.Unspecified));
        }

        [Test]
        public void Entry_WithoutAPlayerOrDetails_HasNullPlayerAndEmptyData()
        {
            var feed = new EventFeed();
            feed.Append(GameEvent.New(EventType.WorldSave, "saved"));

            var entry = feed.Read(0, 1).Events.Single();

            Assert.That(entry.Player, Is.Null);
            Assert.That(entry.Data, Is.Empty);
        }

        [Test]
        public void TheRecordedEventIsNotAffectedByLaterChangesToTheSource()
        {
            var details = new Dictionary<string, object> { ["Level"] = 1 };
            var feed = new EventFeed();
            feed.Append(GameEvent.New(EventType.EnemyKilled, "m", null, details));

            details["Level"] = 99;

            Assert.That(feed.Read(0, 1).Events.Single().Data["Level"], Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentAppends_NeverProduceADuplicateSeq()
        {
            var feed = new EventFeed(capacity: 10000);
            Parallel.For(0, 2000, _ => feed.Append(Event()));

            var seqs = feed.Read(0, 10000).Events.Select(e => e.Seq).ToList();

            Assert.That(seqs.Count, Is.EqualTo(2000));
            Assert.That(seqs.Distinct().Count(), Is.EqualTo(2000));
            Assert.That(seqs, Is.Ordered);
        }

        [Test]
        public void Constructor_RejectsANonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventFeed(capacity: 0));
        }

        // --- query handling -------------------------------------------------------

        [TestCase(null, null, 0L, 500)]
        [TestCase("12", "50", 12L, 50)]
        [TestCase("-4", "0", 0L, 1)]
        [TestCase("abc", "abc", 0L, 500)]
        [TestCase("7", "999999", 7L, 1000)]
        public void EventsQuery_ParsesAndClamps(string afterRaw, string limitRaw, long expectedAfter, int expectedLimit)
        {
            EventsQuery.Parse(afterRaw, limitRaw, out var after, out var limit);

            Assert.That(after, Is.EqualTo(expectedAfter));
            Assert.That(limit, Is.EqualTo(expectedLimit));
        }

        [TestCase("/events", "/events")]
        [TestCase("/events?after=12&limit=500", "/events")]
        [TestCase("/players/stats", "/players/stats")]
        [TestCase("/events?", "/events")]
        public void RoutePath_StripsTheQueryString(string rawUrl, string expected)
        {
            Assert.That(HttpWebServer.RoutePath(rawUrl), Is.EqualTo(expected));
        }
    }
}
