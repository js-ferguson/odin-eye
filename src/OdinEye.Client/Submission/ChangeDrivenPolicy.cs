namespace OdinEye.Client.Submission
{
    using System;

    // ODINEYE-36 (supersedes ODINEYE-16 Decision 4): decides WHEN to submit.
    //
    // Achievements are awarded automatically as soon as they are earned, so
    // waiting up to 5 minutes for the next scheduled submission made them lag
    // by up to ~6 minutes. Instead: every `checkInterval` (30s) look at the
    // stats, and submit if anything changed. A HEARTBEAT is still sent at
    // least every `heartbeatInterval` (5 min) even when nothing changed,
    // because OdinEye's server keeps stats only in memory: after a server
    // restart the heartbeat repopulates it.
    //
    // Pure: time is passed in. The login submission itself is the caller's
    // job and is always immediate; OnLogin only arms the timers.
    public sealed class ChangeDrivenPolicy
    {
        private readonly TimeSpan checkInterval;
        private readonly TimeSpan heartbeatInterval;
        private DateTime? nextCheckAtUtc;
        private DateTime? lastSubmittedAtUtc;

        public ChangeDrivenPolicy(TimeSpan checkInterval, TimeSpan heartbeatInterval)
        {
            if (checkInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(checkInterval), "Check interval must be positive.");
            }

            if (heartbeatInterval < checkInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(heartbeatInterval), "The heartbeat cannot be more frequent than the change check.");
            }

            this.checkInterval = checkInterval;
            this.heartbeatInterval = heartbeatInterval;
        }

        public void OnLogin(DateTime nowUtc)
        {
            lastSubmittedAtUtc = nowUtc; // the login submission is sent by the caller
            nextCheckAtUtc = nowUtc + checkInterval;
        }

        // True at most once per checkInterval (and never before login), then
        // re-arms itself -- so submissions can never exceed one per interval.
        public bool IsCheckDue(DateTime nowUtc)
        {
            if (nextCheckAtUtc == null || nowUtc < nextCheckAtUtc)
            {
                return false;
            }

            nextCheckAtUtc = nowUtc + checkInterval;
            return true;
        }

        public bool ShouldSubmit(DateTime nowUtc, bool changed) =>
            lastSubmittedAtUtc != null && (changed || nowUtc - lastSubmittedAtUtc.Value >= heartbeatInterval);

        public void MarkSubmitted(DateTime nowUtc) => lastSubmittedAtUtc = nowUtc;
    }
}
