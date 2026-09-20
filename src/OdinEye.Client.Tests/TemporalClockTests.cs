namespace OdinEye.Client.Tests
{
    using NUnit.Framework;
    using OdinEye.Client.Hud;

    // Cross-checked against agent/app.py's own _temporal_clock()
    // (valheim_server repo, VALSER-40/41) with a standalone Python run
    // of the identical formula -- these two implementations must always
    // agree, or the in-game HUD clock and the admin panel's nav clock
    // would show different times for the same moment.
    [TestFixture]
    public class TemporalClockTests
    {
        [TestCase(0, "12:00 AM")]
        [TestCase(270, "6:00 AM")] // dawn
        [TestCase(900, "12:00 PM")]
        [TestCase(1530, "6:00 PM")] // dusk
        [TestCase(1799, "11:59 PM")]
        [TestCase(1800, "12:00 AM")] // wraps into the next day cleanly
        [TestCase(3870, "6:00 AM")] // day 2's dawn (3600 + 270)
        [TestCase(100, "2:13 AM")]
        [TestCase(269, "5:59 AM")] // one second before dawn
        [TestCase(8100, "12:00 PM")] // day 4's noon (1800*4 + 900)
        public void Format_MatchesTheServerSidePythonImplementation(double totalSeconds, string expected)
        {
            Assert.That(TemporalClock.Format(totalSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void Format_NeverThrowsForANegativeInput()
        {
            // totalSeconds should always be >= 0 in practice, but the
            // Mod() helper's negative-handling is real behavior worth
            // locking in, not just an unreachable defensive branch.
            Assert.That(() => TemporalClock.Format(-100), Throws.Nothing);
        }
    }
}
