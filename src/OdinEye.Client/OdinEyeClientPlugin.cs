namespace OdinEye.Client
{
    using BepInEx;
    using BepInEx.Configuration;
    using HarmonyLib;
    using OdinEye.Client.Counters;
    using OdinEye.Client.Hud;
    using OdinEye.Client.Patches;
    using OdinEye.Client.Stats;
    using OdinEye.Client.Submission;
    using OdinEye.Models;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

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
        // ODINEYE-36 (supersedes Decision 4 of ODINEYE-16's "every 5
        // minutes"): achievements are awarded the moment they are earned, so
        // look for a change every 30 seconds and submit if there is one. A
        // heartbeat still goes out every 5 minutes with no change, because
        // OdinEye's server holds stats only in memory and a restart empties it.
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(5);

        // Queued events (a bed removed, a respawn at the circle) are sent a
        // little sooner than stats: the two halves of Homeless come from
        // different players' machines.
        private static readonly TimeSpan EventFlushInterval = TimeSpan.FromSeconds(5);

        // Gilligan's Island: OutpostTracking.Scan enumerates every loaded
        // Bed each time it runs, so this stays slow -- an outpost being
        // finished a minute later than it could be is unnoticeable; the
        // per-frame cost of FindObjectsOfType every frame would not be.
        private static readonly TimeSpan OutpostScanInterval = TimeSpan.FromSeconds(60);

        // How long a login waits for the server to say which counters it
        // already holds before submitting anyway (ODINEYE-36).
        private static readonly TimeSpan SeedTimeout = TimeSpan.FromSeconds(10);

        // ODINEYE-32: cheap enough to recompute every frame (it's just a
        // few float ops + string formatting), but there's no reason to
        // -- the displayed minute can't change faster than this anyway.
        private static readonly TimeSpan ClockUpdateInterval = TimeSpan.FromSeconds(1);

        private IPlayerStatsSource statsSource;
        private IStatsSubmitter statsSubmitter;
        private ICheatStatusSubmitter cheatStatusSubmitter;
        private ChangeDrivenPolicy policy;
        private Guid? currentPlayerId;

        // ODINEYE-36/38/39: per-login state.
        private Uri serverBaseUri;
        private HttpClient seedClient;
        private HttpEventSubmitter eventSubmitter;
        private CustomCounterStore counters;
        private OutpostAnchorStore outpostAnchors;
        private DateTime nextOutpostScanUtc;
        private IPlayerStatsSource fullStatsSource;
        private volatile bool seeded;
        private DateTime seedDeadlineUtc;
        private bool loginSubmitted;
        private DateTime nextEventFlushUtc;

        // The stats last accepted by the server; null forces the next check
        // to count as "changed" (first submission, or the last one failed).
        private volatile System.Collections.Generic.IReadOnlyDictionary<string, float> lastSubmittedStats;

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

            serverBaseUri = baseUri;
            statsSource = new PlayerProfileStatsSource();
            statsSubmitter = new HttpStatsSubmitter(baseUri, message => Logger.LogWarning(message));
            // VALSER-50: same ServerUrl, same submission cadence as the
            // lifetime-stats submitter above -- a separate call (not part
            // of CharacterStatsSubmission's payload) since it hits a
            // different endpoint with different semantics. See
            // CheatStatusReader's header comment for why this can't reuse
            // the stats pipe.
            cheatStatusSubmitter = new HttpCheatStatusSubmitter(baseUri, message => Logger.LogWarning(message));
            policy = new ChangeDrivenPolicy(CheckInterval, HeartbeatInterval);
            seedClient = new HttpClient();
            eventSubmitter = new HttpEventSubmitter(baseUri, message => Logger.LogWarning(message));
            ClientRuntime.LogWarning = message => Logger.LogWarning(message);
            ApplyPatches();

            Logger.LogInfo($"OdinEye client: character-stats submission enabled, reporting to {baseUri}");
        }

        // Each patch is applied on its own: the game is updated often, and a
        // patch whose target moved must cost only that one feature (it logs
        // and the rest carry on), never the whole client.
        private void ApplyPatches()
        {
            var harmony = new Harmony("org.bepinex.plugins.odineye.client");
            foreach (var type in typeof(OdinEyeClientPlugin).Assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), false))
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"OdinEye client: could not apply {type.Name} ({ex.Message}); the achievement it feeds will not progress until this is updated for the current game version.");
                }
            }
        }

        private void OnDestroy()
        {
            EndSession();
            (statsSubmitter as IDisposable)?.Dispose();
            (cheatStatusSubmitter as IDisposable)?.Dispose();
            eventSubmitter?.Dispose();
            seedClient?.Dispose();
        }

        // Character unloaded (logout, quit): stop recording, save what was
        // counted. Safe to call when there is nothing to end.
        private void EndSession()
        {
            ClientRuntime.Counters = null;
            counters?.Flush();
            counters = null;
            outpostAnchors?.Flush();
            outpostAnchors = null;
            fullStatsSource = null;
            currentPlayerId = null;
            loginSubmitted = false;
            lastSubmittedStats = null;
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
                EndSession(); // reset so the next login is detected fresh
                return;
            }

            var nowUtc = DateTime.UtcNow;

            if (currentPlayerId == null)
            {
                currentPlayerId = ComputeLocalPlayerId();
                if (currentPlayerId == null)
                {
                    return; // profile not ready yet this frame -- try again next Update
                }

                BeginSession(currentPlayerId.Value, nowUtc);
            }

            var playerId = currentPlayerId.Value;

            // Events go out on their own, shorter timer.
            if (nowUtc >= nextEventFlushUtc)
            {
                nextEventFlushUtc = nowUtc + EventFlushInterval;
                eventSubmitter.Flush(playerId, ClientRuntime.Events);
            }

            if (nowUtc >= nextOutpostScanUtc)
            {
                nextOutpostScanUtc = nowUtc + OutpostScanInterval;
                OutpostTracking.Scan(outpostAnchors);
                outpostAnchors.Flush();
            }

            if (!loginSubmitted)
            {
                // The login submission waits for the server's answer about
                // counters it already holds (or gives up waiting), so a lost
                // counter file can never make the first submission go DOWN.
                if (!seeded && nowUtc < seedDeadlineUtc)
                {
                    return;
                }

                loginSubmitted = true;
                policy.OnLogin(nowUtc);
                SubmitAll(playerId, nowUtc);
                return;
            }

            if (!policy.IsCheckDue(nowUtc))
            {
                return;
            }

            counters?.Flush();
            var current = fullStatsSource.GetStats();
            if (policy.ShouldSubmit(nowUtc, StatsChange.HasChanged(lastSubmittedStats, current)))
            {
                SubmitAll(playerId, nowUtc);
            }
        }

        // One login: this character's counter file, then a look at what the
        // server already knows so the counters never start below it.
        private void BeginSession(Guid playerId, DateTime nowUtc)
        {
            counters = new CustomCounterStore(
                Path.Combine(Paths.ConfigPath, $"odineye.client.counters.{playerId:N}.json"),
                message => Logger.LogWarning(message));
            outpostAnchors = new OutpostAnchorStore(
                Path.Combine(Paths.ConfigPath, $"odineye.client.outposts.{playerId:N}.json"),
                message => Logger.LogWarning(message));
            fullStatsSource = new CounterAugmentedStatsSource(statsSource, counters, StationNames.Get,
                NorthTracking.CurrentNorthZ, NorthTracking.IsInDeepNorth, BoatTracking.IsOnBoat, SwampTracking.IsInSwamp);
            seeded = false;
            seedDeadlineUtc = nowUtc + SeedTimeout;
            loginSubmitted = false;
            lastSubmittedStats = null;
            nextEventFlushUtc = nowUtc + EventFlushInterval;
            nextOutpostScanUtc = nowUtc + OutpostScanInterval;

            var store = counters;
            Task.Run(async () =>
            {
                var existing = await StatsSeeder.FetchAsync(seedClient, serverBaseUri, playerId, SeedTimeout, message => Logger.LogWarning(message)).ConfigureAwait(false);
                store.SeedFrom(existing);
                if (ReferenceEquals(counters, store))
                {
                    seeded = true;
                }
            });

            // Counting starts now; anything counted before the seed lands is
            // simply added to the higher of (file, server) afterwards.
            ClientRuntime.Counters = counters;
        }

        private void SubmitAll(Guid playerId, DateTime nowUtc)
        {
            SubmitCurrentStats(playerId);
            SubmitCheatStatus(playerId);
            policy.MarkSubmitted(nowUtc);
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
            var rawStats = fullStatsSource.GetStats();
            if (rawStats.Count == 0)
            {
                return;
            }

            var meta = new OdinEye.Models.Api.SubmissionMeta
            {
                PlayerId = ClientRuntime.LocalPlayerId().ToString(),
                ClientVersion = typeof(OdinEyeClientPlugin).Assembly.GetName().Version.ToString()
            };
            var submission = CharacterStatsPayloadBuilder.Build(rawStats, meta);
            if (submission.Stats.Count == 0)
            {
                return;
            }

            // Assume it will be accepted; if it is not, forget it so the
            // next check counts as "changed" and sends it again.
            lastSubmittedStats = rawStats;
            statsSubmitter.Submit(playerId, submission, accepted =>
            {
                if (!accepted)
                {
                    lastSubmittedStats = null;
                }
            });
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
