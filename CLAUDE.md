# odin-eye — status & next steps

See `PLAN.md` for why this fork exists in the first place (upstream was
abandoned with no usable release artifact) and the original fork/build
plan -- that plan is now **complete**; this file describes where things
actually stand today, not the original proposal.

## Plane project

This repo's Plane project is **Odin-Eye** (identifier `ODINEYE`).
Whenever we're working in this dir/repo, any mention of Plane, tickets,
todos, backlog, issues, or work items refers to that project — no need
to ask which project. Kept deliberately separate from `valheim_server`'s
`VALSER` project (that repo is scoped to its own project to avoid
cross-posting tickets between the two).

Whenever a ticket that's in the "In Progress" column gets finished, move
it to the "Done" column.

**In practice, most day-to-day feature work on this repo (new
achievements/counters especially) has been tracked as `VALSER-*`
tickets in the `valheim_server` repo's project instead**, since that's
where the achievement design itself lives and each ticket usually spans
both repos. `ODINEYE-*` tickets are for work scoped to this repo alone
(build tooling, the character-stats investigation, version-string
fixes, etc.).

## Status

An actively maintained fork, well past the original "get it building
again" goal: real CI (`.NET Build PR`/`.NET Build Main`/`Release` on
`windows-latest`), a real test suite for both the server plugin
(`OdinEye.Tests`) and the client (`OdinEye.Client.Tests`), and regular
tagged releases published to both GitHub and Thunderstore. Check
`git tag --list | sort -V | tail` or the GitHub releases page for the
current version rather than trusting a number in this file.

The overwhelming majority of ongoing work is **achievement support**
for `valheim_server`'s Achievements page (see the section below) —
OdinEye.Client in particular has grown from "submit native game stats"
into the primary mechanism for detecting nearly everything a custom
achievement can be built on.

## Achievements support (valheim_server VALSER-55 and its many children)

Full design and the current, authoritative list of achievements:
`valheim_server/docs/ACHIEVEMENTS.md`. This section covers only what's
specific to THIS repo's side of that system, and deliberately does not
try to enumerate every achievement/counter here — that list changes
often and would just go stale again (see "A lesson learned the hard
way" below for how badly stale docs already burned us once). The
CURRENT, authoritative counter list is `CounterRules.cs` itself
(`src/OdinEye.Client/Counters/CounterRules.cs`) — every `Custom:*` key
this mod can ever submit is a constant there, each with a comment
naming the achievement it feeds.

### Server mod (`OdinEye`)

* `GET /events?after=<seq>&limit=<n>` — in-memory ring buffer (5000) of
  game events with `BootId` (changes on restart), monotonic `Seq`, and
  `Gap`. Chat is never recorded. `EnemyKilled` carries `Enemy`, `Level`,
  `Boss`, `Attackers`.
* `GET /players/meta` — per-character `PlayerId` + `ClientVersion`
  reported by the client (the latter is USELESS for telling builds
  apart — see the version-string note below).
* `POST /players/{id}/events` — client-reported `BedRemoved` /
  `BedMissingAtRespawn` only (allow-list, whole-batch validation, 20/min
  per player). Stays stateless: nothing is written to disk.
* `POST /players/{steamId}/notify` — a short in-game HUD banner to one
  connected player, content-restricted by an allow-listed prefix set
  (`agent/app.py`'s `NOTIFY_PREFIXES`, on the `valheim_server` side) —
  see ACHIEVEMENTS.md for the full notification design.

### Client mod (`OdinEye.Client`)

* Checks for a change every 30s and submits immediately if anything
  changed; a heartbeat submission still goes out at least every 5
  minutes even with no change, since OdinEye's own server-side stats
  store is in-memory only and needs repopulating after a restart
  (`ChangeDrivenPolicy`).
* **Three categories of data, all riding the same submission**:
  1. Every native `PlayerProfile` stat the game already tracks
     (`PlayerProfileStatsSource`) — playtime, deaths, `EnemyKill:*` per
     species, `FishCaught`, `PiecePlaced:*`, and everything else, none
     of which a dedicated server can read on its own.
  2. This mod's own extra counters (`Custom:*`/`Derived:*`,
     `CounterAugmentedStatsSource` + `CounterRules.cs`) for things
     Valheim keeps no native stat for at all — arrow hits, items
     collected by type, time spent on a boat/in a biome/near a
     structure, and similar. New ones get added via a Harmony patch in
     `Patches/` plus a pure decision function in `CounterRules.cs`
     (tested without a game); several patches now cover more than one
     achievement each by sharing one hook (see `ItemPickupPatch.cs`,
     `CookingStationPatches.cs`).
  3. The few things only a player's own client can OBSERVE as events
     rather than read as a stat (a claimed bed removed with the
     hammer, a respawn at the circle) — `POST /players/{id}/events`.
* `Counters/CustomCounterStore` keeps counters per character in
  BepInEx's config folder and is seeded from the server at login,
  because the server rejects a whole submission if any value goes DOWN.
* Harmony patches (`Patches/`) are applied one class at a time
  (`OdinEyeClientPlugin.ApplyPatches`) so a game update that moves one
  target only costs that one feature — everything else keeps working,
  and the log names exactly which patch failed.

### A lesson learned the hard way: item/prefab identity

Confirmed live, 2026-09-23: `ItemDrop.ItemData.m_shared.m_name` is a
**localization token** (`"$item_pukeberries"`), never the item's actual
name — proven by reading `Character.ShowPickupMessage`'s own IL, which
concatenates a message token directly onto `m_shared.m_name` and hands
the whole thing to the game's own localizer. Two achievements (Vomit
Bomb, Dead-Eye Dick) were matched against this field for months and
could never have fired, on any build, regardless of an earlier,
separate hook-point bug. The correct field is the item's actual prefab
name — `item.m_dropPrefab.name`, or better, `Utils.GetPrefabName
(item.m_dropPrefab)` (the `assembly_utils` helper, which additionally
strips a "(Clone)"/space suffix a raw `.name` read can carry) — the
same convention already used for tamed-creature/enemy identity
(`TameablePatch.cs`, `IncomingHitPatch.cs`, `MudPilePatch.cs`).

