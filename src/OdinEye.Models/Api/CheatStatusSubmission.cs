namespace OdinEye.Models.Api
{
    // The POST body for /players/{id}/cheatStatus. Deliberately a separate
    // model/endpoint from CharacterStatsSubmission/{id}/stats (VALSER-50):
    // that endpoint's server-side validation rejects any value lower than
    // the previously submitted one (built for ever-increasing lifetime
    // counters), but this flag must be able to flip back to false the
    // instant a player drops a cheated item -- see
    // OdinEye.Client.Stats.CheatStatusReader's header comment.
    public class CheatStatusSubmission
    {
        public bool Cheated { get; set; }

        // ODINEYE-30: whether this character has run Valheim's own
        // "yesiuseddevcommandsbutiwantmyachievementsanyway 1" console
        // command -- see OdinEye.Client.Stats.CheatBypassReader.
        // Independent of Cheated: a character can be Cheated=true
        // (still carrying a flagged item, or the world itself is
        // modded) while also having BypassEnabled=true (their own
        // permanent flag is cleared) -- worth showing both rather than
        // collapsing into one status.
        public bool BypassEnabled { get; set; }
    }
}
