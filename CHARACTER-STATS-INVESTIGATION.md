# Character stats investigation: what's available, what isn't, and why

Local working notes (like `PLAN.md`/`CLAUDE.md`, not committed/PR'd
upstream). Captures a full investigation into how OdinEye gets its
player/character data today, what richer stats (real playtime, deaths,
boss kills, etc.) could be added, and why most obvious approaches turn
out to be dead ends — so this doesn't have to be re-derived later.

## 1. How OdinEye gets player/character data today

Valheim's own network code (`ZNet`, `ZNetPeer`, `ZDO`) holds live
per-player state in memory. OdinEye uses Harmony to patch specific
`ZNet`/`Chat`/`Talker`/`Game` methods (`src/OdinEye/Patches/*.cs`) so its
own code runs alongside the game's — no polling of an external API, it's
all in-process.

**Harmony patches** (`src/OdinEye/Patches/`):
- `ZNetPatch.cs` is the main source: `RPC_PeerInfo` (peer finishes join
  handshake) → `PlayerJoin`; `RPC_CharacterID` (character ZDOID
  assigned) → `PlayerSpawn`; `RPC_Disconnect` → `PlayerDisconnect`;
  `SendPeriodicData` (runs continuously) checks each peer's ZDO for a
  "dead" flag → `PlayerDeath` (text-only, no `Player` payload).
- `ChatPatch.cs` / `TalkerPatch.cs` hook chat methods → `PlayerChat`
  (also text-only — `senderID`, chat position, and `UserInfo.UserId` are
  all read into the method signature but never placed in the event).
- `GamePatch.cs` hooks sleep start/stop → `PlayersSleepStart`/
  `PlayerSleepStop` (also text-only, just a joined string of names).

**Data flow → models**:
- `ZNetPeerExtensions.ToPlayer()` — used for Join/Spawn/Disconnect —
  only sets `Id`/`Name`/`SteamId`; never populates `CharacterId`,
  `Health`, `MaxHealth`, `Stamina` even though the model has fields for
  them.
- `ZNetExtensions.GetAllPeers()` — used by `/players` and the stats
  coroutine — filters to ready peers with a spawned character, fetches
  each one's `ZDO` (`ZDOMan.instance.GetZDO(characterId)`), and reads
  `zdo.GetFloat(ZDOVars.s_health/s_maxHealth/s_stamina)`.
- `GameStatsSnapshotCoroutine` broadcasts all online players' stats +
  world day/night over the WebSocket every 1 second.

