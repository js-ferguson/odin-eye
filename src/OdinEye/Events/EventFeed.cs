namespace OdinEye.Events
{
    using Models.Proto;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ODINEYE-35: an in-memory, capped feed of GameEvents that a consumer
    // (valheim_server's game_events_poller.py) can PULL, so events survive
    // nobody being connected to the /activity WebSocket. OdinEye stays
    // stateless: nothing is written to disk and a restart clears the feed.
    //
    // Every event gets a monotonic Seq starting at 1, and the feed has a
    // BootId (a GUID generated when it is created). A restart therefore
    // shows up to the consumer as a NEW BootId with Seq beginning again at
    // 1, and it dedupes on (BootId, Seq).
    //
    // Gap: the consumer asks "everything after Seq N". If events after N
    // have already been pushed out of the capped buffer, Gap is true so the
    // consumer knows it missed some instead of silently assuming it did not.
    public sealed class EventFeed
    {
        public const int DefaultCapacity = 5000;

        private readonly object gate = new object();
        private readonly Queue<EventFeedEntry> entries = new Queue<EventFeedEntry>();
        private readonly int capacity;
        private long nextSeq = 1;

        public EventFeed(int capacity = DefaultCapacity, string bootId = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }

            this.capacity = capacity;
            BootId = bootId ?? Guid.NewGuid().ToString();
        }

        public string BootId { get; }

        // Chat messages are deliberately NOT recorded: the feed is persisted
        // by the agent for weeks, and what players type to each other is not
        // something the achievements (or anything else) needs to keep.
        public static bool ShouldRecord(EventType type) => type != EventType.PlayerChat;

        public long Append(GameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                throw new ArgumentNullException(nameof(gameEvent));
            }

            lock (gate)
            {
                var seq = nextSeq++;
                entries.Enqueue(EventFeedEntry.From(seq, gameEvent));
                while (entries.Count > capacity)
                {
                    entries.Dequeue();
                }

                return seq;
            }
        }

        public EventFeedResult Read(long after, int limit)
        {
            if (limit <= 0)
            {
                limit = 1;
            }

            lock (gate)
            {
                var oldestRetained = entries.Count > 0 ? entries.Peek().Seq : nextSeq;
                return new EventFeedResult
                {
                    BootId = BootId,
                    NextSeq = nextSeq,
                    // Events after `after` existed but are no longer held.
                    Gap = oldestRetained > after + 1,
                    Events = entries.Where(e => e.Seq > after).Take(limit).ToList()
                };
            }
        }
    }

    public sealed class EventFeedResult
    {
        public string BootId { get; set; }
        public long NextSeq { get; set; }
        public bool Gap { get; set; }
        public List<EventFeedEntry> Events { get; set; }
    }

    // JSON shape of one feed entry (PascalCase, like the rest of OdinEye's
    // REST API): what the agent's poller reads.
    public sealed class EventFeedEntry
    {
        public long Seq { get; set; }
        public string Ts { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public EventFeedPlayer Player { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public static EventFeedEntry From(long seq, GameEvent gameEvent) =>
            new EventFeedEntry
            {
                Seq = seq,
                Ts = gameEvent.CreatedDate.ToUniversalTime().ToString("o"),
                Type = gameEvent.Type.ToString(),
                Message = gameEvent.Message,
                Player = gameEvent.Player == null
                    ? null
                    : new EventFeedPlayer
                    {
                        Id = gameEvent.Player.Id.ToString(),
                        Name = gameEvent.Player.Name,
                        SteamId = gameEvent.Player.SteamId
                    },
                // Copied, so a later change to the source cannot alter the
                // recorded event.
                Data = gameEvent.Details == null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(gameEvent.Details)
            };
    }

    public sealed class EventFeedPlayer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SteamId { get; set; }
    }

    // Query-string handling for GET /events, pure so it can be tested.
    public static class EventsQuery
    {
        public const int DefaultLimit = 500;
        public const int MaxLimit = 1000;

        public static void Parse(string afterRaw, string limitRaw, out long after, out int limit)
        {
            after = long.TryParse(afterRaw, out var a) && a > 0 ? a : 0;
            limit = int.TryParse(limitRaw, out var l) ? Math.Max(1, Math.Min(MaxLimit, l)) : DefaultLimit;
        }
    }
}
