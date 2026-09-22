namespace OdinEye.Client.Patches
{
    using OdinEye.Client.Counters;
    using OdinEye.Client.Events;
    using System;

    // What the Harmony patches in this folder read and write. Static because
    // a Harmony patch is a static method; the plugin fills these in on login
    // and clears them on logout, so nothing is recorded while no character
    // is loaded.
    //
    // Every patch body is wrapped in Guard() below: a patch runs inside the
    // game's own code, and an exception thrown from one would break the very
    // thing (a hit, a removal, a respawn) it is only there to observe.
    internal static class ClientRuntime
    {
        public static volatile CustomCounterStore Counters;

        public static readonly ClientEventQueue Events = new ClientEventQueue();

        public static Action<string> LogWarning = _ => { };

        public static long LocalPlayerId() => Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? 0L;

        public static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogWarning($"OdinEye client: {what} failed: {ex.Message}");
            }
        }
    }
}