**What's exposed**: `GET /players` (7 fields: `Id, CharacterId, SteamId,
Name, Health, MaxHealth, Stamina`), and the WebSocket's `GameEvent`
(join/spawn/disconnect/death/chat/sleep) + `GameStatsSnapshot` (all
players' 3 live vitals + world day/night, every 1s).

**Already read from the game but not surfaced today** (no new Harmony
patches needed, just wiring): `ZNetPeer.m_uid`, position (`m_refPos`),
`eitr` (mana — same ZDO-read pattern as health/stamina), `dead`/
`sleeping`/`inWater`/`pvp`/`stealth`/`noise`/`level` ZDO vars, and
`ZNetPeer.m_serverSyncedPlayerData` (see §3). `GameEvent.Data`
(`IDictionary<string,object>`) is an unused generic extensibility field
already in the proto model.

## 2. The original question: real cumulative in-world hours played

Investigated directly in the live game assembly
(`assembly_valheim.dll`, via a Mono.Cecil-based tool built for this —
`monodis` itself segfaults on full dumps of this newer assembly; Cecil
with `ReadingMode.Deferred` works fine).

Two candidates were found on `PlayerProfile` (the character save-data
class):
- `PlayerProfile.GetStat(PlayerStatType.TimeInBase)` +
  `GetStat(PlayerStatType.TimeOutOfBase)` — public API, but the
  underlying `Player.UpdateStats()` only increments it in ~2.5s windows
  where the player moved >1m, so it undercounts AFK/idle time.
- `PlayerProfile.PlayerStats.m_knownWorlds` (`Dictionary<string,float>`,
  seconds per world, **public field**, no reflection needed after all —
  confirmed by direct field dump) — a true wall-clock diff recorded on
  every save (`SavePlayerToDisk()`: `elapsed = DateTime.Now -
  m_lastSaveLoad`), the accurate number.

Also found: `PlayerProfile.PlayerStats.m_stats`
(`Dictionary<PlayerStatType, float>`) backs ~207 other lifetime stats
via the same public `GetStat`/`IncrementStat` API — deaths, kills (by
boss/enemy type), distance traveled, builds, crafts, fish caught,
consecutive days survived, etc. All public, same mechanism.

**This looked like a solved problem. It wasn't** — see §3.

## 3. The actual blocker: `PlayerProfile` doesn't exist server-side, for anyone but the local player

`PlayerProfile` only exists as a **single instance on `Game`**
(`Game.m_playerProfile`, confirmed via full field dump of `Game`) — the
*local* player's own profile. A dedicated server has no local player.
Checked exhaustively for any per-connected-peer profile reference:

- `ZNetPeer`'s complete field list has no `PlayerProfile` reference —
  only identity/network fields (`m_uid`, `m_socket`, `m_characterID`,
  `m_playerID`, `m_serverSyncedPlayerData`, `m_playerName`, etc.).
- `ZNet` has no such mapping either (full field dump checked).
- **No character save files exist on the live server's disk at all** —
  confirmed directly (`find ... -iname "*.fch"` on whitey's actual
  `valheim_server` data directory: zero results, only world `.db`/`.fwl`
  files). Character profiles are purely client-side.
- Checked every `ZNet` RPC handler that could plausibly carry profile
  data: `RPC_PeerInfo`, `RPC_CharacterID`, `RPC_PlayerID`,
  `RPC_ServerSyncedPlayerData` (generic string k/v only — see below),
  `RPC_PlayerList`/`RPC_HistoricalPlayerList` (display metadata only),
  `RPC_AdminList`/`RPC_Ban`/`RPC_Kick`/`RPC_RemoteCommand`. None carry
  stats/playtime.
- The one method whose *name* looked most promising —
  `ZNet.SaveOtherPlayerProfiles()` — turned out to be the opposite of
  what it sounds like. Decompiled its IL: it's a **server → client**
  broadcast that sends an RPC (`"SavePlayerProfile"`) telling every
  *other* connected client to save *their own* profile to *their own*
  local disk. No profile data ever flows back to the server. This closes
  off the last plausible network path.

**Also checked: Steam Cloud.** Same problem, one platform layer up.
Steam Cloud storage (`ISteamRemoteStorage`) is scoped to whichever Steam
account is logged into the *local* process using it. Valheim's own
Cloud-save code (`ManageSavesMenu`, `Menu/CloudStorageFullOkCallback` —
confirmed present in the assembly, but purely client-side menu code)
only ever manages the *local* player's own save data. A dedicated
server isn't logged in as any connecting player's personal Steam
account, and Steamworks has no API for a server (or anyone else) to
read an arbitrary other user's personal cloud files.

**Conclusion**: a character's real playtime/deaths/kills/etc. has never
been visible to anything except that player's own machine. There is no
retroactive path to it from a server-only mod.

## 4. Is a forward-tracking playtime counter needed in OdinEye itself?

**No** — checked the adjacent `valheim_server` project (the "player
portal"). `agent/session_tailer.py` already does this, and better:

- Tails the dedicated server's Docker logs for "Got connection SteamID
  X" / "Closing socket X" and persists whole session start/end
  timestamps per SteamID to `agent/data/sessions.db` — a *whole-
  connection-duration* measure, strictly more accurate than
  `TimeInBase`/`TimeOutOfBase` (no movement-gating gap).
- Already correlates SteamID → character name by calling OdinEye's own
  `GET /players` (`agent/odin_eye_client.py`), with a documented FIFO
  fallback for whenever OdinEye is unreachable — confirms a live
  integration pattern already exists between the two projects: OdinEye
  serves live state, `valheim_server`'s agent polls/consumes and
  persists history.
- Already powers a real playtime leaderboard in the admin-panel
  (`admin-panel/templates/leaderboard.html`, backed by `/data/playtime`).

Building a second, separate, less-accurate playtime tracker inside
OdinEye would be pure duplication.

## 5. Decided direction: OdinEye becomes a server+client mod

Since the server can never see this data on its own, the only real
source is the player's own machine, where the data genuinely and
legitimately exists. Decided approach:

- A new **client-side** BepInEx component (OdinEye running on the
  connecting player's own machine) reads their local `PlayerProfile`
  (fully accessible there — no barrier, unlike server-side) for real
  playtime and the other `PlayerStatType` lifetime stats, and submits it
  to a new inbound endpoint on OdinEye's **server** side.
- The client component is **optional** — a player without it changes
  nothing; the dashboard/API behave exactly as they do today.
- OdinEye's server side stays **stateless** — a narrow inbound API
  (accept a submission, re-serve it) only, no persistence added to the
  plugin itself.
- Persistence lives in a **new, dedicated SQLite database on the
  `valheim_server` host (whitey)** — not added to `admin-panel`'s
  existing `panel.db`, not inside OdinEye. Owned by a new small ingest
  component on the `valheim_server` side, following
  `session_tailer.py`'s existing pattern (its own DB file, separate
  from the admin-panel's).
- Playtime specifically can keep incrementing from `session_tailer.py`'s
  *already-tracked* session data after an initial baseline submission —
  but deaths/kills/other one-shot stats have no ongoing server-visible
  signal at all (confirmed in §3) and can only advance when the client
  resubmits an updated save.
- Release packaging: each release needs to publish **two** zips (server
  + client) once the client plugin exists, extending the existing
  `release.yaml` workflow.

See the Plane ticket "Investigate: server+client OdinEye — client-side
save extraction submitted to the server API" for the actual follow-on
scope (ingest endpoint shape/auth, submission trigger, new DB schema —
all left open for that investigation to resolve).

## 6. Mac / Apple Silicon compatibility (researched, no blocker)

- Valheim runs **natively** on Apple Silicon (M1/M2/M3/M4) via Steam —
  no translation layer for the game itself.
- **The limitation is entirely upstream, in BepInEx, not Valheim or
  OdinEye**: BepInEx 5 relies on MonoMod, which has no Apple Silicon
  (arm64) native support. Straight from the official BepInExPack
  Valheim docs: *"BepInEx does not run natively on Apple Silicon
  yet... not something we can fix in the pack."* Tracked upstream at
  [BepInEx/BepInEx#899](https://github.com/BepInEx/BepInEx/issues/899)
  (open, no committed timeline).
- **Existing, working community workaround**: Apple Silicon players
  force the game through Rosetta 2 via a Steam launch option
  (`/usr/bin/arch -x86_64 /bin/bash ./start_game_bepinex.sh %command%`),
  running the Intel/x64 build of BepInEx. Intel Macs need no workaround.
  A community mod manager ("Macheim") automates this. Same hurdle every
  existing Valheim BepInEx mod's Mac players already clear — nothing
  OdinEye-specific.
- **Implication for OdinEye's client plugin**: a BepInEx plugin is a
  managed .NET/Mono DLL (IL, JIT'd at runtime) — no native/architecture-
  specific component of its own. The architecture constraint lives
  entirely in BepInEx's own native injection layer (doorstop), not in
  plugin assemblies. So **the client plugin needs no separate Mac build
  or packaging** — the same DLL that runs under BepInEx on Windows/
  Linux runs identically under BepInEx-via-Rosetta on a Mac. One client
  zip covers Windows/Linux/Mac alike.
