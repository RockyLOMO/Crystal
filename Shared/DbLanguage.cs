using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Display-only database names. Internal database/script identifiers stay unchanged.</summary>
public static class DbLanguage
{
    private static Dictionary<string, Dictionary<string, string>> names = new(StringComparer.Ordinal);
    private static readonly Regex NpcMenu = new(@"<(?<caption>[^<>]*?)/(?<action>@[^<>]*?)>");
    public static bool Chinese { get; private set; }

    public static void Load(string directory, string language)
    {
        var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Chinese = string.Equals(language, "Chinese", StringComparison.OrdinalIgnoreCase);
        if (Chinese)
        {
            foreach (string file in new[] { "DbLanguage.zh-CN.json", "ItemsMonsters.zh-CN.json", "NpcText.zh-CN.json", "NpcNarrative.zh-CN.json" })
            {
                string path = Path.Combine(directory, file);
                if (!File.Exists(path)) continue;
                var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path));
                if (data == null) continue;
                foreach (var category in data)
                {
                    if (!loaded.TryGetValue(category.Key, out var entries))
                        loaded[category.Key] = entries = new(StringComparer.Ordinal);
                    foreach (var entry in category.Value)
                        if (!string.IsNullOrEmpty(entry.Value)) entries[entry.Key] = entry.Value;
                }
            }
        }
        names = loaded;
    }

    public static string Get(string category, string original)
    {
        if (original == null) return null;
        return names.TryGetValue(category, out var entries) && entries.TryGetValue(original, out string translated)
            ? translated : original;
    }

    /// <summary>
    /// Floor item names may carry the server's stack suffix, e.g. "Name (5)". The count is
    /// kept and only the item name is translated.
    /// </summary>
    public static string Item(string original)
    {
        string translated = Get("Items", original);
        if (translated != original || original == null || !original.EndsWith(')')) return translated;
        int count = original.LastIndexOf(" (", StringComparison.Ordinal);
        if (count <= 0) return original;
        for (int i = count + 2; i < original.Length - 1; i++)
            if (!char.IsDigit(original[i])) return original;
        return count + 3 <= original.Length - 1
            ? Get("Items", original[..count]) + original[count..] : original;
    }

    public static string Monster(string original)
    {
        if (original == null) return null;
        string prefix = string.Empty;
        if (original.StartsWith('[') && original.IndexOf(']') is int closeIdx && closeIdx > 0)
        {
            prefix = original[..(closeIdx + 1)];
            original = original[(closeIdx + 1)..];
            if (original.StartsWith("血煞", StringComparison.Ordinal))
            {
                prefix += "血煞";
                original = original[2..];
            }
        }
        // The server appends a player-owned pet's owner; never translate the player's name.
        int owner = original.LastIndexOf('(');
        return prefix + (owner > 0 && original.EndsWith(')')
            ? Get("Monsters", original[..owner]) + original[owner..] : Get("Monsters", original));
    }

    public static string NpcLine(string original)
    {
        if (!Chinese || original == null) return original;
        string line = Get("NpcLines", original);
        if (line == original)
        {
            string trimmed = original.Trim();
            string translated = Get("NpcLines", trimmed);
            if (translated != trimmed)
            {
                int start = original.IndexOf(trimmed, StringComparison.Ordinal);
                line = original[..start] + translated + original[(start + trimmed.Length)..];
            }
        }
        return NpcMenu.Replace(line, match =>
            "<" + Get("NpcCaptions", match.Groups["caption"].Value) + "/" + match.Groups["action"].Value + ">");
    }
}
