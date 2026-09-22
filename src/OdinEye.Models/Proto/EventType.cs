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
        EnemyKilled = 300,

        // ACHIEVEMENTS (VALSER-55): reported by OdinEye.Client over
        // POST /players/{id}/events (ODINEYE-39/40), never observed by the
        // server itself, because a hammer removal and a respawn are only
        // visible on the players' own machines.
        //
        // BedRemoved: reported by the REMOVER's client -- a claimed bed was
        // taken apart with the hammer. Details: OwnerPlayerId,
        // RemoverPlayerId, SpawnX/Y/Z.
        BedRemoved = 400,

        // BedMissingAtRespawn: reported by the VICTIM's client -- the game
        // found no bed at their custom spawn point and put them at the
        // circle. Details: LostSpawnX/Y/Z.
        BedMissingAtRespawn = 401
    }
}