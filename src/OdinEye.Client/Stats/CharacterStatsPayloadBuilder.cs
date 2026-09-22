namespace OdinEye.Client.Stats
{
    using OdinEye.Models.Api;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Pure/testable: turns a raw stats snapshot from IPlayerStatsSource into
    // the wire payload OdinEye's server-side ingest API accepts. Filters out
    // any value the server would reject anyway (see
    // CharacterStatsController.IsValidStatValue on the server side) so a bad
    // client-side read never wastes a round trip on a guaranteed 400 -- and
    // so one unreadable stat doesn't block every other, valid stat from
    // being submitted.
    public static class CharacterStatsPayloadBuilder
    {
        public static CharacterStatsSubmission Build(IReadOnlyDictionary<string, float> rawStats, SubmissionMeta meta = null)
        {
            if (rawStats == null)
            {
                throw new ArgumentNullException(nameof(rawStats));
            }

            var validStats = rawStats
                .Where(stat => IsValidStatValue(stat.Value))
                .ToDictionary(stat => stat.Key, stat => stat.Value);

            return new CharacterStatsSubmission { Stats = validStats, Meta = meta };
        }

        private static bool IsValidStatValue(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    }
}
