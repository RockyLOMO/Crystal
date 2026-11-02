using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Client.MirControls;
using Client.MirNetwork;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.MirScenes;

// The two GameScene hooks deliberately leave NPC scripts, permissions and packets upstream-owned.
internal static class NpcAutoService
{
    private static readonly ConditionalWeakTable<GameScene, NpcAutoController> Sessions = new();

    public static void Process(GameScene scene)
    {
        if (Sessions.TryGetValue(scene, out var session)) session.Process(CMain.Time);
    }

    public static void OnPacket(GameScene scene, Packet packet)
    {
        // Repairing worn equipment can restore stats lost at zero durability, including manual repairs.
        if (packet is S.ItemRepaired wornRepair && GameScene.User is { } user
            && user.Equipment.Any(item => item?.UniqueID == wornRepair.UniqueID)) user.RefreshStats();
        if (packet is not (S.NPCResponse or S.NPCSell or S.NPCSRepair or S.SellItem or S.RepairItem or S.ItemRepaired)) return;
        if (!Sessions.TryGetValue(scene, out var session))
        {
            if (packet is not S.NPCResponse || (!Settings.AutoSellNormalEquipment && !Settings.AutoSpecialRepairEquipment)) return;
            session = Sessions.GetValue(scene, s => new NpcAutoController(new GameContext(s)));
        }
        switch (packet)
        {
            case S.NPCResponse p: session.OnPage(p.Page, CMain.Time); break;
            case S.NPCSell: session.OnSellPanel(CMain.Time); break;
            case S.NPCSRepair p: session.OnSpecialRepairPanel(p.Rate, CMain.Time); break;
            case S.SellItem p: session.OnSold(p.UniqueID, p.Success, CMain.Time); break;
            case S.RepairItem p: session.OnRepairAcknowledged(p.UniqueID); break;
            case S.ItemRepaired p: session.OnRepaired(p.UniqueID, CMain.Time); break;
        }
    }

    private sealed class GameContext(GameScene scene) : INpcAutoContext
    {
        public uint NpcId => GameScene.NPCID;
        public Point Location => GameScene.User?.CurrentLocation ?? Point.Empty;
        public bool IsAvailable => Network.Connected && GameScene.User is { Dead: false, TradeLocked: false }
            && scene.NPCDialog.Visible && !scene.TradeDialog.Visible && !scene.GuestTradeDialog.Visible
            && GameScene.SelectedCell == null;
        public bool SellEnabled => Settings.AutoSellNormalEquipment;
        public bool RepairEnabled => Settings.AutoSpecialRepairEquipment;
        public uint Gold => GameScene.Gold;
        public IReadOnlyList<UserItem> Inventory => GameScene.User?.Inventory ?? Array.Empty<UserItem>();
        public IReadOnlyList<UserItem> Equipment => GameScene.User?.Equipment ?? Array.Empty<UserItem>();
        private MirItemCell Cell(ulong id) => scene.InventoryDialog.GetCell(id) ?? scene.BeltDialog.GetCell(id) ?? scene.CharacterDialog.GetCell(id);
        public bool IsLocked(ulong id) => Cell(id)?.Locked != false;
        public void Lock(ulong id, bool value) { if (Cell(id) is { } cell) cell.Locked = value; }
        public void OpenPage(string key)
        {
            GameScene.NPCTime = CMain.Time + 5000;
            Network.Enqueue(new C.CallNPC { ObjectID = NpcId, Key = $"[{key}]" });
        }
        public void Sell(UserItem item) => Network.Enqueue(new C.SellItem { UniqueID = item.UniqueID, Count = item.Count });
        public void SpecialRepair(UserItem item) => Network.Enqueue(new C.SRepairItem { UniqueID = item.UniqueID });
        public void Notify(string text) => scene.ChatDialog.ReceiveChat(text, ChatType.System);
    }
}

// No graphics or network dependency: deterministic state and item policy are exercised by the focused tests.
public interface INpcAutoContext
{
    uint NpcId { get; }
    Point Location { get; }
    bool IsAvailable { get; }
    bool SellEnabled { get; }
    bool RepairEnabled { get; }
    uint Gold { get; }
    IReadOnlyList<UserItem> Inventory { get; }
    IReadOnlyList<UserItem> Equipment { get; }
    bool IsLocked(ulong id);
    void Lock(ulong id, bool value);
    void OpenPage(string key);
    void Sell(UserItem item);
    void SpecialRepair(UserItem item);
    void Notify(string text);
}

