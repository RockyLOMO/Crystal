using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;
using Server.SoloExtension;

// No account data or game server is loaded. Profile writes stay under this unique fixture.
string originalDirectory = Directory.GetCurrentDirectory();
string fixture = Path.Combine(Path.GetTempPath(), "ai", DateTime.Now.ToString("yyyyMMdd"), "mir2-tome-pickup", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
int passed = 0;
void Check(bool ok, string description)
{
    if (!ok) throw new Exception(description);
    Console.WriteLine($"PASS {++passed}: {description}");
}

try
{
    Directory.SetCurrentDirectory(fixture);
    var map = new Map(new MapInfo { Index = 1, Title = "TestField" }) { Width = 9, Height = 9, Cells = new Cell[9, 9] };
    for (int x = 0; x < 9; x++)
        for (int y = 0; y < 9; y++) map.Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
    var player = CreatePlayer("TomePickupA", map);
    var other = CreatePlayer("TomePickupB", map);
    var profile = SoloPlayerProfile.GetOrLoad(player.Name);
    Check(profile.AutoPickRangeMode == 2, "new character defaults to smart range pickup");
    Check(SoloPlayerProfile.GetOrLoad(other.Name).AutoPickRangeMode == 2, "second character has its own default");
    Check(!HunterTomeModule.TryHandleNpcCommand(player, "@NOT_A_TOME_COMMAND"), "unrelated NPC command remains unhandled");
    foreach (int expected in new[] { 1, 0, 2 })
    {
        Check(HunterTomeModule.TryHandleNpcCommand(player, "[@Tome_TogglePick]"), "tome NPC command is handled");
        Check(profile.AutoPickRangeMode == expected, $"tome cycles to mode {expected}");
        string path = SoloPlayerProfile.GetProfilePath(player.Name);
        Check(Path.GetFullPath(path).StartsWith(fixture + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "profile path stays inside isolated TEMP fixture");
        var disk = JsonSerializer.Deserialize<SoloPlayerProfile>(File.ReadAllText(path));
        Check(disk?.AutoPickRangeMode == expected, $"toggle persists mode {expected} immediately");
        Check(SoloPlayerProfile.GetOrLoad(other.Name).AutoPickRangeMode == 2, "toggle leaves the other character unchanged");
    }
    Check(player.Packets.OfType<ServerPackets.NPCResponse>().Count() == 3, "each toggle refreshes the real tome NPC dialog");
    profile.AutoPickRangeMode = 0;
    profile.Save();
    var cache = (ConcurrentDictionary<string, SoloPlayerProfile>)typeof(SoloPlayerProfile)
        .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    cache.TryRemove(player.Name, out _);
    profile = SoloPlayerProfile.GetOrLoad(player.Name);
    Check(profile.AutoPickRangeMode == 0, "disabled mode survives cache eviction and disk reload");

    UserItem Item(ItemType type, string name, int requiredLevel = 1) => new(new ItemInfo
    {
        Name = name, Type = type, StackSize = 1, RequiredType = RequiredType.Level,
        RequiredAmount = (byte)requiredLevel, Weight = 1, Grade = ItemGrade.Common
    }) { UniqueID = (ulong)Random.Shared.NextInt64(1, long.MaxValue) };
    ItemObject Drop(UserItem item, int dx, int dy)
    {
        var drop = new ItemObject(player, item, new Point(4 + dx, 4 + dy));
        drop.Node = Envir.Main.Objects.AddLast(drop);
        map.AddObject(drop);
        return drop;
    }
    ItemObject Gold(uint value, int dx, int dy)
    {
        var drop = new ItemObject(player, value, new Point(4 + dx, 4 + dy));
        drop.Node = Envir.Main.Objects.AddLast(drop);
        map.AddObject(drop);
        return drop;
    }
    bool OnGround(ItemObject item) => map.GetCell(item.CurrentLocation).Objects.Contains(item);
    var feet = Drop(Item(ItemType.Meat, "TestMeat"), 0, 0);
    var neighbor = Drop(Item(ItemType.Potion, "TestPotion"), 1, 0);
    var gold = Gold(123, -1, 0);
    HunterTomeModule.ProcessRangeAutoPickup(player);
    Check(OnGround(feet) && OnGround(neighbor) && OnGround(gold), "disabled mode leaves foot, neighboring item and gold untouched");
    Check(player.Info.Inventory.All(i => i == null) && player.Account.Gold == 0, "disabled mode gains neither inventory items nor gold");
    profile.AutoPickRangeMode = 2;
    HunterTomeModule.ProcessRangeAutoPickup(player);
    Check(OnGround(feet), "smart mode filters meat even under the character's feet");
    Check(!OnGround(neighbor) && player.Info.Inventory.Contains(neighbor.Item), "smart mode gains a real nearby potion into the real inventory");
    Check(!OnGround(gold) && player.Account.Gold == 123, "smart mode gains nearby gold");
    Check(player.Packets.OfType<ServerPackets.GainedItem>().Any(p => p.Item.UniqueID == neighbor.Item.UniqueID), "item pickup emits the real gained-item packet");
    Check(player.Packets.OfType<ServerPackets.GainedGold>().Any(p => p.Gold == 123), "gold pickup emits the real gained-gold packet");

    var cloth = Drop(Item(ItemType.Armour, "BaseDress(M)"), 0, 1);
    var rareCloth = Item(ItemType.Armour, "BaseDress(F)"); rareCloth.AddedStats[Stat.MaxAC] = 3;
    var rareDrop = Drop(rareCloth, 1, 1);
    var lowOre = Item(ItemType.Ore, "TestOre"); lowOre.CurrentDura = 9999;
    var oreDrop = Drop(lowOre, -1, -1);
    HunterTomeModule.ProcessRangeAutoPickup(player);
    Check(OnGround(cloth), "smart mode filters plain low-level cloth");
    Check(!OnGround(rareDrop) && player.Info.Inventory.Contains(rareCloth), "smart mode retains rare equipment pickup");
    Check(OnGround(oreDrop), "smart mode filters ore below purity ten");
    profile.AutoPickRangeMode = 1;
    HunterTomeModule.ProcessRangeAutoPickup(player);
    Check(!OnGround(feet) && !OnGround(cloth) && !OnGround(oreDrop), "all mode gains meat, ordinary cloth and low-purity ore");
    Check(player.Info.Inventory.Contains(feet.Item) && player.Info.Inventory.Contains(cloth.Item) && player.Info.Inventory.Contains(oreDrop.Item), "all-mode pickups reach the real inventory");

    var owned = Drop(Item(ItemType.Book, "ReservedBook"), 0, -1); owned.Owner = other;
    var outside = Drop(Item(ItemType.Book, "OutOfRangeBook"), 3, 0);
    var heavyItem = Item(ItemType.Book, "OverweightBook"); heavyItem.Info.Weight = 255;
    var heavy = Drop(heavyItem, 0, 1);
    player.Stats[Stat.BagWeight] = player.CurrentBagWeight + 1;
    HunterTomeModule.ProcessRangeAutoPickup(player);
    Check(OnGround(owned), "all mode respects another character's item ownership");
    Check(OnGround(outside), "all mode leaves items outside the existing range untouched");
    Check(OnGround(heavy), "all mode preserves the bag weight limit");
    profile.AutoPickRangeMode = 0;
    map.Cells = null;
    HunterTomeModule.ProcessRangeAutoPickup(player);
    Check(true, "disabled mode returns before accessing map cells");
    Console.WriteLine($"{passed} checks passed. Fixture: {fixture}");
}
finally { Directory.SetCurrentDirectory(originalDirectory); }

static TestPlayer CreatePlayer(string name, Map map)
{
    // The real GainItem path requires CheckItem metadata state. This offline shell
    // has no socket, receive loop, account login or connection processing.
    var connection = (MirConnection)RuntimeHelpers.GetUninitializedObject(typeof(MirConnection));
    connection.Observers = [];
    connection.SentItemInfo = [];
    return new TestPlayer
    {
        Info = new CharacterInfo { Name = name, CurrentLocation = new Point(4, 4) },
        Account = new AccountInfo(), CurrentMap = map, Connection = connection,
        Stats = new Stats { [Stat.BagWeight] = 10000 }
    };
}

sealed class TestPlayer : PlayerObject
{
    public List<Packet> Packets { get; } = [];
    public override void Enqueue(Packet packet) => Packets.Add(packet);
}
