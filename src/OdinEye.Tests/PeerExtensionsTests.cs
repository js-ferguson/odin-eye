namespace OdinEye.Tests
{
    using NUnit.Framework;
    using OdinEye.Extensions;
    using OdinEye.Models;
    using System.Linq;
    using Player = OdinEye.Models.Api.Player;

    // ToDto()/ToPlayerStats() are pure DTO mapping over a plain Peer --
    // zero ZNet/EnvMan/ZDOMan dependency, unlike ZNetExtensions.GetAllPeers()
    // (which actually builds the Peer list from live game state and stays
    // out of scope for this pass, per ODINEYE-18). Worth testing directly
    // since both are on the same real-time "who's online"/leaderboard path
    // ODINEYE-25 already found one bug on (a different method reusing the
    // wrong peer source) -- these confirm the mapping itself, and the Id
    // computation, stay correct in isolation.
    [TestFixture]
    public class PeerExtensionsTests
    {
        private static Peer MakePeer() => new Peer
        {
            CharacterId = "955131913:1",
            SteamId = "76561198014724303",
            Name = "Balgore",
            Health = 42.5f,
            MaxHealth = 50f,
            Stamina = 88f,
        };

        [Test]
        public void ToDto_MapsEveryFieldFromThePeer()
        {
            var peer = MakePeer();

            var player = new[] { peer }.ToDto().Single();

            Assert.That(player.CharacterId, Is.EqualTo(peer.CharacterId));
            Assert.That(player.SteamId, Is.EqualTo(peer.SteamId));
            Assert.That(player.Name, Is.EqualTo(peer.Name));
            Assert.That(player.Health, Is.EqualTo(peer.Health));
            Assert.That(player.MaxHealth, Is.EqualTo(peer.MaxHealth));
            Assert.That(player.Stamina, Is.EqualTo(peer.Stamina));
        }

        [Test]
        public void ToDto_ComputesTheSameIdNameBasedGuidWould()
        {
            var peer = MakePeer();

            var player = new[] { peer }.ToDto().Single();

            Assert.That(player.Id, Is.EqualTo(NameBasedGuid.NewPlayerGuid(peer.SteamId, peer.Name)));
        }

        [Test]
        public void ToPlayerStats_MapsEveryFieldFromThePeer()
        {
            var peer = MakePeer();

            var stats = new[] { peer }.ToPlayerStats().Single();

            Assert.That(stats.CharacterId, Is.EqualTo(peer.CharacterId));
            Assert.That(stats.Health, Is.EqualTo(peer.Health));
            Assert.That(stats.MaxHealth, Is.EqualTo(peer.MaxHealth));
            Assert.That(stats.Stamina, Is.EqualTo(peer.Stamina));
            Assert.That(stats.Id, Is.EqualTo(NameBasedGuid.NewPlayerGuid(peer.SteamId, peer.Name)));
        }

        [Test]
        public void ToDto_PreservesOrderAndCountAcrossMultiplePeers()
        {
            var peers = new[]
            {
                new Peer { SteamId = "1", Name = "Alpha" },
                new Peer { SteamId = "2", Name = "Beta" },
                new Peer { SteamId = "3", Name = "Gamma" },
            };

            var players = peers.ToDto().ToList();

            Assert.That(players.Select(p => p.Name), Is.EqualTo(new[] { "Alpha", "Beta", "Gamma" }));
        }
    }
}
