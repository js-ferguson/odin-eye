namespace OdinEye.Client.Stats
{
    // ODINEYE-30: whether the LOCAL character currently has Valheim's own
    // "yesiuseddevcommandsbutiwantmyachievementsanyway" cheat-bypass
    // switched on. Confirmed via IL disassembly (ikdasm) of the real
    // running server's assembly_valheim.dll: that console command
    // (Terminal's InitTerminal handler) stores its setting as a
    // per-CHARACTER unique key/value pair --
    //   Player.AddUniqueKeyValue("bypasscheatchecks", "1"/"0")
    // -- part of the character's own persistent save data
    // (Humanoid.m_uniques, the same mechanism Valheim uses for one-off
    // quest/unlock flags), NOT the PlayerProfile stats
    // PlayerProfileStatsSource reads.
    //
    // Unlike CheatStatusReader (which needs reflection because
    // Achievements was added in Valheim 1.0, after this project's
    // ValheimGameLibs compile-time reference package was published),
    // Player and TryGetUniqueKeyValue are old, stable pre-1.0 APIs
    // already present in that same stub -- confirmed directly against
    // the actual ValheimGameLibs 0.221.4 package via ikdasm/monodis --
    // so this reads them as a normal compiled call, no reflection needed.
    public static class CheatBypassReader
    {
        private const string BypassKey = "bypasscheatchecks";

        // false both when the player has never run the command AND when
        // they explicitly ran it with "0" (turned their own bypass back
        // off) -- there's no user-facing difference between those two
        // states worth reporting separately.
        public static bool IsEnabled()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }

            return player.TryGetUniqueKeyValue(BypassKey, out var value) && value == "1";
        }
    }
}
