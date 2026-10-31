using System.Text.Json;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
int passed = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine($"PASS {++passed}: {message}");
}
DbLanguage.Load(Path.Combine(root, "Localization"), "Chinese");
var item = new ItemInfo { Name = "JudgementMace" };
Check(item.FriendlyName == "裁决之杖" && item.Name == "JudgementMace", "display translation preserves item identifier");
var ore = new ItemInfo { Name = "BlackIronOre1[Quest]" };
Check(ore.RawFriendlyName == "BlackIronOre1", "raw normalization retains upstream ordering for refine logic");
ore.Name = "BlackIronOre1";
Check(ore.RawFriendlyName == "BlackIronOre", "refine ore matching remains independent of translated display");
Check(DbLanguage.Monster("Hen(张三)") == "鸡(张三)", "pet owner identity remains unchanged");
Check(DbLanguage.Item("JudgementMace (5)") == "裁决之杖 (5)" && DbLanguage.Item("(HP)DrugLarge (10)") == "金创药（大） (10)",
    "stacked floor item translates base name while preserving stack count");
Check(DbLanguage.Get("Maps", "__unknown__") == "__unknown__", "unknown entries retain original text");
Check(DbLanguage.NpcLine("  <<Buy/@buy/red>> <Exit/@exit>  ") == "  <<购买/@buy/red>> <离开/@exit>  ", "NPC captions preserve action, colour, delimiters and whitespace");
// Validate the tracked dictionaries so this suite also works in a fresh fork
// checkout, without the local database export or any deployed game resources.
foreach (string category in new[] { "Items", "Maps", "Npcs", "Magics", "Quests" })
{
    string file = category == "Items" ? "ItemsMonsters.zh-CN.json" : "DbLanguage.zh-CN.json";
    using var dictionary = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Localization", file)));
    var entries = dictionary.RootElement.GetProperty(category);
    if (!entries.EnumerateObject().Any()) throw new Exception($"Empty dictionary: {category}");
    foreach (var entry in entries.EnumerateObject())
    {
        string name = entry.Name;
        string translated = DbLanguage.Get(category, name);
        if (translated != entry.Value.GetString()) throw new Exception($"Dictionary not loaded: {category}/{name}");
        if (!string.IsNullOrEmpty(name) && !translated.Any(c => c is >= '\u3400' and <= '\u9fff'))
            throw new Exception($"Untranslated {category}: {name}");
        if (category == "Npcs" && name.Count(c => c == '_') != translated.Count(c => c == '_'))
            throw new Exception($"NPC line separator changed: {name}");
    }
    Check(true, $"{category} dictionary loading and name structure");
}
DbLanguage.Load(Path.Combine(root, "Localization"), "English");
Check(item.FriendlyName == "JudgementMace" && DbLanguage.Monster("Hen") == "Hen", "English selection restores original display");
