namespace OdinEye.Client.Stats
{
    // The Admiral: a small game-dependent adapter, same reasoning as
    // NorthTracking/StationNames (a thin, untested-by-design read of live
    // game state). Ship.GetLocalShip() is public static and already used
    // by the live game's own Player.UpdateStats (confirmed via IL
    // disassembly), so no reflection is needed.
    public static class BoatTracking
    {
        // Non-null whenever the local player is aboard ANY ship -- as a
        // passenger or at the helm, "of any kind" per the brief -- not just
        // while actively steering (that narrower case is
        // Player.GetControlledShip(), used nowhere in this file).
        public static bool IsOnBoat() => Ship.GetLocalShip() != null;
    }
}
