namespace OdinEye.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using OdinEye.Events;
    using Player = OdinEye.Models.Proto.Player;
    using OdinEye.Models.Api;
    using OdinEye.Models.Proto;

    // ODINEYE-39: what a client may put in the event feed, and how fast.
    [TestFixture]
    public class ClientEventCleanerTests
    {
        private static readonly Player Astrid = new Player { Id = Guid.NewGuid(), Name = "Astrid", SteamId = "111" };

        private static ClientEvent Removed(string owner = "-123456", string remover = "789", float x = 10, float y = 20, float z = 30) =>
            new ClientEvent { Type = "BedRemoved", OwnerPlayerId = owner, RemoverPlayerId = remover, X = x, Y = y, Z = z };

        private static ClientEvent Missing(float x = 1, float y = 2, float z = 3) =>
            new ClientEvent { Type = "BedMissingAtRespawn", X = x, Y = y, Z = z };

        private static bool Clean(out List<GameEvent> events, params ClientEvent[] sent) =>
            ClientEventCleaner.TryClean(new ClientEventsSubmission { Events = sent.ToList() }, Astrid, out events);

        [Test]
        public void ABedRemoval_BecomesAnEventWithTheFieldsTheEngineReads()
        {
            Assert.That(Clean(out var events, Removed()), Is.True);

            var e = events.Single();
            Assert.That(e.Type, Is.EqualTo(EventType.BedRemoved));
            Assert.That(e.Player, Is.SameAs(Astrid), "attributed to the submitting player");
            Assert.That(e.Details["OwnerPlayerId"], Is.EqualTo("-123456"));
            Assert.That(e.Details["RemoverPlayerId"], Is.EqualTo("789"));
            Assert.That(e.Details["SpawnX"], Is.EqualTo(10f));
            Assert.That(e.Details["SpawnY"], Is.EqualTo(20f));
            Assert.That(e.Details["SpawnZ"], Is.EqualTo(30f));
        }

        [Test]
        public void AMissingBed_BecomesAnEventWithTheLostSpawnPoint()
        {
            Assert.That(Clean(out var events, Missing(4, 5, 6)), Is.True);

            var e = events.Single();
            Assert.That(e.Type, Is.EqualTo(EventType.BedMissingAtRespawn));
            Assert.That(e.Details["LostSpawnX"], Is.EqualTo(4f));
            Assert.That(e.Details["LostSpawnY"], Is.EqualTo(5f));
            Assert.That(e.Details["LostSpawnZ"], Is.EqualTo(6f));
        }

        [Test]
        public void TheFeedMessageNamesNobody()
        {
            Clean(out var events, Removed(), Missing());

            Assert.That(events.Select(e => e.Message), Has.None.Contain("Astrid"));
        }

        [TestCase("EnemyKilled")]
        [TestCase("PlayerChat")]
        [TestCase("bedremoved")]
        [TestCase("")]
        [TestCase(null)]
        public void AnyOtherType_IsRejected(string type)
        {
            Assert.That(Clean(out var events, new ClientEvent { Type = type, OwnerPlayerId = "1", RemoverPlayerId = "2" }), Is.False);
            Assert.That(events, Is.Null);
        }

        [TestCase(null, "1")]
        [TestCase("1", null)]
        [TestCase("abc", "1")]
        [TestCase("1", "1.5")]
        [TestCase("", "1")]
        public void ABedRemovalNeedsBothPlayerIds_AsWholeNumbers(string owner, string remover)
        {
            Assert.That(Clean(out _, Removed(owner: owner, remover: remover)), Is.False);
        }

        [Test]
        public void PlayerIdsAreNormalisedThroughLong()
        {
            Clean(out var events, Removed(owner: "+0042", remover: "-0"));

            Assert.That(events.Single().Details["OwnerPlayerId"], Is.EqualTo("42"));
            Assert.That(events.Single().Details["RemoverPlayerId"], Is.EqualTo("0"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(1e9f)]
        [TestCase(-1e9f)]
        public void ANonsenseCoordinate_IsRejected(float bad)
        {
            Assert.That(Clean(out _, Removed(x: bad)), Is.False);
            Assert.That(Clean(out _, Missing(z: bad)), Is.False);
        }

        [Test]
        public void ABatchIsAllOrNothing()
        {
            Assert.That(Clean(out var events, Removed(), new ClientEvent { Type = "Nope" }), Is.False);
            Assert.That(events, Is.Null);
        }

        [Test]
        public void EmptyNullAndOversizedBatchesAreRejected()
        {
            Assert.That(ClientEventCleaner.TryClean(null, Astrid, out _), Is.False);
            Assert.That(ClientEventCleaner.TryClean(new ClientEventsSubmission(), Astrid, out _), Is.False);
            Assert.That(Clean(out _), Is.False);
            Assert.That(Clean(out _, new ClientEvent[] { null }), Is.False);

            var tooMany = Enumerable.Range(0, ClientEventCleaner.MaxEventsPerRequest + 1).Select(_ => Missing()).ToArray();
            Assert.That(Clean(out _, tooMany), Is.False);
            Assert.That(Clean(out _, tooMany.Take(ClientEventCleaner.MaxEventsPerRequest).ToArray()), Is.True);
        }

        // --- rate limiter -----------------------------------------------------------

        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void RateLimiter_AllowsUpToTheLimitThenRefuses()
        {
            var limiter = new ClientEventRateLimiter(5, TimeSpan.FromMinutes(1));

            Assert.That(limiter.TryAcquire("a", 3, T0), Is.True);
            Assert.That(limiter.TryAcquire("a", 2, T0), Is.True);
            Assert.That(limiter.TryAcquire("a", 1, T0), Is.False);
        }

        [Test]
        public void RateLimiter_ARefusedRequestReservesNothing()
        {
            var limiter = new ClientEventRateLimiter(5, TimeSpan.FromMinutes(1));
            limiter.TryAcquire("a", 4, T0);

            Assert.That(limiter.TryAcquire("a", 3, T0), Is.False);
            Assert.That(limiter.TryAcquire("a", 1, T0), Is.True, "the refused 3 must not have used any slots");
        }

        [Test]
        public void RateLimiter_SlotsFreeUpAsTheWindowPasses()
        {
            var limiter = new ClientEventRateLimiter(2, TimeSpan.FromMinutes(1));
            limiter.TryAcquire("a", 2, T0);

            Assert.That(limiter.TryAcquire("a", 1, T0 + TimeSpan.FromSeconds(59)), Is.False);
            Assert.That(limiter.TryAcquire("a", 1, T0 + TimeSpan.FromSeconds(61)), Is.True);
        }

        [Test]
        public void RateLimiter_PlayersAreIndependent()
        {
            var limiter = new ClientEventRateLimiter(1, TimeSpan.FromMinutes(1));
            limiter.TryAcquire("a", 1, T0);

            Assert.That(limiter.TryAcquire("b", 1, T0), Is.True);
        }

        [Test]
        public void RateLimiter_RejectsBadConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientEventRateLimiter(0, TimeSpan.FromMinutes(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientEventRateLimiter(1, TimeSpan.Zero));
        }
    }
}
