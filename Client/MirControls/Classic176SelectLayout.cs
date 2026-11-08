using Client.MirGraphics;
using Client.MirScenes;
using Client.MirScenes.Dialogs;

namespace Client.MirControls;

/// <summary>Four Crystal character slots on verified ten-year gate art; animation and actions stay native.</summary>
public static class Classic176SelectLayout
{
    public static bool Apply(SelectScene scene)
    {
        if (Classic176RoleLayout.Apply(scene)) return true;
        if (!Settings.UseClassic176Select || !Classic176FrontEndSkin.Available) return false;
        if (!Classic176FrontEndSkin.Begin(scene)) return true;
        var background = scene.Background;
        background.AutoSize = false;
        background.Library = Libraries.Classic176FrontEnd;
        background.Index = -1;
        background.Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
        background.DrawImage = false;
        Classic176FrontEndSkin.Background(background, 1);
        scene.Title.Visible = false;
        scene.ServerLabel.Location = new Point(0, 96);
        scene.ServerLabel.Size = new Size(Settings.ScreenWidth, 24);
        scene.ServerLabel.ForeColour = Classic176FrontEndSkin.Ink;
        int left = (Settings.ScreenWidth - 760) / 2;
        int top = Math.Max(128, (Settings.ScreenHeight - 600) / 2 + 128);
        scene.CharacterDisplay.Location = new Point(left + 210, top + 280);
        for (int i = 0; i < scene.CharacterButtons.Length; i++)
        {
            var card = scene.CharacterButtons[i];
            Classic176FrontEndSkin.Panel(card, new Size(334, 76), "");
            card.Location = new Point(left + 412, top + i * 84);
            card.NameLabel.Location = new Point(20, 9);
            card.NameLabel.Size = new Size(290, 24);
            card.LevelLabel.Location = new Point(20, 42);
            card.LevelLabel.Size = new Size(112, 22);
            card.ClassLabel.Location = new Point(160, 42);
            card.ClassLabel.Size = new Size(150, 22);
            var empty = Classic176FrontEndSkin.Label(card, "空角色位", new Rectangle(15, 0, 304, 76), 12);
            card.BeforeDraw += (_, _) =>
            {
                card.BorderColour = card.Selected ? Color.Gold : Classic176FrontEndSkin.Edge;
                empty.Visible = string.IsNullOrEmpty(card.NameLabel.Text);
            };
        }
        var buttons = new[] { scene.StartGameButton, scene.NewCharacterButton, scene.DeleteCharacterButton, scene.CreditsButton, scene.ExitGameButton };
        string[] captions = ["进入玛法", "创建角色", "删除角色", "制作鸣谢", "退出游戏"];
        int buttonWidth = Math.Min(144, (Settings.ScreenWidth - 72) / 5 - 10);
        int allWidth = 5 * buttonWidth + 4 * 10;
        for (int i = 0; i < buttons.Length; i++)
            Classic176FrontEndSkin.Button(buttons[i], captions[i],
                new Rectangle((Settings.ScreenWidth - allWidth) / 2 + i * (buttonWidth + 10), Settings.ScreenHeight - 60, buttonWidth, 38));
        scene.LastAccessLabel.Location = new Point(left + 126, Settings.ScreenHeight - 100);
        scene.LastAccessLabel.Size = new Size(260, 22);
        scene.LastAccessLabel.Border = false;
        scene.LastAccessLabelLabel.Parent = background;
        scene.LastAccessLabelLabel.Location = new Point(left + 14, Settings.ScreenHeight - 100);
        scene.LastAccessLabelLabel.Size = new Size(108, 22);
        scene.LastAccessLabelLabel.Border = false;
        RefreshCards(scene);
        return true;
    }

    public static void RefreshCards(SelectScene scene)
    {
        if (Classic176RoleLayout.Refresh(scene)) return;
        if (scene.Background.Library != Libraries.Classic176FrontEnd) return;
        foreach (var card in scene.CharacterButtons)
        {
            // CharacterButton.Update rewrites its old atlas/index; this hook preserves its data and events.
            card.AutoSize = false;
            card.Library = Libraries.Classic176FrontEnd;
            card.Index = -1;
            card.DrawImage = false;
        }
    }

