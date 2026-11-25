# VaporTrail

A lightweight ASP.NET Core Minimal API (.NET 10) Steam profile viewer that pulls player summaries, Steam level, and playtime from the official Steam Web API and displays them in a responsive **Light & Dark Neumorphism** web interface.

## Features

- **Flexible Steam Input Parsing**: Accepts 17-digit 64-bit SteamIDs (`76561197960435530`), custom vanity names (`gabelogannewell`), or pasted `steamcommunity.com/profiles/...` and `steamcommunity.com/id/...` URLs.
- **Parallel Steam Web API Fetching**: Resolves vanity names via `ISteamUser/ResolveVanityURL/v1`, then fetches `GetPlayerSummaries`, `GetSteamLevel`, `GetRecentlyPlayedGames`, and `GetOwnedGames` concurrently.
- **Graceful Private Profile Handling**: Detects private profiles or hidden game libraries and cleanly displays available identity info with a clear notice.
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

- `GET /api/status` — Current mode (`demo` or `live`), cache TTL, and sample lookup hints.
- `GET /api/steam/profile?query=<id-or-vanity-or-url>&refresh=<true|false>` — Fetches and aggregates Steam profile data.
- `GET /api/health` — Basic health check.
