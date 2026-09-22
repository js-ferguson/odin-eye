namespace OdinEye.Client.Patches
{
    using HarmonyLib;
    using OdinEye.Client.Events;
    using System.Reflection;
    using UnityEngine;

    // ODINEYE-40: the two halves of the Homeless achievement. Neither is
    // visible to the server, so each client reports its own half.
    public static class BedPatches
    {
        // REMOVER's half. WearNTear.Remove is what the hammer's remove action
        // ends in on the removing player's own client, after the game has
        // already checked they may remove it -- so a bed knocked down by a
        // weapon or the weather never passes through here. Only a bed
        // somebody else claimed is reported (see BedEvents.Removed).
        [HarmonyPatch(typeof(WearNTear), "Remove")]
        public static class BedRemovedPatch
        {
            private static readonly MethodInfo GetOwner = AccessTools.Method(typeof(Bed), "GetOwner");

            [HarmonyPrepare]
            private static bool Prepare() => GetOwner != null;

            [HarmonyPrefix]
            private static void Prefix(WearNTear __instance) =>
                ClientRuntime.Guard("bed removal report", () =>
                {
                    if (ClientRuntime.Counters == null)
                    {
                        return;
                    }

                    var bed = __instance.GetComponent<Bed>();
                    if (bed == null)
                    {
                        return;
                    }

                    var spawn = bed.GetSpawnPoint();
                    var removed = BedEvents.Removed((long)GetOwner.Invoke(bed, null), ClientRuntime.LocalPlayerId(), spawn.x, spawn.y, spawn.z);
                    if (removed != null)
                    {
                        ClientRuntime.Events.Enqueue(removed);
                    }
                });
        }

        // VICTIM's half. Game.FindSpawnPoint runs (repeatedly) while
        // respawning. When the player had a custom spawn point and no bed is
        // found there, the game clears it and sends them to the circle; that
        // "had one before, has none after" transition is the signal, and the
        // point that was lost is what the server matches to a removed bed.
        [HarmonyPatch(typeof(Game), "FindSpawnPoint")]
        public static class BedMissingPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Game __instance, out object __state)
            {
                __state = null;
                var captured = default(object);
                ClientRuntime.Guard("respawn check", () =>
                {
                    var profile = __instance.GetPlayerProfile();
                    if (ClientRuntime.Counters != null && profile != null && profile.HaveCustomSpawnPoint())
                    {
                        captured = profile.GetCustomSpawnPoint();
                    }
                });
                __state = captured;
            }

            [HarmonyPostfix]
            private static void Postfix(Game __instance, object __state)
            {
                if (__state == null)
                {
                    return;
                }

                ClientRuntime.Guard("respawn report", () =>
                {
                    var profile = __instance.GetPlayerProfile();
                    if (profile == null || profile.HaveCustomSpawnPoint())
                    {
                        return; // the bed was found: nothing was lost
                    }

                    var lost = (Vector3)__state;
                    ClientRuntime.Events.Enqueue(BedEvents.MissingAtRespawn(lost.x, lost.y, lost.z));
                });
            }
        }
    }
}
