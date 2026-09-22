<p align="center">
  <img src="docs/odineye.png" height="128">
  <h2 align="center">OdinEye</h2>
  <p align="center">Valheim dedicated-server plugin exposing game server data through a REST API and a WebSocket event stream.<p>
  <p align="center">
    <img src="https://img.shields.io/github/last-commit/js-ferguson/odin-eye">
    <a href="https://github.com/js-ferguson/odin-eye/releases/latest">
      <img alt="Latest release" src="https://img.shields.io/github/v/release/js-ferguson/odin-eye"></a>
    <a href="https://thunderstore.io/c/valheim/p/SeasonedProfessionals/OdinEye/">
      <img alt="Thunderstore" src="https://img.shields.io/badge/dynamic/json?label=OdinEye&query=%24.package.version_number&url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fexperimental%2Fpackage%2FSeasonedProfessionals%2FOdinEye%2F&color=00C7D4"></a>
    <a href="https://thunderstore.io/c/valheim/p/SeasonedProfessionals/OdinEyeClient/">
      <img alt="Thunderstore" src="https://img.shields.io/badge/dynamic/json?label=OdinEye.Client&query=%24.package.version_number&url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fexperimental%2Fpackage%2FSeasonedProfessionals%2FOdinEyeClient%2F&color=00C7D4"></a>
    <a href="https://github.com/js-ferguson/odin-eye/actions/workflows/build-main.yaml" >
      <img alt="GitHub Workflow Status (with event)" src="https://img.shields.io/github/actions/workflow/status/js-ferguson/odin-eye/build-main.yaml?label=main"></a>
    <a href="https://github.com/js-ferguson/odin-eye/actions/workflows/build-pr.yaml" >
      <img alt="GitHub Workflow Status (with event)" src="https://img.shields.io/github/actions/workflow/status/js-ferguson/odin-eye/build-pr.yaml?label=pull%20request"></a>
  </p>
</p>

> [!NOTE]
> This is a maintained fork of [sparcopt/odin-eye](https://github.com/sparcopt/odin-eye), which was archived/abandoned. This fork keeps it building against current Valheim/BepInEx versions and extends it with new capabilities -- see [What's new in this fork](#-whats-new-in-this-fork) below.

## ℹ️ Overview

OdinEye is a free, open-source Valheim dedicated-server plugin that exposes server and gameplay data. It provides a REST API for querying data like connected players, boss progression and world/server details, plus a push stream (WebSocket) and a durable pull feed (`GET /events`) of in-game events -- player actions, kills, world saves, and more.

An optional companion mod, **OdinEye.Client**, runs on a player's own PC and adds things only a player's own machine can see: their character's full lifetime stats (deaths, boss kills, every world they've played on, ...) and a couple of client-observed events. Nobody has to install it for the server-side plugin to work -- it only adds to what OdinEye already reports on its own.

## ✨ Features

- **Seamless integration**
  - Install the plugin and extend your server's capabilities in just a few minutes.
- **Game server API**
  - Query player info, boss progression, world details, and the world's current difficulty modifiers.
- **Game events**
  - Consume live events over a WebSocket, or poll `GET /events` for a durable, replayable feed -- useful when nothing was connected to the socket at the moment something happened.
- **Character stats (optional, via OdinEye.Client)**
  - A player's real lifetime stats and per-piece build history, submitted from their own PC and readable back from the server's own API.

## 🏆 Built for: Achievements in valheim_server

This fork exists to support the [`valheim_server`](https://github.com/js-ferguson/valheim_server) project's **Achievements system**: a page where a group of friends define their own achievements and get awarded them automatically as soon as they're earned. Everything under "What's new in this fork" below was built for that -- see [`valheim_server`'s own `docs/ACHIEVEMENTS.md`](https://github.com/js-ferguson/valheim_server/blob/main/docs/ACHIEVEMENTS.md) for the full design. OdinEye and OdinEye.Client are both general-purpose, though -- nothing here requires running valheim_server.

## 🆕 What's new in this fork

Beyond keeping the plugin building against current Valheim/BepInEx (the original was unmaintained for two years):

- `GET /events` -- a durable, pull-based event feed (an in-memory ring buffer with a boot ID and a monotonic sequence number), so a consumer that wasn't connected to the WebSocket doesn't miss anything.
- `GET /players/meta` -- what each connected character's OdinEye.Client (if installed) has reported about itself.
- `POST /players/{id}/events` -- lets OdinEye.Client report the few things only a player's own machine can observe (currently: a claimed bed removed with the hammer, and a respawn at the circle because a bed was missing).
- `GET`/`POST /players/cheatStatus` -- live cheat-detection status per character, reported by OdinEye.Client.
- `POST /players/{steamId}/notify` -- a short HUD banner shown to one connected player.
- `GET /worldModifiers` -- the world's current difficulty settings, read back from the game's own state.
- OdinEye.Client now reads the game's own per-piece placement history and submits changes within 30 seconds (was a flat 5 minutes), plus a small set of counters for things Valheim doesn't track on its own.
- Both plugins are published on Thunderstore and ship a proper automated test suite (neither existed before this fork).

## 📦 Getting started

Installation instructions: [Installation](https://js-ferguson.github.io/odin-eye/docs/getting-started/installation)

## 📨 API

Learn more about the API and how to use it: [API Reference](https://js-ferguson.github.io/odin-eye/docs/category/api-reference)

## ⚡ Events

Learn more about the game events and how to use them: [Events](https://js-ferguson.github.io/odin-eye/docs/category/events)
