namespace OdinEye.Http.Api.Controllers
{
    using Events;
    using Extensions;
    using Models.Api;
    using System;
    using System.Linq;
    using Utf8Json;
    using WebSocketSharp.Server;

    // ODINEYE-39: POST /players/{id}/events -- lets OdinEye.Client report
    // the few things only a player's own machine can observe (a bed removed
    // with the hammer, a respawn at the circle because the bed was gone).
    // POST-only: like /notify there is nothing to read back; the events show
    // up in GET /events like any other.
    //
    // {id} is the same derived character GUID as /players/{id}/stats, and
    // must belong to a currently connected player -- the event is attributed
    // to THAT player, so a client can only speak for itself.
    public class PlayerEventsController : IPostController
    {
        // 20 events a minute is far beyond anything a real player produces
        // (a bed removal is a deliberate act) and far below what could crowd
        // real events out of the 5000-event buffer.
        internal static readonly ClientEventRateLimiter RateLimiter =
            new ClientEventRateLimiter(20, TimeSpan.FromMinutes(1));

        public string RoutePrefix => "/players/";

        public string RouteSuffix => "/events";

        public void OnPost(HttpRequestEventArgs requestArguments, string routeParameter)
        {
            if (!Guid.TryParse(routeParameter, out var playerId))
            {
                requestArguments.Response.Error(400);
                return;
            }

            var peer = ZNet.instance.m_peers.FirstOrDefault(p => p.ToPlayer().Id == playerId);
            if (peer == null)
            {
                requestArguments.Response.Error(404);
                return;
            }

            ClientEventsSubmission submission;
            try
            {
                submission = JsonSerializer.Deserialize<ClientEventsSubmission>(requestArguments.Request.InputStream);
            }
            catch
            {
                requestArguments.Response.Error(400);
                return;
            }

            var player = peer.ToPlayer();
            if (!ClientEventCleaner.TryClean(submission, player, out var events))
            {
                requestArguments.Response.Error(400);
                return;
            }

            if (!RateLimiter.TryAcquire(playerId.ToString(), events.Count, DateTime.UtcNow))
            {
                requestArguments.Response.Error(429);
                return;
            }

            foreach (var gameEvent in events)
            {
                OdinEyePlugin.Instance.EventHandler.Handle(gameEvent);
            }

            requestArguments.Response.Ok(new AcceptedResponse());
        }
    }
}
