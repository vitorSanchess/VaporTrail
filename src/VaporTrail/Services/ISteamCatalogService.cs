using VaporTrail.Models;

namespace VaporTrail.Services;

public interface ISteamCatalogService
{
    SteamCatalogStatus GetStatus();

    Task<IReadOnlyDictionary<int, SteamCatalogGame>> GetGamesByAppIdsAsync(
        IEnumerable<int> appIds,
        CancellationToken cancellationToken = default);

    Task<SteamCatalogStatus> SyncFromRemoteAsync(
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<int> SyncFromStreamAsync(
        Stream stream,
        bool isGzip,
        CancellationToken cancellationToken = default);
}
