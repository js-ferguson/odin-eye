namespace OdinEye.Client.Submission
{
    using OdinEye.Models.Api;
    using System;

    public interface IStatsSubmitter
    {
        // Fire-and-forget by design: a failed submission is simply retried
        // on the next scheduled tick (see SubmissionScheduler), so this must
        // never throw back into the caller's Update loop.
        void Submit(Guid playerId, CharacterStatsSubmission submission);
    }
}
