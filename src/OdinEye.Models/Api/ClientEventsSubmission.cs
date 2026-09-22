namespace OdinEye.Models.Api
{
    using System.Collections.Generic;

    // The POST body for /players/{id}/events (ODINEYE-39): things only a
    // player's own machine can see, reported by OdinEye.Client.
    //
    // Fixed fields rather than an open dictionary so the server can validate
    // every value it will republish: nothing free-form from a client ever
    // reaches the event feed.
    public class ClientEventsSubmission
    {
        public List<ClientEvent> Events { get; set; }
    }

    // Type is "BedRemoved" (reported by the remover) or "BedMissingAtRespawn"
    // (reported by the player who respawned at the circle). X/Y/Z is the bed's
    // spawn point in the first case and the lost custom spawn point in the
    // second. OwnerPlayerId/RemoverPlayerId are game player IDs (longs, as
    // strings) and only mean something for BedRemoved.
    public class ClientEvent
    {
        public string Type { get; set; }
        public string OwnerPlayerId { get; set; }
        public string RemoverPlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
