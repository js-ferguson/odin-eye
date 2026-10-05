namespace OdinEye.Client.Stats
{
    // All in vein: a small game-dependent adapter, same reasoning as
    // SwampTracking/NorthTracking (a thin, untested-by-design read of live
    // game state). Player.GetCurrentBiome() and Heightmap.Biome.Mountain
    // are both present in the compile-time stub, confirmed via IL the
    // same way SwampTracking already documents for Heightmap.Biome.Swamp,
    // so no reflection is needed here either.
    public static class MountainTracking
    {
        // Whether the local player is standing in the Mountain biome right
        // now. Read fresh on every check, same "sample, don't reconstruct
        // the whole path" shape SwampTracking.IsInSwamp already uses.
        public static bool IsInMountain() =>
            Player.m_localPlayer != null && Player.m_localPlayer.GetCurrentBiome() == Heightmap.Biome.Mountain;
    }
}
