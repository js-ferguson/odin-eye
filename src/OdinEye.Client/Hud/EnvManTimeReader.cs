namespace OdinEye.Client.Hud
{
    using System;
    using System.Reflection;

    // ODINEYE-32: reads EnvMan.m_totalSeconds -- Valheim's own live,
    // ever-increasing elapsed-game-time counter, the same value the
    // real game's day/night cycle and world-modifier timers are driven
    // from. EnvMan itself, and this field, both resolve fine against
    // this project's compile-time ValheimGameLibs stub -- but the
    // stub's own declared accessibility for m_totalSeconds (public) is
    // WRONG for the real running game (confirmed private via IL
    // disassembly of the real server's assembly_valheim.dll) -- the
    // same "stub said public/existed differently than the real game"
    // class of surprise PlayerProfileStatsSource's own header comment
    // already documents two real shipped bugs from. Read via
    // reflection with both binding-flag cases covered, rather than
    // trusting either the stub's claim or the one real build already
    // checked.
    public static class EnvManTimeReader
    {
        private static readonly Lazy<FieldInfo> TotalSecondsField = new Lazy<FieldInfo>(() =>
            typeof(EnvMan).GetField("m_totalSeconds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

        // null when EnvMan isn't loaded yet (e.g. still at the main
        // menu) or the field couldn't be resolved at all (a future game
        // update renames/removes it) -- distinct from 0, so callers can
        // skip rather than show a confidently wrong midnight.
        public static double? GetTotalSeconds()
        {
            var env = EnvMan.instance;
            var field = TotalSecondsField.Value;
            if (env == null || field == null)
            {
                return null;
            }

            try
            {
                return (double)field.GetValue(env);
            }
            catch
            {
                return null;
            }
        }
    }
}
