using System.Runtime.CompilerServices;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using Client.MirSounds;

namespace Client.MirControls;

/// <summary>Native two-slot stone cave, with two pages retaining all four Crystal roles.</summary>
public static class Classic176RoleLayout
{
    public const int BackgroundIndex = 13, CreationIndex = 14, PortraitOffset = -25;
    private static readonly ConditionalWeakTable<SelectScene, Layout> Layouts = new();
    private static readonly Color Gold = Color.FromArgb(225, 203, 139);

    private static bool Available
    {
        get
        {
            if (!Settings.UseClassic176Select || !Classic176FrontEndSkin.Available) return false;
            try
            {
                return Libraries.Classic176FrontEnd.GetSize(13) == new Size(800, 600) &&
                    Libraries.Classic176FrontEnd.GetSize(14) == new Size(300, 417) &&
                    Libraries.Classic176FrontEnd.GetSize(247) == new Size(196, 314) &&
                    Libraries.Classic176FrontEnd.GetSize(248) == new Size(76, 33) &&
                    Libraries.Classic176FrontEnd.GetSize(264) == new Size(168, 41);
            }
            catch { return false; }
        }
    }

    public static bool Apply(SelectScene scene)
    {
        if (Layouts.TryGetValue(scene, out _)) return true;
        if (!Available) return false;
        var state = new Layout(scene);
        Layouts.Add(scene, state);
        state.Build();
        return true;
    }

    public static bool Refresh(SelectScene scene)
    {
        if (!Layouts.TryGetValue(scene, out var state)) return false;
        state.Refresh();
        return true;
    }

    public static int PageIndex(SelectScene scene) => Layouts.TryGetValue(scene, out var state) ? state.Page : 0;

    public static void SetPage(SelectScene scene, int page)
    {
        if (!Layouts.TryGetValue(scene, out var state) || page is < 0 or > 1 || scene.IsDisposed) return;
        state.Page = page;
        int first = page * 2;
        if (first < scene.Characters.Count) scene.CharacterButtons[first].InvokeMouseClick(EventArgs.Empty);
        state.Refresh();
    }

    // Each old character has a one-shot stone-to-color reveal followed by a colorful action loop.
    public static int PortraitFrame(MirClass type, MirGender gender, bool selected, long elapsed)
    {
        if (type > MirClass.Taoist) return -1;
        int action = (gender == MirGender.Male ? 40 : 160) + (int)type * 40;
        if (!selected) return action + 20 + PortraitOffset;
        if (elapsed < 13 * 80) return action + 20 + (int)Math.Max(0, elapsed / 80) + PortraitOffset;
        return action + (int)((elapsed - 13 * 80) / 120 % 16) + PortraitOffset;
    }

    private sealed class Layout(SelectScene scene)
    {
        internal int Page;
        private int selected = -1, signature;
        private bool? savedDelete;
        private bool creating;
        private readonly Portrait[] portraits = new Portrait[4];
        private readonly MirButton[] selects = new MirButton[4];
        private readonly MirLabel[] numbers = new MirLabel[4];
        private MirLabel pager;
        private readonly float scale = Math.Min(Settings.ScreenWidth / 800f, Settings.ScreenHeight / 600f);
        private readonly Point origin = new((Settings.ScreenWidth - (int)(800 * Math.Min(Settings.ScreenWidth / 800f, Settings.ScreenHeight / 600f))) / 2,
            (Settings.ScreenHeight - (int)(600 * Math.Min(Settings.ScreenWidth / 800f, Settings.ScreenHeight / 600f))) / 2);

