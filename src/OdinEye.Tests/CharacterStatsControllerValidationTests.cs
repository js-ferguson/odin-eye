namespace OdinEye.Tests
{
    using System;
    using NUnit.Framework;
    using OdinEye.Http.Api.Controllers;

    // IsValidStatValue is pure (no ZNet/EnvMan/ZDOMan dependency) --
    // internal + [InternalsVisibleTo("OdinEye.Tests")] on OdinEye's own
    // AssemblyInfo.cs is what makes it reachable here (ODINEYE-18).
    // Worth pinning down directly: this exact class of validation logic
    // is what stood between the server and accepting garbage stat
    // submissions during this session's own all-zero-stats and
    // stale-data investigations (VALSER-25/ODINEYE-25's history), even
    // though those specific bugs were elsewhere.
    [TestFixture]
    public class CharacterStatsControllerValidationTests
    {
        [Test]
        public void RejectsNaN()
        {
            Assert.That(CharacterStatsController.IsValidStatValue(float.NaN, previousValue: 0f), Is.False);
        }

        [Test]
        public void RejectsPositiveInfinity()
        {
            Assert.That(CharacterStatsController.IsValidStatValue(float.PositiveInfinity, previousValue: 0f), Is.False);
        }

        // RejectsNegativeInfinity was removed (code review): the guard is
        // a single sign-agnostic float.IsInfinity(value) check, so it
        // executes the identical branch as RejectsPositiveInfinity above
        // -- the sign never changes which code path runs.

        [Test]
        public void RejectsNegativeValues()
        {
            Assert.That(CharacterStatsController.IsValidStatValue(-1f, previousValue: 0f), Is.False);
        }

        [Test]
        public void RejectsAValuePastThePlausibleCeiling()
        {
            // Code review: this used to assert against float.MaxValue,
            // which is astronomically larger than the real ceiling
            // ("seconds since Valheim's Early Access release") -- so it
            // could never have actually exercised the real boundary
            // check, only proven SOME value fails. Computed the same way
            // production code does, plus a margin, so a wrong epoch/unit/
            // sign in that real calculation would actually be caught.
            var realCeiling = (float)(DateTime.UtcNow - new DateTime(2021, 2, 2, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Assert.That(CharacterStatsController.IsValidStatValue(realCeiling + 1000f, previousValue: 0f), Is.False);
        }

        [Test]
        public void RejectsADecreaseFromANonzeroPreviousValue()
        {
            // A stat going DOWN would mean the client's own local count
            // reset or rolled back -- never legitimate for a lifetime,
            // monotonically-increasing stat.
            Assert.That(CharacterStatsController.IsValidStatValue(5f, previousValue: 10f), Is.False);
        }

        [Test]
        public void AcceptsTheFirstSubmissionAgainstAZeroPreviousValue()
        {
            // previousValue defaults to 0 when a stat has never been
            // submitted before -- must not be treated as "decreasing".
            Assert.That(CharacterStatsController.IsValidStatValue(100f, previousValue: 0f), Is.True);
        }

        [Test]
        public void AcceptsAnIncreaseFromANonzeroPreviousValue()
        {
            Assert.That(CharacterStatsController.IsValidStatValue(15f, previousValue: 10f), Is.True);
        }

        [Test]
        public void AcceptsAnUnchangedValue()
        {
            // >= previousValue, not strictly >, so a resubmission of the
            // exact same value (nothing changed since last submit) is
            // valid, not rejected as "not increasing".
            Assert.That(CharacterStatsController.IsValidStatValue(10f, previousValue: 10f), Is.True);
        }

        [Test]
        public void AcceptsZero()
        {
            Assert.That(CharacterStatsController.IsValidStatValue(0f, previousValue: 0f), Is.True);
        }
    }
}
