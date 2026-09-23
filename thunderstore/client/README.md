# OdinEye.Client

Optional client-side companion to [OdinEye](https://github.com/js-ferguson/odin-eye), a Valheim dedicated-server plugin. Install this on **your own machine** (not the server) to submit your character's real lifetime stats and progress toward the server's custom achievements — a dedicated server has no way to read any of this on its own, so without this mod it simply never gets recorded.

## Do I need this?

Only if the server you play on runs OdinEye and its admin has asked you to install it. It's entirely optional and safe to skip:

- The server's basic functionality (session tracking, who's online, hours played) works exactly the same with or without it.
- Installed but left unconfigured, it does nothing at all — no data is read from your character or sent anywhere.
- If your server runs a custom Achievements page, though, this mod is what makes it work: nearly every achievement depends on data only this client can see, so without it your own progress simply never shows up.

## What it reports

- Every lifetime stat Valheim itself already tracks for your character — playtime, deaths, kills by enemy, fish caught, pieces built, and everything else your own character's stats screen shows — none of which a dedicated server can read on its own.
- A set of extra counters this mod tracks itself, for things Valheim keeps no stat for at all: arrow hits landed, food burnt to coal, meads brewed, time spent on a boat or in a particular biome or near a particular structure, items collected by type, and other similar counts an achievement can be built around.
- Progress toward whatever custom achievements your server's admin has set up, so they show up correctly on the Achievements page the moment you qualify.

Everything reported is about your OWN character's own activity, sent to your own server's own OdinEye instance — nothing leaves that server, and nothing about any other player is ever read or sent.

## Setup

1. Install this alongside BepInEx like any other Valheim mod.
2. Launch the game once so BepInEx generates its config file (`BepInEx/config/org.bepinex.plugins.odineye.client.cfg`).
3. Open that file and set `ServerUrl` under `[Server]` to the address your server's admin gives you, e.g.:
   ```
   [Server]
   ServerUrl = http://yourserver.com:2469/
   ```
4. Restart the game. From then on, submission is automatic: immediately on login, then within 30 seconds of anything changing, with a heartbeat at least every 5 minutes even when nothing has — nothing else to do.

## Source

[github.com/js-ferguson/odin-eye](https://github.com/js-ferguson/odin-eye)
