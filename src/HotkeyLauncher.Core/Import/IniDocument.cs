namespace HotkeyLauncher.Core.Import;

/// <summary>Minimal INI reader keeping the order of sections and the raw lines of each section.</summary>
internal sealed class IniDocument
{
    private readonly List<string> _sections = [];
    private readonly Dictionary<string, List<string>> _lines = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Sections => _sections;

    public static IniDocument Parse(string content)
    {
        var doc = new IniDocument();
        List<string>? current = null;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var name = trimmed[1..^1].Trim();
                if (!doc._lines.TryGetValue(name, out current))
                {
                    current = [];
                    doc._lines[name] = current;
                    doc._sections.Add(name);
                }
            }
            else if (current != null && trimmed.Length > 0 && !trimmed.StartsWith(';'))
            {
                current.Add(line);
            }
        }

        return doc;
    }

    public IReadOnlyList<string> RawLines(string section) => _lines.TryGetValue(section, out var lines) ? lines : [];

    public string Get(string section, string key)
    {
        foreach (var line in RawLines(section))
        {
            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return line[(eq + 1)..];
        }

        return string.Empty;
    }
}
