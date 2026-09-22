namespace OdinEye.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using OdinEye.Events;
    using OdinEye.Http.Api.Controllers;
    using OdinEye.Models.Api;

    // ODINEYE-31: the details the achievements engine counts solo / 2-star
    // kills from. ODINEYE-36: the optional, never-blocking client metadata.
    [TestFixture]
    public class EnemyKillDetailsAndMetaTests
    {
        [Test]
        public void ALoneAttackerIsASoloKill()
        {
            var d = EnemyKillDetails.Build("$enemy_troll", 3, false, new[] { "Astrid" });

            Assert.That(d["Enemy"], Is.EqualTo("$enemy_troll"));
            Assert.That(d["Level"], Is.EqualTo(3));
            Assert.That(d["Boss"], Is.False);
            Assert.That((List<string>)d["Attackers"], Is.EqualTo(new[] { "Astrid" }));
            Assert.That(d["Solo"], Is.True);
        }

        [Test]
        public void TwoAttackersIsNotSolo()
        {
            var d = EnemyKillDetails.Build("$enemy_troll", 1, false, new[] { "Astrid", "Bjorn" });

            Assert.That(d["Solo"], Is.False);
            Assert.That((List<string>)d["Attackers"], Has.Count.EqualTo(2));
        }

        [Test]
        public void ADuplicatedNameStillCountsAsOneAttacker()
        {
            Assert.That(EnemyKillDetails.Build("t", 1, false, new[] { "Astrid", "Astrid" })["Solo"], Is.True);
        }

        [Test]
        public void EmptyOrMissingAttackersIsNeverSolo()
        {
            Assert.That(EnemyKillDetails.Build("t", 1, false, new string[0])["Solo"], Is.False);
            Assert.That(EnemyKillDetails.Build("t", 1, false, null)["Solo"], Is.False);
            Assert.That(EnemyKillDetails.Build("t", 1, false, new[] { "" })["Solo"], Is.False);
        }

        [Test]
        public void ABossFlagIsCarriedThrough()
        {
            Assert.That(EnemyKillDetails.Build("$enemy_eikthyr", 1, true, new[] { "Astrid" })["Boss"], Is.True);
        }

        // --- Meta -----------------------------------------------------------------

        [Test]
        public void AValidMetaIsAccepted()
        {
            var ok = CharacterStatsController.TryCleanMeta(
                new SubmissionMeta { PlayerId = "123456789012", ClientVersion = "1.4.0" }, out var clean);

            Assert.That(ok, Is.True);
            Assert.That(clean.PlayerId, Is.EqualTo("123456789012"));
            Assert.That(clean.ClientVersion, Is.EqualTo("1.4.0"));
        }

        [Test]
        public void ANegativePlayerIdIsAcceptedBecauseTheGameUsesALong()
        {
            Assert.That(CharacterStatsController.TryCleanMeta(new SubmissionMeta { PlayerId = "-42" }, out var clean), Is.True);
            Assert.That(clean.PlayerId, Is.EqualTo("-42"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("12 34")]
        [TestCase("1e5")]
        [TestCase("99999999999999999999999")]
        public void ABadPlayerIdMakesTheWholeMetaBeIgnored(string playerId)
        {
            Assert.That(CharacterStatsController.TryCleanMeta(new SubmissionMeta { PlayerId = playerId }, out var clean), Is.False);
            Assert.That(clean, Is.Null);
        }

        [Test]
        public void NoMetaAtAllIsFine()
        {
            Assert.That(CharacterStatsController.TryCleanMeta(null, out var clean), Is.False);
            Assert.That(clean, Is.Null);
        }

        [TestCase("<script>")]
        [TestCase("1.0 beta")]
        [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void AnOddVersionIsDroppedButThePlayerIdIsKept(string version)
        {
            var ok = CharacterStatsController.TryCleanMeta(new SubmissionMeta { PlayerId = "7", ClientVersion = version }, out var clean);

            Assert.That(ok, Is.True);
            Assert.That(clean.PlayerId, Is.EqualTo("7"));
            Assert.That(clean.ClientVersion, Is.Null, "nothing client-supplied of an unexpected shape is stored or echoed back");
        }
    }
}
