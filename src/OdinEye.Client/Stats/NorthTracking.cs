namespace OdinEye.Client.Stats
{
    // Peter North: a small game-dependent adapter (not unit tested, same
    // reasoning as PlayerProfileStatsSource/StationNames -- it is a thin
    // read of live game state). Kept separate from
    // CounterAugmentedStatsSource so that class stays free of any direct
    // dependency on Player/Heightmap and can be tested with plain delegates.
    //
    // Both Heightmap.Biome.DeepNorth/AshLands and Player.GetCurrentBiome()
    // (a trivial read of the already-maintained Player.m_currentBiome field)
    // are present in the compile-time stub, unlike some of this project's
    // other reflection-needing reads -- confirmed via ikdasm against
    // ValheimGameLibs.0.221.4's own assembly_valheim.dll, so no reflection
    // is needed here.
    public static class NorthTracking
    {
        // The local player's current world Z position, or null if no
        // character is loaded.
        public static float? CurrentNorthZ() =>
            Player.m_localPlayer != null ? Player.m_localPlayer.transform.position.z : (float?)null;

        // Whether the local player is standing in the Deep North biome
        // right now. Once-ever is what matters for Peter North's completion
        // condition, so the caller is expected to latch this with a
        // high-water mark (CustomCounterStore.RaiseTo) rather than call it
        // "reached" only while still standing there.
        public static bool IsInDeepNorth() =>
            Player.m_localPlayer != null && Player.m_localPlayer.GetCurrentBiome() == Heightmap.Biome.DeepNorth;
    }
}
