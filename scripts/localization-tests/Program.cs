using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
int passed = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine($"PASS {++passed}: {message}");
}
DbLanguage.Load(Path.Combine(root, "Localization"), "Chinese");
Check(DbLanguage.Monster("Yob") == "蛤蟆" && DbLanguage.Monster("Yob0") == "蛤蟆·0"
    && DbLanguage.NpcLine("Once you go far away from town, hunt HookingCat, RakingCat, Yob, Wolf,").Contains("蛤蟆"),
    "classic Yob frog name and NPC guidance stay consistent");
Check(DbLanguage.Monster("BugBat") == "蝙蝠" && DbLanguage.Monster("BugBatMaggot") == "角蝇"
    && DbLanguage.Monster("WhimperingBee0") == "跳跳蜂·0"
    && DbLanguage.Monster("Keratoid") == "多角虫" && DbLanguage.Monster("ShellNipper") == "盔甲虫"
    && DbLanguage.Monster("WoomaSoldier") == "沃玛战士" && DbLanguage.Monster("WoomaFighter") == "沃玛勇士",
    "classic monster identities are preserved instead of literal English translations");
Check(DbLanguage.Get("Items", "DragonRing") == "龙之戒指" && DbLanguage.Get("Items", "DragonBracelet") == "龙之手镯"
    && DbLanguage.Get("Items", "DragonRobe(M)") == "法神披风（男）" && DbLanguage.Get("Items", "DragonRobe(F)") == "霓裳羽衣"
    && DbLanguage.Get("Items", "FireRobe(M)") == "恶魔长袍（男）" && DbLanguage.Get("Items", "FireRobe(F)") == "恶魔长袍（女）",
    "classic dragon accessories and wizard robes retain their correct identities");
Check(DbLanguage.Get("NpcCaptions", "No") == "否" && DbLanguage.Get("NpcCaptions", "Repair") == "修理"
    && DbLanguage.Get("NpcCaptions", "Bichon Wall") == "比奇城"
    && DbLanguage.Get("Magics", "MeteorShower") == "流星雨" && DbLanguage.Get("Magics", "MeteorStrike") == "流星火雨",
    "later dictionaries do not override menu meanings or merge distinct spells");
Check(DbLanguage.Get("NpcCaptions", "SummonHolyDeva 38") == "召唤月灵 38"
    && DbLanguage.NpcLine("Summons a HolyDeva.") == "召唤一只月灵。"
    && DbLanguage.NpcLine("ShellNipper, Keratoid and etc.") == "盔甲虫、多角虫等。",
    "NPC teaching and hunting guidance use the same identities as the name dictionaries");
