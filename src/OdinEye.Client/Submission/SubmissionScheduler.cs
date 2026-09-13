namespace OdinEye.Client.Submission
{
    using System;

    // Pure/testable: decides when a periodic character-stats submission is
    // due (Decision 4 -- ODINEYE-16: submit on login, then every 5 minutes).
    // The login submission itself is always immediate and is triggered
    // directly by the caller; this only arms the recurring timer so it
    // doesn't also fire on the very next tick right after login.
    public sealed class SubmissionScheduler
    {
        private readonly TimeSpan interval;
        private DateTime? nextSubmissionAtUtc;

        public SubmissionScheduler(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Submission interval must be positive.");
            }

            this.interval = interval;
        }

        public void OnLogin(DateTime nowUtc) => nextSubmissionAtUtc = nowUtc + interval;

        // Call periodically (e.g. every Update) once logged in. Returns true
        // at most once per interval, and resets the timer for the next one.
        public bool IsDue(DateTime nowUtc)
        {
            if (nextSubmissionAtUtc == null || nowUtc < nextSubmissionAtUtc)
            {
                return false;
            }

            nextSubmissionAtUtc = nowUtc + interval;
            return true;
        }
    }
}
