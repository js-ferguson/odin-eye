# OdinEye

**Install this on your dedicated server, not your own machine.**

A Valheim dedicated-server plugin that exposes game server data over a REST API and WebSocket event stream: connected players, boss/world-event progression, day/night cycle, and more. Build dashboards, Discord bots, or admin tools against your own server without touching its save files or console.

## Features

- **REST API** — query current online players, boss progression, and world details.
- **WebSocket events** — a live stream of in-game events (player actions, raids, world state) as they happen.
- **Character stats (optional)** — pairs with the separate [OdinEyeClient](https://thunderstore.io/c/valheim/p/SeasonedProfessionals/OdinEyeClient/) mod, installed by players on their own machines, to record real lifetime character stats (playtime, deaths, boss kills). Entirely optional — this server package works the same with or without any player having it installed.

## Setup

Install like any other server-side BepInEx mod. On first launch it generates a config file (`BepInEx/config/org.bepinex.plugins.odineye.cfg`) with a `HttpServerAddress` that works out of the box for API consumers running on the same machine.

## Source & full docs

[github.com/js-ferguson/odin-eye](https://github.com/js-ferguson/odin-eye)
