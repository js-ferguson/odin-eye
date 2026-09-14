namespace OdinEye.Models.Api
{
    using System.Collections.Generic;
    using System.Linq;

    public class WorldDetails
    {
        public int DayNumber { get; set; }
        public string DayCycle { get; set; }
        // Live in-game elapsed seconds (ZNet.m_netTime) -- the SAME
        // quantity a Valheim world save's flat-format .db file header
        // stores, just read live instead of from a save file that's been
        // stale since the 1.0 chunked-world-format conversion. DayNumber
        // above is (int)(NetTime / dayLengthSeconds); this is the
        // finer-grained value a consumer needs to render an actual
        // time-of-day clock rather than just a day count.
        public double NetTime { get; set; }
        public string WorldName { get; set; }
        public string SeedName { get; set; }
        public IEnumerable<string> WorldKeys { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<GlobalKey> GlobalKeys { get; set; } = Enumerable.Empty<GlobalKey>();
    }
}