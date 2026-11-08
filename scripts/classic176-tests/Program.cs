using System.Reflection;
using System.Runtime.CompilerServices;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using SlimDX.Direct3D9;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length is < 3 or > 4) throw new ArgumentException("Pass repo root, client dependency directory, Prguse176.Lib and optional new preview PNG path.");
        args = args.Select(TestWorkspace.ResolvePath).ToArray();
        foreach (string file in new[] { "msvcr100.dll", "msvcp100.dll", "d3dx9_43.dll", "d3dcompiler_43.dll" })
            File.Copy(Path.Combine(args[1], file), Path.Combine(AppContext.BaseDirectory, file), true);
        Run(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string[] args)
    {
        string fixture = Path.Combine(TestWorkspace.RunDirectory, "fixture");
        Directory.CreateDirectory(Path.Combine(fixture, "Data"));
        Directory.CreateDirectory(Path.Combine(fixture, "Localization"));
        File.Copy(Path.Combine(args[0], "Client", "Localization", "Overlay.zh-CN.json"),
            Path.Combine(fixture, "Localization", "Overlay.zh-CN.json"));
        Directory.SetCurrentDirectory(fixture);
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        using var form = new Client.CMain(); // Hidden native textbox host; never shown or connected.
        typeof(GameScene).Assembly.GetType("Client.Program")!.GetField("Form")!.SetValue(null, form);
        Client.CMain.Graphics = graphics;
        Client.CMain.Cursors = Enumerable.Repeat(Cursors.Default, 256).ToArray();
        DbLanguage.Load(Path.Combine(args[0], "Localization"), "Chinese");
        var settings = typeof(GameScene).Assembly.GetType("Client.Settings")!;
        void Set(string name, object value) => settings.GetField(name)!.SetValue(null, value);
        int passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine($"PASS {++passed}: {name}"); }
        Set("FontName", "Microsoft YaHei"); Set("Resolution", 1366); Set("ScreenWidth", 1366); Set("ScreenHeight", 768);
        Set("UseClassic176HUD", true);
        using (var missing = new GameScene())
            Check(missing.MainDialog.Library == Libraries.Prguse && missing.BeltDialog.Movable, "missing library preserves the whole original HUD");

        File.Copy(args[2], Path.Combine(fixture, "Data", "Prguse176.Lib"));
        Libraries.Prguse176.Initialize();
        // Cache metadata sizes only; no renderer, socket, credentials or server needed.
        for (int i = 0; i < 1770; i++) Libraries.Prguse176.GetSize(i);
        var images = (Array)typeof(MLibrary).GetField("_images", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Libraries.Prguse176)!;
        foreach (MImage im in images) im.TrueSize = new Size(im.Width, im.Height);
        foreach (int width in new[] { 800, 1024, 1280, 1366, 1367 })
        {
            Set("Resolution", width); Set("ScreenWidth", width);
            using var scene = new GameScene();
            var main = scene.MainDialog;
            Check(main.Library == Libraries.Prguse176 && main.Size == new Size(800, 251) &&
                main.Location == new Point((width - 800) / 2, 517), $"{width}: native frame centered at screen bottom");
            int count = main.Controls.Count;
            Classic176HUDLayout.Apply176BottomHUD(scene);
            Check(count == main.Controls.Count, $"{width}: repeated hook creates no duplicate controls");
            Check(!main.HealthOrb.Visible && !main.ExperienceBar.Visible && !main.LeftCap.Visible && !main.RightCap.Visible,
                $"{width}: legacy orb, bars and caps hidden");
            foreach (var b in main.Controls.OfType<MirButton>().Where(b => b.Library == Libraries.Prguse176))
            {
                Check(b.Location.X >= 0 && b.Location.X + b.Size.Width <= 800 && b.Location.Y + b.Size.Height <= 251,
                    $"{width}: button {b.Controls.FirstOrDefault()?.GetType().Name} inside frame width");
                Client.CMain.MPoint = b.DisplayLocation.Add(new Point(b.Size.Width / 2, b.Size.Height / 2));
                scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, Client.CMain.MPoint.X, Client.CMain.MPoint.Y, 0));
                Check(MirControl.MouseControl == b, $"{width}: stone button reachable through scene mouse routing");
            }
            var panels = scene.Controls.Where(c => c.GetType().Name is "ExtensionDock" or "StoneWing").ToArray();
            var dockButtons = panels.SelectMany(p => p.Controls.OfType<MirButton>()).ToArray();
            bool split = width >= 992;
            Check(dockButtons.Length == 13 && dockButtons.SelectMany(b => b.Controls.OfType<MirLabel>()).Count(l =>
                l.Text is "属性" or "商城" or "菜单" or "英雄") == 4,
                $"{width}: all thirteen actions remain accessible with one entry per migrated action");
            if (split)
            {
                Check(panels.Length == 2 && panels.All(p => p.Size.Height == 144 && p.Location.Y == 624 && p.IsMouseOver(p.DisplayLocation.Add(new Point(1, 1)))) &&
                    panels[0].DisplayRectangle.Right == main.DisplayRectangle.Left && panels[1].DisplayRectangle.Left == main.DisplayRectangle.Right &&
                    panels[0].Location.X == 0 && panels[1].DisplayRectangle.Right == width,
                    $"{width}: two stone wings fill both side gaps exactly and remain mouse reachable");
                Check(panels[0].Controls.OfType<MirButton>().Count() == 7 && panels[1].Controls.OfType<MirButton>().Count() == 6 &&
                    panels.All(p => p.GetType().Name == "StoneWing"),
                    $"{width}: character/social actions are left, hero/travel actions are right, center dock removed");
                Check(dockButtons.All(b => b.DisplayRectangle.Bottom <= b.Parent.DisplayLocation.Y + 108),
                    $"{width}: compact rows do not cover the status wells");
                Check(panels.All(p => !p.IsMouseOver(new Point(p.Location.X + p.Size.Width / 2, p.Location.Y - 1))),
                    $"{width}: transparent map area above the wings has no wing hit target");
            }
            else Check(panels.Length == 1 && panels[0].GetType().Name == "ExtensionDock" && panels[0].Size == new Size(280, 44),
                $"{width}: narrow viewport retains compact action dock without clipped side controls");
            Check(dockButtons.All(b => b.Parent.DisplayRectangle.Contains(b.DisplayRectangle)),
                $"{width}: every action stays inside its parent's hit rectangle");
            Check(panels.All(p =>
            {
                var buttons = p.Controls.OfType<MirButton>().ToArray();
                return buttons.SelectMany((a, i) => buttons.Skip(i + 1).Select(b => !a.DisplayRectangle.IntersectsWith(b.DisplayRectangle))).All(ok => ok);
            }), $"{width}: wing buttons never overlap each other");
            Check(dockButtons.All(b => b.Controls.OfType<MirLabel>().Where(l => !string.IsNullOrEmpty(l.Text)).All(l => l.Size == b.Size)),
                $"{width}: visible dock captions fit the actual clickable button dimensions");
            foreach (var b in dockButtons)
            {
                Client.CMain.MPoint = b.DisplayLocation.Add(new Point(b.Size.Width / 2, b.Size.Height / 2));
                scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, Client.CMain.MPoint.X, Client.CMain.MPoint.Y, 0));
                Check(MirControl.MouseControl == b, $"{width}: hero dock reachable above main frame");
            }
            var chat = scene.ChatDialog;
            for (int size = 0; size < 3; size++)
            {
                Check(chat.Size == new Size(388, 126 + size * 48) && chat.DisplayRectangle.Bottom == 761,
                    $"{width}: chat size {size} retains its bottom anchor");
                Check(scene.ChatControl.DisplayRectangle.Bottom == chat.DisplayRectangle.Top &&
                    scene.ChatControl.Size.Height == 14 && (split ? panels.All(p => p.Location.Y == 624) : panels[0].DisplayRectangle.Bottom < scene.BeltDialog.DisplayRectangle.Top),
                    $"{width}: chat channel strip avoids messages and wing/fallback placement remains correct at size {size}");
                Check(chat.ChatTextBox.Location.X + chat.ChatTextBox.Size.Width <= chat.Size.Width &&
                    chat.ChatTextBox.Location.Y + chat.ChatTextBox.Size.Height <= chat.Size.Height,
                    $"{width}: chat input {size} stays inside frame");
                chat.ReceiveChat("测试中文聊天换行，包含足够长的文字用于确认聊天行不会越过右侧的石雕按钮。这是经典底栏适配回归测试。", ChatType.Normal);
                Check(chat.ChatLines.All(l => l.Size.Width <= chat.Size.Width - 14), $"{width}: Chinese chat wraps within new width");
                chat.UpdateBackground(); chat.ChangeSize();
            }
            Check(chat.WindowSize == 0 && scene.ChatControl.Location.Y == chat.Location.Y - 14,
                $"{width}: chat expansion cycle restores control bar position");
            for (int i = 0; i < 30; i++) chat.ReceiveChat($"历史消息{i}", ChatType.Normal);
            chat.StartIndex = 10; chat.Update();
            scene.ChatControl.SizeButton.InvokeMouseClick(EventArgs.Empty);
            Check(chat.StartIndex == 10 && chat.PositionBar.Location.Y > 16,
                $"{width}: clicking size preserves scrolled history and thumb position");
            chat.WindowSize = 0; chat.UpdateBackground(); chat.Update();
            scene.HeroBeltDialog = new HeroBeltDialog { Parent = scene, Visible = false };
            Classic176HUDLayout.AlignHeroBelt(scene);
            Check(scene.HeroBeltDialog.DisplayRectangle.Bottom < scene.BeltDialog.DisplayRectangle.Top &&
                (split || scene.HeroBeltDialog.DisplayRectangle.Bottom < panels[0].DisplayRectangle.Top),
                $"{width}: late-created hero belt sits above player belt and chat");
            var belt = scene.BeltDialog;
            Check(!belt.DrawControlTexture && !belt.Border && !belt.CloseButton.Visible && belt.Grid.All(c => !c.Border),
                $"{width}: native belt artwork has no opaque cover or extra close button");
            int[] nativeSlotX = [285, 328, 372, 415, 459, 503];
            Check(belt.Grid.Select((cell, i) => cell.DisplayLocation == main.DisplayLocation.Add(new Point(nativeSlotX[i], 59))).All(ok => ok),
                $"{width}: six items align to measured native slot coordinates");
            Check(main.Controls.Any(c => c.Location == new Point(40, 91) && c.Size == new Size(92, 90)),
                $"{width}: red-blue sprite aligns to the measured orb socket");
            Check(new[] { main.CharacterButton, main.InventoryButton, main.SkillButton, main.OptionButton }.All(b => !b.DrawControlTexture && !b.Border && b.Controls.OfType<MirLabel>().All(l => string.IsNullOrEmpty(l.Text))),
                $"{width}: four round icons retain native art without black text plaques");
            Check(chat.FullHistory.All(line => line.BackColour == Color.Transparent && line.ForeColour == Color.White),
                $"{width}: classic normal chat uses readable white text without opaque white blocks");
            Check(chat.DisplayRectangle.Contains(new Rectangle(main.Location.X + 207, main.Location.Y + 118, 388, 112)) &&
                chat.ChatTextBox.DisplayRectangle == new Rectangle(main.Location.X + 208, main.Location.Y + 232, 387, 12),
                $"{width}: chat and input cover measured white artwork apertures");
            Point beltPosition = belt.Location;
            belt.Flip();
            Check(belt.Location == beltPosition && !belt.RotateButton.Visible && !belt.Movable && belt.Grid.Length == 6,
                $"{width}: fixed six-slot belt cannot rotate out of its frame");
            Check(belt.Grid.All(c => c.Location.X >= 0 && c.Location.X + c.Size.Width < belt.Size.Width),
                $"{width}: all inventory cells remain inside belt hit area");
            GameScene.User = new UserObject((uint)width) { Name = "HUD测试", Level = 30, HP = 50, MP = 30, MaxExperience = 100 };
            GameScene.User.Stats[Stat.HP] = 100; GameScene.User.Stats[Stat.MP] = 60; GameScene.User.Stats[Stat.BagWeight] = 100;
            GameScene.User.CurrentBagWeight = 141;
            main.Process();
            Check(main.LevelLabel.Location.X > 664 && main.LevelLabel.Location.Y == 143 &&
                (main.LevelLabel.DrawFormat & TextFormatFlags.HorizontalCenter) == 0 && !main.LevelLabel.AutoSize,
                $"{width}: level digits sit left-aligned beyond the baked Lv caption");
            var header = main.Controls.OfType<MirButton>().Where(b => b.Controls.OfType<MirLabel>().Any(l => l.Text is "属性" or "商城" or "英雄")).ToArray();
            Check(header.Length == 0 && main.MenuButton.Parent == panels[0] && main.GameShopButton.Parent == panels[0] &&
                dockButtons.Contains(main.QuestButton),
                $"{width}: right stone gauges are clear and native menu/shop/quest handlers are retained");
            var weight = main.Controls.OfType<MirLabel>().Single(l => l.Location.Y == 209);
            Check(weight.Location.X == main.LevelLabel.Location.X && main.GoldLabel.Location.X == weight.Location.X &&
                weight.DrawFormat == main.LevelLabel.DrawFormat && main.GoldLabel.DrawFormat == weight.DrawFormat,
                $"{width}: level, bag weight and gold share a left-aligned numeric baseline");
            Check(!main.HealthOrb.Visible && main.CharacterName.Text == "HUD测试", $"{width}: normal status processing preserves adapted HUD");
            // Existing popup actions and absent/disposed hero guards.
            foreach (string caption in new[] { "英包", "英装" })
            {
                var button = dockButtons.Single(b => b.Controls.OfType<MirLabel>().Any(l => l.Text == caption));
                button.Sound = SoundList.None;
                button.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
            }
            Check(true, $"{width}: hero inventory/equipment safely ignore absent hero");
            var mode = dockButtons.Single(b => b.Controls.OfType<MirLabel>().Any(l => l.Text == "英雄"));
            mode.Sound = SoundList.None;
            mode.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
            Check(!scene.HeroMenuPanel.Visible, $"{width}: mode menu stays closed without live hero dialogs");
            main.MenuButton.Sound = SoundList.None;
            bool menuWasVisible = scene.MenuDialog.Visible;
            main.MenuButton.InvokeMouseClick(EventArgs.Empty);
            Check(scene.MenuDialog.Visible != menuWasVisible, $"{width}: migrated menu button keeps its original toggle action");
        }
        Set("UseClassic176HUD", false);
        using (var original = new GameScene())
            Check(original.MainDialog.Library == Libraries.Prguse && original.BeltDialog.Movable, "disabled setting preserves original HUD even with resource installed");
        var rect = new Rectangle(46, 0, 46, 90);
        Check(Classic176HUDLayout.FillSection(rect, 50, 100, true) == new Rectangle(46, 45, 46, 45), "half-full MP clips from bottom with correct source X");
        Check(Classic176HUDLayout.FillSection(rect, -1, 100, true).Height == 0, "negative HP is empty");
        Check(Classic176HUDLayout.FillSection(rect, 200, 100, true) == rect, "over-max HP clamps to full");
        Check(Classic176HUDLayout.FillSection(rect, 10, 0, true).Height == 0, "zero max HP is empty without division error");
        Check(Classic176HUDLayout.FillSection(new Rectangle(0, 0, 76, 13), 25, 100, false).Width == 19, "experience fill uses horizontal percentage");
        Console.WriteLine($"Classic HUD: {passed} checks passed; fixture: {fixture}");
        if (args.Length == 4)
        {
            if (File.Exists(args[3])) throw new IOException("Preview already exists.");
            Set("UseClassic176HUD", true); Set("ScreenWidth", 1366); Set("Resolution", 1366); Set("FullScreen", false);
            var dx = typeof(GameScene).Assembly.GetType("Client.MirGraphics.DXManager")!;
            dx.GetMethod("Create")!.Invoke(null, null);
            foreach (string name in new[] { "Prguse", "Prguse2", "Prguse3", "UI_32bit" })
            {
                File.Copy(Path.Combine(args[1], "Data", name + ".Lib"), Path.Combine(fixture, "Data", name + ".Lib"));
                ((MLibrary)typeof(Libraries).GetField(name)!.GetValue(null)!).Initialize();
            }
            using var scene = new GameScene();
            GameScene.User = new UserObject(9000) { Name = "经典底栏测试", Level = 30, HP = 72, MP = 36, Experience = 25, MaxExperience = 100 };
            GameScene.User.Stats[Stat.HP] = 100; GameScene.User.Stats[Stat.MP] = 60; GameScene.User.Stats[Stat.BagWeight] = 100;
            scene.MainDialog.Process();
            scene.ChatDialog.ReceiveChat("十周年石雕界面 · 聊天与六格药水栏测试", ChatType.Normal);
            scene.ChatDialog.ReceiveChat("独立 Prguse176 图库，保留原生英雄和坐骑窗口。", ChatType.System);
            var device = (Device)dx.GetField("Device")!.GetValue(null)!;
            var sprite = (Sprite)dx.GetField("Sprite")!.GetValue(null)!;
            for (int size = 0; size < 3; size++)
            {
                device.Clear(ClearFlags.Target, Color.FromArgb(30, 38, 30), 0, 0);
                device.BeginScene(); sprite.Begin(SpriteFlags.AlphaBlend);
                scene.MainDialog.Draw(); scene.ChatDialog.Draw(); scene.ChatControl.Draw(); scene.BeltDialog.Draw();
                foreach (var panel in scene.Controls.Where(c => c.GetType().Name is "ExtensionDock" or "StoneWing")) panel.Draw();
                sprite.End(); device.EndScene();
                var textureSize = (Size)typeof(MirControl).GetField("TextureSize", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scene.ChatDialog)!;
                Check(textureSize == scene.ChatDialog.Size, $"Direct3D: chat size {size} recreates texture at the new dimensions");
                Check(scene.ChatControl.Controls.OfType<MirButton>().Where(b => b.Visible).All(b =>
                    scene.ChatControl.DisplayRectangle.Contains(b.DisplayRectangle)),
                    $"Direct3D: loaded channel icons stay within the lowered strip at chat size {size}");
                string preview = size == 0 ? args[3] : Path.ChangeExtension(args[3], $"size{size}.png");
                Surface.ToFile((Surface)dx.GetField("MainSurface")!.GetValue(null)!, preview, ImageFileFormat.Png);
                scene.ChatDialog.ChangeSize();
            }
            Console.WriteLine($"Direct3D HUD preview (no map or login): {args[3]}");
            var npc = scene.NPCDialog;
            var npcLines = new List<string>
            {
                "欢迎测试",
                "<一键安全回收杂物/@SAFE>",
                "前缀 <装备/@GEAR> 后缀 {红色/Red}",
                "<补给/@SUPPLY>   <特修/@REPAIR>",
                "坐标 [NPC:1|传送向导] 和 [ITEM:1|金创药]",
                "(帮助/http://example.invalid)",
                "Test <A&B/@EN> end"
            };
            npc.NewText(new List<string>(npcLines));
            npc.Location = new Point(10, 10); npc.Visible = true;
            Check(npc.TextLabel[1].Text == "一键安全回收杂物" && !npc.TextLabel[1].DrawControlTexture &&
                npc.TextLabel[0].DrawControlTexture, "NPC: complete service metadata remains, styled base glyphs are not redrawn");
            Check(npc.TextLabel[2].Controls.OfType<MirLabel>().Select(l => l.Text).SequenceEqual(new[] { "前缀 ", " 后缀 " }),
                "NPC: normal prefix and suffix remain visible around multiple styled spans");
            Check(npc.TextButtons.Count == 9 && npc.TextButtons.All(b => b.Text.Length > 0 && b.DrawControlTexture),
                "NPC: actions, colors, entity links and web links each have one visible styled run");
            Check(npc.TextButtons.All(b => b.DrawFormat.HasFlag(TextFormatFlags.NoPrefix)),
                "NPC: ampersands in English options remain literal visible characters");
            var yellow = npc.TextButtons[0];
            typeof(MirControl).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(yellow, null);
            Check(yellow.ForeColour == Color.Red, "NPC: original option hover event remains attached");
            typeof(MirControl).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(yellow, null);
            Check(yellow.ForeColour == Color.Yellow, "NPC: leaving the option restores yellow without another text layer");
            Client.CMain.MPoint = yellow.DisplayLocation.Add(new Point(yellow.Size.Width / 2, yellow.Size.Height / 2));
            scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, Client.CMain.MPoint.X, Client.CMain.MPoint.Y, 0));
            Check(MirControl.MouseControl == yellow, "NPC: original clickable option remains reachable through scene routing");
            Client.CMain.MPoint = new Point(1000, 400);
            scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 1000, 400, 0));
            typeof(MirControl).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(yellow, null);
            device.Clear(ClearFlags.Target, Color.FromArgb(30, 38, 30), 0, 0);
            device.BeginScene(); sprite.Begin(SpriteFlags.AlphaBlend);
            npc.Draw(); sprite.End(); device.EndScene();
            string npcPreview = Path.ChangeExtension(args[3], "npc.png");
            Surface.ToFile((Surface)dx.GetField("MainSurface")!.GetValue(null)!, npcPreview, ImageFileFormat.Png);
            using (var rendered = new Bitmap(npcPreview))
            {
                int white = 0, yellowInk = 0;
                for (int y = yellow.DisplayRectangle.Top; y < yellow.DisplayRectangle.Bottom; y++)
                    for (int x = yellow.DisplayRectangle.Left; x < yellow.DisplayRectangle.Right; x++)
                    {
                        var pixel = rendered.GetPixel(x, y);
                        if (pixel.R > 190 && pixel.G > 190 && pixel.B > 190) white++;
                        if (pixel.R > 180 && pixel.G > 150 && pixel.B < 60) yellowInk++;
                    }
                Check(yellowInk > 20 && white == 0, $"NPC: rendered option contains yellow ink and no duplicate white glyphs ({yellowInk} yellow, {white} white pixels)");
            }
            var oldLine = npc.TextLabel[2];
            var oldRuns = oldLine.Controls.ToArray();
            int controlCount = npc.Controls.Count;
            for (int i = 0; i < 20; i++) npc.NewText(new List<string>(npcLines));
            Check(oldLine.IsDisposed && oldRuns.All(l => l.IsDisposed) && npc.Controls.Count == controlCount,
                "NPC: repeated page refresh disposes old runs without accumulating or repainting stale text");
            npc.NewText(new List<string> { "提示 {红字/Red}" + new string('中', 40) + "<越界选项/@OFFSCREEN>" });
            Check(npc.TextLabel[0].Controls.Where(c => c.Visible).All(c => npc.TextLabel[0].DisplayRectangle.Contains(c.DisplayRectangle)) &&
                npc.TextButtons.Where(c => c.Visible).All(c => npc.TextLabel[0].DisplayRectangle.Contains(c.DisplayRectangle)) &&
                !npc.TextButtons.Last().Visible,
                "NPC: long formatted lines clip to the original text area and offscreen options have no hit target");
            using (var dpiBitmap = new Bitmap(1, 1))
            {
                dpiBitmap.SetResolution(144, 144);
                using var dpiGraphics = Graphics.FromImage(dpiBitmap);
                Client.CMain.Graphics = dpiGraphics;
                try
                {
                    npc.NewText(new List<string> { "前缀 <装备/@GEAR> 后缀" });
                    Check(npc.TextLabel[0].Controls.OfType<MirLabel>().All(l => Math.Abs(l.Font.Size - npc.TextButtons[0].Font.Size) < .01),
                        "NPC: normal and styled text use the same effective font at 150 percent DPI");
                }
                finally { Client.CMain.Graphics = graphics; }
            }
            Console.WriteLine($"NPC single-pass text preview: {npcPreview}");
            GameplayPolishChecks.Run(scene, Check, args[3], device, sprite,
                (Surface)dx.GetField("MainSurface")!.GetValue(null)!, (List<Texture>)dx.GetField("Lights")!.GetValue(null)!);
            Console.WriteLine($"Total checks including rendering: {passed}");
        }
    }
}
