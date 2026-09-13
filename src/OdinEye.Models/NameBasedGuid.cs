namespace OdinEye.Models
{
    using NGuid;
    using System;

    // Shared between the server (OdinEye) and client (OdinEye.Client)
    // plugins -- both must derive the exact same Id for the same
    // SteamId+Name, since the client submits stats keyed by this Id to an
    // endpoint the server gates on "does this Id belong to a currently-
    // connected peer" (see ODINEYE-19). Living here, in the one assembly
    // both reference, rules out the two ever drifting into different
    // formulas.
    public static class NameBasedGuid
    {
        private static readonly Guid NamespaceId = new Guid("4b01a09a-9b18-493c-9877-4786611eeea2");

        public static Guid NewPlayerGuid(string steamId, string playerName) => GuidHelpers.CreateFromName(NamespaceId, steamId + playerName);
    }
}
