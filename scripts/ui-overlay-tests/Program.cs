using System.Reflection;
using System.Runtime.CompilerServices;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Pass repository root and a client dependency directory (msvcr100.dll/msvcp100.dll). No game data is required.");
        foreach (string file in new[] { "msvcr100.dll", "msvcp100.dll", "d3dx9_43.dll", "d3dcompiler_43.dll" })
            File.Copy(Path.Combine(args[1], file), Path.Combine(AppContext.BaseDirectory, file), true);
        Run(Path.GetFullPath(args[0]));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string root)
    {
        string fixture = Path.Combine(Path.GetTempPath(), "ai", DateTime.Now.ToString("yyyyMMdd"), "mir2-ui-overlay-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "Localization"));
        File.Copy(Path.Combine(root, "Client", "Localization", "Overlay.zh-CN.json"), Path.Combine(fixture, "Localization", "Overlay.zh-CN.json"));
        Directory.SetCurrentDirectory(fixture);
        File.WriteAllText("Mir2Config.ini", "[Game]\nAutoPickUp=True\nSmartShift=True\nLanguage=Chinese\n");
        using var measurementBitmap = new Bitmap(1, 1);
        using var measurementGraphics = Graphics.FromImage(measurementBitmap);
        Client.CMain.Graphics = measurementGraphics;
        DbLanguage.Load(Path.Combine(root, "Localization"), "Chinese");
        var method = typeof(ChineseUIOverlay).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!;
        void Prepare(MirImageControl control) => method.Invoke(null, [control]);
        int passed = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine($"PASS {++passed}: {name}"); }
        MirButton Button(int index) => new() { AutoSize = false, Library = Libraries.Title, Index = index, Size = new Size(76, 25), Sound = SoundList.None };
        using var button = Button(200);
        int before = button.Controls.Count;
        Prepare(button);
        var plaque = button.Controls.Last();
        var label = plaque.Controls.OfType<MirLabel>().Single();
        Check(label.Text == "确定" && button.Controls.Count == before + 1, "lazy dialog button receives its Chinese art mask");
        for (int i = 0; i < 100; i++) Prepare(button);
        Check(button.Controls.Count == before + 1 && ReferenceEquals(button.Controls.Last(), plaque), "repeated rendering does not duplicate overlays");
        Check(!plaque.IsMouseOver(new Point(12, 12)) && !label.IsMouseOver(new Point(12, 12)), "mask and label pass mouse hit testing through");
        int clicks = 0; button.Click += (_, _) => clicks++;
        button.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 12, 12, 0));
        Check(clicks == 1, "the original button click handler still runs");
        MirControl.MouseControl = button; MirControl.ActiveControl = button; Prepare(button);
        Check(label.Location == new Point(1, 1), "pressed button text follows the original active button");
        button.Enabled = false; Prepare(button);
        Check(label.ForeColour == Color.Gray && label.Location == Point.Empty, "disabled state resets press offset and dims the label");
        button.Enabled = true; MirControl.ActiveControl = null; MirControl.MouseControl = null;
        button.Index = 203; Prepare(button);
        Check(plaque.IsDisposed && button.Controls.Last().Controls.OfType<MirLabel>().Single().Text == "取消", "dynamic art changes replace the old mask and dispose its label");
        using var icon = new MirButton { AutoSize = false, Size = new Size(16, 16), Library = Libraries.Prguse, Index = 1970 };
        before = icon.Controls.Count; Prepare(icon);
        Check(icon.Controls.Count == before, "pure icons remain visible without a text plaque");
        using var role = new CharacterDialog(MirGridType.Equipment, new UserObject());
        role.NameLabel.Text = "Alice张三";
        Prepare(role); Prepare(role.StatusPage); Prepare(role.StatePage);
        var tabs = role.Controls.Where(c => c.GetType().Name == "Plaque").ToArray();
        Check(tabs.Length == 4 && tabs.All(p => !p.DisplayRectangle.IntersectsWith(role.NameLabel.DisplayRectangle)) && role.NameLabel.Text == "Alice张三", "character tabs preserve player name and guild header");
        var attributes = role.StatusPage.Controls.Where(c => c.GetType().Name == "Plaque").ToArray();
        var states = role.StatePage.Controls.Where(c => c.GetType().Name == "Plaque").ToArray();
        Check(attributes.Length == 13 && states.Length == 12 && attributes.Concat(states).All(p => p.Location.X + p.Size.Width <= 120), "all 25 character attribute captions stay left of numeric values");
        using var hero = new CharacterDialog(MirGridType.HeroEquipment, new UserObject());
        Prepare(hero); Prepare(hero.StatusPage); Prepare(hero.StatePage);
        Check(hero.Controls.Count(c => c.GetType().Name == "Plaque") == 4 && hero.StatusPage.Controls.Count(c => c.GetType().Name == "Plaque") == 13, "hero uses the same tabs and stat overlays");
        DbLanguage.Load(Path.Combine(root, "Localization"), "English"); Prepare(button); Prepare(role);
        Check(!button.Controls.Last().Visible && tabs.All(p => !p.Visible), "English language hides previously attached overlays");
        using var english = Button(200); before = english.Controls.Count; Prepare(english);
        Check(english.Controls.Count == before, "fresh English controls retain their original art");
        DbLanguage.Load(Path.Combine(root, "Localization"), "Chinese"); Prepare(button);
        var last = button.Controls.Last(); button.Dispose();
        Check(last.IsDisposed, "original owner disposal releases overlay controls and fonts");

        var settings = typeof(OptionDialog).Assembly.GetType("Client.Settings")!;
        settings.GetMethod("Load")!.Invoke(null, null);
        Check(settings.GetField("AutoPickUp") == null && typeof(OptionDialog).GetField("AutoPickUpBox") == null
            && File.ReadAllText("Mir2Config.ini").Contains("AutoPickUp=True"),
            "legacy AutoPickUp=true is harmless: the setting and standalone option no longer exist");
        var sellSetting = settings.GetField("AutoSellNormalEquipment")!;
        var repairSetting = settings.GetField("AutoSpecialRepairEquipment")!;
        sellSetting.SetValue(null, true); repairSetting.SetValue(null, true);
        using var options = new OptionDialog();
        var services = options.Controls.OfType<NpcAutoServiceOptions>().Single();
        Check(services.AutoSellBox.Checked && services.AutoRepairBox.Checked, "NPC option switches reflect the existing enabled settings");
        Check(services.Location.Y >= 350 && services.Controls.All(c => c.Location.Y >= 0 && c.Location.Y + c.Size.Height <= services.Size.Height)
            && services.Location.Y + services.Size.Height <= options.Size.Height, "NPC options extend below the original art and stay inside the dialog hit area");
        services.AutoSellBox.Sound = SoundList.None; services.AutoRepairBox.Sound = SoundList.None;
        services.AutoSellBox.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
        string config = File.ReadAllText("Mir2Config.ini");
        Check(!(bool)sellSetting.GetValue(null)! && config.Contains("AutoSellNormalEquipment=False"), "selling switch updates its runtime setting and saves immediately");
        Check((bool)repairSetting.GetValue(null)! && services.AutoRepairBox.Checked, "selling switch does not change repair preference");
        services.AutoRepairBox.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
        config = File.ReadAllText("Mir2Config.ini");
        Check(!(bool)repairSetting.GetValue(null)! && config.Contains("AutoSpecialRepairEquipment=False"), "special repair switch updates its runtime setting and saves immediately");
        sellSetting.SetValue(null, true); repairSetting.SetValue(null, true);
        options.Hide(); options.Show();
        Check(services.AutoSellBox.Checked && services.AutoRepairBox.Checked, "reopening options synchronizes changed preferences");
        Check(services.AutoSellBox.Hint.Contains("跳过") && services.AutoRepairBox.Hint.Contains("不做普通修理"), "Chinese tooltips explain sale protection and special-repair-only policy");
        DbLanguage.Load(Path.Combine(root, "Localization"), "English");
        using var englishOptions = new OptionDialog();
        var englishServices = englishOptions.Controls.OfType<NpcAutoServiceOptions>().Single();
        Check(englishServices.AutoSellBox.Controls.OfType<MirLabel>().Any(l => l.Text == "Auto-sell normal equipment")
            && englishServices.AutoRepairBox.Hint.Contains("never use normal repair"), "English options use English labels and repair policy tooltip");
        Check(englishServices.AutoSellBox.Controls.Concat(englishServices.AutoRepairBox.Controls).OfType<MirLabel>()
            .All(l => l.Location.X + l.Size.Width <= englishServices.AutoSellBox.Size.Width), "English switch labels fit within their clickable rows");
        options.Dispose();
        Check(services.IsDisposed && services.AutoSellBox.IsDisposed && services.AutoRepairBox.IsDisposed, "closing the option owner disposes the custom panel and switches");
        Console.WriteLine($"All {passed} UI overlay checks passed. No game resources, account or Direct3D device used.");
    }
}
