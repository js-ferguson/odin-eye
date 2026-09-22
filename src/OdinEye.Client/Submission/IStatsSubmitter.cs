namespace OdinEye.Client.Submission
{
    using OdinEye.Models.Api;
    using System;

    public interface IStatsSubmitter
    {
        // Fire-and-forget by design: a failed submission is simply retried
        // on the next check (see ChangeDrivenPolicy), so this must never
        // throw back into the caller's Update loop. onComplete (optional,
        // called from a background thread) says whether the server accepted
        // it, so the caller can know the change still needs sending.
        void Submit(Guid playerId, CharacterStatsSubmission submission, Action<bool> onComplete = null);
    }
}
