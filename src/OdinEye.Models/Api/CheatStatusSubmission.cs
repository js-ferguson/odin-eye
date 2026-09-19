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
    }
}
