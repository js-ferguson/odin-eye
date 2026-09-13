namespace OdinEye.Client.Stats
{
    using System.Collections.Generic;

    public interface IPlayerStatsSource
    {
        // Returns the local player's current lifetime stats snapshot (all of
        // Valheim's PlayerStatType values plus total playtime), keyed the
        // same way the server's ingest API expects. Empty when no character
        // profile is currently loaded (e.g. still at the main menu).
        IReadOnlyDictionary<string, float> GetStats();
    }
}
