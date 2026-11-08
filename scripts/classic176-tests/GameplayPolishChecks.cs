using System.Reflection;
using Client;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using SlimDX;
using SlimDX.Direct3D9;
using Font = System.Drawing.Font;

internal static class GameplayPolishChecks
{
    internal static void Run(GameScene scene, Action<bool, string> check, string preview,
        Device device, Sprite sprite, Surface surface, List<Texture> lights)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        using var up = new MirButton(); using var down = new MirButton(); using var thumb = new MirButton();
        using var quest = new QuestMessage(up, down, thumb, 10)
        {
            Parent = scene, Location = new Point(40, 40), Size = new Size(280, 200), PosMaxY = 100,
            Font = new Font("Microsoft YaHei", 9F)
        };
        var newText = typeof(QuestMessage).GetMethod("NewText", privateInstance)!;
        MirLabel[] Lines() => (MirLabel[])typeof(QuestMessage).GetField("_textLabel", privateInstance)!.GetValue(quest)!;
        List<MirLabel> Runs() => (List<MirLabel>)typeof(QuestMessage).GetField("_textButtons", privateInstance)!.GetValue(quest)!;
        void Show(List<string> lines) => newText.Invoke(quest, new object[] { new List<string>(lines), true });
        var text = new List<string>
        {
            "{任务标题/Gold}", "{任务彩字/Yellow}", "前缀 {1/2/Gold} 和 {红字/Red} 后缀",
            "寻找 [NPC:1|向导] 及 [MONSTER:1|稻草人]", "取得 [ITEM:1|金创药]",
            "{[ITEM:1]/Yellow} & 普通文字"
        };
        Show(text);
        check(Lines()[2].Text == "前缀 1/2 和 红字 后缀" && !Lines()[2].DrawControlTexture,
            "Quest: resolved ratios and metadata are preserved without a duplicate base text layer");
        check(Runs().Single(r => r.Text == "任务标题").Font.Bold &&
            Math.Abs(Runs().Single(r => r.Text == "任务标题").Font.Size - Lines()[0].Font.Size) < .01,
            "Quest: colored titles retain the title's bold font and size");
        check(Runs().Any(r => r.Text == "[ITEM:1]") && !Runs().Any(r => r.Text.Contains("LINK_")),
            "Quest: bracketed display text inside an outer color run is not parsed twice");
        check(Runs().Count(r => r.ForeColour == Color.Cyan) == 3 &&
            Lines()[2].Controls.OfType<MirLabel>().Select(r => r.Text).SequenceEqual(new[] { "前缀 ", " 和 ", " 后缀" }),
            "Quest: entity links and normal text around multiple styled runs remain visible");
        device.Clear(ClearFlags.Target, Color.FromArgb(14, 13, 12), 0, 0);
        device.BeginScene(); sprite.Begin(SpriteFlags.AlphaBlend); quest.Draw(); sprite.End(); device.EndScene();
        string questPreview = Path.ChangeExtension(preview, "quest.png");
        Surface.ToFile(surface, questPreview, ImageFileFormat.Png);
        using (var image = new Bitmap(questPreview))
        {
            var yellow = Runs().Single(r => r.Text == "任务彩字");
            int white = 0, gold = 0;
            // Text controls include transparent padding below the 15px row; exclude the next row's ink.
            for (int y = yellow.DisplayRectangle.Top; y < Lines()[2].DisplayRectangle.Top; y++)
                for (int x = yellow.DisplayRectangle.Left; x < yellow.DisplayRectangle.Right; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (pixel.R > 190 && pixel.G > 190 && pixel.B > 190) white++;
                    if (pixel.R > 180 && pixel.G > 150 && pixel.B < 60) gold++;
                }
            check(gold > 20 && white == 0, $"Quest: actual render has {gold} yellow and {white} duplicate white pixels");
        }
        var old = Lines().Where(l => l != null).ToArray(); int controls = quest.Controls.Count;
        for (int i = 0; i < 20; i++) Show(text);
        check(old.All(l => l.IsDisposed) && quest.Controls.Count == controls, "Quest: repeated refresh releases old text runs without control growth");
        Show(new List<string> { "任务标题", "{颜色/Red}" + new string('中', 40) + "[NPC:1|越界链接]" });
        check(Runs().All(r => !r.Visible || quest.DisplayRectangle.Contains(r.DisplayRectangle)) && !Runs().Last().Visible,
            "Quest: long formatted rows and offscreen links stay inside the task text area");
        Show(Enumerable.Range(0, 15).Select(i => $"{{任务{i}/Gold}}").ToList());
        down.InvokeMouseClick(EventArgs.Empty);
        check(quest.TopLine == 1 && Lines()[0].Text == "任务1", "Quest: scroll buttons continue to refresh the correct formatted rows");
        using (var dpiImage = new Bitmap(1, 1))
        {
            dpiImage.SetResolution(144, 144); using var graphics = Graphics.FromImage(dpiImage);
            var original = CMain.Graphics; CMain.Graphics = graphics;
            try
            {
                Show(text);
                check(Runs().Single(r => r.Text == "任务标题").Font.Size == Lines()[0].Font.Size &&
                    Lines()[2].Controls.OfType<MirLabel>().All(l => l.Font.Size == Runs().Single(r => r.Text == "红字").Font.Size),
                    "Quest: title and normal/styled fonts remain consistent at 150 percent DPI");
            }
            finally { CMain.Graphics = original; }
        }