public sealed class NpcAutoController(INpcAutoContext context)
{
    private enum Phase { Idle, Ready, SellPage, RepairPage, MainPage, Selling, Repairing }
    private static readonly Regex ActionLinks = new(@"<{1,2}[^<>\r\n]*?/(@[A-Za-z][A-Za-z0-9_]*)(?:/[^<>\r\n]*)?>{1,2}", RegexOptions.Compiled);
    private const long Timeout = 5000;
    private readonly HashSet<string> actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<ulong> queue = new();
    private Phase phase;
    private uint npcId;
    private Point location;
    private bool sellDone, repairDone, rootHadRepair, ordinaryRepairOnly, hadWork;
    private ulong pending;
    private long deadline, nextAction;
    private float rate;
    private int sold, repaired, skipped;

    public bool Active => phase != Phase.Idle;

    public static bool IsOrdinaryEquipment(UserItem item)
    {
        if (item?.Info == null || !IsEquipmentType(item.Info.Type)) return false;
        return item.Info.Name != null && !item.Info.Name.Contains('[') && !item.Info.Name.Contains(']')
            && item.Info.Grade is ItemGrade.None or ItemGrade.Common
            && item.Info.Bind == BindMode.None && item.Info.Unique == SpecialItemMode.None
            && item.Info.Set == ItemSet.None && !item.Info.StartItem
            && !item.Info.NeedIdentify && item.SoulBoundId == -1 && item.WeddingRing == -1
            && item.AddedStats is { Count: 0 } && !item.IsAdded && item.GemCount == 0
            && item.RefinedValue == RefinedValue.None && item.RefineAdded == 0 && item.RefineSuccessChance == 0
            && item.Awake != null && item.Awake.Type == AwakeType.None && item.Awake.GetAwakeLevel() == 0
            && item.Slots != null && item.Slots.All(s => s == null)
            && item.ExpireInfo == null && item.RentalInformation == null && item.SealedInfo == null
            && !item.Cursed && !item.GMMade && !item.IsShopItem && item.Count == 1 && item.UniqueID != 0;
    }

    public static bool IsEquipmentType(ItemType type) => type is ItemType.Weapon or ItemType.Armour or ItemType.Helmet
        or ItemType.Necklace or ItemType.Bracelet or ItemType.Ring or ItemType.Belt or ItemType.Boots;

    public static bool CanSpecialRepair(UserItem item) => item?.Info != null && IsEquipmentType(item.Info.Type)
        && item.Info.Durability > 0 && item.MaxDura > 0 && item.CurrentDura < item.MaxDura
        && (item.Info.Bind & (BindMode.DontRepair | BindMode.NoSRepair)) == 0
        && (item.RentalInformation?.BindingFlags & (BindMode.DontRepair | BindMode.NoSRepair)) is null or BindMode.None;

    public void OnPage(IEnumerable<string> lines, long now)
    {
        if (!context.IsAvailable || (!context.SellEnabled && !context.RepairEnabled)) { Cancel(); return; }
        if (Active && !SameSession()) { Cancel(); return; }
        if (Active && phase is not (Phase.SellPage or Phase.RepairPage or Phase.MainPage))
        {
            // A user-selected page supersedes the batch. Do not drive the user back into it.
            Cancel();
            return;
        }
        if (!Active)
        {
            // One batch per interaction. Closing the window, moving or changing NPC clears this marker.
            if (npcId == context.NpcId && location == context.Location) return;
            npcId = context.NpcId;
            location = context.Location;
            sellDone = repairDone = rootHadRepair = ordinaryRepairOnly = hadWork = false;
            sold = repaired = skipped = 0;
            queue.Clear();
            pending = 0;
            phase = Phase.Ready;
        }
        actions.Clear();
        foreach (var line in lines)
            foreach (Match match in ActionLinks.Matches(line ?? "")) actions.Add(match.Groups[1].Value.ToUpperInvariant());
        rootHadRepair |= actions.Contains("@SREPAIR");
        ordinaryRepairOnly |= actions.Contains("@REPAIR");
        if (phase == Phase.MainPage) phase = Phase.Ready;
        deadline = now + Timeout;
        nextAction = now + 150;
    }

    public void OnSellPanel(long now)
    {
        if (phase != Phase.SellPage || !SameSession()) return;
        queue.Clear();
        foreach (var item in context.Inventory)
            if (IsOrdinaryEquipment(item) && !context.IsLocked(item.UniqueID)) queue.Enqueue(item.UniqueID);
        phase = Phase.Selling;
        nextAction = now + 150;
    }

