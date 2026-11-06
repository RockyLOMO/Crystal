using System.Runtime.CompilerServices;
using System.Text.Json;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirScenes.Dialogs;

namespace Client.MirControls;

/// <summary>Read-only art localization. Profiles never change libraries, controls or game identifiers.</summary>
public static class ChineseUIOverlay
{
    private static readonly Color Background = Color.FromArgb(255, 22, 19, 15);
    private static readonly Color Edge = Color.FromArgb(255, 92, 78, 56);
    private static readonly Color TextColour = Color.FromArgb(255, 235, 210, 155);
    private static readonly Color Highlight = Color.FromArgb(255, 255, 240, 180);
    private static readonly ConditionalWeakTable<MirImageControl, OverlayState> States = new();
    private static readonly Dictionary<(string Library, int Index), List<Rule>> Profiles = new();
    private static bool loaded;

    // Called by the common image rendering path, after constructors and BeforeDraw
    // have finished. This also covers private, lazily created and re-indexed controls.
    internal static void Prepare(MirImageControl control)
    {
        bool hasState = States.TryGetValue(control, out var state);
        if (!DbLanguage.Chinese)
        {
            state?.SetVisible(false);
            return;
        }
        if (state?.Manual == true)
        {
            state.SetVisible(true);
            state.Update();
            return;
        }
        if (!loaded) LoadProfiles();
        int index = control.Index;
        if (hasState && state.Library == control.Library && state.Index == index && state.Parent == control.Parent)
        {
            state.SetVisible(true);
            state.Update();
            return;
        }
        string library = LibraryName(control.Library);
        Rule match = null;
        if (library != null && Profiles.TryGetValue((library, index), out var candidates))
            foreach (var rule in candidates)
                if (Matches(rule, control)) { match = rule; break; }
        if (match == null && !hasState) return;
        if (!hasState) States.Add(control, state = new OverlayState());
        state.Library = control.Library;
        state.Index = index;
        state.Parent = control.Parent;
        if (!ReferenceEquals(state.Rule, match))
        {
            state.Clear();
            state.Rule = match;
            if (match != null)
            {
                if (!hasState) EnableCoordInspector(control, control.GetType().Name);
                foreach (var rect in match.Rects)
                {
                    MirButton button = match.Button ? control as MirButton : null;
                    if (control is CharacterDialog character && rect.Rect.Length == 4 && rect.Rect[1] is >= 70 and <= 74)
                    {
                        // These plaques must avoid NameLabel/GuildLabel and track
                        // the four original buttons even when their Index is -1.
                        button = rect.Rect[0] switch
                        {
                            < 65 => character.CharacterButton,
                            < 127 => character.StatusButton,
                            < 189 => character.StateButton,
                            _ => character.SkillButton
                        };
                    }
                    var panel = Create(control, rect, button);
                    if (panel != null) state.Panels.Add(panel);
                }
            }
        }
        state.Update();
    }

    private static string LibraryName(MLibrary library)
    {
        if (library == null) return null;
        if (ReferenceEquals(library, Libraries.Title)) return "Title";
        if (ReferenceEquals(library, Libraries.Prguse)) return "Prguse";
        if (ReferenceEquals(library, Libraries.Prguse2)) return "Prguse2";
        if (ReferenceEquals(library, Libraries.Prguse3)) return "Prguse3";
        return null;
    }

    private static bool Matches(Rule rule, MirImageControl control)
    {
        if (rule.Button && control is not MirButton) return false;
        if (rule.ControlType != null && control.GetType().Name != rule.ControlType) return false;
        if (rule.ParentType == null) return true;
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            if (parent.GetType().Name == rule.ParentType) return true;
        return false;
    }