        internal Rectangle Rect(int x, int y, int w, int h) => new(origin.X + (int)(x * scale), origin.Y + (int)(y * scale), (int)(w * scale), (int)(h * scale));
        internal void Build()
        {
            var bg = scene.Background;
            bg.AutoSize = false; bg.Index = -1; bg.DrawImage = false;
            bg.Library = Libraries.Classic176FrontEnd;
            bg.Location = Point.Empty; bg.Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
            bg.InsertControl(0, new Art(13) { Size = bg.Size, Bounds = Rect(0, 0, 800, 600), NotControl = true });
            scene.Title.Visible = scene.ServerLabel.Visible = false;
            scene.CharacterDisplay.Visible = scene.LastAccessLabel.Visible = scene.LastAccessLabelLabel.Visible = false;
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                int x = i % 2 == 0 ? 40 : 590;
                portraits[i] = new Portrait { Parent = bg, Location = Rect(i % 2 == 0 ? 88 : 418, 30, 300, 370).Location,
                    Size = Rect(0, 0, 300, 370).Size, Scale = scale };
                portraits[i].Click += (_, _) => scene.CharacterButtons[slot].InvokeMouseClick(EventArgs.Empty);
                var card = scene.CharacterButtons[i];
                card.AutoSize = false; card.Index = -1; card.DrawImage = false;
                card.Library = Libraries.Classic176FrontEnd; card.Location = Rect(x, 444, 172, 140).Location;
                card.Size = Rect(0, 0, 172, 140).Size;
                PutLabel(card.NameLabel, 70, 46, 102, 20);
                PutLabel(card.LevelLabel, 70, 77, 102, 20);
                PutLabel(card.ClassLabel, 70, 107, 102, 20);
                numbers[i] = new MirLabel { Parent = bg, Location = Rect(x + 42, 12 + 444, 28, 24).Location,
                    Size = Rect(0, 0, 28, 24).Size, BackColour = Color.FromArgb(32, 30, 29), ForeColour = Gold,
                    Text = (i + 1).ToString(), NotControl = true, Font = new Font(Settings.FontName, 10 * scale) };
                selects[i] = new MirButton { Parent = bg, Hint = "选择这个角色", Sound = SoundList.ButtonA };
                Button(selects[i], "", Rect(x + 94, 453, 76, 33), 248, 249);
                selects[i].Click += (_, _) => scene.CharacterButtons[slot].InvokeMouseClick(EventArgs.Empty);
            }
            Button(scene.StartGameButton, "开始", Rect(363, 452, 74, 28));
            Button(scene.NewCharacterButton, "创建人物", Rect(260, 490, 104, 28));
            Button(scene.CreditsButton, "制作鸣谢", Rect(368, 490, 104, 28));
            Button(scene.ExitGameButton, "退出", Rect(476, 490, 64, 28));
            Button(scene.DeleteCharacterButton, "删除人物", Rect(348, 526, 104, 28));
            var previous = new MirButton { Parent = bg, Hint = "上一页角色" };
            var next = new MirButton { Parent = bg, Hint = "下一页角色" };
            Button(previous, "", Rect(302, 562, 36, 27), 250, 250);
            Button(next, "", Rect(462, 562, 36, 27), 251, 251);
            previous.Click += (_, _) => SetPage(scene, (Page + 1) % 2);
            next.Click += (_, _) => SetPage(scene, (Page + 1) % 2);
            pager = new MirLabel { Parent = bg, Location = Rect(342, 566, 116, 20).Location,
                Size = Rect(0, 0, 116, 20).Size, NotControl = true, ForeColour = Gold,
                Font = new Font(Settings.FontName, 9 * scale), DrawFormat = TextFormatFlags.HorizontalCenter };
            bg.BeforeDraw += (_, _) =>
            {
                // A late server reply must not re-enable actions for a role hidden on another page.
                if (scene.Characters != null && (selected < 0 || selected >= scene.Characters.Count || selected / 2 != Page))
                    scene.StartGameButton.Enabled = scene.DeleteCharacterButton.Enabled = false;
            };
        }

        private void PutLabel(MirLabel label, int x, int y, int w, int h)
        {
            label.Location = new Point((int)(x * scale), (int)(y * scale));
            label.Size = new Size((int)(w * scale), (int)(h * scale));
            label.Font = new Font(Settings.FontName, 8 * scale); label.ForeColour = Color.White;
            label.DrawFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        }