    public void OnSpecialRepairPanel(float priceRate, long now)
    {
        if (phase != Phase.RepairPage || !SameSession()) return;
        rate = priceRate;
        queue.Clear();
        foreach (var item in context.Equipment)
            if (CanSpecialRepair(item) && !context.IsLocked(item.UniqueID)) queue.Enqueue(item.UniqueID);
        phase = Phase.Repairing;
        nextAction = now + 150;
    }

    public void OnSold(ulong id, bool success, long now)
    {
        if (phase != Phase.Selling || pending != id) return;
        if (success) sold++; else skipped++;
        pending = 0;
        nextAction = now + 150;
    }

    public void OnRepairAcknowledged(ulong id)
    {
        // This packet is sent before validation, including failed requests. It is NOT success.
        if (pending == id && phase == Phase.Repairing) context.Lock(id, false);
    }

    public void OnRepaired(ulong id, long now)
    {
        if (phase != Phase.Repairing || pending != id) return;
        context.Lock(id, false);
        repaired++;
        pending = 0;
        nextAction = now + 150;
    }

    public void Process(long now)
    {
        if (!context.IsAvailable || (npcId != 0 && !SameSession()))
        {
            Cancel();
            npcId = 0;
            return;
        }
        if (!Active) return;
        if ((!context.SellEnabled && phase is Phase.Selling or Phase.SellPage)
            || (!context.RepairEnabled && phase is Phase.Repairing or Phase.RepairPage)) { Cancel(); return; }
        if (pending != 0 || phase is Phase.SellPage or Phase.RepairPage or Phase.MainPage)
        {
            if (now >= deadline)
            {
                // No retry: a late response must never duplicate a sale or charge a second repair.
                context.Notify("自动处理已停止：NPC未确认操作成功，请检查是否收购该装备、是否有足够金币。");
                Cancel();
            }
            return;
        }
        if (now < nextAction) return;
        if (phase is Phase.Selling or Phase.Repairing)
        {
            while (queue.Count > 0)
            {
                ulong id = queue.Dequeue();
                bool selling = phase == Phase.Selling;
                var item = Find(selling ? context.Inventory : context.Equipment, id);
                if (context.IsLocked(id) || (selling ? !IsOrdinaryEquipment(item) : !CanSpecialRepair(item))) { skipped++; continue; }
                if (selling && (ulong)item.Price() / 2 + context.Gold > uint.MaxValue) { skipped++; continue; }
                if (!selling)
                {
                    double cost = item.RepairPrice() * 3f * rate;
                    if (!float.IsFinite(rate) || rate < 0 || cost > context.Gold || cost > uint.MaxValue) { skipped++; continue; }
                }
                pending = id;
                deadline = now + Timeout;
                hadWork = true;
                context.Lock(id, true);
                if (selling) context.Sell(item); else context.SpecialRepair(item);
                return;
            }
            if (phase == Phase.Selling) sellDone = true; else repairDone = true;
            phase = Phase.Ready;
        }
        if (!sellDone)
        {
            string key = actions.Contains("@SELL") ? "@SELL" : actions.Contains("@BUYSELL") ? "@BUYSELL" : null;
            if (context.SellEnabled && key != null && context.Inventory.Any(IsOrdinaryEquipment)) { Open(key, Phase.SellPage, now); return; }
            sellDone = true;
        }
        if (!repairDone)
        {
            if (context.RepairEnabled && context.Equipment.Any(CanSpecialRepair))
            {
                if (actions.Contains("@SREPAIR")) { Open("@SREPAIR", Phase.RepairPage, now); return; }
                if (rootHadRepair && actions.Contains("@MAIN")) { Open("@MAIN", Phase.MainPage, now); return; }
                if (ordinaryRepairOnly) context.Notify("该NPC未提供特殊修理，已跳过自动维修，避免降低持久上限。");
            }
            repairDone = true;
        }
        if (hadWork) context.Notify($"自动处理完成：出售{sold}件普通装备，特殊修理{repaired}件，跳过{skipped}件。");
        Cancel();
    }

    private bool SameSession() => context.NpcId == npcId && context.Location == location;
    private static UserItem Find(IReadOnlyList<UserItem> items, ulong id)
    {
        foreach (var item in items) if (item?.UniqueID == id) return item;
        return null;
    }
    private void Open(string key, Phase next, long now)
    {
        phase = next;
        deadline = now + Timeout;
        context.OpenPage(key);
    }
    private void Cancel()
    {
        if (pending != 0) context.Lock(pending, false);
        pending = 0;
        queue.Clear();
        phase = Phase.Idle;
    }
}
