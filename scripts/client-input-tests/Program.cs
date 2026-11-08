using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Client;
using Client.MirControls;
using Client.MirObjects;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using C = ClientPackets;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Pass repository root and a client dependency directory.");
        args = args.Select(TestWorkspace.ResolvePath).ToArray();
        foreach (string file in new[] { "msvcr100.dll", "msvcp100.dll", "d3dx9_43.dll", "d3dcompiler_43.dll" })
            File.Copy(Path.Combine(args[1], file), Path.Combine(AppContext.BaseDirectory, file), true);
        Run(Path.GetFullPath(args[0]));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string root)
    {
        string fixture = Path.Combine(TestWorkspace.RunDirectory, "fixture");
        Directory.CreateDirectory(fixture);
        Directory.SetCurrentDirectory(fixture);
        File.WriteAllText("Mir2Config.ini", "[Game]\nAutoPickUp=True\nLanguage=Chinese\n");
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        CMain.Graphics = graphics;
        DbLanguage.Load(Path.Combine(root, "Localization"), "Chinese");

        var assembly = typeof(GameScene).Assembly;
        var settings = assembly.GetType("Client.Settings")!;
        var enabled = settings.GetField("AutoPickUp")!;
        var outgoing = new ConcurrentQueue<Packet>();
        assembly.GetType("Client.MirNetwork.Network")!.GetField("_sendList", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, outgoing);
        var user = new UserObject { Level = 16, Class = MirClass.Warrior, Gender = MirGender.Female, CurrentLocation = new Point(2, 2) };
        GameScene.User = user;
        // Skip the GameScene constructor: it creates a complete live gameplay scene.
        GameScene.Scene = (GameScene)RuntimeHelpers.GetUninitializedObject(typeof(GameScene));
        typeof(MirControl).GetProperty("Controls")!.SetValue(GameScene.Scene, new List<MirControl>());
        using var inventory = new InventoryDialog();
        GameScene.Scene.InventoryDialog = inventory;
        CMain.Ctrl = CMain.Shift = CMain.Alt = false;
        int passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            Console.WriteLine($"PASS {++passed}: {name}");
        }

        var tomeInfo = new ItemInfo { Index = 990002, Name = "玛法猎魔手札", Type = ItemType.Scroll, Shape = 902 };
        var tome = new UserItem(tomeInfo) { UniqueID = 123456 };
        user.Inventory[6] = tome;
        using var cell = new MirItemCell { GridType = MirGridType.Inventory, ItemSlot = 6, Sound = SoundList.None };
        CMain.Time = 1000;
        GameScene.UseItemTime = 0;
        cell.OnMouseClick(new MouseEventArgs(MouseButtons.Right, 1, 1, 1, 0));
        Check(outgoing.TryDequeue(out var packet) && packet is C.UseItem use && use.UniqueID == tome.UniqueID && use.Grid == MirGridType.Inventory,
            "real inventory right-click sends UseItem for the hunter tome without modifiers");
        Check(cell.Locked && outgoing.IsEmpty && ReferenceEquals(user.Inventory[6], tome),
            "right-click locks the cell without removing the permanent tome locally");
        cell.Locked = false;
        CMain.Time = 2000;
        cell.OnMouseDoubleClick(new MouseEventArgs(MouseButtons.Left, 2, 1, 1, 0));
        Check(outgoing.TryDequeue(out packet) && packet is C.UseItem use2 && use2.UniqueID == tome.UniqueID,
            "real inventory double-click also sends hunter tome UseItem");

        using var dialog = new NPCDialog();
        GameScene.Scene.NPCDialog = dialog;
        dialog.Visible = false;
        // Preserve the server's nested slash forms: these previously crashed the NPC renderer before Show.
        var response = new ServerPackets.NPCResponse { Page = new List<string> {
            "{【玛法猎魔手札 · 随身奇物】/Gold}",
            "当前 4×4 范围自动拾取：{智能极品/LimeGreen}",
            "自动拾取严格负重保护：{已开启/LimeGreen} {(可在【设置(O)】面板勾选开关)/Silver}",
            "本层魔物惊扰度进度：{0/100/DeepSkyBlue} (满50出精英 / 满100出猩红领主)",
            "已激活猎魔图鉴加成：{攻魔道+0、双防+0、爆率+0% (0/8章)/Gold}",
            "----------------------------------------",
            "<[切换] 4×4 范围自动拾取模式 (智能极品/全部/关闭)/@Tome_TogglePick>",
            "<[感知] 探查本层魔物惊扰度与存活领主方位/@Tome_Sense>",
            "<[图鉴] 查阅玛法猎魔图鉴进度与永久属性加成/@Tome_Codex>",
            "----------------------------------------", "{【安全区军需与营地快捷通道】/Khaki}",
            "<[营地] 打开玛法行军帐返场/@Camp_Menu>",
            "<[统卖] 一键出售普通战利品 (锁极品)/@Solo_QuickSell>",
            "<[特修] 主号与英雄全身特修/@Solo_QuickRepair>", " ", "<合上手札/@exit>"
        } };
        dialog.NewText(response.Page);
        dialog.Show();
        Check(dialog.Visible && dialog.TextButtons.Any(b => b.Text.Contains("切换")),
            "real tome NPC response with nested ratios renders and becomes visible");

        // Complete the real packet handler's Hide dependencies without creating a live scene.
        T EmptyDialog<T>() where T : MirControl
        {
            var control = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            typeof(MirControl).GetProperty("Controls")!.SetValue(control, new List<MirControl>());
            return control;
        }
        var scene = GameScene.Scene;
        scene.CraftDialog = EmptyDialog<CraftDialog>();
        scene.NPCGoodsDialog = EmptyDialog<NPCGoodsDialog>();
        scene.NPCSubGoodsDialog = EmptyDialog<NPCGoodsDialog>();
        scene.NPCCraftGoodsDialog = EmptyDialog<NPCGoodsDialog>();
        scene.NPCDropDialog = EmptyDialog<NPCDropDialog>();
        scene.StorageDialog = EmptyDialog<StorageDialog>();
        using var awakening = new NPCAwakeDialog();
        scene.NPCAwakeDialog = awakening;
        scene.RefineDialog = EmptyDialog<RefineDialog>();
        scene.TrustMerchantDialog = EmptyDialog<TrustMerchantDialog>();
        scene.QuestListDialog = EmptyDialog<QuestListDialog>();
        var receiveNpc = typeof(GameScene).GetMethod("NPCResponse", BindingFlags.Instance | BindingFlags.NonPublic)!;
        response.Page[3] = "本层魔物惊扰度进度：{0/DeepSkyBlue}/100 (满50出精英、满100出猩红领主)";
        dialog.Visible = false;
        receiveNpc.Invoke(scene, new object[] { response });
        Check(dialog.Visible && GameScene.NPCTime == 0,
            "actual GameScene NPCResponse packet handler renders the tome and shows its panel");
        Check(dialog.TextLabel[4].Text == "已激活猎魔图鉴加成：攻魔道+0、双防+0、爆率+0% (0/8章)"
            && dialog.TextLabel[3].Text == "本层魔物惊扰度进度：0/100 (满50出精英、满100出猩红领主)",
            "packet rendering preserves the full codex ratio and ordinary parenthesized text");
        var toggle = dialog.TextButtons.Single(b => b.Text.StartsWith("[切换]"));
        Check(toggle.Text == "[切换] 4×4 范围自动拾取模式 (智能极品/全部/关闭)",
            "tome action caption preserves its slash-separated parenthesized modes");
        toggle.Sound = SoundList.None;
        outgoing.Clear();
        toggle.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
        Check(outgoing.TryDequeue(out packet) && packet is C.CallNPC call && call.Key == "[@Tome_TogglePick]" && outgoing.IsEmpty,
            "real tome toggle click sends the original NPC action key");

        receiveNpc.Invoke(scene, new object[] { new ServerPackets.NPCResponse { Page = new List<string> {
            "A {红字/Red} + {1/2/Gold} <选项 (甲/乙)/@NEXT> 尾句"
        } } });
        Check(dialog.Visible && dialog.TextLabel[0].Text == "A 红字 + 1/2 选项 (甲/乙) 尾句"
            && dialog.TextButtons.Count == 3 && dialog.TextButtons.Select(b => b.Text).SequenceEqual(new[] { "红字", "1/2", "选项 (甲/乙)" }),
            "mixed color, ratio and NPC action runs retain all text with exactly three styled labels");

        var map = (MapControl)RuntimeHelpers.GetUninitializedObject(typeof(MapControl));
        map.M2CellInfo = new CellInfo[5, 5];
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++) map.M2CellInfo[x, y] = new CellInfo();
        var step = assembly.GetType("Client.MirScenes.StepAutoPickup")!.GetMethod("Process", BindingFlags.Static | BindingFlags.NonPublic)!;
        ItemObject Ground(int frame)
        {
            var item = (ItemObject)RuntimeHelpers.GetUninitializedObject(typeof(ItemObject));
            item.DrawFrame = frame;
            return item;
        }
        void Reset(bool on = true)
        {
            outgoing.Clear();
            enabled.SetValue(null, on);
            user.Dead = false;
            user.CurrentLocation = new Point(2, 2);
            Array.Clear(user.Inventory);
            foreach (var tile in map.M2CellInfo) { tile.CellObjects = null; tile.BackImage = 0; }
            CMain.Time = 3000;
            GameScene.PickUpTime = 0;
        }
        void Process() => step.Invoke(null, new object[] { map });
        void Feet(int frame = 1) => map.M2CellInfo[2, 2].CellObjects = new List<MapObject> { Ground(frame) };
        bool OnePickup() => outgoing.TryDequeue(out var p) && p is C.PickUp && outgoing.IsEmpty;

        Reset(false); Feet(); Process();
        Check(outgoing.IsEmpty, "disabled step pickup sends nothing with an item underfoot");
        Reset(); Feet(); Process();
        Check(OnePickup() && GameScene.PickUpTime == 3250, "enabled step pickup sends one pickup with a 250 ms deadline");
        Process();
        Check(outgoing.IsEmpty, "same-frame processing is throttled");
        CMain.Time = 3250; Process();
        Check(outgoing.IsEmpty, "pickup remains throttled at the shared deadline");
        CMain.Time = 3251; Process();
        Check(OnePickup(), "pickup resumes after the shared deadline");
        Reset(); map.M2CellInfo[3, 2].CellObjects = new List<MapObject> { Ground(1) }; Process();
        Check(outgoing.IsEmpty, "an adjacent item is outside step pickup scope");
        Reset(); Feet(); Array.Fill(user.Inventory, tome); Process();
        Check(outgoing.IsEmpty, "full inventory prevents equipment pickup requests");
        foreach (int goldFrame in Enumerable.Range(112, 5))
        {
            Reset(); Feet(goldFrame); Array.Fill(user.Inventory, tome); Process();
            Check(OnePickup(), $"full inventory still allows gold frame {goldFrame}");
        }
        Reset(); Feet(); user.Dead = true; Process();
        Check(outgoing.IsEmpty, "dead players do not request step pickup");
        Reset(); Feet(); map.M2CellInfo[2, 2].BackImage = 0x20000000; Process();
        Check(outgoing.IsEmpty, "blocked map cells do not request pickup");
        Reset(); user.CurrentLocation = new Point(-1, 2); Process();
        Check(outgoing.IsEmpty, "out-of-range player coordinates safely skip pickup");
        Reset(); map.M2CellInfo = null; Process();
        Check(outgoing.IsEmpty, "unloaded map data safely skips pickup");
        Console.WriteLine($"All {passed} client input checks passed. No sockets, account data or Direct3D device used. Fixture: {fixture}");
    }
}