    public static void RefreshCreation(NewCharacterDialog dialog)
    {
        foreach (var button in new[] { dialog.WarriorButton, dialog.WizardButton, dialog.TaoistButton,
            dialog.AssassinButton, dialog.ArcherButton, dialog.MaleButton, dialog.FemaleButton })
            if (button.Library == Libraries.Classic176FrontEnd) button.Index = -1;
    }

    public static bool ApplyCharacter(NewCharacterDialog dialog)
    {
        if (Classic176RoleLayout.ApplyCreation(dialog)) return true;
        if (!Settings.UseClassic176Select || !Classic176FrontEndSkin.Available) return false;
        if (!Classic176FrontEndSkin.Begin(dialog)) return true;
        Classic176FrontEndSkin.Panel(dialog, new Size(592, 470), "创建玛法角色");
        dialog.Location = Classic176FrontEndSkin.Center(dialog.Size);
        dialog.TitleLabel.Visible = false;
        Classic176FrontEndSkin.Label(dialog, "角色名字", new Rectangle(298, 248, 100, 28));
        Classic176FrontEndSkin.Input(dialog.NameTextBox, new Rectangle(398, 248, 168, 28));
        dialog.DescriptionLabel.Location = new Point(280, 62);
        dialog.DescriptionLabel.Size = new Size(286, 174);
        dialog.DescriptionLabel.ForeColour = Classic176FrontEndSkin.Ink;
        dialog.DescriptionLabel.BackColour = Color.FromArgb(255, 17, 14, 11);
        if (MirScene.ActiveScene is SelectScene select)
        {
            var selectionPreview = select.CharacterDisplay;
            bool hidden = false, previousVisible = false;
            void SyncPreview()
            {
                if (select.IsDisposed || selectionPreview.IsDisposed) return;
                if (dialog.Visible)
                {
                    if (!hidden) previousVisible = selectionPreview.Visible;
                    hidden = true;
                    selectionPreview.Visible = false;
                }
                else if (hidden)
                {
                    hidden = false;
                    selectionPreview.Visible = previousVisible;
                }
            }
            dialog.VisibleChanged += (_, _) => SyncPreview();
            dialog.Disposing += (_, _) => { if (!select.IsDisposed && !selectionPreview.IsDisposed) selectionPreview.Visible = previousVisible; };
            SyncPreview();
        }
        MirButton[] classes = [dialog.WarriorButton, dialog.WizardButton, dialog.TaoistButton, dialog.AssassinButton, dialog.ArcherButton];
        string[] captions = ["战士", "法师", "道士", "刺客", "弓手"];
        for (int i = 0; i < classes.Length; i++)
            Classic176FrontEndSkin.Button(classes[i], captions[i], new Rectangle(280 + i * 58, 297, 54, 34));
        Classic176FrontEndSkin.Button(dialog.MaleButton, "男", new Rectangle(340, 350, 92, 34));
        Classic176FrontEndSkin.Button(dialog.FemaleButton, "女", new Rectangle(446, 350, 92, 34));
        Classic176FrontEndSkin.Button(dialog.OKButton, "创建角色", new Rectangle(142, 416, 138, 36));
        Classic176FrontEndSkin.Button(dialog.CancelButton, "取消返回", new Rectangle(310, 416, 138, 36));
        // Existing class/gender changes and all five preview families remain on the original controls.
        dialog.BeforeDraw += (_, _) =>
            {
                for (int i = 0; i < classes.Length; i++) classes[i].BorderColour = (int)dialog.Class == i ? Color.Gold : Classic176FrontEndSkin.Edge;
                dialog.MaleButton.BorderColour = dialog.Gender == MirGender.Male ? Color.Gold : Classic176FrontEndSkin.Edge;
                dialog.FemaleButton.BorderColour = dialog.Gender == MirGender.Female ? Color.Gold : Classic176FrontEndSkin.Edge;
            };
        return true;
    }
}
