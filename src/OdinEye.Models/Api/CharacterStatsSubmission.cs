namespace OdinEye.Models.Api
{
    using System.Collections.Generic;

    // The POST body for /players/{id}/stats. Stats is deliberately an open
    // dictionary (PlayerStatType enum name -> value, plus the synthetic
    // "PlayTimeSeconds") rather than fixed fields, so a new stat -- including
    // any Valheim itself adds in a future update -- needs no payload schema
    // change on either the submitting client or this server.
    public class CharacterStatsSubmission
    {
        public Dictionary<string, float> Stats { get; set; }

        // ODINEYE-36: optional facts the client reports about itself. An
        // older server ignores this member (Utf8Json skips unknown keys) and
        // an older client simply never sends it, so both directions stay
        // compatible.
        public SubmissionMeta Meta { get; set; }
    }

    // PlayerId: the game profile's player ID (PlayerProfile.GetPlayerID()),
    // as a string. It is what a bed records as its owner, so the panel needs
    // it to attribute bed events to a character. ClientVersion: the client
    // plugin's assembly version.
    public class SubmissionMeta
    {
        public string PlayerId { get; set; }
        public string ClientVersion { get; set; }
    }
}
