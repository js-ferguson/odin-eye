namespace OdinEye.Client.Tests
{
    using NUnit.Framework;
    using OdinEye.Client.Submission;
    using System;

    [TestFixture]
    public class SubmissionSchedulerTests
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Constructor_WithNonPositiveInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SubmissionScheduler(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SubmissionScheduler(TimeSpan.FromSeconds(-1)));
        }

        [Test]
        public void IsDue_BeforeAnyLogin_IsAlwaysFalse()
        {
            var scheduler = new SubmissionScheduler(Interval);

            Assert.That(scheduler.IsDue(T0), Is.False);
            Assert.That(scheduler.IsDue(T0 + Interval), Is.False);
        }

        [Test]
        public void IsDue_ImmediatelyAfterLogin_IsFalse()
        {
            // The login submission itself is the caller's responsibility and
            // is always immediate -- IsDue only governs the *next* periodic
            // one, so it must not also fire right on login's own tick.
            var scheduler = new SubmissionScheduler(Interval);
            scheduler.OnLogin(T0);

            Assert.That(scheduler.IsDue(T0), Is.False);
        }

        [Test]
        public void IsDue_BeforeIntervalElapsed_IsFalse()
        {
            var scheduler = new SubmissionScheduler(Interval);
            scheduler.OnLogin(T0);

            Assert.That(scheduler.IsDue(T0 + TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59)), Is.False);
        }

        [Test]
        public void IsDue_OnceIntervalElapsed_IsTrueThenResets()
        {
            var scheduler = new SubmissionScheduler(Interval);
            scheduler.OnLogin(T0);

            var dueAt = T0 + Interval;
            Assert.That(scheduler.IsDue(dueAt), Is.True);
            Assert.That(scheduler.IsDue(dueAt), Is.False, "should not fire again on the same tick");
            Assert.That(scheduler.IsDue(dueAt + Interval), Is.True, "should fire again a full interval later");
        }

        [Test]
        public void OnLogin_CalledAgain_RestartsTheTimer()
        {
            var scheduler = new SubmissionScheduler(Interval);
            scheduler.OnLogin(T0);

            var relogAt = T0 + TimeSpan.FromMinutes(1);
            scheduler.OnLogin(relogAt);

            // Without the restart this would already be due (5 min after T0).
            Assert.That(scheduler.IsDue(T0 + Interval), Is.False);
            Assert.That(scheduler.IsDue(relogAt + Interval), Is.True);
        }
    }
}
