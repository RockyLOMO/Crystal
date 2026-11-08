using System.Drawing;
using System.Reflection;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using Server.SoloExtension;
using S = ServerPackets;

// TestWorkspace's early initializer isolates all server-relative paths.
int passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine($"PASS {++passed}: {description}");
}

var environment = Envir.Main;
var randomSlot = (ThreadLocal<Random>)typeof(RandomProvider).GetField("RandomWrapper", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
var previousRandom = randomSlot.Value!;
var random = new FixedRandom();
var previousDropRate = Settings.DropRate;
randomSlot.Value = random;
Settings.DropRate = 1;
try
{
    var questInfo = new ItemInfo { Index = 990101, Name = "QuestDropFixture", Type = ItemType.Quest, StackSize = 1 };
    var ordinaryInfo = new ItemInfo { Index = 990102, Name = "OrdinaryMeatFixture", Type = ItemType.Meat, StackSize = 1 };
    var equipmentInfo = new ItemInfo { Index = 990103, Name = "OrdinaryRingFixture", Type = ItemType.Ring, StackSize = 1 };
    var questDrop = new DropInfo { Item = questInfo, QuestRequired = true, Chance = 5 };
    foreach (int rate in new[] { 1, 2, 3, 5, 6, 20, 180 })
    {
        questDrop.Chance = rate;
        int successes = 0;
        for (int roll = 0; roll < rate; roll++)
        {
            random.Roll = roll;
            if (questDrop.AttemptDrop() != null) successes++;
        }
        Check(successes == Math.Min(2, rate),
            $"actual AttemptDrop turns 1/{rate} into {Math.Min(2, rate)}/{rate}, capped at 100 percent");
    }
    questDrop.Chance = 5;
    random.Roll = 2;
    Check(questDrop.AttemptDrop() == null,
        "a boosted 1/5 quest drop still fails on roll 2 instead of becoming guaranteed");
    random.Roll = 1;
    Check(new DropInfo { Item = questInfo, Chance = 20 }.AttemptDrop() != null,
        "dedicated ItemType.Quest items receive the same boost even without a Q marker");
    Check(new DropInfo { Item = ordinaryInfo, QuestRequired = true, Chance = 5 }.AttemptDrop() != null,
        "an explicitly Q-marked task ingredient receives the boost without retyping the item");
    Check(new DropInfo { Item = ordinaryInfo, Chance = 5 }.AttemptDrop() == null &&
          new DropInfo { Item = equipmentInfo, Chance = 5 }.AttemptDrop() == null,
        "ordinary meat and equipment retain one successful roll, even when they can be quest turn-ins");
    var goldDrop = new DropInfo { Gold = 100, Chance = 5 };
    Check(goldDrop.AttemptDrop() == null,
        "gold drop probability receives no task-item bonus");
    random.Roll = 0;
    Check(goldDrop.AttemptDrop()!.Gold == 50,
        "gold amount generation retains its existing range and behavior");
    random.Roll = 1;
    Check(new DropInfo { Item = questInfo, Gold = 100, QuestRequired = true, Chance = 5 }.AttemptDrop() == null,
        "a gold entry with stray quest metadata still receives no bonus");
    var group = new DropInfo { Chance = 1, GroupedDrop = new GroupDropInfo() };
    group.GroupedDrop.Add(new DropInfo { Item = ordinaryInfo, Chance = 5 });
    group.GroupedDrop.Add(new DropInfo { Item = questInfo, QuestRequired = true, Chance = 5 });
    var grouped = group.AttemptDrop()!;
    Check(grouped.Items.SequenceEqual(new[] { questInfo }),
        "group recursion boosts only its dedicated quest leaf and keeps an ordinary sibling unchanged");
    group.Chance = 5;
    group.QuestRequired = true;
    Check(group.AttemptDrop() == null,
        "a group parent is not boosted even if it has stray Q metadata");
    questDrop.Chance = 10;
    int offsetSuccesses = 0;
    for (int roll = 0; roll < 5; roll++)
    {
        random.Roll = roll;
        if (questDrop.AttemptDrop(100) != null) offsetSuccesses++;
    }
    Check(offsetSuccesses == 2,
        "the task bonus doubles the existing effective chance after the owner's drop bonus");
    Settings.DropRate = 2;
    random.Roll = 1;
    Check(questDrop.AttemptDrop() != null,
        "the task boost composes with the existing global drop rate without rewriting it");
    Settings.DropRate = 1;
    questDrop.Chance = int.MaxValue;
    Check(questDrop.AttemptDrop() != null,
        "a very rare quest drop doubles its chance without denominator overflow");

    // Real MonsterObject.Drop and HarvestMonster.Harvest entry points. Capture
    // only delivery boundaries so fixtures never publish loot on a live map.
    var definition = new MonsterInfo();
    questDrop.Chance = 5;
    definition.Drops.Add(questDrop);
    var player = new GatePlayer { Info = new CharacterInfo(), Stats = new Stats(), AcceptQuest = true };
    var monster = new DroppingMonster(definition)
    {
        CurrentMap = new Map(new MapInfo()), EXPOwner = player
    };
    random.Roll = 1;
    monster.RollDrops();
    Check(player.QuestChecks == 1 && player.QuestItems == 1 && monster.GroundItems.Count == 0,
        "real monster death rolls the boosted item and still routes it through its EXP owner's quest gate");
    player.AcceptQuest = false;
    monster.RollDrops();
    Check(player.QuestChecks == 2 && player.QuestItems == 1 && monster.GroundItems.Count == 0,
        "an unneeded Q drop is not granted or converted into ordinary ground loot");
    monster.EXPOwner = null;
    monster.RollDrops();
    Check(player.QuestChecks == 2 && monster.GroundItems.Count == 0,
        "a Q drop without an eligible EXP owner remains undistributed");
    definition.Drops.Clear();
    definition.Drops.Add(new DropInfo { Item = ordinaryInfo, Chance = 5 });
    monster.RollDrops();
    Check(monster.GroundItems.Count == 0,
        "real monster death does not boost an ordinary drop on the same new winning quest roll");
    random.Roll = 0;
    monster.RollDrops();
    Check(monster.GroundItems.Count == 1 && monster.GroundItems[0].Info == ordinaryInfo,
        "an ordinary drop still uses the existing ground-loot path on its original winning roll");
    definition.Drops.Clear();
    definition.Drops.Add(questDrop);
    player.AcceptQuest = true;
    int checksBefore = player.QuestChecks;
    var harvest = new HarvestFixture(definition);
    random.Roll = 1;
    harvest.Harvest(player);
    harvest.Harvest(player);
    Check(player.QuestChecks == checksBefore + 1 && !harvest.Harvested,
        "real harvest preparation uses the boosted probability but checks task eligibility before retaining its Q item");
    harvest.Harvest(player);
    Check(harvest.Harvested && player.QuestChecks == checksBefore + 2,
        "real harvest delivery keeps its second quest gate and original harvesting completion flow");
    player.AcceptQuest = false;
    harvest = new HarvestFixture(definition);
    harvest.Harvest(player);
    harvest.Harvest(player);
    Check(harvest.Harvested,
        "harvesting an unneeded Q item still discards it rather than awarding it to a normal bag");

    // Exercise the unmodified real need/capacity rules separately, without gain
    // side effects, packets requiring a connection, or loading any account data.
    var gate = new GatePlayer { Info = new CharacterInfo(), Stats = new Stats() };
    var item = new UserItem(questInfo);
    Check(!gate.CheckNeedQuestItem(item, false),
        "the real quest check rejects a task item before the task has been accepted");
    var task = new QuestInfo { Index = 990104 };
    task.ItemTasks.Add(new QuestItemTask { Item = questInfo, Count = 2 });
    environment.QuestInfoList.Add(task);
    var progress = new QuestProgressInfo(task.Index) { Owner = gate };
    gate.CurrentQuests.Add(progress);
    Check(gate.CheckNeedQuestItem(item, false),
        "the real quest check still requires an active matching unfinished item objective");
    progress.ItemTaskCount[0].Count = 2;
    Check(!gate.CheckNeedQuestItem(item, false),
        "the real quest check rejects an already completed item objective");
    progress.ItemTaskCount[0].Count = 0;
    for (int i = 0; i < gate.Info.QuestInventory.Length; i++) gate.Info.QuestInventory[i] = new UserItem(ordinaryInfo);
    Check(!gate.CheckNeedQuestItem(item, false),
        "the real quest inventory capacity restriction still rejects a full task bag");
    environment.QuestInfoList.Remove(task);
    Console.WriteLine($"PASS: {passed} quest-drop checks. No server started, runtime database or account data loaded.");
}
finally
{
    randomSlot.Value = previousRandom;
    Settings.DropRate = previousDropRate;
}

sealed class FixedRandom : Random
{
    public int Roll;
    public override int Next(int maxValue) => maxValue <= 1 ? 0 : Math.Min(Roll, maxValue - 1);
    public override int Next(int minValue, int maxValue) => minValue;
}

sealed class GatePlayer : PlayerObject
{
    public bool AcceptQuest;
    public int QuestChecks, QuestItems;
    public override bool CheckGroupQuestItem(UserItem item, bool gainItem = true)
    {
        QuestChecks++;
        if (!AcceptQuest) return false;
        if (gainItem) QuestItems++;
        return true;
    }
    public override void Enqueue(Packet packet) { }
    public override void ReceiveChat(string text, ChatType type) { }
}

sealed class DroppingMonster : MonsterObject
{
    public readonly List<UserItem> GroundItems = new();
    public DroppingMonster(MonsterInfo info) : base(info) { }
    public void RollDrops() => base.Drop();
    protected override bool DropItem(UserItem item) { GroundItems.Add(item); return true; }
}

sealed class HarvestFixture : HarvestMonster
{
    public HarvestFixture(MonsterInfo info) : base(info) { }
    public override void Broadcast(Packet packet) { }
}
