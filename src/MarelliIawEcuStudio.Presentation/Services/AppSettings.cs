using System.Text.Json;

namespace MarelliIawEcuStudio.Presentation.Services;

/// <summary>User preferences, stored as JSON (replaces the app.config settings of IES2).</summary>
public sealed record AppSettings
{
    /// <summary>Last used connection target id (a port name, or "simulator").</summary>
    public string? Target { get; init; }

    /// <summary>Last selected ECU family, or null for automatic detection.</summary>
    public string? EcuId { get; init; }

    /// <summary>Slower timing for unreliable adapters (IES2 "compatibility mode").</summary>
    public bool Compatibility { get; init; }

    /// <summary>Listen for the ISO broadcast at key-on before initialising.</summary>
    public bool ListenForIsoBroadcast { get; init; } = true;

    /// <summary>Raise DTR on the serial port.</summary>
    public bool DtrEnable { get; init; }

    /// <summary>Allow actuator tests, clear codes and adjustments (always with confirmation).</summary>
    public bool AllowActiveCommands { get; init; } = true;

    public string? Language { get; init; }

    public string LogFolder { get; init; } = DefaultLogFolder;

    public bool SeparateScales { get; init; } = true;

    /// <summary>Visible time window of the live graph, in seconds (0 = everything).</summary>
    public int GraphWindowSeconds { get; init; } = 30;

    public static string DefaultLogFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify),
            "marelli-iaw-ecu-studio", "logs");
}

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>Settings in <c>settings.json</c> next to the user definition folder.</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "marelli-iaw-ecu-studio", "settings.json");

    public AppSettings Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; failing to save them must not break diagnostics.
        }
    }
}

/// <summary>In-memory settings, for tests.</summary>
public sealed class MemorySettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Current { get; private set; } = initial ?? new();

    public AppSettings Load() => Current;

    public void Save(AppSettings settings) => Current = settings;
}
