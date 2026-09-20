namespace OdinEye.Client.Hud
{
    // ODINEYE-32: the exact same "temporal hours" clock reading
    // valheim_server's admin panel already computes server-side
    // (agent/app.py's _temporal_clock(), built for VALSER-40/41's nav
    // clock) -- ported here so the in-game HUD clock and the admin
    // panel's own clock always agree. Dawn is always 6:00 AM and dusk
    // is always 6:00 PM, the way pre-mechanical sundial clocks divided
    // day and night into 12 hours each regardless of season, so
    // day-hours and night-hours are different real lengths (~105s and
    // ~45s) rather than a fixed 60.
    //
    // A pure function -- no Unity/game-state dependency at all -- kept
    // deliberately separate from EnvManTimeReader (which supplies the
    // raw totalSeconds this consumes) so it's genuinely unit-testable,
    // unlike most of this project's live-game-state readers.
    public static class TemporalClock
    {
        private const double DayLengthSeconds = 1800;
        private const double DawnPhaseOffset = 270;
        private const double DaylightSeconds = 1260;
        private const double NightSeconds = 540;

        public static string Format(double totalSeconds)
        {
            var t = Mod(totalSeconds, DayLengthSeconds);
            var sinceDawn = Mod(t - DawnPhaseOffset, DayLengthSeconds);

            double hour24 = sinceDawn < DaylightSeconds
                ? 6 + (sinceDawn / DaylightSeconds) * 12
                : 18 + ((sinceDawn - DaylightSeconds) / NightSeconds) * 12;
            hour24 = Mod(hour24, 24);

            var hourInt = (int)hour24;
            var minute = (int)System.Math.Round((hour24 - hourInt) * 60);
            if (minute == 60)
            {
                minute = 0;
                hourInt = (hourInt + 1) % 24;
            }

            var period = hourInt < 12 ? "AM" : "PM";
            var hour12 = hourInt % 12;
            if (hour12 == 0)
            {
                hour12 = 12;
            }

            return $"{hour12}:{minute:D2} {period}";
        }

        // C#'s % can return a negative result for a negative dividend
        // (unlike Python's, which agent/app.py's own version relies
        // on) -- normalized here so this behaves identically for any
        // input, not just the always-non-negative totalSeconds the
        // real game ever actually reports.
        private static double Mod(double value, double modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
