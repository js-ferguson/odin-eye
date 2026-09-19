namespace OdinEye.Client.Stats
{
    using System;
    using System.Reflection;

    // Game-dependent adapter (VALSER-50), same "not unit tested, thin read
    // of live game state" category as PlayerProfileStatsSource -- see that
    // class's header comment for the general reasoning.
    //
    // Reads Achievements.IsCheatedAtAll() -- confirmed via IL disassembly
    // (ikdasm) of the real running server's assembly_valheim.dll to be the
    // exact bool the game's own achievements-menu "disabled" banner uses:
    //   usedCheats (PlayerProfile.m_usedCheats, this character's permanent
    //   flag) OR IsWorldCheated() (world-modifier cheats) OR
    //   Player.m_localPlayer.GetInventory().AnyCheatedItem() (currently
    //   carrying a flagged item -- the specific symptom this ticket exists
    //   for) OR, when none of those are true, Game.isModded as a fallback.
    // That last term means this can read "cheated" for every player on a
    // modded install even with no devcommand ever used -- a real, disclosed
    // quirk of the game's own logic, not a bug in this reader.
    //
    // Achievements is absent from ValheimGameLibs' compile-time reference
    // stub (achievements were added in Valheim 1.0, the stub tops out at
    // 0.221.4 -- confirmed via `monodis --typedef` against the actual
    // package, no such type) -- resolved at runtime off an already-in-stub
    // type's own live Assembly instead, same pattern
    // PlayerProfileStatsSource uses for its own out-of-stub members.
    public static class CheatStatusReader
    {
        private static readonly Lazy<MethodInfo> IsCheatedAtAllMethod = new Lazy<MethodInfo>(() =>
            typeof(PlayerProfile).Assembly.GetType("Achievements")
                ?.GetMethod("IsCheatedAtAll", BindingFlags.Public | BindingFlags.Static));

        // null when the method couldn't be resolved (e.g. a future game
        // update renames or removes it) -- deliberately distinct from
        // false, so callers can skip submitting rather than reporting a
        // confidently wrong "clean".
        public static bool? IsCheated()
        {
            var method = IsCheatedAtAllMethod.Value;
            if (method == null)
            {
                return null;
            }

            try
            {
                return (bool)method.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }
    }
}
