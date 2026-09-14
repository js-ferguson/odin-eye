# OdinEye — maintenance plan

## Context

OdinEye (`github.com/sparcopt/odin-eye`) is a Valheim dedicated-server
BepInEx plugin exposing server/gameplay data (players online, boss
progression, world details) via a REST API and WebSocket event stream —
wanted as the first **server-only** mod for `valheim_server`'s admin
panel, to feed richer telemetry into the players dashboard than what's
there today (session start/end times only, via `session_tailer.py`).

The upstream repo looks unmaintained: no activity in roughly two years,
and its only tagged release (`v1.0.0`, December 2023) predates both the
current BepInEx releases and the Valheim 1.0 launch — compatibility with
either is unconfirmed, not just unbuilt.

## Confirmed so far (2026-09-11)

- The `v1.0.0` release on GitHub has **zero uploaded build artifacts**
  (`assets: []`, confirmed via the GitHub API) despite the project's own
  install docs saying to "download the plugin files from the latest
  release." The GitHub-auto-generated source archive at
  `.../archive/refs/tags/v1.0.0.zip` is genuinely just the C# source
  tree (`.cs`/`.csproj`/`.sln`) — no `.dll`, nothing installable as-is.
- `main` (this local clone) is further ahead than the `v1.0.0` tag —
  worth building from `main`, not the stale tag, once tooling is sorted.
- Build requirements, from `src/OdinEye/OdinEye.csproj`:
  - **Old-style, non-SDK `.csproj`** (MSBuild 4.0 project format,
    `packages.config`-style NuGet restore) targeting **.NET Framework
    4.8.1** — not a modern `dotnet build`-friendly SDK-style project.
  - References Valheim/Unity **managed assemblies** (normally sourced
    from `Valheim_Data/Managed/` inside an actual Steam install of the
    game) and BepInEx reference packages.
  - Hardcoded Windows plugin output path
    (`C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/plugins`)
    baked into the project file.
  - `whitey` (the box running `valheim_server`) has none of this build
    tooling installed, and as a headless Linux dedicated-server host has
    no full Steam game-client copy to source the managed assemblies
    from either.

## Plan

1. **Get a working build environment** — Mono (or Windows/Visual
   Studio, if easier) to build the old-style `.csproj`, plus the
   Valheim managed assemblies (from any machine with the actual game
   installed) and current BepInEx reference packages. Patch/override the
   hardcoded Windows output path rather than relying on it.
2. **Build from `main`** and see what actually breaks against current
   BepInEx + Valheim 1.0 — this is the real unknown; the repo being
   stale doesn't necessarily mean it's broken, but it hasn't been
   verified either way.
3. **Fix whatever compatibility issues turn up.**
4. **Submit a PR upstream** to `sparcopt/odin-eye` with the fixes,
   including (if missing) a proper CI-published release artifact so
   this isn't a one-off local build next time either.
5. **If no response from the maintainer** within a reasonable window,
   **fork the repo** under the user's own account and continue
   maintaining it there — a real release with build artifacts on the
   fork, kept current with BepInEx/Valheim going forward.
6. **Install it**: once a working build exists anywhere (upstream
   release, or the fork's own release), `valheim_server`'s admin panel
   already has the install path ready for exactly this case — `/mods` →
   "Add a mod manually" → **"From a GitHub repo"** (one-click install
   from a repo's latest release, if it has exactly one matchable
   `.zip`/`.dll` asset) or **"Upload a file directly"** otherwise. No
   further work needed on that side (see VALSER-11 in the
   `valheim_server` repo's Plane project).

## Tracking

This work is tracked in its own Plane project (separate from
`valheim_server`'s `VALSER` project, kept intentionally un-cross-posted)
— see that project for actual tickets/tasks. This file is background/
context, not a substitute for the tracked work items.
