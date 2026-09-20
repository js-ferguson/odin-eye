namespace OdinEye.Client
{
    using BepInEx;
    using BepInEx.Configuration;
    using OdinEye.Client.Hud;
    using OdinEye.Client.Stats;
    using OdinEye.Client.Submission;
    using OdinEye.Models;
    using System;

    // Optional companion to the server-side OdinEye plugin (ODINEYE-20).
    // A player who never installs this changes nothing about the server or
    // its API -- see CHARACTER-STATS-INVESTIGATION.md. When installed but
    // left unconfigured (the default), character-stats submission also
    // does nothing: no local data is read or sent unless ServerUrl is
    // explicitly set. The HUD clock (ODINEYE-32) is the one exception --
    // a purely local, no-server-dependency visual feature, on by default
    // (its own ShowClock toggle), independent of ServerUrl entirely.
    [BepInPlugin("org.bepinex.plugins.odineye.client", "odineye.client", "1.0.0.0")]
    public class OdinEyeClientPlugin : BaseUnityPlugin
    {
        // Decision 4 (ODINEYE-16): submit on login, then every 5 minutes.
        private static readonly TimeSpan SubmissionInterval = TimeSpan.FromMinutes(5);

        // ODINEYE-32: cheap enough to recompute every frame (it's just a
        // few float ops + string formatting), but there's no reason to
        // -- the displayed minute can't change faster than this anyway.
        private static readonly TimeSpan ClockUpdateInterval = TimeSpan.FromSeconds(1);

        private IPlayerStatsSource statsSource;
        private IStatsSubmitter statsSubmitter;
        private ICheatStatusSubmitter cheatStatusSubmitter;
        private SubmissionScheduler scheduler;
        private Guid? currentPlayerId;

        private ConfigEntry<bool> showClock;
        private ClockHudElement clockHud;
        private DateTime nextClockUpdateUtc;

        private void Awake()
        {
            showClock = Config.Bind(
                "Hud",
                "ShowClock",
                true,
                "Show a small in-game clock just below the minimap (VALSER-40/41's own \"temporal hours\" " +
                "reading -- dawn is always 6:00 AM, dusk always 6:00 PM). Purely local/visual -- reads " +
                "nothing from your character and sends nothing anywhere, unaffected by ServerUrl below.");
            clockHud = new ClockHudElement();

            var serverUrl = Config.Bind(
                "Server",
                "ServerUrl",
                string.Empty,
                "The OdinEye server's base URL to submit this character's lifetime stats to " +
                "(e.g. http://yourserver.com:2469/). Leave empty to disable stats/cheat-status " +
                "submission -- nothing is read from your character or sent anywhere unless this is set. " +
                "Does not affect the HUD clock above.");

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
            // VALSER-50: same ServerUrl, same submission cadence as the
            // lifetime-stats submitter above -- a separate call (not part
            // of CharacterStatsSubmission's payload) since it hits a
            // different endpoint with different semantics. See
            // CheatStatusReader's header comment for why this can't reuse
            // the stats pipe.
            cheatStatusSubmitter = new HttpCheatStatusSubmitter(baseUri, message => Logger.LogWarning(message));
            scheduler = new SubmissionScheduler(SubmissionInterval);

            Logger.LogInfo($"OdinEye client: character-stats submission enabled, reporting to {baseUri}");
        }

        private void OnDestroy()
        {
            (statsSubmitter as IDisposable)?.Dispose();
            (cheatStatusSubmitter as IDisposable)?.Dispose();
        }

        private void Update()
        {
            UpdateClock();

            if (statsSubmitter == null)
            {
                return; // stats/cheat-status disabled: not configured, or a bad ServerUrl
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
            SubmitCheatStatus(currentPlayerId.Value);
        }

        // ODINEYE-32: entirely independent of the stats/cheat-status
        // pipeline above -- runs (when enabled) even with no ServerUrl
        // configured at all, and even before the local player has
        // spawned in (Minimap.instance is what actually gates it, via
        // ClockHudElement itself).
        private void UpdateClock()
        {
            if (!showClock.Value)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            if (nowUtc < nextClockUpdateUtc)
            {
                return;
            }
            nextClockUpdateUtc = nowUtc + ClockUpdateInterval;

            var totalSeconds = EnvManTimeReader.GetTotalSeconds();
            if (totalSeconds == null)
            {
                return;
            }

            clockHud.SetText(TemporalClock.Format(totalSeconds.Value));
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

        // VALSER-50: null means CheatStatusReader couldn't resolve the
        // game API this build (see its own header comment) -- skip rather
        // than submit a confidently wrong "clean". CheatBypassReader
        // (ODINEYE-30) has no such uncertainty -- it's built on old,
        // stable pre-1.0 APIs -- so it's read unconditionally whenever
        // Cheated itself was resolvable.
        private void SubmitCheatStatus(Guid playerId)
        {
            var cheated = CheatStatusReader.IsCheated();
            if (cheated == null)
            {
                return;
            }

            cheatStatusSubmitter.Submit(playerId, cheated.Value, CheatBypassReader.IsEnabled());
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