    private static void LoadProfiles()
    {
        loaded = true;
        try
        {
            string path = Path.Combine("Localization", "Overlay.zh-CN.json");
            if (!File.Exists(path)) return;
            var file = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(path));
            foreach (var rule in file?.Rules ?? [])
            {
                if (rule.Library == null || rule.Rects == null) continue;
                foreach (int index in rule.Indices)
                {
                    var key = (rule.Library, index);
                    if (!Profiles.TryGetValue(key, out var rules)) Profiles[key] = rules = [];
                    rules.Add(rule);
                }
            }
        }
        catch (Exception ex)
        {
            CMain.SaveError($"Chinese UI overlay profiles: {ex.Message}");
        }
    }

    private static Plaque Create(MirControl parent, Region region, MirButton button)
    {
        if (region.Rect is not { Length: 4 } || region.Rect[2] <= 0 || region.Rect[3] <= 0) return null;
        var rect = new Rectangle(region.Rect[0], region.Rect[1], region.Rect[2], region.Rect[3]);
        // Profiles use original art coordinates, never screen coordinates.
        var plaque = new Plaque
        {
            Parent = parent, Location = rect.Location, Size = rect.Size,
            BackColour = Background, DrawControlTexture = true,
            Border = region.Border, BorderColour = Edge, NotControl = true,
            Button = button, CharacterTab = parent is CharacterDialog
        };
        var label = new MirLabel
        {
            Parent = plaque, AutoSize = false, Location = Point.Empty, Size = rect.Size,
            DrawFormat = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                (region.Align == "Right" ? TextFormatFlags.Right : region.Align == "Left" ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter),
            ForeColour = TextColour, Text = region.Text, NotControl = true
        };
        // MirLabel.ScaleFont consumes the incoming Font. Dispose the constructor's
        // replaced font too; do not cache/share either instance between labels.
        var oldFont = label.Font;
        label.Font = new Font(Settings.FontName, region.FontSize, region.Border ? FontStyle.Bold : FontStyle.Regular);
        oldFont.Dispose();
        plaque.Label = label;
        return plaque;
    }

    public static MirControl MaskRect(MirControl parent, string text, Rectangle rect, float fontSize = 8.5F, bool showBorder = false)
        => !DbLanguage.Chinese || parent == null ? null : Create(parent,
            new Region { Text = text, Rect = [rect.X, rect.Y, rect.Width, rect.Height], FontSize = fontSize, Border = showBorder }, null);

    public static MirControl MaskTitle(MirControl parent, string text, int width = 92, int height = 18, int offsetX = 0, int offsetY = 7)
        => parent == null ? null : MaskRect(parent, text,
            new Rectangle((parent.Size.Width - width) / 2 + offsetX, offsetY, width, height), 9F, true);

    public static void MaskButton(MirButton button, string text, int padX = 5, int padY = 4)
    {
        if (!DbLanguage.Chinese || button == null) return;
        var size = button.Index >= 0 && button.Library != null ? button.Library.GetSize(button.Index) : button.Size;
        if (size.Width <= 2 * padX || size.Height <= 2 * padY) return;
        var state = States.GetValue(button, _ => new OverlayState());
        state.Clear();
        state.Manual = true;
        var panel = Create(button, new Region { Text = text, Rect = [padX, padY, size.Width - 2 * padX, size.Height - 2 * padY] }, button);
        if (panel != null) state.Panels.Add(panel);
    }

    public static readonly Color PlaqueBorderColor = Color.FromArgb(255, 92, 78, 56);
    public static readonly Color ButtonTextColor = Color.FromArgb(255, 235, 210, 155);
    public static readonly Color ButtonHoverColor = Color.FromArgb(255, 255, 240, 180);
    private static readonly ConditionalWeakTable<MirControl, object> AppliedControls = new();

    public static void ApplyToSelectScene(SelectScene scene)
    {
        if (!DbLanguage.Chinese || scene == null) return;
        MaskButton(scene.StartGameButton, "进入玛法");
        MaskButton(scene.NewCharacterButton, "创建角色");
        MaskButton(scene.DeleteCharacterButton, "删除角色");
        MaskButton(scene.CreditsButton, "制作鸣谢");
        MaskButton(scene.ExitGameButton, "退出游戏");

        if (scene.NewCharacterDialog != null)
            ApplyToNewCharacterDialog(scene.NewCharacterDialog);
    }

    public static void ApplyToNewCharacterDialog(NewCharacterDialog newCharDlg)
    {
        if (!DbLanguage.Chinese || newCharDlg == null) return;
        if (AppliedControls.TryGetValue(newCharDlg, out _)) return;
        AppliedControls.Add(newCharDlg, new object());

        if (newCharDlg.TitleLabel != null)
            MaskTitle(newCharDlg.TitleLabel, "创建新角色", width: 120, height: 22, offsetX: 0, offsetY: 2);

        MaskButton(newCharDlg.OKButton, "创 建");
        MaskButton(newCharDlg.CancelButton, "取 消");

        AttachClassBadgeLabel(newCharDlg.WarriorButton, "战士", offsetY: 4);
        AttachClassBadgeLabel(newCharDlg.WizardButton, "法师", offsetY: 4);
        AttachClassBadgeLabel(newCharDlg.TaoistButton, "道士", offsetY: 4);
        AttachClassBadgeLabel(newCharDlg.AssassinButton, "刺客", offsetY: 4);
        AttachClassBadgeLabel(newCharDlg.ArcherButton, "弓手", offsetY: 4);
        AttachClassBadgeLabel(newCharDlg.MaleButton, "男", offsetY: 4);
        AttachClassBadgeLabel(newCharDlg.FemaleButton, "女", offsetY: 4);

        SetupDynamicClassDescriptionOverlay(newCharDlg);
    }

    public static void AttachClassBadgeLabel(MirButton btn, string cnName, int offsetY = 0)
    {
        if (!DbLanguage.Chinese || btn == null || btn.Parent == null) return;
        int btnWidth = btn.Size.Width > 0 ? btn.Size.Width : 44;
        int btnHeight = btn.Size.Height > 0 ? btn.Size.Height : 42;

        var badge = new MirLabel
        {
            Parent = btn.Parent,
            Location = new Point(btn.Location.X + (btnWidth - 40) / 2, btn.Location.Y + btnHeight - 12 + offsetY),
            Size = new Size(40, 16),
            AutoSize = false,
            DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine,
            BackColour = Color.FromArgb(200, 18, 15, 12),
            ForeColour = ButtonTextColor,
            OutLine = true,
            OutLineColour = Color.Black,
            Text = cnName,
            NotControl = true
        };
        var oldFont = badge.Font;
        badge.Font = new Font(Settings.FontName, 8F, FontStyle.Bold);
        oldFont.Dispose();

        btn.MouseEnter += (_, _) => badge.ForeColour = ButtonHoverColor;
        btn.MouseLeave += (_, _) => badge.ForeColour = ButtonTextColor;
    }

    public static void SetupDynamicClassDescriptionOverlay(NewCharacterDialog dlg)
    {
        if (!DbLanguage.Chinese || dlg?.Description == null) return;

        var descLabel = dlg.Description;
        descLabel.ForeColour = Color.FromArgb(255, 232, 218, 185);
        descLabel.BackColour = Color.FromArgb(180, 16, 13, 10);
        descLabel.Border = true;
        descLabel.BorderColour = PlaqueBorderColor;

        void SetClassDesc(string title, string body)
        {
            if (!DbLanguage.Chinese) return;
            descLabel.Text = $"{title}\n\n{body}";
        }

        if (dlg.WarriorButton != null)
            dlg.WarriorButton.Click += (_, _) => SetClassDesc("【近战霸主・烈火开天】",
                "高血量、强体魄的近身格斗宗师。中期习得《刺杀剑术》《半月弯刀》，后期凭《烈火剑法》《开天斩》拥有玛法大陆最强的单点爆发，适合搭配道士或法师英雄独闯龙潭。");
        if (dlg.WizardButton != null)
            dlg.WizardButton.Click += (_, _) => SetClassDesc("【元素掌控・群刷清图】",
                "操控雷电、烈焰与寒冰的远程法术大师。《雷电术》《火墙》《冰咆哮》清图效率冠绝三职业，《魔法盾》成型后生存质变，搭配战士英雄顶怪可轻松越级挑战领主。");
        if (dlg.TaoistButton != null)
            dlg.TaoistButton.Click += (_, _) => SetClassDesc("【万金油毒奶・单挑真神】",
                "通晓道法自然、医毒双修的独行首选。《施毒术》破防禁回血，《召唤神兽》充当坚实肉盾，配合《幽灵盾》《神圣战甲术》《治愈术》，单人低消耗磨死一切赤月与魔龙首领。");
        if (dlg.AssassinButton != null)
            dlg.AssassinButton.Click += (_, _) => SetClassDesc("【暗影暴击・瞬移收割】",
                "潜行于阴影中的致命猎手。极高攻速与暴击加成，擅长隐身切入与背刺爆发，操作上限极高。");
        if (dlg.ArcherButton != null)
            dlg.ArcherButton.Click += (_, _) => SetClassDesc("【远程风筝・陷阱蓄力】",
                "百步穿杨的精准神射。依托超远射程、击退陷阱与蓄力穿透箭矢，在安全距离外瓦解强敌。");

        SetClassDesc("【近战霸主・烈火开天】",
            "高血量、强体魄的近身格斗宗师。中期习得《刺杀剑术》《半月弯刀》，后期凭《烈火剑法》《开天斩》拥有玛法大陆最强的单点爆发，适合搭配道士或法师英雄独闯龙潭。");
    }

    public static void ApplyToGameScene(GameScene scene)
    {
        if (!DbLanguage.Chinese || scene == null) return;
        EnableCoordInspector(scene.MainDialog, "MainDialog");
        EnableCoordInspector(scene.CharacterDialog, "CharacterDialog");
        EnableCoordInspector(scene.InventoryDialog, "InventoryDialog");
    }

    // Opt-in only: Alt is a normal gameplay modifier and must not produce chat
    // spam in a release client. Enable Game/ChineseOverlayInspector explicitly.
    public static void EnableCoordInspector(MirControl parent, string name)
    {
        if (!Settings.ChineseOverlayInspector || parent == null) return;
        long lastReport = 0;
        Point previous = new(int.MinValue, int.MinValue);
        parent.MouseMove += (_, _) =>
        {
            if ((Control.ModifierKeys & Keys.Alt) == 0 || CMain.Time - lastReport < 250) return;
            Point point = new(CMain.MPoint.X - parent.DisplayRectangle.X, CMain.MPoint.Y - parent.DisplayRectangle.Y);
            if (point == previous) return;
            previous = point; lastReport = CMain.Time;
            GameScene.Scene?.ChatDialog?.ReceiveChat($"[坐标尺 {name}] X={point.X}, Y={point.Y}", ChatType.Hint);
        };
    }

    private sealed class OverlayState
    {
        public MLibrary Library;
        public MirControl Parent;
        public int Index;
        public Rule Rule;
        public bool Manual;
        public readonly List<Plaque> Panels = [];
        public void SetVisible(bool visible) { foreach (var panel in Panels) panel.Visible = visible; }
        public void Update() { foreach (var panel in Panels) panel.UpdateState(); }
        public void Clear() { foreach (var panel in Panels) panel.Dispose(); Panels.Clear(); }
    }

    private sealed class Plaque : MirControl
    {
        public MirLabel Label;
        public MirButton Button;
        public bool CharacterTab;
        public void UpdateState()
        {
            if (Button == null) return;
            bool enabled = Button.Enabled;
            bool hovered = enabled && MouseControl == Button;
            bool pressed = enabled && ActiveControl == Button && hovered;
            bool selected = CharacterTab && Button.Index >= 0;
            Label.ForeColour = !enabled ? Color.Gray : hovered || selected ? ChineseUIOverlay.Highlight : TextColour;
            Label.Location = pressed ? new Point(1, 1) : Point.Empty;
            BackColour = selected ? Color.FromArgb(255, 48, 37, 23) : Background;
        }
        protected override void CreateTexture()
        {
            if (TextureSize != Size) DisposeTexture();
            base.CreateTexture();
        }
    }

    public sealed class ProfileFile { public Rule[] Rules { get; set; } = []; }
    public sealed class Rule
    {
        public string Library { get; set; }
        public int[] Indices { get; set; } = [];
        public string ControlType { get; set; }
        public string ParentType { get; set; }
        public bool Button { get; set; }
        public Region[] Rects { get; set; } = [];
    }
    public sealed class Region
    {
        public string Text { get; set; }
        public int[] Rect { get; set; } = [];
        public float FontSize { get; set; } = 8.5F;
        public string Align { get; set; } = "Center";
        public bool Border { get; set; }
    }
}
