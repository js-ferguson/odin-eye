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
    }
}