using (var spellFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Localization", "DbLanguage.zh-CN.json"))))
{
    foreach (var spell in spellFile.RootElement.GetProperty("Magics").EnumerateObject())
    {
        string bookName = DbLanguage.Get("Items", spell.Name);
        if (bookName != spell.Name && bookName != spell.Value.GetString()) throw new Exception($"Skill book title mismatch: {spell.Name}");
    }
    Check(true, "skill books and the skill panel use the same display name");
}
var item = new ItemInfo { Name = "JudgementMace" };
Check(item.FriendlyName == "裁决之杖" && item.Name == "JudgementMace", "display translation preserves item identifier");
var ore = new ItemInfo { Name = "BlackIronOre1[Quest]" };
Check(ore.RawFriendlyName == "BlackIronOre1", "raw normalization retains upstream ordering for refine logic");
ore.Name = "BlackIronOre1";
Check(ore.RawFriendlyName == "BlackIronOre", "refine ore matching remains independent of translated display");
Check(DbLanguage.Monster("Hen(张三)") == "鸡(张三)", "pet owner identity remains unchanged");
Check(DbLanguage.Monster("[领主]Hen") == "[领主]鸡" && DbLanguage.Monster("[精英]WoomaTaurus") == "[精英]沃玛教主",
    "elite and lord prefixes preserve prefix and translate monster base name");
var weaponInfo = new ItemInfo { Name = "JudgementMace", Type = ItemType.Weapon };
var userWeapon = new UserItem(weaponInfo);
userWeapon.RollFullVersionRareStats(forceRare: true, minAdd: 2, maxAdd: 6);
Check(userWeapon.AddedStats.Values.Values.Sum() >= 2, "RollFullVersionRareStats adds rare stats within bounds when forced");
Check(DbLanguage.Item("JudgementMace (5)") == "裁决之杖 (5)" && DbLanguage.Item("(HP)DrugLarge (10)") == "金创药（大） (10)",
    "stacked floor item translates base name while preserving stack count");
Check(DbLanguage.Get("Maps", "__unknown__") == "__unknown__", "unknown entries retain original text");
Check(DbLanguage.NpcLine("  <<Buy/@buy/red>> <Exit/@exit>  ") == "  <<购买/@buy/red>> <离开/@exit>  ", "NPC captions preserve action, colour, delimiters and whitespace");
Check(DbLanguage.NpcLine("Hello {<$USERNAME>/KHAKI}, my name is {<$NPCNAME>/KHAKI}.") ==
    "你好，{<$USERNAME>/KHAKI}，我叫{<$NPCNAME>/KHAKI}。", "NPC prose translates before expanding player variables");
Check(DbLanguage.Get("Tooltips", "wont") != "wont", "item tooltip dictionary is loaded");
Check(DbLanguage.Get("Ui", "Warrior") == "战士" && DbLanguage.Get("QuestGroups", "BichonProvince") == "比奇省",
    "display-only class and quest grouping dictionaries are loaded");
var originalLog = GameLanguage.ServerLogTextMap.GetLocalization(ServerTextKeys.EnvirStarted);
GameLanguage.LoadServerLanguage(Path.Combine(root, "Server.MirForms", "Localization", "Chinese.json"));
Check(GameLanguage.ServerLogTextMap.GetLocalization(ServerTextKeys.EnvirStarted) == originalLog &&
    GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.EnvirStarted) != originalLog,
    "English operational log templates remain independent from Chinese game messages");
Check(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotSummonPetsOnMap) == "此地图无法召唤宠物。" &&
    GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotRecycleFireWall) == "已有火墙暂时无法回收，请稍后再试。",
    "new summon and firewall player messages load Chinese instead of English fallback");
