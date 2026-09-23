namespace OdinEye.Client.Stats
{
    // Stink Fish: a small game-dependent adapter, same reasoning as
    // NorthTracking/BoatTracking (a thin, untested-by-design read of live
    // game state). Player.GetCurrentBiome() and Heightmap.Biome.Swamp are
    // both present in the compile-time stub (same confirmation
    // NorthTracking.IsInDeepNorth already documents for Heightmap.Biome.
    // DeepNorth -- ikdasm against ValheimGameLibs.0.221.4's own
    // assembly_valheim.dll), so no reflection is needed here either.
    public static class SwampTracking
    {
        // Whether the local player is standing in the Swamp biome right
        // now. Unlike IsInDeepNorth's once-ever latch, this is read fresh
        // on every check -- CounterAugmentedStatsSource only credits time
        // while it is true THIS sample, the same "sample, don't reconstruct
        // the whole path" shape BoatTracking.IsOnBoat already feeds into
        // for The Admiral.
        public static bool IsInSwamp() =>
            Player.m_localPlayer != null && Player.m_localPlayer.GetCurrentBiome() == Heightmap.Biome.Swamp;
    }
}
