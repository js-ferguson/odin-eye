# OdinEye.Client

Optional client-side companion to [OdinEye](https://github.com/js-ferguson/odin-eye), a Valheim dedicated-server plugin. Install this on **your own machine** (not the server) to submit your character's real lifetime stats — total playtime, deaths, boss kills, and every other stat Valheim tracks — to your server's OdinEye instance, so it's recorded even though a dedicated server has no way to read that data itself.

## Do I need this?

Only if the server you play on runs OdinEye and its admin has asked you to install it. It's entirely optional and safe to skip:

- The server works exactly the same with or without it.
- Installed but left unconfigured, it does nothing at all — no data is read from your character or sent anywhere.

## Setup

1. Install this alongside BepInEx like any other Valheim mod.
2. Launch the game once so BepInEx generates its config file (`BepInEx/config/org.bepinex.plugins.odineye.client.cfg`).
3. Open that file and set `ServerUrl` under `[Server]` to the address your server's admin gives you, e.g.:
   ```
   [Server]
   ServerUrl = http://yourserver.com:2469/
   ```
4. Restart the game. Your stats submit automatically on login and every 5 minutes while connected — nothing else to do.

## Source

[github.com/js-ferguson/odin-eye](https://github.com/js-ferguson/odin-eye)
