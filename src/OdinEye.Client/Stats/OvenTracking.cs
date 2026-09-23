namespace OdinEye.Client.Stats
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    // VALSER-84: Baker's High. Same shape as NorthTracking/BoatTracking/
    // SwampTracking (a trivial read fed into CounterAugmentedStatsSource's
    // regular elapsed-time sample), but this one needs a proximity search
    // rather than a plain state read: Piece.GetAllPiecesInRadius(Vector3,
    // float, List<Piece>) -- public static, confirmed via IL, the same
    // API OutpostTracking.cs already uses for its own "nearby structure"
    // checks, backed by the game's own Piece.s_allPieces. Works for ANY
    // oven, not just the local player's own -- there is no ownership
    // check here, unlike OutpostTracking's own bed scan.
    public static class OvenTracking
    {
        // "Vicinity" per the brief: no canonical value given. Same
        // reasoning as OutpostTracking.OutpostRadius (Tameable's own
        // m_playerMaxDistance is 15f), but a touch tighter -- a bakery is
        // a room someone is standing in, not an outdoor base footprint --
        // unconfirmed live.
        public const float OvenRadius = 10f;

        // The stone oven's own piece prefab (confirmed "piece_oven.prefab"
        // in the game's own asset manifest, the same naming convention
        // WORKBENCH_PIECE_KEY's own "$piece_workbench" already follows) --
        // NOT the basic early-game cooking station (a different piece;
        // see CookingStationPatches.cs's own header), matching "Baker's
        // High"/"the bakery"'s own bread-baking theme.
        public const string OvenPieceName = "piece_oven";

        // Reused across calls rather than allocated fresh each time --
        // same reasoning OutpostTracking.cs's own "nearby" field gives.
        private static readonly List<Piece> nearby = new List<Piece>();

        public static bool IsNearOven()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }

            nearby.Clear();
            Piece.GetAllPiecesInRadius(Player.m_localPlayer.transform.position, OvenRadius, nearby);
            return nearby.Any(p => p != null && Utils.GetPrefabName(p.gameObject) == OvenPieceName);
        }
    }
}
