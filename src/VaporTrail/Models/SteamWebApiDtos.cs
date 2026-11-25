using System.Text.Json.Serialization;

namespace VaporTrail.Models;

public sealed class ResolveVanityUrlEnvelope
{
    [JsonPropertyName("response")]
    public ResolveVanityUrlBody? Response { get; set; }
}

public sealed class ResolveVanityUrlBody
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }

    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public sealed class PlayerSummariesEnvelope
{
    [JsonPropertyName("response")]
    public PlayerSummariesBody? Response { get; set; }
}

public sealed class PlayerSummariesBody
{
    [JsonPropertyName("players")]
    public List<SteamPlayerDto>? Players { get; set; }
}

public sealed class SteamPlayerDto
{
    [JsonPropertyName("steamid")]
    public string SteamId { get; set; } = string.Empty;

    [JsonPropertyName("communityvisibilitystate")]
    public int CommunityVisibilityState { get; set; }

    [JsonPropertyName("personaname")]
    public string PersonaName { get; set; } = string.Empty;

    [JsonPropertyName("realname")]
    public string? RealName { get; set; }

    [JsonPropertyName("profileurl")]
    public string ProfileUrl { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("avatarmedium")]
    public string? AvatarMedium { get; set; }

    [JsonPropertyName("avatarfull")]
    public string? AvatarFull { get; set; }

    [JsonPropertyName("personastate")]
    public int PersonaState { get; set; }

    [JsonPropertyName("timecreated")]
    public long? TimeCreated { get; set; }

    [JsonPropertyName("lastlogoff")]
    public long? LastLogoff { get; set; }

    [JsonPropertyName("loccountrycode")]
    public string? LocCountryCode { get; set; }

    [JsonPropertyName("gameextrainfo")]
    public string? GameExtraInfo { get; set; }

    [JsonPropertyName("gameid")]
    public string? GameId { get; set; }
}

public sealed class SteamLevelEnvelope
{
    [JsonPropertyName("response")]
    public SteamLevelBody? Response { get; set; }
}

public sealed class SteamLevelBody
{
    [JsonPropertyName("player_level")]
    public int? PlayerLevel { get; set; }
}

public sealed class RecentlyPlayedGamesEnvelope
{
    [JsonPropertyName("response")]
    public RecentlyPlayedGamesBody? Response { get; set; }
}

public sealed class RecentlyPlayedGamesBody
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamGameDto>? Games { get; set; }
}

public sealed class OwnedGamesEnvelope
{
    [JsonPropertyName("response")]
    public OwnedGamesBody? Response { get; set; }
}

public sealed class OwnedGamesBody
{
    [JsonPropertyName("game_count")]
    public int? GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamGameDto>? Games { get; set; }
}

public sealed class SteamGameDto
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }

    [JsonPropertyName("playtime_2weeks")]
    public int? Playtime2Weeks { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string? ImgIconUrl { get; set; }
}
