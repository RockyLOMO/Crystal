using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

// Actual server scheduler, without Start(), maps from disk, sockets or deployment.
const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
var batch = typeof(Envir).GetMethod("ProcessMobBatch", Private)!;
var worker = typeof(Envir).GetMethod("ThreadLoop", Private)!;
var running = typeof(Envir).GetProperty(nameof(Envir.Running))!;
var time = typeof(Envir).GetProperty(nameof(Envir.Time))!;
var mainField = typeof(Envir).GetField("_mainObjectSchedule", Private)!;
var heapField = typeof(MobThread).GetField("_scheduled", Private)!;
var membership = typeof(MapObject).GetField("ScheduledThread", Private)!;
var lockerField = typeof(Envir).GetField("_locker", Private)!;
var env = Envir.Main;
var mainQueue = (MobThread)mainField.GetValue(env)!;
var passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine($"PASS {++passed}: {description}");
}
int Size(MobThread queue) => ((List<MapObject>)heapField.GetValue(queue)!).Count;
int Run(MobThread queue)
{
    queue.EndTime = long.MaxValue;
    return (int)batch.Invoke(env, [queue])!;
}
void Tick(long now) => time.SetValue(env, now);
CountingMonster Spawn(bool npc = false)
{
    var obj = new CountingMonster { PretendNpc = npc, CurrentMap = new Map(new MapInfo()) { Thread = 0 } };
    obj.Spawned();
    return obj;
}
void DespawnAll()
{
    foreach (var obj in env.Objects.ToArray()) obj.Despawn();
}
Settings.Multithreaded = true;
for (var i = 0; i < env.MobThreads.Length; i++) env.MobThreads[i] = new MobThread { Id = i };
var partition = env.MobThreads[0];
running.SetValue(env, true);
Tick(100);
Check(Run(partition) == 0, "empty partition returns without scanning");
var due = Spawn(); due.OperateTime = 0;
var future = Spawn(); future.OperateTime = 200;
var pet = Spawn(); pet.Master = due; pet.OperateTime = 0;
Check(Size(partition) == 2 && Size(mainQueue) == 1, "wild monsters and pets occupy their assigned queues exactly once");
Run(partition);
Check(due.Calls == 1 && future.Calls == 0 && pet.Calls == 0, "worker executes only the due wild monster");
Run(mainQueue);
Check(pet.Calls == 1, "pet executes on the main queue");
future.OperateTime = 100;
Check(Run(partition) == 0, "dueTime equal to Time retains the original strict greater-than gate");
future.OperateTime = 0;
Check(Run(partition) == 1 && future.Calls == 1, "external earlier OperateTime immediately repairs heap priority");
future.OperateTime = 0;
Check(Run(partition) == 0, "same frame cannot execute the same object twice");
Tick(110);
Check(Run(partition) == 1 && future.Calls == 2, "externally expedited event executes on the next frame");
future.OperateTime = 1000;
future.OperateTime = 2000;
Tick(1500);
Check(Run(partition) == 0, "postponed event removes the former earlier due priority");
DespawnAll();
Check(Size(partition) == 0 && Size(mainQueue) == 0, "despawn removes all heap references");

Tick(2000);
var first = Spawn(); first.OperateTime = 0; first.DelayMilliseconds = 20;
var second = Spawn(); second.OperateTime = 1;
partition.EndTime = env.Stopwatch.ElapsedMilliseconds + 10;
batch.Invoke(env, [partition]);
Check(first.Calls == 1 && second.Calls == 0 && Size(partition) == 2,
    "batch budget leaves unprocessed due objects in the heap");
Run(partition);
Check(second.Calls == 1, "next batch retains unfinished due work");
DespawnAll();

Tick(3000);
var changing = Spawn(); changing.OperateTime = 0;
changing.OnProcess = obj => obj.Master = first;
Run(partition);
Check(Size(partition) == 0 && Size(mainQueue) == 1 && membership.GetValue(changing) == mainQueue,
    "Master change inside Process transfers one entry to the main queue");
