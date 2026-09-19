namespace OdinEye.Client.Submission
{
    using System;

    // Separate from IStatsSubmitter (VALSER-50) -- see
    // CheatStatusReader's header comment for why this flag can't travel
    // through the same monotonic-only /players/{id}/stats pipe.
    public interface ICheatStatusSubmitter
    {
        // Fire-and-forget by design, same reasoning as IStatsSubmitter:
        // a failed submission is simply retried on the next scheduled
        // tick, so this must never throw back into the caller's Update
        // loop.
        void Submit(Guid playerId, bool cheated);
    }
}