**Every new achievement built since has applied this from the start**
rather than repeating the mistake, and confirms the prefab name against
the game's own asset manifest (`StreamingAssets/SoftRef/
manifest_extended` inside an actual game install) before trusting it —
see any recent `Patches/*.cs` file's own header comments for worked
examples of this check.

### Version strings are useless for telling builds apart (ODINEYE-43)

Both plugins' `[BepInPlugin]`/assembly version are hardcoded to
`"1.0.0.0"` regardless of the actual release tag — confirmed live: a
BepInEx log line reads `Loading [odineye 1.0.0.0]` even when a much
later version is genuinely running, and `OdinEyeClient.players_meta()`'s
`ClientVersion` field is the same hardcoded string for every character.
**Do not trust any in-game or log version string** when trying to
confirm which build a player has installed — ask them to check their
Thunderstore Mod Manager's own reported version instead. Fixing this
(stamping the real version at build time) is tracked but not done yet.

### Testing

```
msbuild OdinEye.sln -t:rebuild -property:Configuration=Release
mono src/OdinEye.Tests/bin/Release/OdinEye.Tests.exe
mono src/OdinEye.Client.Tests/bin/Release/OdinEye.Client.Tests.exe
```

Both are real `NUnit`/`NUnitLite`-over-`mono` test projects (no `dotnet`
CLI needed locally — `msbuild`/`mono`/`nuget` from this box's own
package manager are enough). `OdinEye.Client.Tests` can reference the
whole `OdinEye.Client` assembly (Harmony patches, game types, and all)
safely without ever loading BepInEx/Unity/Valheim, because the CLR
resolves an assembly lazily — the tests just never touch anything that
actually calls into a live game singleton. Patch/controller logic that
DOES touch live game state stays untested here by design; the pure
decision functions behind each one (`CounterRules.cs`, `BedEvents.cs`,
etc.) are what's actually covered.

### Releasing

Tag `vX.Y.Z` as a GitHub Release (`gh release create`) — `release.yaml`
triggers on `release: published`, builds both plugins, uploads the
GitHub release zips, and (if `THUNDERSTORE_TOKEN` is configured, which
it is) publishes both packages to Thunderstore in the same run. No
manual Thunderstore step needed. Confirmed reliable across many real
releases now (v1.2.24 onward at minimum) — build/publish failures are
not a live risk class here anymore, unlike earlier in this fork's
history (see PLAN.md/§5.5 of `valheim_server/docs/ARCHITECTURE.md` for
the real bugs that WERE found shipping this).
