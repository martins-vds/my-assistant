namespace FocusAssistant.Infrastructure.Persistence;

/// <summary>
/// Resolves the data directory for file-based persistence.
/// Default: ~/.focus-assistant/data/
/// Override: FOCUS_ASSISTANT_DATA_DIR environment variable.
/// </summary>
public static class DataDirectory
{
    private static string? _override;

    public static string BasePath
    {
        get
        {
            if (!string.IsNullOrEmpty(_override))
                return _override;

            var envPath = Environment.GetEnvironmentVariable("FOCUS_ASSISTANT_DATA_DIR");
            if (!string.IsNullOrEmpty(envPath))
                return envPath;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".focus-assistant",
                "data");
        }
    }

    public static string GetFilePath(string fileName)
        => Path.Combine(BasePath, fileName);

    /// <summary>
    /// Override the base path (for testing).
    /// </summary>
    public static void SetBasePath(string path)
    {
        _override = path;
    }

    /// <summary>
    /// Reset to default path resolution.
    /// </summary>
    public static void ResetBasePath()
    {
        _override = null;
    }
}
