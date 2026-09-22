---
sidebar_position: 1
---

# Introduction

:::note[This is a fork]
This site documents [js-ferguson/odin-eye](https://github.com/js-ferguson/odin-eye), a maintained fork of the original [sparcopt/odin-eye](https://github.com/sparcopt/odin-eye), which was archived. Breaking changes may still happen without prior notice -- this remains a small hobby project, not a stable public API.
:::

OdinEye is a free, open-source Valheim dedicated-server plugin that exposes server and gameplay data. It provides a REST API for querying things like connected players, boss progression and world/server details, plus in-game events -- player actions, kills, world saves, and more -- delivered either as a live WebSocket stream or through a durable, pollable feed (`GET /events`).

An optional companion mod, **OdinEye.Client**, installs on a player's own PC and adds things only their own machine can see: full lifetime character stats (deaths, boss kills, per-piece build history, every world played on, ...), plus a couple of client-observed events. It is entirely optional -- the server-side plugin works fully without it, OdinEye.Client just adds more.

## Features

- <b>Seamless integration</b>
  - Install the plugin and extend your server's capabilities in just a few minutes.
- <b>Game server API</b>
  - Query game server data such as player info, boss progression, world details and the current difficulty modifiers.
- <b>Game events</b>
  - Consume live events over a WebSocket, or poll `GET /events` for a durable feed that survives nobody being connected at the moment something happened.
- <b>Character stats (optional, via OdinEye.Client)</b>
  - A player's real lifetime stats, submitted from their own PC and readable back through the server's own API.
