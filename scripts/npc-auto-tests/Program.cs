using Client.MirScenes;
using System.Drawing;

int passed = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine($"PASS {++passed}: {label}"); }
UserItem Ordinary(ulong id = 1) => new(new ItemInfo { Type = ItemType.Weapon, Grade = ItemGrade.Common, Price = 1000, Durability = 1000 }) { UniqueID = id, CurrentDura = 400, MaxDura = 1000 };
Check(NpcAutoController.IsOrdinaryEquipment(Ordinary()), "plain white equipment eligible");
foreach (var change in new Action<UserItem>[]
{
    i => i.AddedStats[Stat.MaxDC] = 1,
    i => i.Info.Grade = ItemGrade.Rare,
    i => i.Info.Type = ItemType.Quest,
    i => i.Info.Name = "WoodenSword[Quest]",
    i => i.Info.Type = ItemType.Book,
    i => i.Info.Type = ItemType.Potion,
    i => i.Info.Type = ItemType.CraftingMaterial,
    i => i.Info.Bind = BindMode.DontSell,
    i => i.Info.Set = ItemSet.Spirit,
    i => i.Info.Unique = SpecialItemMode.Teleport,
    i => i.Info.NeedIdentify = true,
    i => i.SoulBoundId = 2,
    i => i.WeddingRing = 2,
    i => i.RefineAdded = 1,
    i => i.GemCount = 1,
    i => i.Awake.Type = AwakeType.DC,
    i => i.Slots = new[] { Ordinary(2) },
    i => i.RentalInformation = new RentalInformation(),
    i => i.ExpireInfo = new ExpireInfo(),
    i => i.SealedInfo = new SealedInfo(),
    i => i.GMMade = true,
    i => i.Cursed = true,
})
{
    var i = Ordinary(); change(i); Check(!NpcAutoController.IsOrdinaryEquipment(i), "valuable or non-equipment excluded");
}
foreach (string name in new[] { "BaseDress(M)", "BaseDress(F)" })
{
    var cloth = Ordinary(); cloth.Info.Name = name; cloth.Info.Type = ItemType.Armour; cloth.Info.StartItem = true;
    Check(NpcAutoController.IsOrdinaryEquipment(cloth), "ordinary inventory cloth remains eligible despite starter-template flag: " + name);
    cloth.AddedStats[Stat.MaxAC] = 1;
    Check(!NpcAutoController.IsOrdinaryEquipment(cloth), "enhanced cloth still protected: " + name);
}
var drapery = new Fake { Bag = new[] { Ordinary() } };
var redirected = new NpcAutoController(drapery);
redirected.OnPage(Array.Empty<string>(), 0);
redirected.OnPage(new[] { "<View/@BuySell> Store.", "<Repair/@Repair> Drapery pieces." }, 10);
redirected.Process(200);
Check(drapery.Pages.SequenceEqual(new[] { "@BUYSELL" }), "redirected drapery dialogue follows its final service page");
var maleCloth = Ordinary(31); maleCloth.Info.Name = "BaseDress(M)"; maleCloth.Info.Type = ItemType.Armour; maleCloth.Info.StartItem = true;
var femaleCloth = Ordinary(32); femaleCloth.Info.Name = "BaseDress(F)"; femaleCloth.Info.Type = ItemType.Armour; femaleCloth.Info.StartItem = true;
drapery.Bag = new[] { maleCloth, femaleCloth };
redirected.OnPage(new[] { "<Back/@main>" }, 210); redirected.OnSellPanel(210); redirected.Process(400);
redirected.OnSold(31, true, 500); drapery.Bag = new[] { femaleCloth }; redirected.Process(700);
Check(drapery.Sales.SequenceEqual(new ulong[] { 31, 32 }), "drapery automatically sells ordinary male and female cloth sequentially");
var multiPage = new Fake { Bag = new[] { Ordinary() } }; var multipageController = new NpcAutoController(multiPage);
multipageController.OnPage(new[] { "Opening page <Close/@Exit>" }, 0);
multipageController.OnPage(new[] { "Final page <Sell/@Sell>" }, 10); multipageController.Process(200);
Check(multiPage.Pages.SequenceEqual(new[] { "@SELL" }), "same-NPC redirect before the first action does not cancel automation");
// Exercise the server's real GOTO response sequence, rather than assuming every
// merchant sends exactly one page. The fixture does not start a game server.
string originalDirectory = Directory.GetCurrentDirectory();
string dialogueFixture = Path.Combine(TestWorkspace.RunDirectory, "npc-script");
Directory.CreateDirectory(Path.Combine(dialogueFixture, "Envir", "NPCs"));
try
{
    Directory.SetCurrentDirectory(dialogueFixture);
    File.WriteAllLines(Path.Combine("Envir", "NPCs", "drapery.txt"),
        ["[@MAIN]", "#IF", "CHECKPKPOINT > 199", "#SAY", "No service", "#ELSEACT", "GOTO @Main-1",
         "[@Main-1]", "#SAY", "<View/@BuySell> Store.", "<Repair/@Repair> Drapery pieces.", "[Types]", "2", "4", "9", "10"]);
    var npc = new Server.MirObjects.NPCObject(new Server.MirDatabase.NPCInfo { FileName = "drapery", Rate = 100 });
    var script = Server.MirObjects.NPCScript.Get(npc.ScriptID);
    var actor = new DialoguePlayer { Info = new Server.MirDatabase.CharacterInfo(), Account = new Server.MirDatabase.AccountInfo() };
    script.Call(actor, npc.ObjectID, Server.MirObjects.NPCScript.MainKey);
    foreach (var action in actor.ActionList.ToArray()) actor.Process(action);
    var pages = actor.Packets.OfType<ServerPackets.NPCResponse>().ToArray();
    Check(pages.Length == 2 && pages[0].Page.Count == 0 && pages[1].Page.Any(s => s.Contains("/@BuySell")),
        "real drapery GOTO emits an empty response followed by the merchant page");
    var live = new Fake { Bag = new[] { femaleCloth } }; var controller = new NpcAutoController(live);
    for (int i = 0; i < pages.Length; i++) { live.IsAvailable = pages[i].Page.Count > 0; controller.OnPage(pages[i].Page, i * 10); }
    controller.Process(200);
    Check(live.Pages.SequenceEqual(new[] { "@BUYSELL" }) && script.Types.Contains(ItemType.Armour),
        "real merchant dialogue and inventory cloth enter the supported armour sell service");
    npc.Despawn(); Server.MirEnvir.Envir.Main.NPCs.Remove(npc); Server.MirEnvir.Envir.Main.Scripts.Remove(npc.ScriptID);
}
finally { Directory.SetCurrentDirectory(originalDirectory); }
var fake = new Fake { Bag = new[] { Ordinary(), Ordinary(2) }, Worn = new[] { Ordinary(9) } };
var c = new NpcAutoController(fake);
c.OnPage(new[] { "<出售/@Sell>", "<<特殊修理/@SRepair>>" }, 0);
c.Process(200); Check(fake.Pages.SequenceEqual(new[] { "@SELL" }), "follows advertised sell action");
c.OnPage(new[] { "<返回/@Main>" }, 210); c.OnSellPanel(210);
c.Process(400); c.Process(800); Check(fake.Sales.SequenceEqual(new ulong[] { 1 }), "only one request awaits sell ack");
c.OnSold(1, true, 900); fake.Bag = new[] { Ordinary(2) }; c.Process(1100);
Check(fake.Sales.SequenceEqual(new ulong[] { 1, 2 }), "next sale follows matching success ack");
c.OnSold(2, false, 1150); c.Process(1350);
Check(fake.Pages.Last() == "@MAIN", "returns through advertised main link for second service");
c.OnPage(new[] { "<出售/@SELL>", "<特殊修理/@SREPAIR>" }, 1400); c.Process(1600);
Check(fake.Pages.Last() == "@SREPAIR", "uses special repair instead of ordinary repair");
c.OnPage(new[] { "<返回/@Main>" }, 1610); c.OnSpecialRepairPanel(1, 1610); c.Process(1800);
c.OnRepairAcknowledged(9); c.Process(2200);
Check(c.Active && fake.Repairs.Count == 1, "early repair ack never counted as success or retried");
c.OnRepaired(9, 2300); c.Process(2500);
Check(!c.Active && fake.Messages.Last().Contains("特殊修理1件"), "ItemRepaired completes repaired count");
c.OnPage(new[] { "<Sell/@SELL>" }, 2600); c.Process(2800);
Check(fake.Pages.Count == 3, "one batch per NPC interaction");

