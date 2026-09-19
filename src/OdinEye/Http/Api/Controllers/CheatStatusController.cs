namespace OdinEye.Http.Api.Controllers
{
    using Extensions;
    using Models.Api;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Utf8Json;
    using WebSocketSharp.Server;

    // Backs the live cheat/achievement-eligibility status API (VALSER-50):
    // GET /players/cheatStatus and POST /players/{id}/cheatStatus.
    // Deliberately separate from CharacterStatsController/{id}/stats
    // (ODINEYE-12/19): that endpoint's IsValidStatValue() rejects any
    // submission lower than the previous one, built for ever-increasing
    // lifetime counters. This flag is the opposite -- it must be able to
    // flip back to false the instant a player drops a cheated item -- so
    // every submission here is simply accepted and overwrites the last
    // one, no history kept. Same in-memory-only, cleared-on-restart
    // statelessness as every other live-state controller here.
    public class CheatStatusController : IController, IPostController
    {
        private static readonly ConcurrentDictionary<string, bool> CheatedByPlayerId =
            new ConcurrentDictionary<string, bool>();

        public string Route => "/players/cheatStatus";

        public string RoutePrefix => "/players/";

        public string RouteSuffix => "/cheatStatus";

        public void OnGet(HttpRequestEventArgs requestArguments)
        {
            requestArguments.Response.Ok(new Dictionary<string, bool>(CheatedByPlayerId));
        }

        public void OnPost(HttpRequestEventArgs requestArguments, string routeParameter)
        {
            if (!Guid.TryParse(routeParameter, out var playerId))
            {
                requestArguments.Response.Error(400);
                return;
            }

            if (!IsConnectedPlayer(playerId))
            {
                requestArguments.Response.Error(404);
                return;
            }

            CheatStatusSubmission submission;
            try
            {
                submission = JsonSerializer.Deserialize<CheatStatusSubmission>(requestArguments.Request.InputStream);
            }
            catch
            {
                requestArguments.Response.Error(400);
                return;
            }

            if (submission == null)
            {
                requestArguments.Response.Error(400);
                return;
            }

            CheatedByPlayerId[playerId.ToString()] = submission.Cheated;
            requestArguments.Response.Ok(new AcceptedResponse());
        }

        // Matches CharacterStatsController.IsConnectedPlayer() exactly --
        // see that method's own comment for why this iterates
        // ZNet.instance.m_peers/ToPlayer() rather than the ZDO-gated peer
        // list PlayersController/GameStatsSnapshotCoroutine use.
        private static bool IsConnectedPlayer(Guid playerId) =>
            ZNet.instance.m_peers.Any(peer => peer.ToPlayer().Id == playerId);
    }
}
