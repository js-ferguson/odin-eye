# odin-eye — status & next steps

See `PLAN.md` for the full context and plan (why this fork/maintenance
effort exists, what's confirmed broken/missing upstream, and the
build/PR/fork sequence).

## Plane project

This repo's Plane project is **Odin-Eye** (identifier `ODINEYE`).
Whenever we're working in this dir/repo, any mention of Plane, tickets,
todos, backlog, issues, or work items refers to that project — no need
to ask which project. Kept deliberately separate from `valheim_server`'s
`VALSER` project (that repo is scoped to its own project to avoid
cross-posting tickets between the two).

Whenever a ticket that's in the "In Progress" column gets finished, move
it to the "Done" column.

## Status

Just cloned from `github.com/sparcopt/odin-eye` (upstream, unmaintained
~2 years) — no work started yet. See `PLAN.md`.

## Achievements support (ODINEYE-34..40; valheim_server VALSER-55)

Implemented on `feature/achievements`, with unit tests, NOT yet released.
Full design and rollout: `valheim_server/docs/ACHIEVEMENTS.md`.

Server mod (`OdinEye`):
* `GET /events?after=<seq>&limit=<n>` — in-memory ring buffer (5000) of game
  events with `BootId` (changes on restart), monotonic `Seq`, and `Gap`.
  Chat is never recorded. `EnemyKilled` carries `Enemy`, `Level`, `Boss`,
  `Attackers`.
* `GET /players/meta` — per-character `PlayerId` + `ClientVersion` reported by
  the client (needed to match beds to their owners).
* `POST /players/{id}/events` — client-reported `BedRemoved` /
  `BedMissingAtRespawn` only (allow-list, whole-batch validation, 20/min per
  player). Stays stateless: nothing is written to disk.

Client mod (`OdinEye.Client`):
* Checks stats every 30 s and submits on change (5-minute heartbeat).
* Sends `PiecePlaced:*` (the game's `m_piecesPlacedStats`),
  `Derived:StationOrUpgradePlaced` (high-water), `Custom:ArrowHitsEnemy`,
  `Custom:BreadCollected`, `Custom:DwarfEyesTouched`, `Custom:FoodBurntToCoal`,
  `Derived:FurthestNorthZ` (high-water, +Z is north), `Custom:ReachedDeepNorth`
  (once-ever flag, via `Player.GetCurrentBiome()`), and `Meta` (player ID,
  version). The last two feed valheim_server's "Peter North" achievement --
  the one achievement in that whole system that gets taken away from one
  player and given to another, so it lives on a rule type of its own
  (`leaderboard`) rather than the usual plain threshold.
* `Counters/CustomCounterStore` keeps counters per character in BepInEx's
  config folder and is seeded from the server at login, because the server
  rejects a whole submission if any value goes DOWN.
* Harmony patches (`Patches/`) are applied one class at a time so a game update
  that moves one target only costs that feature. Their decisions live in pure
  classes (`CounterRules`, `BedEvents`) with tests; the patches themselves need a
  live game (see the checklist in ODINEYE-34).

Test: `msbuild OdinEye.sln /p:Configuration=Release` then
`mono src/OdinEye.Tests/bin/Release/OdinEye.Tests.exe` and
`mono src/OdinEye.Client.Tests/bin/Release/OdinEye.Client.Tests.exe`.