var noSpecial = new Fake { Worn = new[] { Ordinary(9) } };
var ns = new NpcAutoController(noSpecial); ns.OnPage(new[] { "<维修/@REPAIR>" }, 0); ns.Process(200);
Check(noSpecial.Pages.Count == 0 && noSpecial.Repairs.Count == 0 && noSpecial.Messages.Any(m => m.Contains("避免降低")), "ordinary-only merchant never silently repairs");
var hidden = new Fake { Bag = new[] { Ordinary() }, Worn = new[] { Ordinary(9) } };
var hc = new NpcAutoController(hidden); hc.OnPage(new[] { "Welcome <Close/@EXIT>" }, 0); hc.Process(200);
Check(hidden.Pages.Count == 0 && hidden.Sales.Count == 0 && hidden.Repairs.Count == 0, "hidden or conditional unadvertised services never called");
var poor = new Fake { Gold = 0, Worn = new[] { Ordinary(9) } };
var pc = new NpcAutoController(poor); pc.OnPage(new[] { "<Special/@SREPAIR>" }, 0); pc.Process(200);
pc.OnPage(new[] { "<Back/@MAIN>" }, 210); pc.OnSpecialRepairPanel(1, 210); pc.Process(400); pc.Process(10000);
Check(poor.Repairs.Count == 0, "insufficient gold causes no repair request");
var rich = new Fake { Gold = uint.MaxValue, Bag = new[] { Ordinary() } };
var rc = new NpcAutoController(rich); rc.OnPage(new[] { "<Sell/@SELL>" }, 0); rc.Process(200);
rc.OnPage(new[] { "<Back/@MAIN>" }, 210); rc.OnSellPanel(210); rc.Process(400);
Check(rich.Sales.Count == 0, "sale skipped before gold overflow");
foreach (string reason in new[] { "move", "close", "switch", "disable" })
{
    var f = new Fake { Bag = new[] { Ordinary(), Ordinary(2) } }; var s = new NpcAutoController(f);
    s.OnPage(new[] { "<Sell/@SELL>" }, 0); s.Process(200); s.OnPage(new[] { "<Back/@MAIN>" }, 210); s.OnSellPanel(210); s.Process(400);
    switch (reason) { case "move": f.Location = new Point(2, 2); break; case "close": f.IsAvailable = false; break; case "switch": f.NpcId = 4; break; case "disable": f.SellEnabled = false; break; }
    s.Process(500); s.OnSold(1, true, 600); s.Process(800);
    Check(!s.Active && f.Sales.Count == 1, $"{reason} cancels further queued operations");
}
var timed = new Fake { Bag = new[] { Ordinary(), Ordinary(2) } }; var tc = new NpcAutoController(timed);
tc.OnPage(new[] { "<Sell/@SELL>" }, 0); tc.Process(200); tc.OnPage(new[] { "<Back/@MAIN>" }, 210); tc.OnSellPanel(210); tc.Process(400);
tc.Process(5500); tc.OnSold(1, true, 5600); tc.Process(5800);
Check(!tc.Active && timed.Sales.Count == 1, "timeout and late ack never retry or continue batch");
var manual = new Fake { Bag = new[] { Ordinary() } }; var mc = new NpcAutoController(manual);
mc.OnPage(new[] { "<Sell/@SELL>" }, 0); mc.Process(200); mc.OnPage(new[] { "<Back/@MAIN>" }, 210); mc.OnSellPanel(210);
mc.OnPage(new[] { "Player chose another page" }, 220); mc.Process(400);
Check(manual.Sales.Count == 0, "manual page navigation takes precedence");
var badRepair = new Fake { Worn = new[] { Ordinary(9), Ordinary(10) } }; var bc = new NpcAutoController(badRepair);
bc.OnPage(new[] { "<Special/@SREPAIR>" }, 0); bc.Process(200); bc.OnPage(new[] { "<Back/@MAIN>" }, 210); bc.OnSpecialRepairPanel(1, 210); bc.Process(400);
bc.OnRepairAcknowledged(9); bc.Process(5500); bc.OnRepaired(9, 5600); bc.Process(5800);
Check(!bc.Active && badRepair.Repairs.SequenceEqual(new ulong[] { 9 }), "repair timeout stops whole batch, late result never repeats charge");
var noSRepair = Ordinary(9); noSRepair.Info.Bind = BindMode.NoSRepair;
Check(!NpcAutoController.CanSpecialRepair(noSRepair), "NoSRepair equipment excluded");
var rented = Ordinary(9); rented.RentalInformation = new RentalInformation { BindingFlags = BindMode.DontRepair };
Check(!NpcAutoController.CanSpecialRepair(rented), "rental repair restriction honored");
var full = Ordinary(9); full.CurrentDura = full.MaxDura;
Check(!NpcAutoController.CanSpecialRepair(full), "full durability skipped without charge");
var switched = new Fake { Bag = new[] { Ordinary() } }; var switchedController = new NpcAutoController(switched);
switchedController.OnPage(new[] { "<Sell/@SELL>" }, 0); switchedController.Process(200);
switchedController.OnPage(new[] { "<Back/@MAIN>" }, 210); switchedController.OnSellPanel(210); switchedController.Process(400);
switched.SellEnabled = false; switchedController.PreferencesChanged(); switchedController.Process(500);
Check(!switchedController.Active && !switched.IsLocked(1), "option change cancels queued work and releases the pending cell lock");
switched.SellEnabled = true; switchedController.PreferencesChanged(); switched.Bag = new[] { Ordinary(2) };
switchedController.OnPage(new[] { "<Sell/@SELL>" }, 600); switchedController.Process(800);
Check(switched.Pages.Count == 2, "re-enabling the switch lets the same NPC start a fresh interaction");
Console.WriteLine($"{passed} checks passed.");

