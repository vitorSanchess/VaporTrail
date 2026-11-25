# VaporTrail

A lightweight ASP.NET Core Minimal API (.NET 10) Steam profile viewer that pulls player summaries, Steam level, and playtime from the official Steam Web API and displays them in a responsive **Light & Dark Neumorphism** web interface.

## Features

- **Flexible Steam Input Parsing**: Accepts 17-digit 64-bit SteamIDs (`76561197960435530`), custom vanity names (`gabelogannewell`), or pasted `steamcommunity.com/profiles/...` and `steamcommunity.com/id/...` URLs.
- **Parallel Steam Web API Fetching**: Resolves vanity names via `ISteamUser/ResolveVanityURL/v1`, then fetches `GetPlayerSummaries`, `GetSteamLevel`, `GetRecentlyPlayedGames`, `GetOwnedGames`, `GetBadges`, `GetPlayerBans`, and `GetFriendList` concurrently.
- **Account Standing & Progression**: Displays VAC, game, community, and economy ban standing alongside badge count, friend count, and an XP progress bar toward the next Steam level.
- **Backlog & Engagement ("Pile of Shame")**: Tracks played vs. unplayed games across playtime depth buckets (`Unplayed`, `Under 2h`, `2–20h`, `20h+`), top-3 playtime share, median played hours, and estimated main-story hours to clear unplayed titles via HowLongToBeat.
- **Genre, Tag & Developer Breakdown**: Interactive pure-SVG hollow pie (donut) chart switching between playtime and game count, proportional top developers list, and top community tags.
- **Local SQLite Game Catalog (`leinstay/steamdb`)**: Streams the nightly [`leinstay/steamdb`](https://github.com/leinstay/steamdb) `steamdb.min.json.gz` dump into a gitignored local SQLite cache (`src/VaporTrail/App_Data/steamdb.sqlite`) with automatic background sync and built-in offline seed fallback for Demo Mode.
- **Graceful Private Profile Handling**: Detects private profiles, friend lists, or hidden game libraries and cleanly displays available identity and standing info with a clear notice.
- **5-Minute In-Memory Caching**: Uses `IMemoryCache` to keep repeat lookups instant and minimize Steam API calls, with a manual **Refresh** button (`?refresh=true`).
- **Built-in Demo Mode**: Runs immediately out-of-the-box with sample profiles (public in-game, SteamID64, and private profile examples) when no API key is configured.
- **Light & Dark Neumorphism UI**: Tactile theme toggle persisted in `localStorage`, multi-layered shadows with specular rims, recent lookup history, clickable sample chips, and offline-safe SVG fallbacks.

## Getting Started

### Run in Demo Mode (no API key required)

```bash
dotnet run --project src/VaporTrail
```

### Connect Your Live Steam Web API Key

Get a free key from [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey), then configure it via **.NET User Secrets** (recommended for local dev) or an environment variable:

```bash
# Option 1: .NET User Secrets
dotnet user-secrets init --project src/VaporTrail
dotnet user-secrets set "Steam:ApiKey" "YOUR_STEAM_API_KEY" --project src/VaporTrail
dotnet run --project src/VaporTrail

# Option 2: Environment variable
STEAM_API_KEY="YOUR_STEAM_API_KEY" dotnet run --project src/VaporTrail
```

## Running Tests

```bash
dotnet test
```

## API Endpoints

- `GET /api/status` — Current mode (`demo` or `live`), cache TTL, sample lookup hints, and local catalog status.
- `GET /api/steam/profile?query=<id-or-vanity-or-url>&refresh=<true|false>` — Fetches and aggregates Steam profile data, standing, progression, and library breakdown.
- `GET /api/catalog/status` — Local SQLite catalog readiness, cached game count, and last sync timestamp.
- `POST /api/catalog/sync?force=<true|false>` — Triggers an on-demand sync from the nightly `leinstay/steamdb` dump.
- `GET /api/health` — Basic health check.

## Deploy on Render

This repo includes [Dockerfile](Dockerfile) and a [render.yaml](render.yaml) Blueprint for Render:

1. Push the repository to GitHub.
2. In the Render dashboard, select **New + -> Blueprint** and choose your `VaporTrail` repository.
3. When prompted for `STEAM_API_KEY`, paste your Steam Web API key (or leave it blank to deploy in Demo Mode).
4. Render builds the `.NET 10` container, binds to `PORT`, and verifies `/api/health`.

> **Note:** Render's Free tier uses an ephemeral filesystem, so the app starts immediately with the built-in seed and re-syncs the `steamdb` SQLite cache in the background on cold starts. If you upgrade to a paid Render plan with a Persistent Disk mounted at `/var/data`, set `Catalog__DatabasePath=/var/data/steamdb.sqlite`.
