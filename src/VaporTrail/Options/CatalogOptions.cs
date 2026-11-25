namespace VaporTrail.Options;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>
    /// Path to the local SQLite database built from the steamdb dump.
    /// Relative paths resolve against ContentRootPath.
    /// </summary>
    public string DatabasePath { get; set; } = "App_Data/steamdb.sqlite";

    /// <summary>
    /// Nightly gzipped JSON dump from https://github.com/leinstay/steamdb.
    /// </summary>
    public string DumpUrl { get; set; } = "https://github.com/leinstay/steamdb/releases/latest/download/steamdb.min.json.gz";

    /// <summary>
    /// Minimum hours before an automatic re-sync is triggered.
    /// </summary>
    public int RefreshIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether to sync the local SQLite catalog in the background on startup.
    /// </summary>
    public bool AutoSyncOnStartup { get; set; } = true;
}
