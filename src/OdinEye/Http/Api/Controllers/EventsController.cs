namespace OdinEye.Http.Api.Controllers
{
    using Events;
    using Extensions;
    using WebSocketSharp.Server;

    // ODINEYE-35: GET /events?after=<seq>&limit=<n> -- the pull feed of game
    // events. Response: {BootId, NextSeq, Gap, Events:[{Seq, Ts, Type,
    // Message, Player, Data}]}. Stateless like every other controller here:
    // the buffer lives in memory and is cleared on restart (new BootId).
    public class EventsController : IController
    {
        public string Route => "/events";

        public void OnGet(HttpRequestEventArgs requestArguments)
        {
            var query = requestArguments.Request.QueryString;
            EventsQuery.Parse(query["after"], query["limit"], out var after, out var limit);
            requestArguments.Response.Ok(OdinEyePlugin.Instance.EventFeed.Read(after, limit));
        }
    }
}
