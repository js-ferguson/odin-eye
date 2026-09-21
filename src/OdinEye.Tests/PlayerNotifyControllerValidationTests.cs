namespace OdinEye.Tests
{
    using NUnit.Framework;
    using OdinEye.Http.Api.Controllers;

    // IsValidSteamId/IsValidMessage are pure (no ZNet dependency) -- see
    // PlayerNotifyController's own header comment for why the actual
    // peer-lookup/RPC-send path (the real, unverified risk this ticket
    // disclosed, ODINEYE-33) can't be covered here and needs a live check
    // instead.
    [TestFixture]
    public class PlayerNotifyControllerValidationTests
    {
        [Test]
        public void RejectsAnEmptySteamId()
        {
            Assert.That(PlayerNotifyController.IsValidSteamId(""), Is.False);
        }

        [Test]
        public void RejectsANonNumericSteamId()
        {
            Assert.That(PlayerNotifyController.IsValidSteamId("abc123"), Is.False);
        }

        [Test]
        public void AcceptsARealLookingSteamId()
        {
            Assert.That(PlayerNotifyController.IsValidSteamId("76561198014724303"), Is.True);
        }

        [Test]
        public void RejectsANullMessage()
        {
            Assert.That(PlayerNotifyController.IsValidMessage(null), Is.False);
        }

        [Test]
        public void RejectsAWhitespaceOnlyMessage()
        {
            Assert.That(PlayerNotifyController.IsValidMessage("   "), Is.False);
        }

        [Test]
        public void RejectsAMessagePastThePlausibleLength()
        {
            Assert.That(PlayerNotifyController.IsValidMessage(new string('x', 201)), Is.False);
        }

        [Test]
        public void AcceptsAMessageAtExactlyTheLengthCeiling()
        {
            Assert.That(PlayerNotifyController.IsValidMessage(new string('x', 200)), Is.True);
        }

        [Test]
        public void AcceptsAnOrdinaryMessage()
        {
            Assert.That(PlayerNotifyController.IsValidMessage("Your server sub is due -- see /player/contribute!"), Is.True);
        }
    }
}
