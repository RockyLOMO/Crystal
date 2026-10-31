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
            foreach (string file in new[] { "DbLanguage.zh-CN.json", "ItemsMonsters.zh-CN.json", "NpcText.zh-CN.json" })
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
        if (original.StartsWith("[领主]", StringComparison.Ordinal) || original.StartsWith("[精英]", StringComparison.Ordinal))
        {
            prefix = original[..4];
            original = original[4..];
        }
        string translated = Get("Monsters", original);
        if (translated != original) return prefix + translated;
        // The server appends a player-owned pet's owner; never translate the player's name.
        int owner = original.LastIndexOf('(');
        return prefix + (owner > 0 && original.EndsWith(')')
            ? Get("Monsters", original[..owner]) + original[owner..] : original);
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
