namespace OdinEye.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using OdinEye.Client.Stats;
    using OdinEye.Client.Submission;

    // ODINEYE-38 (per-piece stats) and ODINEYE-36 (change detection and the
    // 30-second submission policy).
    [TestFixture]
    public class PieceStatsAndPolicyTests
    {
        private static KeyValuePair<string, float> P(string k, float v) => new KeyValuePair<string, float>(k, v);

        // --- PieceStats ------------------------------------------------------------

        [Test]
        public void Prefix_NamesEveryPieceWithTheGamesOwnKey()
        {
            var keys = PieceStats.Prefix(new[] { P("$piece_workbench", 12f), P("$piece_forge", 3f) });

            Assert.That(keys["PiecePlaced:$piece_workbench"], Is.EqualTo(12f));
            Assert.That(keys["PiecePlaced:$piece_forge"], Is.EqualTo(3f));
            Assert.That(keys.Count, Is.EqualTo(2));
        }

        [Test]
        public void Prefix_DropsUnusableEntries()
        {
            var keys = PieceStats.Prefix(new[] { P("", 1f), P(null, 1f), P("a", float.NaN), P("b", -1f), P("c", float.PositiveInfinity), P("ok", 0f) });

            Assert.That(keys.Keys, Is.EquivalentTo(new[] { "PiecePlaced:ok" }));
        }

        [Test]
        public void Prefix_OfNothingIsEmpty()
        {
            Assert.That(PieceStats.Prefix(null), Is.Empty);
        }

        [Test]
        public void StationTotal_SumsOnlyStationsAndUpgrades()
        {
            var placed = new[] { P("$piece_workbench", 100f), P("$piece_chopblock", 40f), P("$piece_wood_wall", 9999f), P("$piece_forge", 10f) };
            var stations = new HashSet<string> { "$piece_workbench", "$piece_chopblock", "$piece_forge" };

            Assert.That(PieceStats.StationTotal(placed, stations), Is.EqualTo(150f));
        }

        [Test]
        public void StationTotal_IsZeroWithoutANameSetOrData()
        {
            var placed = new[] { P("$piece_workbench", 100f) };

            Assert.That(PieceStats.StationTotal(placed, null), Is.EqualTo(0f));
            Assert.That(PieceStats.StationTotal(placed, new HashSet<string>()), Is.EqualTo(0f));
            Assert.That(PieceStats.StationTotal(null, new HashSet<string> { "x" }), Is.EqualTo(0f));
        }

        [Test]
        public void StationTotal_IgnoresNonsenseCounts()
        {
            var placed = new[] { P("a", float.NaN), P("b", -5f), P("c", 7f) };

            Assert.That(PieceStats.StationTotal(placed, new HashSet<string> { "a", "b", "c" }), Is.EqualTo(7f));
        }

        // --- StatsChange ---------------------------------------------------------------

        [Test]
        public void HasChanged_IsTrueForTheFirstSnapshot()
        {
            Assert.That(StatsChange.HasChanged(null, new Dictionary<string, float> { ["a"] = 1f }), Is.True);
        }

        [Test]
        public void HasChanged_IsFalseWhenNothingMoved()
        {
            var a = new Dictionary<string, float> { ["x"] = 1f, ["y"] = 2f };
            var b = new Dictionary<string, float> { ["y"] = 2f, ["x"] = 1f };

            Assert.That(StatsChange.HasChanged(a, b), Is.False);
        }

        [Test]
        public void HasChanged_SeesAChangedValueANewKeyAndARemovedKey()
        {
            var baseline = new Dictionary<string, float> { ["x"] = 1f };

            Assert.That(StatsChange.HasChanged(baseline, new Dictionary<string, float> { ["x"] = 2f }), Is.True);
            Assert.That(StatsChange.HasChanged(baseline, new Dictionary<string, float> { ["x"] = 1f, ["y"] = 1f }), Is.True);
            Assert.That(StatsChange.HasChanged(baseline, new Dictionary<string, float> { ["z"] = 1f }), Is.True);
            Assert.That(StatsChange.HasChanged(baseline, new Dictionary<string, float>()), Is.True);
        }

        // --- ChangeDrivenPolicy -----------------------------------------------------------

        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Check = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(5);

        private static ChangeDrivenPolicy LoggedIn()
        {
            var policy = new ChangeDrivenPolicy(Check, Heartbeat);
            policy.OnLogin(T0);
            return policy;
        }

        [Test]
        public void Constructor_RejectsBadIntervals()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChangeDrivenPolicy(TimeSpan.Zero, Heartbeat));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChangeDrivenPolicy(Check, TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void NothingIsDueBeforeLogin()
        {
            var policy = new ChangeDrivenPolicy(Check, Heartbeat);

            Assert.That(policy.IsCheckDue(T0 + TimeSpan.FromHours(1)), Is.False);
            Assert.That(policy.ShouldSubmit(T0, changed: true), Is.False);
        }

        [Test]
        public void ACheckIsDueEveryThirtySeconds_NotBefore()
        {
            var policy = LoggedIn();

            Assert.That(policy.IsCheckDue(T0), Is.False);
            Assert.That(policy.IsCheckDue(T0 + TimeSpan.FromSeconds(29)), Is.False);
            Assert.That(policy.IsCheckDue(T0 + Check), Is.True);
            Assert.That(policy.IsCheckDue(T0 + Check), Is.False, "at most once per interval");
            Assert.That(policy.IsCheckDue(T0 + Check + Check), Is.True);
        }

        [Test]
        public void AChangeSubmitsWithinOneCheckInterval()
        {
            var policy = LoggedIn();
            var t = T0 + Check;

            Assert.That(policy.IsCheckDue(t), Is.True);
            Assert.That(policy.ShouldSubmit(t, changed: true), Is.True);
        }

        [Test]
        public void NoChange_MeansNoSubmission_UntilTheHeartbeat()
        {
            var policy = LoggedIn();
            for (var i = 1; i < 10; i++)   // 30s .. 270s
            {
                var t = T0 + TimeSpan.FromSeconds(30 * i);
                Assert.That(policy.IsCheckDue(t), Is.True);
                Assert.That(policy.ShouldSubmit(t, changed: false), Is.False, $"at {30 * i}s");
            }

            var heartbeat = T0 + Heartbeat;
            Assert.That(policy.IsCheckDue(heartbeat), Is.True);
            Assert.That(policy.ShouldSubmit(heartbeat, changed: false), Is.True, "the 5 minute heartbeat repopulates a restarted server");
        }

        [Test]
        public void MarkSubmitted_RestartsTheHeartbeatClock()
        {
            var policy = LoggedIn();
            var t = T0 + TimeSpan.FromMinutes(2);
            policy.MarkSubmitted(t);

            Assert.That(policy.ShouldSubmit(T0 + TimeSpan.FromMinutes(6), changed: false), Is.False, "only 4 minutes since the last submission");
            Assert.That(policy.ShouldSubmit(t + Heartbeat, changed: false), Is.True);
        }
    }
}
