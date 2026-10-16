namespace MarelliIawEcuStudio.Cli;

/// <summary>Minimal parser: a command, positional values, <c>--flag</c> and <c>--option value</c>.</summary>
internal sealed class CliArguments
{
    private static readonly HashSet<string> Flags =
    [
        "compat", "no-echo", "no-broadcast", "allow-active", "allow-unverified", "yes", "dtr", "raw", "help",
    ];

    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public string Command { get; private set; } = "help";

    public List<string> Positional { get; } = [];

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        var result = new CliArguments();
        var commandSet = false;
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a is "-h" or "--help")
            {
                result._flags.Add("help");
            }
            else if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var name = a[2..];
                var eq = name.IndexOf('=');
                if (eq > 0)
                {
                    result._options[name[..eq]] = name[(eq + 1)..];
                }
                else if (Flags.Contains(name))
                {
                    result._flags.Add(name);
                }
                else if (name == "simulate" && (i + 1 >= args.Count || args[i + 1].StartsWith('-')))
                {
                    result._options[name] = "";
                }
                else
                {
                    if (i + 1 >= args.Count)
                        throw new CliException($"Option --{name} needs a value.");
                    result._options[name] = args[++i];
                }
            }
            else if (!commandSet)
            {
                result.Command = a;
                commandSet = true;
            }
            else
            {
                result.Positional.Add(a);
            }
        }
        return result;
    }

    public bool Has(string flag) => _flags.Contains(flag);

    public string? Get(string option) => _options.GetValueOrDefault(option);

    public bool IsSet(string option) => _options.ContainsKey(option);

    public int GetInt(string option, int fallback)
    {
        var v = Get(option);
        if (v is null)
            return fallback;
        return int.TryParse(v, out var n) && n >= 0 ? n : throw new CliException($"--{option} must be a non-negative number.");
    }
}

internal sealed class CliException(string message) : Exception(message);
