using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using C = ClientPackets;

namespace Client.MirControls;

/// <summary>Ten-year client art, isolated from Crystal's popup libraries and actions.</summary>
public static class Classic176HUDLayout
{
    public const int FrameIndex = 1, OrbIndex = 4, ExperienceIndex = 7;
    public static readonly Size FrameSize = new(800, 251);
    public static readonly Point OrbLocation = new(40, 91);
    public static readonly Point ChatLocation = new(207, 118);
    public static readonly Size ChatSize = new(388, 126);
    public const int WingHeight = 144;
    private static readonly int[] BeltSlots = [5, 48, 92, 135, 179, 223];
    private static readonly Color Bronze = Color.FromArgb(240, 32, 27, 21);
    private static readonly Color Ink = Color.FromArgb(235, 216, 173);

    public static bool IsClassicChat(ChatDialog chat) => chat?.Library == Libraries.Prguse176;
    public static bool IsClassicBelt(BeltDialog belt) => belt?.Parent is GameScene scene &&
        scene.MainDialog.Library == Libraries.Prguse176;

    public static void Apply176BottomHUD(GameScene scene)
    {
        if (!Settings.UseClassic176HUD || scene?.MainDialog == null ||
            scene.MainDialog.Library == Libraries.Prguse176 || Settings.ScreenWidth < FrameSize.Width) return;
        // Missing or unrelated resource: leave the entire original HUD untouched.
        if (Libraries.Prguse176.GetSize(FrameIndex) != FrameSize ||
            Libraries.Prguse176.GetSize(OrbIndex) != new Size(92, 90) ||
            Libraries.Prguse176.GetSize(ExperienceIndex) != new Size(76, 13)) return;

        var main = scene.MainDialog;
        main.AutoSize = false;
        main.Library = Libraries.Prguse176;
        main.Index = FrameIndex;
        main.Size = FrameSize;
        main.UseClassicHitArea();
        main.Location = new Point((Settings.ScreenWidth - FrameSize.Width) / 2, Settings.ScreenHeight - FrameSize.Height);
        foreach (var control in new MirControl[] { main.HealthOrb, main.ExperienceBar, main.WeightBar,
            main.LeftCap, main.RightCap, main.WeightLabel, main.SpaceLabel }) control.Visible = false;

        // The ten-year file contains a single red/blue sphere. Clip its two halves independently.
        var orb = new MirControl { Parent = main, Location = OrbLocation, Size = new Size(92, 90), NotControl = true };
        orb.AfterDraw += (_, _) =>
        {
            var user = MapObject.User;
            if (user == null) return;
            DrawFill(OrbIndex, new Rectangle(0, 0, 46, 90), orb.DisplayLocation, user.HP, user.Stats[Stat.HP], true);
            DrawFill(OrbIndex, new Rectangle(46, 0, 46, 90), orb.DisplayLocation, user.MP, user.Stats[Stat.MP], true);
            DrawFill(ExperienceIndex, new Rectangle(0, 0, 76, 13),
                main.DisplayLocation.Add(new Point(663, 179)), user.Experience, user.MaxExperience, false);
        };
        var health = Label(main, new Point(10, 208), new Size(155, 16), "");
        health.BeforeDraw += (_, _) =>
        {
            if (MapObject.User is { } user) health.Text = $"HP {user.HP}/{user.Stats[Stat.HP]}  MP {user.MP}/{user.Stats[Stat.MP]}";
        };
        main.LevelLabel.AutoSize = false;
        main.LevelLabel.Location = new Point(666, 143);
        main.LevelLabel.Size = new Size(33, 18);
        main.LevelLabel.DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        main.CharacterName.Location = new Point(26, 232);
        main.CharacterName.Size = new Size(130, 16);
        main.GoldLabel.Location = new Point(665, 231);
        main.GoldLabel.Size = new Size(120, 16);
        main.AModeLabel.Location = new Point(8, -52);
        main.PModeLabel.Location = new Point(8, -36);
        main.SModeLabel.Location = new Point(8, -20);

        // Keep Crystal's original event handlers (including popup validation).
        RoundSeat(main.CharacterButton, 8, 643, 61);
        RoundSeat(main.InventoryButton, 9, 682, 41);
        RoundSeat(main.SkillButton, 10, 722, 21);
        RoundSeat(main.OptionButton, 11, 764, 11);
        StoneButton(main.MenuButton, "菜单", 752, 100);
        SizeButton(main.MenuButton, new Size(32, 30));
        var status = Button(main, "属性", new Point(630, 108), new Size(36, 22), () =>
        {
            scene.CharacterDialog.Show();
            scene.CharacterDialog.StatusButton.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0));
        }, false);
        status.Hint = "人物属性";
        // Retain the shop and quest entries in the right-hand top strip.
        StoneButton(main.GameShopButton, "商城", 670, 108);
        SizeButton(main.GameShopButton, new Size(36, 22));
        Button(main, "英雄", new Point(710, 108), new Size(36, 22), () =>
        {
            if (GameScene.Hero != null && scene.HeroDialog is { IsDisposed: false } &&
                scene.HeroInventoryDialog is { IsDisposed: false }) scene.HeroMenuPanel.Toggle();
        }, false);
        var weight = Label(main, new Point(665, 205), new Size(76, 16), "");
        weight.BeforeDraw += (_, _) =>
        {
            if (MapObject.User is { } user) weight.Text = $"{user.CurrentBagWeight}/{user.Stats[Stat.BagWeight]}";
        };

        // Original hero controls still receive server visibility updates, away from the stone buttons.
        main.HeroInfoPanel.Parent = scene;
        main.HeroInfoPanel.Location = main.Location.Add(new Point(0, -160));
        main.HeroMenuButton.Parent = scene;
        main.HeroMenuButton.Location = main.Location.Add(new Point(174, -54));
        main.HeroSummonButton.Parent = scene;
        main.HeroSummonButton.Location = main.Location.Add(new Point(174, -28));
        scene.HeroBehaviourPanel.Location = new Point(main.Location.X + 205, main.Location.Y - 160);
        scene.HeroMenuPanel.Location = new Point(main.Location.X + 205, main.Location.Y - 130);
        BuildExtensions(scene);

        scene.ChatDialog.AutoSize = false;
        scene.ChatDialog.Library = Libraries.Prguse176;
        scene.ChatDialog.Index = -1;
        scene.ChatDialog.DrawImage = false;
        scene.ChatDialog.UpdateBackground();
        var belt = scene.BeltDialog;
        belt.AutoSize = false;
        belt.DrawImage = false;
        belt.DrawControlTexture = false;
        belt.BackColour = Bronze;
        belt.Border = false;
        belt.BorderColour = Color.FromArgb(110, 92, 65);
        belt.Movable = false;
        belt.Size = new Size(260, 38);
        belt.Location = main.Location.Add(new Point(280, 56));
        belt.RotateButton.Visible = false; // The six slots are part of the fixed frame art.
        belt.CloseButton.Visible = false;
        for (int i = 0; i < 6; i++)
        {
            belt.Grid[i].Location = new Point(BeltSlots[i], 3);
            belt.Grid[i].Border = false;
            belt.Grid[i].BorderColour = Color.FromArgb(110, 92, 65);
            belt.Key[i].Location = new Point(BeltSlots[i], 0);
        }
    }

    private static void BuildExtensions(GameScene scene)
    {
        var main = scene.MainDialog;
        int leftWidth = main.Location.X;
        int rightX = leftWidth + FrameSize.Width;
        int rightWidth = Settings.ScreenWidth - rightX;
        bool split = Math.Min(leftWidth, rightWidth) >= 96;
        MirControl left = null, right = null;
        if (split)
        {
            left = new StoneWing { Parent = scene, Location = new Point(0, Settings.ScreenHeight - WingHeight), Size = new Size(leftWidth, WingHeight) };
            right = new StoneWing { Parent = scene, Location = new Point(rightX, Settings.ScreenHeight - WingHeight), Size = new Size(rightWidth, WingHeight) };
            WingCaption(left, "社交与地图");
            WingCaption(right, "英雄与旅途");
            var map = WingLine(left, 108);
            var coordinates = WingLine(left, 124);
            var hero = WingLine(right, 108);
            var stats = WingLine(right, 124);
            map.BeforeDraw += (_, _) => map.Text = DbLanguage.Get("Maps", scene.MapControl?.Title ?? "当前位置");
            coordinates.BeforeDraw += (_, _) =>
            {
                if (MapObject.User is { } user) coordinates.Text = $"坐标 [{user.CurrentLocation.X} : {user.CurrentLocation.Y}]";
            };
            hero.BeforeDraw += (_, _) => hero.Text = scene.HeroSpawnState switch
            {
                HeroSpawnState.Dead => "英雄已阵亡",
                HeroSpawnState.Summoned when GameScene.Hero != null => rightWidth >= 180
                    ? $"{GameScene.Hero.Name} · HP {GameScene.Hero.PercentHealth}%" : $"英雄 HP {GameScene.Hero.PercentHealth}%",
                _ => "英雄未出战"
            };
            stats.BeforeDraw += (_, _) =>
            {
                if (MapObject.User is not { } user) return;
                double exp = user.MaxExperience > 0 ? user.Experience * 100.0 / user.MaxExperience : 0;
                stats.Text = rightWidth >= 180
                    ? $"负重 {user.CurrentBagWeight}/{user.Stats[Stat.BagWeight]} · EXP {exp:F1}%" : $"EXP {exp:F1}%";
            };
        }
        else
        {
            // An 800px viewport has no side space. Keep all nine actions accessible.
            left = right = new ExtensionDock
            {
                Parent = scene, Size = new Size(348, 22), BackColour = Bronze, DrawControlTexture = true,
                Border = true, BorderColour = Color.FromArgb(110, 92, 65)
            };
        }
        string[] captions = ["交易", "组队", "行会", "地图", "召英", "英包", "英装", "任务", "坐骑"];
        Action[] actions = [
            () => Network.Enqueue(new C.TradeRequest()),
            () => Toggle(scene.GroupDialog),
            () => Toggle(scene.GuildDialog),
            () => scene.MiniMapDialog.Toggle(),
            () => Network.Enqueue(new C.Chat { Message = "@SUMMONHERO" }),
            () => { if (GameScene.Hero != null && scene.HeroInventoryDialog is { IsDisposed: false } dialog) Toggle(dialog); },
            () => { if (GameScene.Hero != null && scene.HeroDialog is { IsDisposed: false } dialog) { if (dialog.Visible) dialog.Hide(); else { dialog.Show(); dialog.ShowCharacterPage(); } } },
            () => Toggle(scene.QuestLogDialog),
            () => Toggle(scene.MountDialog)
        ];
        for (int i = 0; i < captions.Length; i++)
        {
            MirControl parent = i < 4 ? left : right;
            int localIndex = i < 4 ? i : i - 4;
            int count = i < 4 ? 4 : 5;
            int columns = i < 4 || rightWidth < 232 ? 2 : 3;
            int buttonWidth = split ? Math.Min(76, (parent.Size.Width - 16 - 6 * (columns - 1)) / columns) : 36;
            int row = localIndex / columns;
            int rowItems = Math.Min(columns, count - row * columns);
            var point = split
                ? new Point((parent.Size.Width - rowItems * buttonWidth - (rowItems - 1) * 6) / 2 + localIndex % columns * (buttonWidth + 6), 30 + row * 26)
                : new Point(i * 38 + (i >= 4 ? 8 : 0), 0);
            var size = new Size(buttonWidth, split ? 24 : 22);
            MirButton button;
            if (i == 7)
            {
                button = main.QuestButton;
                button.Parent = parent;
                StoneButton(button, captions[i], point.X, point.Y, true);
                SizeButton(button, size);
            }
            else button = Button(parent, captions[i], point, size, actions[i]);
            button.Border = split;
        }
        if (!split)
            _ = new MirControl { Parent = left, Location = new Point(154, 4), Size = new Size(1, 14),
                BackColour = Color.FromArgb(110, 92, 65), DrawControlTexture = true, NotControl = true };
    }

    public static bool LayoutChat(ChatDialog chat)
    {
        if (!IsClassicChat(chat) || chat.Parent is not GameScene scene) return false;
        int extra = Math.Clamp(chat.WindowSize, 0, 2) * 48;
        Resize(chat, new Size(ChatSize.Width, ChatSize.Height + extra));
        chat.Location = scene.MainDialog.Location.Add(new Point(ChatLocation.X, ChatLocation.Y - extra));
        chat.DrawControlTexture = true;
        chat.BackColour = chat.Transparent ? Color.FromArgb(150, 18, 16, 14) : Color.FromArgb(255, 18, 16, 14);
        chat.LineCount = 6 + extra / 16;
        chat.ChatTextBox.Location = new Point(1, chat.Size.Height - 12);
        chat.ChatTextBox.Size = new Size(387, 12);
        chat.HomeButton.Location = new Point(374, 1);
        chat.UpButton.Location = new Point(374, 9);
        chat.DownButton.Location = new Point(374, chat.Size.Height - 29);
        chat.EndButton.Location = new Point(374, chat.Size.Height - 23);
        chat.CountBar.Location = new Point(378, 16);
        chat.CountBar.AutoSize = false;
        chat.CountBar.DrawImage = false;
        Resize(chat.CountBar, new Size(5, chat.Size.Height - 48));
        chat.CountBar.DrawControlTexture = true;
        chat.CountBar.BackColour = Bronze;
        chat.PositionBar.AutoSize = false;
        chat.PositionBar.DrawImage = false;
        chat.PositionBar.Size = new Size(10, 14);
        chat.PositionBar.DrawControlTexture = true;
        chat.PositionBar.BackColour = Color.FromArgb(110, 92, 65);
        chat.PositionBar.Location = new Point(376, 16);
        var bar = scene.ChatControl;
        bar.AutoSize = false;
        bar.DrawImage = false;
        bar.Size = new Size(ChatSize.Width, 18);
        bar.Location = new Point(chat.Location.X, chat.Location.Y - 18);
        bar.SizeButton.Location = new Point(338, 1);
        bar.SettingsButton.Location = new Point(362, 1);
        var belt = scene.BeltDialog;
        belt.Location = scene.MainDialog.Location.Add(new Point(280, 56 - extra));
        belt.DrawControlTexture = belt.Border = extra > 0;
        foreach (var cell in belt.Grid) cell.Border = extra > 0;
        var dock = scene.Controls.OfType<ExtensionDock>().FirstOrDefault();
        if (dock != null) dock.Location = scene.MainDialog.Location.Add(new Point(227, 22 - extra));
        AlignHeroBelt(scene);
        return true;
    }

    public static void AlignHeroBelt(GameScene scene)
    {
        if (scene.MainDialog.Library != Libraries.Prguse176 || scene.HeroBeltDialog is not { IsDisposed: false } belt) return;
        belt.Location = new Point(scene.MainDialog.Location.X + 460, scene.ChatControl.Location.Y - 47 - belt.Size.Height);
    }

    public static void AdaptChatColours(ChatDialog chat, ChatType type, ref Color background, ref Color foreground)
    {
        if (!IsClassicChat(chat)) return;
        background = Color.Transparent;
        foreground = type switch
        {
            ChatType.Hint or ChatType.Guild => Color.LightGreen,
            ChatType.System or ChatType.System2 or ChatType.LevelUp => Color.Gold,
            ChatType.WhisperIn or ChatType.WhisperOut => Color.Plum,
            ChatType.Group or ChatType.Shout or ChatType.Shout2 or ChatType.Shout3 => Color.Orange,
            ChatType.Mentor => Color.LightSkyBlue,
            ChatType.Relationship => Color.HotPink,
            _ => Color.White
        };
    }

    // Public for deterministic boundary tests without a Direct3D device.
    public static Rectangle FillSection(Rectangle source, double value, double maximum, bool vertical)
    {
        double ratio = maximum > 0 ? Math.Clamp(value / maximum, 0, 1) : 0;
        if (vertical)
        {
            int height = (int)(source.Height * ratio);
            return new Rectangle(source.X, source.Bottom - height, source.Width, height);
        }
        return new Rectangle(source.X, source.Y, (int)(source.Width * ratio), source.Height);
    }

    private static void DrawFill(int index, Rectangle source, Point origin, double value, double maximum, bool vertical)
    {
        var section = FillSection(source, value, maximum, vertical);
        if (section.Width > 0 && section.Height > 0)
            Libraries.Prguse176.Draw(index, section, origin.Add(new Point(section.X, section.Y)), Color.White, false);
    }

    private static void Toggle(MirControl dialog)
    {
        if (dialog == null || dialog.IsDisposed) return;
        if (dialog.Visible) dialog.Hide(); else dialog.Show();
    }

    private static void Resize(MirControl control, Size size)
    {
        if (control.Size == size) return;
        control.DisposeTexture();
        control.Size = size;
    }

    private static MirLabel Label(MirControl parent, Point location, Size size, string text) => new()
    {
        Parent = parent, Location = location, Size = size, AutoSize = false, Text = text,
        BackColour = Color.Transparent,
        NotControl = true, Font = new Font(Settings.FontName, 8F), ForeColour = Ink,
        DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
    };

    private static void StoneButton(MirButton button, string caption, int x, int y, bool panel = false)
    {
        button.AutoSize = false;
        button.Index = button.HoverIndex = button.PressedIndex = -1;
        button.Library = Libraries.Prguse176;
        button.DrawImage = false;
        button.DrawControlTexture = panel;
        button.BackColour = Bronze;
        button.Border = panel;
        button.BorderColour = Color.FromArgb(110, 92, 65);
        button.Location = new Point(x, y);
        button.Size = new Size(38, 28);
        var label = Label(button, Point.Empty, button.Size, caption);
        button.MouseEnter += (_, _) => { button.BackColour = Color.FromArgb(65, 52, 32); label.ForeColour = Color.Gold; };
        button.MouseLeave += (_, _) => { button.BackColour = Bronze; label.ForeColour = Ink; };
    }

    private static void RoundSeat(MirButton button, int index, int x, int y)
    {
        button.AutoSize = false;
        button.Library = Libraries.Prguse176;
        button.Index = button.HoverIndex = button.PressedIndex = index;
        button.DrawImage = button.DrawControlTexture = button.Border = false;
        button.Location = new Point(x, y);
        button.Size = new Size(24, 24);
        // Frame already contains the resting icon; only draw its native bright sprite on hover.
        button.MouseEnter += (_, _) => button.DrawImage = true;
        button.MouseLeave += (_, _) => button.DrawImage = false;
    }

    private static MirButton Button(MirControl parent, string caption, Point location, Size size, Action action, bool panel = true)
    {
        var button = new MirButton { Parent = parent, Sound = SoundList.ButtonA };
        StoneButton(button, caption, location.X, location.Y, panel);
        SizeButton(button, size);
        button.Click += (_, _) => action();
        return button;
    }

    private static void SizeButton(MirButton button, Size size)
    {
        button.Size = size;
        // MirButton owns an empty built-in label before our visible caption.
        button.Controls.OfType<MirLabel>().Last().Size = size;
    }

    private sealed class ExtensionDock : MirControl { }

    private static void WingCaption(MirControl wing, string text)
    {
        var label = Label(wing, new Point(8, 9), new Size(wing.Size.Width - 16, 16), text);
        label.ForeColour = Color.FromArgb(245, 225, 184);
    }

    private static MirLabel WingLine(MirControl wing, int y)
    {
        var label = Label(wing, new Point(8, y), new Size(wing.Size.Width - 16, 14), "");
        label.DrawFormat |= TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        return label;
    }

    // Repeat only unlabelled stone and edge strips; never stretch the ball or native buttons.
    private sealed class StoneWing : MirControl
    {
        private static readonly Rectangle Stone = new(666, 109, 43, 26);
        private static readonly Rectangle UpperEdge = new(8, 225, 152, 4);
        private static readonly Rectangle LowerEdge = new(8, 246, 152, 5);

        protected internal override void DrawControl()
        {
            Tile(Stone, 0, 0, Size.Width, Size.Height);
            Tile(UpperEdge, 0, 0, Size.Width, UpperEdge.Height);
            Tile(LowerEdge, 0, Size.Height - LowerEdge.Height, Size.Width, LowerEdge.Height);
        }

        private void Tile(Rectangle source, int x, int y, int width, int height)
        {
            Color tint = source == Stone ? Color.FromArgb(255, 190, 190, 190) : Color.White;
            for (int row = 0; row < height; row += source.Height)
                for (int column = 0; column < width; column += source.Width)
                {
                    var section = new Rectangle(source.X, source.Y, Math.Min(source.Width, width - column), Math.Min(source.Height, height - row));
                    Libraries.Prguse176.Draw(FrameIndex, section, DisplayLocation.Add(new Point(x + column, y + row)), tint, false);
                }
        }
    }
}
