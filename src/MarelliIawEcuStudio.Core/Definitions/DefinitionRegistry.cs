namespace MarelliIawEcuStudio.Core.Definitions;

/// <summary>
/// The set of ECU definitions available to the application: the built-in
/// ones plus any found in user folders. A user file with the same id as a
/// built-in definition replaces it, so corrections need no recompilation.
/// </summary>
public sealed class DefinitionRegistry
{
    private readonly Dictionary<string, EcuDefinition> _byId;

    public DefinitionRegistry(IEnumerable<EcuDefinition> definitions, IReadOnlyList<DefinitionIssue>? issues = null)
    {
        _byId = new Dictionary<string, EcuDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in definitions)
            _byId[d.Id] = d;
        Issues = issues ?? [];
    }

    /// <summary>Problems found in user definition files (they were skipped).</summary>
    public IReadOnlyList<DefinitionIssue> Issues { get; }

    public IReadOnlyCollection<EcuDefinition> All => _byId.Values;

    /// <summary>Families that take part in auto detection, in detection order.</summary>
    public IReadOnlyList<EcuDefinition> AutoDetectOrder =>
        [.. _byId.Values.Where(d => d.Detection.AutoDetect).OrderBy(d => d.Detection.Order).ThenBy(d => d.Id, StringComparer.Ordinal)];

    public EcuDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    public EcuDefinition Get(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"Unknown ECU definition '{id}'. Known: {string.Join(", ", _byId.Keys.Order())}");

    /// <summary>Built-in definitions only.</summary>
    public static DefinitionRegistry BuiltIn() => new(DefinitionLoader.LoadBuiltIn());

    /// <summary>Built-in definitions overridden by files in <paramref name="userDirectories"/>.</summary>
    public static DefinitionRegistry Load(params string[] userDirectories)
    {
        var issues = new List<DefinitionIssue>();
        var all = DefinitionLoader.LoadBuiltIn().ToList();
        foreach (var dir in userDirectories)
            all.AddRange(DefinitionLoader.LoadDirectory(dir, issues));
        return new DefinitionRegistry(all, issues);
    }

    /// <summary>Default per-user folder for extra definitions.</summary>
    public static string DefaultUserDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "marelli-iaw-ecu-studio", "definitions");
}
