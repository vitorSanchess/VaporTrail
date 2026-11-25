using VaporTrail.Services;

namespace VaporTrail.Tests;

public class SteamInputParserTests
{
    [Theory]
    [InlineData("76561197960435530", "76561197960435530")]
    [InlineData("  76561198000000001  ", "76561198000000001")]
    [InlineData("https://steamcommunity.com/profiles/76561197960435530", "76561197960435530")]
    [InlineData("https://steamcommunity.com/profiles/76561197960435530/", "76561197960435530")]
    [InlineData("http://www.steamcommunity.com/profiles/76561197960435530/games/?tab=all", "76561197960435530")]
    [InlineData("steamcommunity.com/profiles/76561197960435530", "76561197960435530")]
    public void Parse_ValidSteamId64Inputs_ReturnsSteamId64(string input, string expected)
    {
        var result = SteamInputParser.Parse(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(SteamInputKind.SteamId64, result.Kind);
        Assert.Equal(expected, result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("gabelogannewell", "gabelogannewell")]
    [InlineData("@gordon_freeman-01", "gordon_freeman-01")]
    [InlineData("https://steamcommunity.com/id/gabelogannewell", "gabelogannewell")]
    [InlineData("https://steamcommunity.com/id/gabelogannewell/friends/", "gabelogannewell")]
    [InlineData("steamcommunity.com/id/ValveSoftware", "ValveSoftware")]
    public void Parse_ValidVanityInputs_ReturnsVanityName(string input, string expected)
    {
        var result = SteamInputParser.Parse(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(SteamInputKind.VanityName, result.Kind);
        Assert.Equal(expected, result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("10000000000000000")]
    [InlineData("https://example.com/profiles/76561197960435530")]
    [InlineData("https://steamcommunity.com/profiles/notanumber")]
    [InlineData("https://steamcommunity.com/id/")]
    [InlineData("https://steamcommunity.com/groups/valve")]
    [InlineData("invalid user name!")]
    public void Parse_InvalidInputs_ReturnsFailure(string? input)
    {
        var result = SteamInputParser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}
