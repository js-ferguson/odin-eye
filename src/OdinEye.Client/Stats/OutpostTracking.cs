namespace OdinEye.Client.Stats
{
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using OdinEye.Client.Patches;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    // Gilligan's Island: unlike NorthTracking/BoatTracking (plain reads fed
    // into the ordinary 30-second stats snapshot), there is no single game
    // method to hook for "a bed just became a qualifying outpost" and no
    // per-pass total to recompute -- this runs on its own slow periodic
    // timer (see OdinEyeClientPlugin) and, like a Harmony patch, credits
    // ClientRuntime.Counters directly the moment it finds something new.
    //
    // "Nearby" (the crafting station and the portal) and "covered" (the
    // bed) all read straight off the game's own live world state via
    // public/static APIs confirmed via IL of the live assembly, none of
    // them exercised live yet -- see the ticket's open items:
    //   * Piece.GetAllPiecesInRadius(Vector3, float, List<Piece>) --
    //     public static, backed by the game's own Piece.s_allPieces.
    //   * Cover.IsUnderRoof(Vector3) (assembly_utils) -- public static,
    //     the same check Smelter uses for its own roof-venting.
    //   * WorldGenerator.instance.GetBiome(Vector3) -- public instance,
    //     used here for the ocean-crossing landmass heuristic (see
    //     CounterRules.IsNewLandmass).
    public static class OutpostTracking
    {
        // "Close vicinity" for the crafting station and the portal: no
        // canonical value in the brief. Proposed to match the scale of
        // other proximity constants already seen in the live assembly
        // (Tameable's own m_playerMaxDistance is 15f) -- unconfirmed live.
        public const float OutpostRadius = 15f;

        // Sample points along the straight line between a candidate
        // outpost and an already-credited one, checking for open ocean
        // between them. More samples catch a narrower strait; 20 is a
        // sample roughly every landmass-scale interval without being
        // expensive to run on a slow timer.
        private const int OceanSampleSteps = 20;

        private static readonly MethodInfo GetOwner = AccessTools.Method(typeof(Bed), "GetOwner");

        private static bool Available => GetOwner != null;

        // One scan: every bed the local player owns, that hasn't already
        // credited a landmass, checked for the other two pieces and a
        // roof, and (if it qualifies) checked against every landmass
        // already credited. Safe to call often -- a bed that already
        // credited its landmass, or one that never will, both cost the
        // same cheap "not ocean-separated from itself/its own landmass"
        // check as anything else on that landmass. Guarded so a game
        // update that renames Bed.GetOwner costs only this feature.
        public static void Scan(OutpostAnchorStore anchors)
        {
            if (!Available || anchors == null || Player.m_localPlayer == null || ClientRuntime.Counters == null)
            {
                return;
            }

            var myId = ClientRuntime.LocalPlayerId();
            if (myId == 0)
            {
                return;
            }

            var nearby = new List<Piece>();
            foreach (var bed in Object.FindObjectsByType<Bed>(FindObjectsSortMode.None))
            {
                if (bed == null || (long)GetOwner.Invoke(bed, null) != myId)
                {
                    continue;
                }

                var pos = bed.GetSpawnPoint();
                nearby.Clear();
                Piece.GetAllPiecesInRadius(pos, OutpostRadius, nearby);
                var hasCraftingStation = nearby.Any(p => p != null && p.GetComponent<CraftingStation>() != null);
                var hasPortal = nearby.Any(p => p != null && p.GetComponent<TeleportWorld>() != null);
                var isCovered = Cover.IsUnderRoof(pos);

                if (!CounterRules.IsQualifyingOutpost(isCovered, hasCraftingStation, hasPortal))
                {
                    continue;
                }

                var oceanSeparated = anchors.Anchors.Select(a => CrossesOcean(pos, a.X, a.Z));
                if (CounterRules.IsNewLandmass(oceanSeparated))
                {
                    anchors.Add(pos.x, pos.z);
                    ClientRuntime.Counters.Increment(CounterRules.OutpostLandmassesKey);
                }
            }
        }

        private static bool CrossesOcean(Vector3 from, float toX, float toZ)
        {
            if (WorldGenerator.instance == null)
            {
                return false; // world not ready this frame -- try again next scan
            }

            var to = new Vector3(toX, from.y, toZ);
            for (var i = 0; i <= OceanSampleSteps; i++)
            {
                var p = Vector3.Lerp(from, to, (float)i / OceanSampleSteps);
                if (WorldGenerator.instance.GetBiome(p) == Heightmap.Biome.Ocean)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
