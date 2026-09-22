namespace OdinEye.Client.Stats
{
    using System.Collections.Generic;

    // ODINEYE-36: has anything in the stats changed since the last snapshot
    // that was submitted? Drives change-driven submissions: an idle player
    // sends nothing between heartbeats, and a player who just caught a fish
    // sends within ~30 seconds.
    public static class StatsChange
    {
        public static bool HasChanged(IReadOnlyDictionary<string, float> previous, IReadOnlyDictionary<string, float> current)
        {
            if (current == null)
            {
                return false;
            }

            if (previous == null || previous.Count != current.Count)
            {
                return true;
            }

            foreach (var kv in current)
            {
                if (!previous.TryGetValue(kv.Key, out var old) || old != kv.Value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
