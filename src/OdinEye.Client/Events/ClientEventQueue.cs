namespace OdinEye.Client.Events
{
    using OdinEye.Models.Api;
    using System.Collections.Generic;

    // ODINEYE-39: events waiting to be sent to the server. They are raised
    // on the game's main thread and sent from a background call, so it is
    // thread-safe; and bounded, so a server that stays unreachable cannot
    // make the game hold events forever. If the server refuses or is down, the
    // batch goes back on the FRONT so order is kept for the next attempt.
    public sealed class ClientEventQueue
    {
        public const int DefaultCapacity = 100;

        private readonly object gate = new object();
        private readonly LinkedList<ClientEvent> items = new LinkedList<ClientEvent>();
        private readonly int capacity;

        public ClientEventQueue(int capacity = DefaultCapacity)
        {
            this.capacity = capacity > 0 ? capacity : DefaultCapacity;
        }

        public int Count
        {
            get { lock (gate) { return items.Count; } }
        }

        public void Enqueue(ClientEvent clientEvent)
        {
            if (clientEvent == null)
            {
                return;
            }

            lock (gate)
            {
                items.AddLast(clientEvent);
                Trim();
            }
        }

        public List<ClientEvent> TakeBatch(int max)
        {
            var batch = new List<ClientEvent>();
            lock (gate)
            {
                while (items.Count > 0 && batch.Count < max)
                {
                    batch.Add(items.First.Value);
                    items.RemoveFirst();
                }
            }

            return batch;
        }

        public void Requeue(List<ClientEvent> batch)
        {
            if (batch == null)
            {
                return;
            }

            lock (gate)
            {
                for (var i = batch.Count - 1; i >= 0; i--)
                {
                    items.AddFirst(batch[i]);
                }

                Trim();
            }
        }

        // Over capacity, the OLDEST are dropped: a fresh event is more likely
        // to still match something on the server than a stale one.
        private void Trim()
        {
            while (items.Count > capacity)
            {
                items.RemoveFirst();
            }
        }
    }
}
