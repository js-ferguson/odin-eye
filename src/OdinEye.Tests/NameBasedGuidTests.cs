namespace OdinEye.Tests
{
    using NUnit.Framework;
    using OdinEye.Models;
    using System;

    // The client (OdinEye.Client) and server (OdinEye) independently
    // compute this same GUID from a player's SteamId + character name to
    // agree on "who is this" without ever exchanging an explicit ID --
    // see NameBasedGuid.cs's own header comment. If this ever drifted
    // (a NuGet update to NGuid, an accidental namespace/formula change),
    // every character-stats submission would start failing its
    // IsConnectedPlayer check with no obvious cause -- exactly the kind
    // of silent breakage ODINEYE-25 had to independently re-derive and
    // cross-check by hand in Python against real live server data. These
    // tests pin the algorithm down so that class of regression fails
    // loudly and immediately instead.
    [TestFixture]
    public class NameBasedGuidTests
    {
        [Test]
        public void NewPlayerGuid_SameInputs_AlwaysProducesTheSameGuid()
        {
            var first = NameBasedGuid.NewPlayerGuid("76561198014724303", "Balgore");
            var second = NameBasedGuid.NewPlayerGuid("76561198014724303", "Balgore");

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void NewPlayerGuid_DifferentSteamId_ProducesADifferentGuid()
        {
            var first = NameBasedGuid.NewPlayerGuid("76561198014724303", "Balgore");
            var second = NameBasedGuid.NewPlayerGuid("76561198195907431", "Balgore");

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void NewPlayerGuid_DifferentCharacterName_ProducesADifferentGuid()
        {
            var first = NameBasedGuid.NewPlayerGuid("76561198014724303", "Balgore");
            var second = NameBasedGuid.NewPlayerGuid("76561198014724303", "Guthry");

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void NewPlayerGuid_IsCaseSensitiveOnCharacterName()
        {
            // Confirmed live this session (ODINEYE-25 investigation): the
            // client submits using the character's real display-cased
            // name ("Balgore"), and the server matches against
            // peer.m_playerName -- if the two ever disagreed on casing,
            // this algorithm would silently produce two different GUIDs
            // for what's really the same player.
            var upper = NameBasedGuid.NewPlayerGuid("76561198014724303", "Balgore");
            var lower = NameBasedGuid.NewPlayerGuid("76561198014724303", "balgore");

            Assert.That(lower, Is.Not.EqualTo(upper));
        }

        [Test]
        public void NewPlayerGuid_MatchesTheKnownValueForARealPlayer()
        {
            // Locks the actual algorithm (namespace UUID + NGuid's
            // CreateFromName) against silent drift, not just its
            // determinism -- independently cross-checked this session
            // against the agent's own Python re-derivation
            // (uuid.uuid5) and confirmed live in the running server's
            // /players/stats response for the real player "Balgore".
            var guid = NameBasedGuid.NewPlayerGuid("76561198014724303", "Balgore");

            Assert.That(guid, Is.EqualTo(Guid.Parse("6b4e3aa8-0ad1-5b85-94fd-78e2b835f64a")));
        }
    }
}
