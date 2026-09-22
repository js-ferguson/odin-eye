namespace OdinEye.Client.Submission
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Utf8Json;

    // ODINEYE-36: before the first submission, ask the server what it
    // already holds for this character so CustomCounterStore never sends a
    // value below it (the server rejects the WHOLE submission if one value
    // goes down). Only matters after the local counter file was lost.
    public static class StatsSeeder
    {
        // GET /players/stats answers {characterId: {stat: value}} for every
        // character. Returns just ours, or empty if the body is unusable or
        // has no entry for us -- seeding is best-effort, never blocking.
        public static Dictionary<string, float> ParseOwnStats(string json, Guid playerId)
        {
            var own = new Dictionary<string, float>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return own;
            }

            try
            {
                var all = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, float>>>(json);
                if (all != null && all.TryGetValue(playerId.ToString(), out var mine) && mine != null)
                {
                    foreach (var kv in mine)
                    {
                        own[kv.Key] = kv.Value;
                    }
                }
            }
            catch
            {
                // Best-effort: fall through to whatever was parsed (nothing).
            }

            return own;
        }

        public static async Task<Dictionary<string, float>> FetchAsync(HttpClient client, Uri serverBaseUri, Guid playerId, TimeSpan timeout, Action<string> logWarning)
        {
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(timeout))
                {
                    var response = await client.GetAsync(new Uri(serverBaseUri, "players/stats"), cts.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        logWarning($"OdinEye server would not share existing counters: {(int)response.StatusCode}");
                        return new Dictionary<string, float>();
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ParseOwnStats(body, playerId);
                }
            }
            catch (Exception ex)
            {
                logWarning($"Could not read existing counters from OdinEye server: {ex.Message}");
                return new Dictionary<string, float>();
            }
        }
    }
}
