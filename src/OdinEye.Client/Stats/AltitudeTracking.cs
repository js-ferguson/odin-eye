namespace OdinEye.Client.Stats
{
    // Captain Robert Falcon Scott: a small game-dependent adapter (not unit
    // tested, same reasoning as NorthTracking/BoatTracking/SwampTracking --
    // it is a thin read of live game state, and UnityEngine.Vector3's own
    // static constructor throws outside a real Unity process, so this
    // Vector3 math must stay contained here rather than reach
    // CounterAugmentedStatsSource directly). Kept separate from that class
    // for the same reason NorthTracking is.
    //
    // Sea level is the real, hardcoded ZoneSystem.c_WaterLevel constant
    // (confirmed via IL against the exact referenced game assembly: = 30),
    // not a guess -- "altitude" is height above THAT, not the raw world Y
    // coordinate, which on its own means nothing to a player.
    //
    // Character.InInterior(Vector3) (public, static, confirmed via IL) is
    // critical, not optional: its entire body is "position.y > 3000" --
    // dungeons and caves are placed in world space FAR ABOVE the overworld,
    // not below or beside it. Without this exclusion, walking into any
    // cave door would instantly register a bogus ~3000m "altitude," miles
    // beyond any real mountain summit, and trivially win this achievement
    // for a reason that has nothing to do with climbing one.
    public static class AltitudeTracking
    {
        // The local player's current height above sea level, or null if no
        // character is loaded OR the player is currently inside a
        // dungeon/cave interior.
        public static float? CurrentAltitude()
        {
            if (Player.m_localPlayer == null)
            {
                return null;
            }

            var position = Player.m_localPlayer.transform.position;
            if (Character.InInterior(position))
            {
                return null;
            }

            return position.y - ZoneSystem.c_WaterLevel;
        }
    }
}
