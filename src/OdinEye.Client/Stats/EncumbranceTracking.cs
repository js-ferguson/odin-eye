namespace OdinEye.Client.Stats
{
    using UnityEngine;

    // Palsy/Terrible Palsy (VALSER-90): a small game-dependent adapter,
    // same reasoning as NorthTracking/BoatTracking/SwampTracking (a thin,
    // untested-by-design read of live game state). Player.IsEncumbered()
    // (public override of Character.IsEncumbered(), confirmed via IL --
    // Humanoid.m_inventory.GetTotalWeight() > Player.GetMaxCarryWeight())
    // is the first time this codebase has ever read encumbrance state.
    public static class EncumbranceTracking
    {
        // Same "sample, don't reconstruct the whole path" shape
        // SwampTracking.IsInSwamp() already documents -- read fresh on
        // every check, not a once-ever latch.
        public static bool IsOverburdened() =>
            Player.m_localPlayer != null && Player.m_localPlayer.IsEncumbered();

        private static Vector3? lastPosition;

        // How far the local player has moved since the LAST time this was
        // called, in meters -- 0f on the very first call (nothing to diff
        // against yet) or whenever no character is loaded. Vector3 math
        // deliberately stays entirely inside this adapter rather than in
        // CounterAugmentedStatsSource: UnityEngine.Vector3's own static
        // constructor throws outside a real Unity process (confirmed live
        // against this project's own test suite), so keeping it out of
        // anything the test suite actually exercises -- same
        // "untested-by-design" line BoatTracking/SwampTracking already
        // draw -- is required, not just a style preference.
        public static float DistanceMovedSinceLastSample()
        {
            var current = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : (Vector3?)null;
            var distance = lastPosition.HasValue && current.HasValue
                ? Vector3.Distance(current.Value, lastPosition.Value)
                : 0f;
            lastPosition = current;
            return distance;
        }
    }
}