sealed class Fake : INpcAutoContext
{
    public uint NpcId { get; set; } = 1;
    public Point Location { get; set; } = new(1, 1);
    public bool IsAvailable { get; set; } = true;
    public bool SellEnabled { get; set; } = true;
    public bool RepairEnabled { get; set; } = true;
    public uint Gold { get; set; } = 100000;
    public UserItem[] Bag = Array.Empty<UserItem>(), Worn = Array.Empty<UserItem>();
    public IReadOnlyList<UserItem> Inventory => Bag;
    public IReadOnlyList<UserItem> Equipment => Worn;
    public readonly List<string> Pages = new(), Messages = new();
    public readonly List<ulong> Sales = new(), Repairs = new();
    private readonly HashSet<ulong> locks = new();
    public bool IsLocked(ulong id) => locks.Contains(id);
    public void Lock(ulong id, bool value) { if (value) locks.Add(id); else locks.Remove(id); }
    public void OpenPage(string key) => Pages.Add(key);
    public void Sell(UserItem item) => Sales.Add(item.UniqueID);
    public void SpecialRepair(UserItem item) => Repairs.Add(item.UniqueID);
    public void Notify(string text) => Messages.Add(text);
}

sealed class DialoguePlayer : Server.MirObjects.PlayerObject
{
    public readonly List<Packet> Packets = [];
    public override void Enqueue(Packet packet) => Packets.Add(packet);
}