        internal void Refresh()
        {
            if (scene.IsDisposed || scene.CharacterButtons == null) return;
            int current = Array.FindIndex(scene.CharacterButtons, b => b.Selected);
            int hash = scene.Characters.Aggregate(17, (value, role) => unchecked(value * 31 + role.Index));
            if (current != selected || hash != signature)
            {
                if (current >= 0 && current < scene.Characters.Count) Page = current / 2;
                selected = current; signature = hash;
            }
            for (int i = 0; i < 4; i++)
            {
                bool shown = i / 2 == Page;
                var card = scene.CharacterButtons[i];
                card.AutoSize = false; card.Index = -1; card.DrawImage = false;
                card.Library = Libraries.Classic176FrontEnd; card.Visible = shown;
                selects[i].Visible = numbers[i].Visible = shown;
                selects[i].Enabled = i < scene.Characters.Count;
                selects[i].BorderColour = card.Selected ? Color.Gold : Color.Empty;
                portraits[i].Configure(i < scene.Characters.Count ? scene.Characters[i] : null, card.Selected);
                portraits[i].Visible = shown && !creating && i < scene.Characters.Count;
            }
            bool active = current >= 0 && current < scene.Characters.Count && current / 2 == Page;
            if (!active)
            {
                scene.StartGameButton.Enabled = false;
                savedDelete ??= scene.DeleteCharacterButton.Enabled;
                scene.DeleteCharacterButton.Enabled = false;
            }
            else if (savedDelete.HasValue)
            {
                scene.DeleteCharacterButton.Enabled = savedDelete.Value; savedDelete = null;
            }
            pager.Text = $"{Page + 1} / 2 页";
            scene.CharacterDisplay.Visible = scene.LastAccessLabel.Visible = scene.LastAccessLabelLabel.Visible = false;
        }

