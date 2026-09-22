namespace OdinEye.Client.Events
{
    using OdinEye.Models.Api;

    // ODINEYE-40: builds the two events behind the Homeless achievement.
    // Pure, so what gets reported can be tested without a game.
    public static class BedEvents
    {
        // Reported by the client that took the bed apart. A bed nobody has
        // claimed (owner 0) or one the remover owns themselves is not a
        // removal of someone else's bed, so it is not reported at all.
        public static ClientEvent Removed(long ownerPlayerId, long removerPlayerId, float x, float y, float z)
        {
            if (ownerPlayerId == 0 || ownerPlayerId == removerPlayerId)
            {
                return null;
            }

            return new ClientEvent
            {
                Type = "BedRemoved",
                OwnerPlayerId = ownerPlayerId.ToString(),
                RemoverPlayerId = removerPlayerId.ToString(),
                X = x,
                Y = y,
                Z = z
            };
        }

        // Reported by the client whose custom spawn point the game just
        // cleared because no bed was there any more.
        public static ClientEvent MissingAtRespawn(float x, float y, float z) =>
            new ClientEvent { Type = "BedMissingAtRespawn", X = x, Y = y, Z = z };
    }
}
