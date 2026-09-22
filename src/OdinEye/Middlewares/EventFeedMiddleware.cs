namespace OdinEye.Middlewares
{
    using Events;
    using Models.Proto;

    // Records every (non-chat) GameEvent into the pull feed (ODINEYE-35),
    // then hands it on. Sits BEFORE the WebSocket dispatcher in the chain so
    // that a failure while broadcasting can never stop an event reaching
    // the feed.
    public class EventFeedMiddleware : EventMiddleware
    {
        private readonly EventFeed feed;

        public EventFeedMiddleware(EventFeed feed)
        {
            this.feed = feed;
        }

        protected override void Invoke(GameEvent gameEvent, MiddlewareDelegate next)
        {
            if (EventFeed.ShouldRecord(gameEvent.Type))
            {
                feed.Append(gameEvent);
            }

            next(gameEvent);
        }
    }
}
