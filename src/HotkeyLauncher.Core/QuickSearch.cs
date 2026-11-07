using System.Globalization;
using System.Text;
using HotkeyLauncher.Core.Models;

namespace HotkeyLauncher.Core;

/// <summary>
/// Search used by the quick launcher and the tray menu: tolerant to case and accents, favors the
/// beginning of the name, words and initials ("vsc" finds "Visual Studio Code").
/// </summary>
public static class QuickSearch
{
    public static IReadOnlyList<HotkeyEntry> Search(IEnumerable<HotkeyEntry> entries, string? query, int max)
    {
        var normalizedQuery = Normalize(query ?? string.Empty).Trim();
        if (normalizedQuery.Length == 0)
        {
            // No text yet: the most recently / most often used shortcuts
            return [.. entries
                .OrderByDescending(e => e.LastLaunchedUtc ?? DateTime.MinValue)
                .ThenByDescending(e => e.LaunchCount)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(max)];
        }

        return [.. entries
            .Select(e => (entry: e, score: Score(e, normalizedQuery)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.entry.LaunchCount)
            .ThenBy(x => x.entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(max)
            .Select(x => x.entry)];
    }

    internal static int Score(HotkeyEntry entry, string query)
    {
        var name = Normalize(entry.Name);
        if (name == query) return 1000;
        if (name.StartsWith(query, StringComparison.Ordinal)) return 800;

        var words = name.Split([' ', '-', '_', '.', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(query, StringComparison.Ordinal))) return 600;

        var initials = new string([.. words.Select(w => w[0])]);
        if (initials.StartsWith(query, StringComparison.Ordinal)) return 500;

        if (name.Contains(query, StringComparison.Ordinal)) return 400;
        if (Normalize(entry.Group).Contains(query, StringComparison.Ordinal)) return 200;
        if (Normalize(entry.Target).Contains(query, StringComparison.Ordinal)) return 100;
        return 0;
    }

    /// <summary>Lower case without accents: "Éditeur" → "editeur".</summary>
    internal static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