using (var clientLanguage = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Client", "Localization", "Chinese.json"))))
using (var worldLanguage = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Localization", "DbLanguage.zh-CN.json"))))
{
    var text = clientLanguage.RootElement.GetProperty("Text");
    Check(text.GetProperty("Durability").GetString() == "持久:" && text.GetProperty("MAC").GetString().StartsWith("魔御") &&
        text.GetProperty("LowMC").GetString() == "你的魔法力不足。" &&
        !text.GetProperty("TrapHexagonSkillDescription").GetString().Contains("魔魔法值"),
        "classic durability and magic defence terms are used in tooltips");
    var spells = worldLanguage.RootElement.GetProperty("Magics").EnumerateObject()
        .ToDictionary(entry => entry.Name, entry => entry.Value.GetString(), StringComparer.OrdinalIgnoreCase);
    foreach (var entry in text.EnumerateObject())
    {
        if (!entry.Name.EndsWith("SkillDescription")) continue;
        if (spells.TryGetValue(entry.Name[..^"SkillDescription".Length], out var title))
            if (entry.Value.GetString().Split('\n')[0] != title) throw new Exception($"Skill title mismatch: {entry.Name}");
    }
    Check(!text.GetProperty("FireballSkillDescription").GetString().Contains("ThunderboltSkillDescription"),
        "skill tooltips match displayed skill names and contain no leaked language key");
    Check(text.GetProperty("FatalSwordPassiveSkillDescription").GetString().StartsWith("致命剑术\n")
        && text.GetProperty("UltimateEnhancerSkillDescription").GetString().StartsWith("无极真气\n")
        && text.GetProperty("UseCrossHalfMoon").GetString().Contains("十字半月")
        && text.GetProperty("GivesPoisonAbility").GetString().Contains("施毒能力"),
        "passive skill aliases, toggle hints and poisoning ability keep their actual meanings");
    Check(clientLanguage.RootElement.GetProperty("Enum").GetProperty("BuffType_UltimateEnhancer").GetString() == "无极真气"
        && !text.TryGetProperty("BuffType_UltimateEnhancer", out _),
        "buff display name is translated in the enum table used by the client");
}
var quest = new ClientQuestInfo
{
    Name = "__unknown__", Group = "BichonProvince",
    Description = ["Hello {<$USERNAME>/KHAKI}, my name is {<$NPCNAME>/KHAKI}."],
    TaskDescription = ["Hello {<$USERNAME>/KHAKI}, my name is {<$NPCNAME>/KHAKI}."],
    ReturnDescription = ["Hello {<$USERNAME>/KHAKI}, my name is {<$NPCNAME>/KHAKI}."],
    CompletionDescription = ["Hello {<$USERNAME>/KHAKI}, my name is {<$NPCNAME>/KHAKI}."]
};
using (var stream = new MemoryStream())
{
    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) quest.Save(writer);
    stream.Position = 0;
    var displayed = new ClientQuestInfo(new BinaryReader(stream));
    Check(displayed.Group == "比奇省" && new[] { displayed.Description, displayed.TaskDescription,
        displayed.ReturnDescription, displayed.CompletionDescription }.All(lines => lines[0].StartsWith("你好，")),
        "binary quest packet translates all four narrative sections and the displayed group");
    Check(quest.Group == "BichonProvince" && quest.Description[0].StartsWith("Hello "),
        "display translation leaves original quest data unchanged");
}
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
// Exercise actual server rendering without sockets, databases or Start().
var player = new TextPlayer
{
    Info = new CharacterInfo { Name = "Back张三", Level = 20, Class = MirClass.Warrior },
    CurrentMap = new Map(new MapInfo { FileName = "internal-map-id", Title = "BichonProvince" }),
};
var npcPage = new NPCPage("[@MAIN]");
var segment = new NPCSegment(npcPage, [], [], [], [], []);
var rendered = segment.ParseSay(player, ["<$USERNAME><$LEVEL><$CLASS><$MAP>"])[0];
Check(rendered == "Back张三20战士比奇省", "adjacent NPC variables expand individually after Chinese translation");
Check(segment.ReplaceValue(player, "<$CLASS>") == "Warrior" && segment.ReplaceValue(player, "<$MAP>") == "internal-map-id",
    "script conditions/actions still use raw class and map identifiers");
var message = "Congratulations to <$USERNAME> for reaching Level 20!";
IList<NPCActions> actions = [new NPCActions(ActionType.LocalMessage, message, "System")];
typeof(NPCSegment).GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance, null,
    [typeof(IList<NPCActions>), typeof(PlayerObject)], null)!.Invoke(segment, [actions, player]);
Check(player.Messages.Single() == "恭喜Back张三达到20级！" && actions[0].Params[0] == message,
    "script chat translates its template before variable expansion and preserves stored action parameters");
var progress = (QuestProgressInfo)RuntimeHelpers.GetUninitializedObject(typeof(QuestProgressInfo));
progress.Info = new QuestInfo { GotoMessage = "Return to BichonWall Board." };
progress.TaskList = [];
progress.EndDateTime = DateTime.UtcNow;
progress.UpdateGotoTask();
Check(progress.TaskList.Single() == DbLanguage.NpcLine(progress.Info.GotoMessage) &&
    progress.TaskList[0] != progress.Info.GotoMessage, "quest delivery target translates only the outgoing task text");
DbLanguage.Load(Path.Combine(root, "Localization"), "English");
Check(item.FriendlyName == "JudgementMace" && DbLanguage.Monster("Hen") == "Hen", "English selection restores original display");
Check(segment.ParseSay(player, ["<$USERNAME><$CLASS><$MAP>"])[0] == "Back张三Warriorinternal-map-id",
    "English NPC rendering restores upstream class and map display");

sealed class TextPlayer : PlayerObject
{
    public readonly List<string> Messages = [];
    public override void Enqueue(Packet packet)
    {
        if (packet is ServerPackets.Chat chat) Messages.Add(chat.Message);
    }
}
