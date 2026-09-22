namespace OdinEye.Client.Submission
{
    using OdinEye.Models.Api;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Utf8Json;

    // Talks to OdinEye's server-side ingest API (POST /players/{id}/stats --
    // see ODINEYE-19/CharacterStatsController). JSON, matching the rest of
    // OdinEye's REST API (Decision 2 confirmed the client<->server
    // conversation happens in JSON, same serializer -- Utf8Json -- the
    // server already uses).
    public sealed class HttpStatsSubmitter : IStatsSubmitter, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly Uri serverBaseUri;
        private readonly Action<string> logWarning;

        // serverBaseUri must have a trailing slash so relative PostAsync
        // paths below combine correctly instead of replacing its last
        // segment (standard System.Uri relative-combination behavior).
        public HttpStatsSubmitter(Uri serverBaseUri, Action<string> logWarning)
        {
            this.serverBaseUri = serverBaseUri ?? throw new ArgumentNullException(nameof(serverBaseUri));
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            httpClient = new HttpClient();
        }

        public async void Submit(Guid playerId, CharacterStatsSubmission submission, Action<bool> onComplete = null)
        {
            var accepted = false;
            try
            {
                var json = JsonSerializer.Serialize(submission);
                var requestUri = new Uri(serverBaseUri, $"players/{playerId}/stats");

                using (var content = new ByteArrayContent(json))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };

                    var response = await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
                    accepted = response.IsSuccessStatusCode;
                    if (!accepted)
                    {
                        logWarning($"OdinEye server rejected character-stats submission: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let a network hiccup surface past this boundary --
                // the next scheduled tick (SubmissionScheduler) just tries
                // again.
                logWarning($"Failed to submit character stats to OdinEye server: {ex.Message}");
            }

            try
            {
                onComplete?.Invoke(accepted);
            }
            catch
            {
                // a misbehaving callback must not surface past this boundary either
            }
        }

        public void Dispose() => httpClient.Dispose();
    }
}
