namespace OdinEye.Client.Submission
{
    using OdinEye.Models.Api;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Utf8Json;

    // Talks to OdinEye's server-side live cheat-status API (VALSER-50 --
    // see CheatStatusController). Structurally identical to
    // HttpStatsSubmitter (same JSON serializer, same fire-and-forget
    // shape) but posts to a different route with a different body -- kept
    // as its own class rather than a generic/overload on HttpStatsSubmitter
    // since the two submit to genuinely different endpoints with different
    // server-side semantics (monotonic history vs. always-overwrite live
    // state).
    public sealed class HttpCheatStatusSubmitter : ICheatStatusSubmitter, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly Uri serverBaseUri;
        private readonly Action<string> logWarning;

        // serverBaseUri must have a trailing slash, same requirement as
        // HttpStatsSubmitter, for the same reason (relative PostAsync path
        // combination).
        public HttpCheatStatusSubmitter(Uri serverBaseUri, Action<string> logWarning)
        {
            this.serverBaseUri = serverBaseUri ?? throw new ArgumentNullException(nameof(serverBaseUri));
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            httpClient = new HttpClient();
        }

        public async void Submit(Guid playerId, bool cheated)
        {
            try
            {
                var json = JsonSerializer.Serialize(new CheatStatusSubmission { Cheated = cheated });
                var requestUri = new Uri(serverBaseUri, $"players/{playerId}/cheatStatus");

                using (var content = new ByteArrayContent(json))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };

                    var response = await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        logWarning($"OdinEye server rejected cheat-status submission: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                logWarning($"Failed to submit cheat status to OdinEye server: {ex.Message}");
            }
        }

        public void Dispose() => httpClient.Dispose();
    }
}
