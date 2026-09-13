namespace OdinEye.Client
{
    using BepInEx;
    using BepInEx.Configuration;
    using OdinEye.Client.Stats;
    using OdinEye.Client.Submission;
    using OdinEye.Models;
    using System;

    // Optional companion to the server-side OdinEye plugin (ODINEYE-20).
    // A player who never installs this changes nothing about the server or
    // its API -- see CHARACTER-STATS-INVESTIGATION.md. When installed but
    // left unconfigured (the default), it also does nothing: no local data
    // is read or sent unless ServerUrl is explicitly set.
    [BepInPlugin("org.bepinex.plugins.odineye.client", "odineye.client", "1.0.0.0")]
    public class OdinEyeClientPlugin : BaseUnityPlugin
    {
        // Decision 4 (ODINEYE-16): submit on login, then every 5 minutes.
        private static readonly TimeSpan SubmissionInterval = TimeSpan.FromMinutes(5);

        private IPlayerStatsSource statsSource;
        private IStatsSubmitter statsSubmitter;
        private SubmissionScheduler scheduler;
        private Guid? currentPlayerId;

        private void Awake()
        {
            var serverUrl = Config.Bind(
                "Server",
                "ServerUrl",
                string.Empty,
                "The OdinEye server's base URL to submit this character's lifetime stats to " +
                "(e.g. http://yourserver.com:2469/). Leave empty to disable this plugin entirely -- " +
                "nothing is read from your character or sent anywhere unless this is set.");

            if (string.IsNullOrWhiteSpace(serverUrl.Value))
            {
                Logger.LogInfo("OdinEye client: ServerUrl not configured, character-stats submission is disabled.");
                return;
            }

            if (!Uri.TryCreate(EnsureTrailingSlash(serverUrl.Value), UriKind.Absolute, out var baseUri))
            {
                Logger.LogError($"OdinEye client: ServerUrl '{serverUrl.Value}' is not a valid URL. Character-stats submission is disabled.");
                return;
            }

            statsSource = new PlayerProfileStatsSource();
            statsSubmitter = new HttpStatsSubmitter(baseUri, message => Logger.LogWarning(message));
            scheduler = new SubmissionScheduler(SubmissionInterval);

            Logger.LogInfo($"OdinEye client: character-stats submission enabled, reporting to {baseUri}");
        }

        private void OnDestroy() => (statsSubmitter as IDisposable)?.Dispose();

        private void Update()
        {
            if (statsSubmitter == null)
            {
                return; // disabled: not configured, or a bad ServerUrl
            }

            if (Player.m_localPlayer == null)
            {
                currentPlayerId = null; // reset so the next login is detected fresh
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var justLoggedIn = currentPlayerId == null;

            if (justLoggedIn)
            {
                currentPlayerId = ComputeLocalPlayerId();
                if (currentPlayerId == null)
                {
                    return; // profile not ready yet this frame -- try again next Update
                }

                scheduler.OnLogin(nowUtc);
            }
            else if (!scheduler.IsDue(nowUtc))
            {
                return;
            }

            SubmitCurrentStats(currentPlayerId.Value);
        }

        private void SubmitCurrentStats(Guid playerId)
        {
            var rawStats = statsSource.GetStats();
            if (rawStats.Count == 0)
            {
                return;
            }

            var submission = CharacterStatsPayloadBuilder.Build(rawStats);
            if (submission.Stats.Count == 0)
            {
                return;
            }

            statsSubmitter.Submit(playerId, submission);
        }

        // Must match the server's own derivation exactly (ZNetPeerExtensions/
        // PeerExtensions): the same NameBasedGuid helper, fed the player's
        // Steam64 ID and character name. Server-side this comes from
        // peer.m_socket.GetHostName()/peer.m_playerName; client-side there is
        // no peer for "yourself", so it's read directly from Steamworks and
        // the local PlayerProfile instead.
        private static Guid? ComputeLocalPlayerId()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null)
            {
                return null;
            }

            var playerName = profile.GetName();
            if (string.IsNullOrEmpty(playerName))
            {
                return null;
            }

            var steamId = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();
            return NameBasedGuid.NewPlayerGuid(steamId, playerName);
        }

        private static string EnsureTrailingSlash(string url) => url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
    }
}
