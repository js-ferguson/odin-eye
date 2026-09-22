---
id: intro
description: "Learn how to use the OdinEye Events"
sidebar_label: Introduction
sidebar_position: 0
hide_title: true
custom_edit_url: null
---

# Introduction

The OdinEye events capture a variety of in-game actions and activities initiated by players and the game world. These events are then enriched with data and delivered either as a continuous WebSocket stream, or through a durable, pollable REST feed. For example, events can be used to track players joining the server, when the day changes, or when the game is saved.

There are two ways to consume them, and they carry the same events -- pick whichever fits your consumer:

- **WebSocket stream** (`/activity`) -- push-based, lowest latency, but you only see events that happen while you're connected. Nothing is replayed after a reconnect.
- **`GET /events`** -- a pull-based, durable feed: an in-memory ring buffer (5000 events by default) that any number of consumers can poll independently, each tracking its own position. Use this when your consumer might not always be connected (e.g. a background job that polls every few seconds) and still needs to see everything that happened in between.

## WebSocket stream

Event payloads on the WebSocket are serialized with [protobuf](https://protobuf.dev/).

### Base URL

```
ws://localhost:2469/activity
```

:::info[Important]
The activity path (`/activity`) must be included.
:::

The base URL (including the port) should match wherever OdinEye's `HttpServerAddress` is actually configured to listen -- see [Installation](../getting-started/installation.mdx). The WebSocket is served from the same host and port as the REST API, not a separate one.

## `GET /events` (durable feed)

```
GET /events?after=<seq>&limit=<n>
```

Returns JSON, not protobuf: `{ BootId, NextSeq, Gap, Events: [...] }`.

- **`after`** -- return only events with a sequence number greater than this. Start at `0` to read from the beginning of what's currently held.
- **`limit`** -- how many events to return at most (server-side default and cap apply if omitted or too large).
- **`BootId`** -- a GUID generated when OdinEye starts. It changes every restart, and every event's sequence number (`Seq`) restarts at `1` under a new `BootId` -- so a consumer that dedupes on `(BootId, Seq)` never double-counts an event, even across a restart.
- **`Gap`** -- `true` when the events you asked for (`after` + everything since) are no longer all held in the buffer, meaning some were dropped before you could read them. Check this if your consumer cares about not silently missing anything.

Poll it on whatever interval suits your consumer -- there's no rate limit of its own, but each poll is a full scan of the buffer up to `limit`, so avoid polling far more often than you need to.

**Chat messages are never included** in either the WebSocket stream or `GET /events` -- what players type to each other isn't something OdinEye keeps.

## Authentication

:::note
Authentication has not been implemented at this time.
:::

Neither the WebSocket stream nor `GET /events` (nor any other OdinEye endpoint) require an API key. Anyone who can reach the configured `HttpServerAddress` can read everything -- see the "Server Address" remarks in [Installation](../getting-started/installation.mdx) for how to keep that from being the whole internet.
