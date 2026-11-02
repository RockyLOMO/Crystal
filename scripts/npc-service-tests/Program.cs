using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using S = ServerPackets;

var directory = Path.Combine(Path.GetTempPath(), "ai", DateTime.Now.ToString("yyyyMMdd"), "mir2-npc-service-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
Directory.SetCurrentDirectory(directory);
var env = Envir.Main;
var npc = new NPCObject(new NPCInfo { FileName = "fixture-no-script", Rate = 100 });
var script = NPCScript.Get(npc.ScriptID);
var map = new Map(new MapInfo());
map.NPCs.Add(npc);
var player = new TestPlayer
{
    Info = new CharacterInfo(), Account = new AccountInfo { Gold = 100000 },
    CurrentMap = map, NPCObjectID = npc.ObjectID, NPCScriptID = npc.ScriptID,
    NPCPage = new NPCPage(NPCScript.SRepairKey), Stats = new Stats()
};
player.Info.Mount = new MountInfo(player);
int passed = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine($"PASS {++passed}: {name}"); }
var info = new ItemInfo { Index = 1, Type = ItemType.Weapon, Durability = 10000, Price = 10000 };
var item = new UserItem(info) { UniqueID = 55, MaxDura = 10000, CurrentDura = 4000 };
player.Info.Equipment[0] = item;
var gold = player.Account.Gold;
var price = item.RepairPrice() * 3;
player.RepairItem(item.UniqueID, special: true);
Check(item.CurrentDura == 10000 && item.MaxDura == 10000 && ReferenceEquals(player.Info.Equipment[0], item), "special repair restores worn item without unequipping or reducing maximum durability");
Check(player.Account.Gold == gold - price && player.Packets.OfType<S.ItemRepaired>().Any(p=>p.UniqueID == item.UniqueID), "special repair charges existing triple price and sends the existing equipped-item update");
void Damaged() { item.CurrentDura = 4000; item.MaxDura = 10000; player.Packets.Clear(); player.Account.Gold = 100000; info.Bind = BindMode.None; script.Types.Clear(); player.NPCPage = new NPCPage(NPCScript.SRepairKey); }
Damaged(); player.Account.Gold = 0; player.RepairItem(55, true);
Check(item.CurrentDura == 4000 && !player.Packets.OfType<S.ItemRepaired>().Any(), "insufficient gold does not repair worn item");
Damaged(); info.Bind = BindMode.NoSRepair; player.RepairItem(55, true);
Check(item.CurrentDura == 4000 && player.Account.Gold == 100000, "NoSRepair restriction remains enforced");
Damaged(); info.Bind = BindMode.DontRepair; player.RepairItem(55, true);
Check(item.CurrentDura == 4000 && player.Account.Gold == 100000, "DontRepair restriction remains enforced");
Damaged(); script.Types.Add(ItemType.Helmet); player.RepairItem(55, true);
Check(item.CurrentDura == 4000 && player.Account.Gold == 100000, "merchant item-type restrictions remain enforced");
Damaged(); player.NPCPage = new NPCPage("[@MAIN]"); player.RepairItem(55, true);
Check(item.CurrentDura == 4000 && player.Account.Gold == 100000, "repair outside the authorized NPC service page is rejected");
Damaged(); player.RepairItem(999, true);
Check(item.CurrentDura == 4000 && player.Account.Gold == 100000, "unknown item ID does not repair or charge");
Damaged(); player.CurrentLocation = new System.Drawing.Point(1000,1000); player.RepairItem(55, true);
Check(item.CurrentDura == 4000 && player.Account.Gold == 100000, "remote NPC repair remains rejected");
player.CurrentLocation = default;
Damaged(); player.NPCPage = new NPCPage(NPCScript.RepairKey); player.RepairItem(55);
Check(item.MaxDura == 9800 && item.CurrentDura == 9800, "manual ordinary repair retains upstream durability-loss rules");
Damaged(); player.Info.Equipment[0] = null; player.Info.Inventory[0] = item; player.RepairItem(55, true);
Check(item.CurrentDura == 10000 && item.MaxDura == 10000, "existing inventory special repair still works");
Damaged(); player.Info.Inventory[0] = null; player.Info.Equipment[0] = item;
info.Stats[Stat.MaxDC] = 5; item.CurrentDura = 0; player.RefreshStats();
var brokenAttack = player.Stats[Stat.MaxDC];
player.RepairItem(55, true);
Check(player.Stats[Stat.MaxDC] == brokenAttack + 5, "repairing broken equipped gear immediately restores its server-side stats");
Console.WriteLine($"All {passed} NPC service checks passed. No game server started.");

sealed class TestPlayer : PlayerObject
{
    public readonly List<Packet> Packets = [];
    public override void Enqueue(Packet packet) => Packets.Add(packet);
}
