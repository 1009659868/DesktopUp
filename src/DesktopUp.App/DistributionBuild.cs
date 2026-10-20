using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

// Build-channel identity only; never changes layout, persistence schema or user authoring.
internal static class DistributionBuild
{
    public static PosterDefinition ApplyCreationDefaults(PosterDefinition seed) => CreationDefaults?.ApplyToNew(seed) ?? seed;
#if DESKTOPUP_PREVIEW
    public const bool IsInstallable = true;
    public const string ApplicationId = "DesktopUp.V3.Preview";
    public const string DisplayName = "DesktopUp Preview";
    public const string TrayName = "DesktopUp Preview";
    public const string TraySuffix = " · 预览版";
    public static string? DefaultDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "Preview");
    private static readonly Lazy<(PosterCreationDefaults? Value, string? Error)> LoadedDefaults = new(ReadDefaults);
    public static PosterCreationDefaults? CreationDefaults => LoadedDefaults.Value.Value;
    public static string? CreationDefaultsError => LoadedDefaults.Value.Error;

    private static (PosterCreationDefaults?, string?) ReadDefaults()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "config", "preview-defaults.json");
        try
        {
            using var reader = File.OpenText(path);
            var buffer = new char[PosterCreationDefaults.MaximumJsonLength + 1];
            int count = reader.ReadBlock(buffer, 0, buffer.Length);
            return (PosterCreationDefaults.Parse(new string(buffer, 0, count)), null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            System.Text.Json.JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        {
            // Missing/invalid package data must not overwrite user state or prevent recovery/startup.
            return (null, error.Message);
        }
    }
#else
    public const bool IsInstallable = false;
    public const string ApplicationId = "DesktopUp.V3";
    public const string DisplayName = "DesktopUp";
    public const string TrayName = "DesktopUp V3";
    public const string TraySuffix = "";
    public static string? DefaultDataRoot => null;
    public static PosterCreationDefaults? CreationDefaults => null;
    public static string? CreationDefaultsError => null;
#endif
}
