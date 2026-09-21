namespace OdinEye.Http.Api.Controllers
{
    using Extensions;
    using Models.Api;
    using System.Linq;
    using Utf8Json;
    using WebSocketSharp.Server;

    // ODINEYE-33: POST /players/{steamId}/notify -- shows a brief HUD
    // banner to exactly one currently-connected player, e.g.
    // valheim_server's "your sub is due" nudge on login. Deliberately
    // POST-only (no matching GET, unlike CharacterStatsController/
    // CheatStatusController above) -- there's no state to read back here,
    // OdinEye doesn't remember anything was sent.
    //
    // Reuses Valheim's OWN "ShowMessage" routed RPC (registered by the
    // base game's MessageHud, not anything OdinEye/OdinEyeClient adds) --
    // confirmed against the real game assembly (assembly_valheim.dll)
    // that ZRoutedRpc.InvokeRoutedRPC(long, string, object[]) targets
    // exactly one peer by its m_uid, no broadcast, and that
    // MessageHud.RPC_ShowMessage(int64, int32, string) is already
    // registered client-side by vanilla Valheim itself -- so this works
    // for EVERY player, not just ones with OdinEyeClient installed
    // (unlike the HUD clock/cheat-detection features, which do need it).
    public class PlayerNotifyController : IPostController
    {
        // MessageHud.MessageType.Center's real underlying int value
        // (confirmed via the real game assembly, ODINEYE-33) -- not
        // referenced as the enum itself since MessageHud.MessageType
        // isn't worth a second reference just for one constant; the RPC
        // wire format is the plain int anyway (see
        // MessageHud.RPC_ShowMessage's own int32 parameter).
        private const int MessageTypeCenter = 2;

        // A message this long stops being a brief HUD nudge and starts
        // being something a player has to stand still and read -- same
        // "reject the implausible, not just the merely large" spirit as
        // CharacterStatsController's plausibility ceiling.
        private const int MaxMessageLength = 200;

        public string RoutePrefix => "/players/";

        public string RouteSuffix => "/notify";

        // Pure, no ZNet dependency -- pulled out of OnPost so these two
        // input-shape rules are unit-testable without a live game
        // (ODINEYE-18's InternalsVisibleTo("OdinEye.Tests") pattern,
        // matching CharacterStatsController.IsValidStatValue). The actual
        // peer lookup/RPC call below still can't be tested this way --
        // see this class's own header comment.
        internal static bool IsValidSteamId(string steamId) =>
            !string.IsNullOrEmpty(steamId) && steamId.All(char.IsDigit);

        internal static bool IsValidMessage(string message) =>
            !string.IsNullOrWhiteSpace(message) && message.Length <= MaxMessageLength;

        public void OnPost(HttpRequestEventArgs requestArguments, string routeParameter)
        {
            var steamId = routeParameter;
            if (!IsValidSteamId(steamId))
            {
                requestArguments.Response.Error(400);
                return;
            }

            NotifyRequest request;
            try
            {
                request = JsonSerializer.Deserialize<NotifyRequest>(requestArguments.Request.InputStream);
            }
            catch
            {
                requestArguments.Response.Error(400);
                return;
            }

            if (request == null || !IsValidMessage(request.Message))
            {
                requestArguments.Response.Error(400);
                return;
            }

            // Same ZNet.instance.m_peers scan CheatStatusController/
            // CharacterStatsController's own IsConnectedPlayer() helpers
            // use, just matched on the raw SteamId directly (via
            // ZNetPeerExtensions.ToPlayer()) rather than OdinEye's own
            // derived Guid -- this endpoint has no reason to know that
            // Guid at all.
            var peer = ZNet.instance.m_peers.FirstOrDefault(p => p.ToPlayer().SteamId == steamId);
            if (peer == null)
            {
                // Not connected right now -- the caller's problem to
                // retry on a later connect, not this endpoint's; no
                // queueing/persistence here, matching every other
                // live-only piece of state in this plugin.
                requestArguments.Response.Error(404);
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", MessageTypeCenter, request.Message);
            requestArguments.Response.Ok(new AcceptedResponse());
        }
    }
}