changing.OnProcess = null;
changing.Master = null;
Check(Size(partition) == 1 && Size(mainQueue) == 0, "released pet returns to its original wild-monster partition");
changing.OperateTime = 0;
Tick(3010);
changing.OnProcess = obj => { obj.Despawn(); obj.Spawned(); };
Run(partition);
Check(Size(partition) == 1, "despawn and respawn inside Process never create a duplicate queue node");
changing.OnProcess = obj => obj.Despawn();
changing.OperateTime = 0;
Tick(3020);
Run(partition);
Check(Size(partition) == 0 && changing.Node == null, "self-despawn inside Process is not requeued by completion");

var npc = Spawn(true); npc.OperateTime = 0;
Run(partition);
Check(npc.Calls == 0 && Size(mainQueue) == 1, "non-monster object never enters a worker queue");
Run(mainQueue);
Check(npc.Calls == 1, "non-monster due event executes on main queue");
DespawnAll();
Settings.Multithreaded = false;
var single = Spawn(); single.OperateTime = 0;
Check(Size(mainQueue) == 1 && Size(partition) == 0, "single-thread mode schedules monsters on the main queue");
Run(mainQueue);
Check(single.Calls == 1, "single-thread mode processes due monsters");
DespawnAll();
Settings.Multithreaded = true;

Tick(4000);
var concurrent = Spawn(); concurrent.OperateTime = long.MaxValue;
Parallel.For(0, 10_000, i => concurrent.OperateTime = i + 5000);
concurrent.OperateTime = 0;
Check(Size(partition) == 1 && Run(partition) == 1, "concurrent reschedules retain one node and the final earlier write");
concurrent.OnProcess = obj =>
{
    var writer = Task.Run(() => { for (var i = 0; i < 1000; i++) obj.OperateTime = i; });
    writer.Wait();
};
concurrent.NextDue = 0;
Tick(4010);
concurrent.OperateTime = 0;
Run(partition);
Check(Size(partition) == 1, "writes while the object is dequeued are captured by completion without stale entries");
DespawnAll();

// Compare ordering with a model through many earlier/later moves and removals.
Tick(10_000);
var random = new Random(12345);
var objects = Enumerable.Range(0, 1000).Select(_ => Spawn()).ToArray();
for (var i = 0; i < 20_000; i++) objects[random.Next(objects.Length)].OperateTime = random.Next(20_000);
var expected = objects.Count(obj => obj.OperateTime < 10_000);
Check(Size(partition) == objects.Length && Run(partition) == expected,
    "indexed heap executes exactly the model's due set after 20,000 reprioritizations");
Check(Size(partition) == objects.Length, "heap memory stays bounded to the live object count");
DespawnAll();

// 100,000 not-due objects demonstrate O(1) idle checks rather than full scans.
Tick(20_000);
var idleObjects = Enumerable.Range(0, 100_000).Select(_ => Spawn()).ToArray();
foreach (var obj in idleObjects) obj.OperateTime = long.MaxValue;
var watch = Stopwatch.StartNew();
for (var i = 0; i < 1000; i++) Run(partition);
Console.WriteLine($"1000 idle ticks with 100,000 queued monsters: {watch.Elapsed.TotalMilliseconds:F2} ms");
Check(idleObjects.All(obj => obj.Calls == 0) && Size(partition) == idleObjects.Length,
    "100,000 future monsters remain intact without being executed");
DespawnAll();

