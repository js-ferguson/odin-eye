namespace OdinEye.Client.Submission
{
    using OdinEye.Client.Events;
    using OdinEye.Models.Api;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Utf8Json;

    // ODINEYE-39: POST /players/{id}/events. Sends whatever is queued and, if
    // the server does not accept it, puts it back for the next attempt. Never
    // throws into the game.
    public sealed class HttpEventSubmitter : IDisposable
    {
        private const int BatchSize = 20; // the server's per-request maximum

        private readonly HttpClient httpClient = new HttpClient();
        private readonly Uri serverBaseUri;
        private readonly Action<string> logWarning;
        private int sending;

        public HttpEventSubmitter(Uri serverBaseUri, Action<string> logWarning)
        {
            this.serverBaseUri = serverBaseUri ?? throw new ArgumentNullException(nameof(serverBaseUri));
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
        }

        public async void Flush(Guid playerId, ClientEventQueue queue)
        {
            // One flush at a time: overlapping ones would reorder a requeued batch.
            if (queue.Count == 0 || System.Threading.Interlocked.Exchange(ref sending, 1) == 1)
            {
                return;
            }

            try
            {
                while (queue.Count > 0)
                {
                    var batch = queue.TakeBatch(BatchSize);
                    if (!await SendAsync(playerId, batch).ConfigureAwait(false))
                    {
                        queue.Requeue(batch);
                        return;
                    }
                }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref sending, 0);
            }
        }

        private async Task<bool> SendAsync(Guid playerId, List<ClientEvent> batch)
        {
            try
            {
                var json = JsonSerializer.Serialize(new ClientEventsSubmission { Events = batch });
                using (var content = new ByteArrayContent(json))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };
                    var response = await httpClient.PostAsync(new Uri(serverBaseUri, $"players/{playerId}/events"), content).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    // A 400 will never succeed on retry; dropping it stops one
                    // bad event blocking every later one. Anything else
                    // (404 not connected yet, 429 slow down, 5xx) is retried.
                    var code = (int)response.StatusCode;
                    logWarning($"OdinEye server did not accept {batch.Count} event(s): {code} {response.ReasonPhrase}");
                    return code == 400;
                }
            }
            catch (Exception ex)
            {
                logWarning($"Failed to send events to OdinEye server: {ex.Message}");
                return false;
            }
        }

        public void Dispose() => httpClient.Dispose();
    }
}
