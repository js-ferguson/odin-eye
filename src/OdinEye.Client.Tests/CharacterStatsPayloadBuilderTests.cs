namespace OdinEye.Client.Tests
{
    using NUnit.Framework;
    using OdinEye.Client.Stats;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class CharacterStatsPayloadBuilderTests
    {
        [Test]
        public void Build_WithNullInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CharacterStatsPayloadBuilder.Build(null));
        }

        [Test]
        public void Build_WithAllValidStats_PassesThroughEveryValue()
        {
            var rawStats = new Dictionary<string, float>
            {
                ["Deaths"] = 3f,
                ["PlayTimeSeconds"] = 123456f,
                ["Jumps"] = 0f
            };

            var submission = CharacterStatsPayloadBuilder.Build(rawStats);

            Assert.That(submission.Stats, Is.EquivalentTo(rawStats));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        // float.NegativeInfinity was removed (code review): the guard is
        // a single sign-agnostic float.IsInfinity() check, so it executes
        // the identical branch as the PositiveInfinity case above.
        [TestCase(-1f)]
        public void Build_FiltersOutImpossibleValues(float invalidValue)
        {
            var rawStats = new Dictionary<string, float>
            {
                ["Deaths"] = 3f,
                ["Bogus"] = invalidValue
            };

            var submission = CharacterStatsPayloadBuilder.Build(rawStats);

            Assert.That(submission.Stats.ContainsKey("Bogus"), Is.False);
            Assert.That(submission.Stats["Deaths"], Is.EqualTo(3f));
        }

        [Test]
        public void Build_WithEmptyInput_ReturnsEmptySubmission()
        {
            var submission = CharacterStatsPayloadBuilder.Build(new Dictionary<string, float>());

            Assert.That(submission.Stats, Is.Empty);
        }

        [Test]
        public void Build_WithZero_IsValid()
        {
            var rawStats = new Dictionary<string, float> { ["Deaths"] = 0f };

            var submission = CharacterStatsPayloadBuilder.Build(rawStats);

            Assert.That(submission.Stats["Deaths"], Is.EqualTo(0f));
        }
    }
}