var monitor = lockerField.GetValue(env)!;
var scheduled = Spawn(); scheduled.OperateTime = 0;
Exception threadFailure = null;
var workerThread = new Thread(() =>
{
    try { worker.Invoke(env, [partition]); }
    catch (Exception ex) { threadFailure = ex; }
}) { IsBackground = true };
workerThread.Start();
Check(SpinWait.SpinUntil(() => (workerThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, 2000)
    && scheduled.Calls == 0, "worker blocks until its tick is scheduled");
lock (monitor)
{
    partition.EndTime = env.Stopwatch.ElapsedMilliseconds + 10;
    partition.Stop = false;
    Monitor.PulseAll(monitor);
    var timeout = Stopwatch.StartNew();
    while (!partition.Stop && timeout.ElapsedMilliseconds < 2000) Monitor.Wait(monitor, 100);
    Check(partition.Stop && scheduled.Calls == 1 && threadFailure == null,
        "worker completion wakes a blocking main-thread waiter");
    running.SetValue(env, false);
    Monitor.PulseAll(monitor);
}
Check(workerThread.Join(2000) && threadFailure == null, "stop wakes and terminates the waiting worker");
DespawnAll();
running.SetValue(env, true);
Tick(30_000);
var failing = Spawn(); failing.FailOnProcess = true; failing.OperateTime = 0;
var healthy = Spawn(); healthy.OperateTime = 1;
var errorsBefore = env.ObjectProcessErrors;
Check(Run(partition) == 2 && failing.Calls == 1 && healthy.Calls == 1 && env.Running
    && env.ObjectProcessErrors == errorsBefore + 1 && failing.OperateTime == 31_000,
    "object Process exception is isolated, logged, delayed for retry, and does not stop other objects");
DespawnAll();

// Exercise the real delayed-NPC pipeline: grant gold, then fail after that
// side effect. Retrying the player must not replay the consumed reward action.
var player = new SilentPlayer
{
    Info = new CharacterInfo { Name = "retry-regression" },
    Account = new AccountInfo(),
    Connection = (Server.MirNetwork.MirConnection)RuntimeHelpers.GetUninitializedObject(typeof(Server.MirNetwork.MirConnection)),
    CurrentMap = new Map(new MapInfo()),
    ItemExpireTime = long.MaxValue,
    TorchTime = long.MaxValue,
    DuraTime = long.MaxValue,
};
player.Info.Mount = new MountInfo(player);
player.Stats = new Stats();
player.Node = new LinkedList<MapObject>().AddLast(player);
var rewardPage = new NPCPage("[@REWARD]");
var rewardSegment = new NPCSegment(rewardPage, [], [], [], [], []);
rewardSegment.ActList.Add(new NPCActions(ActionType.GiveGold, "100"));
rewardSegment.ActList.Add(new NPCActions(ActionType.Calc, "1", "/", "0", "A0"));
rewardPage.SegmentList.Add(rewardSegment);
var rewardScript = (NPCScript)RuntimeHelpers.GetUninitializedObject(typeof(NPCScript));
rewardScript.NPCPages = [rewardPage];
var followupPage = new NPCPage("[@FOLLOWUP]");
var followupSegment = new NPCSegment(followupPage, [], [], [], [], []);
followupSegment.ActList.Add(new NPCActions(ActionType.GiveGold, "7"));
followupPage.SegmentList.Add(followupSegment);
rewardScript.NPCPages.Add(followupPage);
int scriptId = env.ScriptIndex + 1;
while (env.Scripts.ContainsKey(scriptId)) scriptId++;
env.Scripts.Add(scriptId, rewardScript);
var processObject = typeof(Envir).GetMethod("ProcessScheduledObject", Private)!;
try
{
    var failedReward = new DelayedAction(DelayedType.NPC, 0, (uint)1, scriptId, "[@REWARD]");
    var followupReward = new DelayedAction(DelayedType.NPC, 0, (uint)1, scriptId, "[@FOLLOWUP]");
    player.ActionList.Add(failedReward);
    player.ActionList.Add(followupReward);
    Tick(40_000);
    errorsBefore = env.ObjectProcessErrors;
    processObject.Invoke(env, [player]);
    Check(player.Account.Gold == 100 && player.ActionList.Count == 1 && player.ActionList[0] == followupReward &&
        env.ObjectProcessErrors == errorsBefore + 1 && player.OperateTime == 41_000,
        "partially failed NPC reward is consumed once, preserving unrelated queued actions");
    Tick(41_001);
    processObject.Invoke(env, [player]);
    Tick(42_002);
    processObject.Invoke(env, [player]);
    Check(player.Account.Gold == 107 && player.ActionList.Count == 0 &&
        env.ObjectProcessErrors == errorsBefore + 1,
        "player retries execute the remaining reward once without replaying the failed reward");
    player.ActionList.Add(new DelayedAction(DelayedType.NPC, 0, (uint)1, scriptId, "[@FOLLOWUP]"));
    player.ActionList.Add(new DelayedAction(DelayedType.NPC, 0, (uint)1, scriptId, "[@FOLLOWUP]"));
    Tick(43_000);
    processObject.Invoke(env, [player]);
    Check(player.Account.Gold == 121 && player.ActionList.Count == 0,
        "multiple delayed actions due in the same tick are all processed without skipping adjacent entries");

    var parsePage = typeof(NPCScript).GetMethod("ParsePage", Private)!;
    var elseIfLines = new List<string>
    {
        "[@ELSEIF_TEST]",
        "#IF",
        "CHECKGOLD >= 999999",
        "#ACT",
        "GIVEGOLD 50",
        "#ELSEIF",
        "CHECKGOLD >= 100",
        "#ACT",
        "GIVEGOLD 9",
        "#ELSEIF",
        "CHECKGOLD >= 1",
        "#ACT",
        "GIVEGOLD 99",
        "#ELSESAY",
        "No gold"
    };
    var parsedElseIfPage = (NPCPage)parsePage.Invoke(rewardScript, [elseIfLines, "[@ELSEIF_TEST]"])!;
    rewardScript.NPCPages.Add(parsedElseIfPage);
    player.NPCDelayed = true;
    rewardScript.Call(player, 1, "[@ELSEIF_TEST]");
    Check(parsedElseIfPage.SegmentList.Count == 3 && player.Account.Gold == 130 && player.NPCSpeech.Count == 0,
        "#ELSEIF parses into chained segments, executes matching branch once, and skips subsequent branches");
    Check(player.CanRun && !player.FastRun,
        "1.76 instant-run: HumanObject.CanRun is true from standstill without 1-step walk warm-up");
}
finally
{
    env.Scripts.Remove(scriptId);
}

// Corrupt the internal heap to verify that a scheduler-level failure still stops the server safely.
((List<MapObject>)heapField.GetValue(partition)!).Add(null!);
partition.Stop = false; partition.EndTime = long.MaxValue;
var failedThread = new Thread(() => worker.Invoke(env, [partition])) { IsBackground = true };
failedThread.Start();
Check(failedThread.Join(2000) && !env.Running && partition.Stop, "worker error cannot strand the main completion waiter");
((List<MapObject>)heapField.GetValue(partition)!).Clear();
DespawnAll();
Check(Size(partition) == 0 && Size(mainQueue) == 0, "final cleanup releases all scheduled references");
var closing = Enumerable.Range(0, 10).Select(_ => Spawn()).ToArray();
env.Objects.Clear();
typeof(MobThread).GetMethod("ClearScheduled", Private)!.Invoke(partition, null);
Check(Size(partition) == 0 && closing.All(obj => membership.GetValue(obj) == null),
    "shutdown queue clear releases retained objects and resets their membership");
Console.WriteLine($"All {passed} scheduling checks passed. No server started.");

sealed class CountingMonster : MonsterObject
{
    public int Calls, DelayMilliseconds;
    public bool FailOnProcess, PretendNpc;
    public long NextDue = long.MaxValue;
    public Action<CountingMonster> OnProcess;
    public CountingMonster() : base(new MonsterInfo()) { }
    public override ObjectType Race => PretendNpc ? ObjectType.Merchant : ObjectType.Monster;
    public override void BroadcastInfo() { }
    public override void BroadcastHealthChange() { }
    public override void Process()
    {
        Interlocked.Increment(ref Calls);
        if (FailOnProcess) throw new InvalidOperationException("Intentional scheduling test failure.");
        if (DelayMilliseconds > 0) Thread.Sleep(DelayMilliseconds);
        OnProcess?.Invoke(this);
    }
    public override void SetOperateTime() => OperateTime = NextDue;
}

sealed class SilentPlayer : PlayerObject
{
    public override void Enqueue(Packet packet) { }
    protected override void ProcessBuffs() { }
}
