namespace OdinEye.Events
{
    using Models.Api;
    using Models.Proto;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ODINEYE-39: turns what a client submitted into events safe to put in
    // the feed. Pure, so every rule can be tested without a running game.
    //
    // A submission is checked as a whole and either fully accepted or fully
    // rejected (like character stats): a half-applied batch would make "did
    // that removal count?" impossible to reason about.
    public static class ClientEventCleaner
    {
        public const int MaxEventsPerRequest = 20;

        // Beyond any Valheim world (radius ~10 km); catches garbage floats.
        private const float MaxCoordinate = 100000f;

        // Only these two may be reported: a client must not be able to make
        // the server record an arbitrary event type (a fake kill, chat, ...).
        private static readonly HashSet<string> AllowedTypes =
            new HashSet<string>(StringComparer.Ordinal) { "BedRemoved", "BedMissingAtRespawn" };

        public static bool TryClean(ClientEventsSubmission submission, Models.Proto.Player player, out List<GameEvent> events)
        {
            events = null;
            if (submission?.Events == null || submission.Events.Count == 0 || submission.Events.Count > MaxEventsPerRequest)
            {
                return false;
            }

            var cleaned = new List<GameEvent>();
            foreach (var clientEvent in submission.Events)
            {
                var gameEvent = CleanOne(clientEvent, player);
                if (gameEvent == null)
                {
                    return false;
                }

                cleaned.Add(gameEvent);
            }

            events = cleaned;
            return true;
        }

        private static GameEvent CleanOne(ClientEvent clientEvent, Models.Proto.Player player)
        {
            if (clientEvent == null || clientEvent.Type == null || !AllowedTypes.Contains(clientEvent.Type) ||
                !IsSaneCoordinate(clientEvent.X) || !IsSaneCoordinate(clientEvent.Y) || !IsSaneCoordinate(clientEvent.Z))
            {
                return null;
            }

            if (clientEvent.Type == "BedRemoved")
            {
                if (!long.TryParse(clientEvent.OwnerPlayerId, out var owner) ||
                    !long.TryParse(clientEvent.RemoverPlayerId, out var remover))
                {
                    return null;
                }

                // The message deliberately names nobody: it ends up in a
                // durable feed, and the structured Data below is what is used.
                return GameEvent.New(EventType.BedRemoved, "A claimed bed was removed with the hammer", player,
                    new Dictionary<string, object>
                    {
                        ["OwnerPlayerId"] = owner.ToString(),
                        ["RemoverPlayerId"] = remover.ToString(),
                        ["SpawnX"] = clientEvent.X,
                        ["SpawnY"] = clientEvent.Y,
                        ["SpawnZ"] = clientEvent.Z
                    });
            }

            return GameEvent.New(EventType.BedMissingAtRespawn, "A player respawned at the circle because their bed was gone", player,
                new Dictionary<string, object>
                {
                    ["LostSpawnX"] = clientEvent.X,
                    ["LostSpawnY"] = clientEvent.Y,
                    ["LostSpawnZ"] = clientEvent.Z
                });
        }

        private static bool IsSaneCoordinate(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= MaxCoordinate;
    }

    // A ceiling on how many events one player can push into the feed, so a
    // buggy or hostile client cannot flush everyone else's events out of the
    // capped buffer. Time is passed in so it is testable.
    public sealed class ClientEventRateLimiter
    {
        private readonly int maxPerWindow;
        private readonly TimeSpan window;
        private readonly Dictionary<string, Queue<DateTime>> recentByPlayer = new Dictionary<string, Queue<DateTime>>();
        private readonly object gate = new object();

        public ClientEventRateLimiter(int maxPerWindow, TimeSpan window)
        {
            if (maxPerWindow <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPerWindow));
            }

            if (window <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(window));
            }

            this.maxPerWindow = maxPerWindow;
            this.window = window;
        }

        // Reserves `count` slots for the player; false (and reserves nothing)
        // if that would exceed the limit within the window.
        public bool TryAcquire(string playerKey, int count, DateTime nowUtc)
        {
            lock (gate)
            {
                if (!recentByPlayer.TryGetValue(playerKey, out var recent))
                {
                    recent = new Queue<DateTime>();
                    recentByPlayer[playerKey] = recent;
                }

                while (recent.Count > 0 && nowUtc - recent.Peek() >= window)
                {
                    recent.Dequeue();
                }

                if (recent.Count + count > maxPerWindow)
                {
                    return false;
                }

                for (var i = 0; i < count; i++)
                {
                    recent.Enqueue(nowUtc);
                }

                return true;
            }
        }
    }
}