        internal void CreationVisible(bool visible) { creating = visible; Refresh(); }
    }

    public static bool ApplyCreation(NewCharacterDialog dialog)
    {
        if (!Available) return false;
        // The upstream constructor probes the skin before its caller assigns Parent.
        // Defer mutation until the existing SelectScene hook runs with its real owner.
        if (dialog.Parent == null && MirScene.ActiveScene is SelectScene owner && Layouts.TryGetValue(owner, out _)) return true;
        if (dialog.Parent is not SelectScene scene || !Layouts.TryGetValue(scene, out var state)) return false;
        if (!Classic176FrontEndSkin.Begin(dialog)) return true;
        dialog.AutoSize = false; dialog.Library = Libraries.Classic176FrontEnd; dialog.Index = -1;
        dialog.DrawImage = false; dialog.Size = new Size(300, 417);
        dialog.Location = Classic176FrontEndSkin.Center(dialog.Size);
        dialog.InsertControl(0, new Art(14) { Size = dialog.Size, Bounds = new Rectangle(Point.Empty, dialog.Size), NotControl = true });
        dialog.TitleLabel.Visible = dialog.CharacterDisplay.Visible = dialog.DescriptionLabel.Visible = false;
        dialog.NameTextBox.Location = new Point(72, 108); dialog.NameTextBox.Size = new Size(160, 20);
        dialog.NameTextBox.BackColour = Color.FromArgb(24, 24, 22); dialog.NameTextBox.ForeColour = Gold;
        MirButton[] types = [dialog.WarriorButton, dialog.WizardButton, dialog.TaoistButton, dialog.AssassinButton, dialog.ArcherButton];
        string[] names = ["战", "法", "道", "刺", "弓"];
        string[] hints = [dialog.WarriorDescription, dialog.WizardDescription, dialog.TaoistDescription, dialog.AssassinDescription, dialog.ArcherDescription];
        for (int i = 0; i < 5; i++)
        {
            var button = types[i];
            Button(button, i < 3 ? "" : names[i], new Rectangle(40 + 44 * i, 158, 44, 36), i < 3 ? 252 + 2 * i : 263, i < 3 ? 253 + 2 * i : 264);
            button.Hint = hints[i];
        }
        Button(dialog.MaleButton, "", new Rectangle(94, 232, 44, 36), 258, 259);
        Button(dialog.FemaleButton, "", new Rectangle(142, 232, 44, 36), 260, 261);
        Button(dialog.OKButton, "创建", new Rectangle(98, 356, 86, 43));
        Button(dialog.CancelButton, "×", new Rectangle(245, 28, 22, 25));
        dialog.BeforeDraw += (_, _) =>
        {
            for (int i = 0; i < types.Length; i++) types[i].BorderColour = (int)dialog.Class == i ? Color.Gold : Color.Empty;
            dialog.MaleButton.BorderColour = dialog.Gender == MirGender.Male ? Color.Gold : Color.Empty;
            dialog.FemaleButton.BorderColour = dialog.Gender == MirGender.Female ? Color.Gold : Color.Empty;
        };
        dialog.VisibleChanged += (_, _) => state.CreationVisible(dialog.Visible);
        dialog.Disposing += (_, _) => state.CreationVisible(false);
        state.CreationVisible(dialog.Visible);
        return true;
    }

    private static void Button(MirButton button, string text, Rectangle bounds, int normal = 263, int highlighted = 264)
    {
        button.AutoSize = false; button.Library = Libraries.Classic176FrontEnd;
        button.Index = button.HoverIndex = button.PressedIndex = -1; button.DrawImage = false;
        button.DrawControlTexture = false; button.Location = bounds.Location; button.Size = bounds.Size;
        button.Text = text; button.CenterText = true; button.FontColour = Gold;
        var label = button.Controls.OfType<MirLabel>().First();
        label.Font = new Font(Settings.FontName, Math.Max(8, bounds.Height / 3.3f));
        label.DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        button.BeforeDraw += (_, _) =>
        {
            bool lit = button.Enabled && (button.BorderColour == Color.Gold || MirControl.MouseControl == button || MirControl.ActiveControl == button);
            Libraries.Classic176FrontEnd.Draw(lit ? highlighted : normal, button.DisplayLocation, button.Size, Color.White);
            label.ForeColour = button.Enabled ? Gold : Color.Gray;
        };
    }

    private sealed class Art(int index) : MirControl
    {
        internal Rectangle Bounds;
        protected internal override void DrawControl()
        {
            // Keep the native canvas proportional; stone strips extend into wide-screen margins.
            if (index == 13)
            {
                for (int x = 0; x < Size.Width; x += 80)
                    Libraries.Classic176FrontEnd.Draw(262, DisplayLocation.Add(new Point(x, 0)), new Size(80, Size.Height), Color.White);
            }
            Libraries.Classic176FrontEnd.Draw(index, DisplayLocation.Add(Bounds.Location), Bounds.Size, Color.White);
        }
    }

    private sealed class Portrait : MirControl
    {
        internal float Scale;
        private SelectInfo role;
        private bool selected;
        private long started;
        internal void Configure(SelectInfo value, bool active)
        {
            if (role?.Index != value?.Index || selected != active) started = CMain.Time;
            role = value; selected = active;
        }
        protected internal override void DrawControl()
        {
            if (role == null) return;
            int frame = PortraitFrame(role.Class, role.Gender, selected, Math.Max(0, CMain.Time - started));
            var lib = Libraries.Classic176FrontEnd;
            if (frame < 0)
            {
                lib = Libraries.ChrSel;
                int basis = role.Class == MirClass.Assassin ? (role.Gender == MirGender.Male ? 80 : 360) : (role.Gender == MirGender.Male ? 100 : 140);
                frame = basis + (int)(CMain.Time / 250 % 16);
            }
            Size native = lib.GetSize(frame);
            if (native.IsEmpty) return;
            float factor = frame >= 0 && lib == Libraries.ChrSel ? Math.Min(300f / native.Width, 350f / native.Height) : 1;
            var size = new Size((int)(native.Width * Scale * factor), (int)(native.Height * Scale * factor));
            lib.Draw(frame, DisplayLocation.Add(new Point((Size.Width - size.Width) / 2, Size.Height - size.Height)), size, Color.White);
        }
    }
}
