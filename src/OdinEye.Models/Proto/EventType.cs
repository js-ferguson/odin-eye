namespace OdinEye.Models.Proto
{
    public enum EventType
    {
        Unknown = 0,
        PlayerJoin = 1,
        PlayerSpawn = 2,
        PlayerDeath = 3,
        PlayerDisconnect = 4,
        PlayerChat = 5,
        PlayersSleepStart = 6,
        PlayerSleepStop = 7,
        
        GameAwake = 50,
        GameQuit = 51,
        WorldLoad = 53,
        WorldSave = 54,
        ServerShutdown = 55,
        GlobalKeyAdd = 56,
        GlobalKeyRemove = 57,
        
        RandomEventActivate = 100,
        RandomEventDeactivate = 101,
        RandomEventSet = 102,
        
        EnvironmentMorningStart = 200,
        EnvironmentEveningStart = 201,

        // ODINEYE-31, stage 1: a non-player Character died and was
        // credited to at least one connected player -- see
        // CharacterDeathPatch. Deliberately just an observable event for
        // now (logged + WebSocket-broadcast, same as every other event
        // here), not yet persisted/aggregated anywhere -- see that
        // ticket for the staged plan and why.
        EnemyKilled = 300
    }
}