        // Exercise the real movement queue on an empty offline map, without a socket or account.
        var map = scene.MapControl ??= new MapControl { Parent = scene };
        scene.Controls.Remove(map); scene.Controls.Insert(0, map);
        map.Width = map.Height = 80; map.M2CellInfo = new CellInfo[80, 80];
        for (int x = 0; x < 80; x++) for (int y = 0; y < 80; y++) map.M2CellInfo[x, y] = new CellInfo();
        var user = GameScene.User; user.CurrentLocation = new Point(40, 40); user.CurrentBagWeight = user.CurrentWearWeight = 0;
        user.Poison = PoisonType.None; user.Dead = false; user.NextMagic = null;
        MapObject.TargetObjectID = MapObject.MouseObjectID = 0; MapControl.Objects.Clear();
        CMain.Time = 10000; MapControl.InputDelay = 0; GameScene.NextRunTime = 0;
        var settings = typeof(GameScene).Assembly.GetType("Client.Settings")!;
        settings.GetField("NewMove")!.SetValue(null, false); settings.GetField("AutoPickUp")!.SetValue(null, false);
        var checkInput = typeof(MapControl).GetMethod("CheckInput", privateInstance)!;
        scene.NPCDialog.Visible = false; quest.Visible = false;
        var originalScene = MirScene.ActiveScene; MirScene.ActiveScene = scene;
        var targets = new List<MirControl> { scene.BeltDialog.Grid[0], scene.ChatDialog, scene.ChatControl.NormalButton };
        targets.Add(scene.Controls.First(c => Classic176HUDLayout.IsBottomExtension(c)).Controls.OfType<MirButton>().First());
        foreach (var target in targets)
        {
            map.OnMouseDown(new MouseEventArgs(MouseButtons.Right, 1, 800, 400, 0));
            CMain.MPoint = target.DisplayLocation.Add(new Point(Math.Max(1, target.Size.Width / 2), Math.Max(1, target.Size.Height / 2)));
            scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, CMain.MPoint.X, CMain.MPoint.Y, 0));
            user.QueuedAction = null; checkInput.Invoke(map, null);
            check(user.QueuedAction?.Action == MirAction.Running && MapControl.MouseLocation == CMain.MPoint,
                $"Run: map-origin right hold continues and refreshes direction through {target.GetType().Name} " +
                $"(hover={MirControl.MouseControl?.GetType().Name}, buttons={MapControl.MapButtons}, action={user.QueuedAction?.Action}, mouse={MapControl.MouseLocation}, pointer={CMain.MPoint})");
        }
        CMain.CMain_MouseUp(null!, new MouseEventArgs(MouseButtons.Right, 1, CMain.MPoint.X, CMain.MPoint.Y, 0));
        user.QueuedAction = null; checkInput.Invoke(map, null);
        check(MapControl.MapButtons == MouseButtons.None && user.QueuedAction == null,
            "Run: releasing right over the HUD stops the gesture without starting another movement");
        MapControl.MapButtons = MouseButtons.None;
        check(!BottomHudInput.CanContinue(scene, scene.BeltDialog.Grid[0], MapControl.MapButtons), "Run: an ordinary HUD right-click cannot start map movement");
        MapControl.MapButtons = MouseButtons.Right;
        typeof(CMain).GetMethod("CMain_Deactivate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { null!, EventArgs.Empty });
        check(MapControl.MapButtons == MouseButtons.None, "Run: focus loss clears the held map button");
        using (var popup = new MirControl { Parent = scene, Location = scene.ChatDialog.Location, Size = new Size(100, 60) })
        {
            CMain.MPoint = popup.DisplayLocation.Add(new Point(20, 20));
            scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, CMain.MPoint.X, CMain.MPoint.Y, 0));
            MapControl.MapButtons = MouseButtons.Right; user.QueuedAction = null; checkInput.Invoke(map, null);
            check(MirControl.MouseControl == popup && user.QueuedAction == null, "Run: an overlapping popup blocks movement instead of being treated as a bottom rectangle");
        }
        check(!BottomHudInput.CanContinue(scene, scene.BeltDialog, MouseButtons.Left), "Run: left clicks retain their normal HUD behavior");
        MapControl.MapButtons = MouseButtons.None; MirScene.ActiveScene = originalScene;

        check(LocalNightVisibility.Range(3, lights.Count, true, LightSetting.Night, false) == 5,
            "Night: the local player's normal radius grows by two grades from the original");
        check(LocalNightVisibility.Range(3, lights.Count, false, LightSetting.Night, false) == 3 &&
            LocalNightVisibility.Range(3, lights.Count, true, LightSetting.Day, false) == 3 &&
            LocalNightVisibility.Range(3, lights.Count, true, LightSetting.Night, true) == 3,
            "Night: other objects, daytime and blindness retain their original radius");
        check(LocalNightVisibility.Range(lights.Count - 1, lights.Count, true, LightSetting.Night, false) == lights.Count - 1,
            "Night: the largest equipped light stays inside the texture table");
        device.Clear(ClearFlags.Target, Color.Black, 0, 0); device.BeginScene(); sprite.Begin(SpriteFlags.AlphaBlend);
        sprite.Draw(lights[4], null, Vector3.Zero, new Vector3(90, 210, 0), Color.White);
        sprite.Draw(lights[5], null, Vector3.Zero, new Vector3(700, 180, 0), Color.White);
        using var before = new MirLabel { Text = "此前夜间光圈", Location = new Point(250, 120), AutoSize = true };
        using var after = new MirLabel { Text = "再次扩大后", Location = new Point(900, 120), AutoSize = true };
        before.Draw(); after.Draw(); sprite.End(); device.EndScene();
        Surface.ToFile(surface, Path.ChangeExtension(preview, "night.png"), ImageFileFormat.Png);
        var oldLight = lights[4].GetLevelDescription(0); var newLight = lights[5].GetLevelDescription(0);
        check(oldLight.Width == 525 && oldLight.Height == 399 && newLight.Width == 605 && newLight.Height == 460,
            "Night: actual base textures expand again from 525x399 to 605x460 without changing their gradient");
        typeof(ChineseUIOverlay).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { scene.NPCDropDialog.HoldButton });
        check(scene.NPCDropDialog.HoldButton.Hint.Contains("自动确认") &&
            scene.NPCDropDialog.HoldButton.Controls.SelectMany(c => c.Controls).OfType<MirLabel>().Any(l => l.Text == "连续"),
            "Shop: HOLD is translated as continuous processing with an automatic-confirmation hint");
        scene.NPCDropDialog.HoldButton.InvokeMouseClick(EventArgs.Empty);
        check(scene.NPCDropDialog.Hold, "Shop: the renamed button retains its original continuous-processing toggle");
        Console.WriteLine($"Task text and night previews: {questPreview}, {Path.ChangeExtension(preview, "night.png")}");
    }
}
