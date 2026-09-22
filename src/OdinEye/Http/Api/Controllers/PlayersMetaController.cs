namespace OdinEye.Http.Api.Controllers
{
    using Extensions;
    using Models.Api;
    using System.Collections.Generic;
    using WebSocketSharp.Server;

    // ODINEYE-36: GET /players/meta -- {characterId: {PlayerId,
    // ClientVersion}} for every character whose client has reported it.
    // A SEPARATE route rather than a change to GET /players/stats, whose
    // shape the valheim_server agent's poller depends on. Same characterId
    // scheme as /players/stats. In memory only; the poller makes it durable.
    public class PlayersMetaController : IController
    {
        public string Route => "/players/meta";

        public void OnGet(HttpRequestEventArgs requestArguments) =>
            requestArguments.Response.Ok(new Dictionary<string, SubmissionMeta>(CharacterStatsController.MetaByPlayerId));
    }
}
