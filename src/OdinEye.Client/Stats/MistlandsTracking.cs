namespace OdinEye.Client.Stats
{
    // Mist Goat: a small game-dependent adapter, same reasoning as
    // SwampTracking/NorthTracking (a thin, untested-by-design read of live
    // game state). Player.GetCurrentBiome() and Heightmap.Biome.Mistlands
    // are both present in the compile-time stub, confirmed via IL the
    // same way SwampTracking already documents for Heightmap.Biome.Swamp,
    // so no reflection is needed here either.
    public static class MistlandsTracking
    {
        // Whether the local player is standing in the Mistlands biome
        // right now. Read fresh on every check, same "sample, don't
        // reconstruct the whole path" shape SwampTracking.IsInSwamp
        // already uses.
        public static bool IsInMistlands() =>
            Player.m_localPlayer != null && Player.m_localPlayer.GetCurrentBiome() == Heightmap.Biome.Mistlands;
    }
}
