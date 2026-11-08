using Client.MirGraphics;
using Client.MirScenes;
using LoginDialog = Client.MirScenes.LoginScene.LoginDialog;
using AccountDialog = Client.MirScenes.LoginScene.NewAccountDialog;
using PasswordDialog = Client.MirScenes.LoginScene.ChangePasswordDialog;
using KeyboardDialog = Client.MirScenes.LoginScene.InputKeyDialog;

namespace Client.MirControls;

/// <summary>Ten-year stone-gate adaptation; retains all original controls and packet handlers.</summary>
public static class Classic176LoginLayout
{
    public static bool Apply(LoginScene scene, MirAnimatedControl background, LoginDialog login)
    {
        if (!Settings.UseClassic176Login || !Classic176FrontEndSkin.Available) return false;
        if (!Classic176FrontEndSkin.Begin(scene)) return true;
        // Keep the original animation timeline: LoginSuccess still completes through AfterAnimation.
        background.AutoSize = false;
        background.Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
        background.Location = Point.Empty;
        background.DrawImage = false;
        Classic176GateTransition.Attach(background);
        Classic176FrontEndSkin.Background(background, 0);
        Classic176FrontEndSkin.Panel(login, new Size(432, 310), "登录玛法");
        login.Location = Classic176FrontEndSkin.Center(login.Size);
        login.TitleLabel.Visible = login.AccountIDLabel.Visible = login.PassLabel.Visible = false;
        Classic176FrontEndSkin.Label(login, "账号", new Rectangle(24, 90, 84, 28));
        Classic176FrontEndSkin.Label(login, "密码", new Rectangle(24, 132, 84, 28));
        Classic176FrontEndSkin.Input(login.AccountIDTextBox, new Rectangle(118, 90, 284, 28));
        Classic176FrontEndSkin.Input(login.PasswordTextBox, new Rectangle(118, 132, 284, 28));
        Classic176FrontEndSkin.Button(login.OKButton, "进入游戏", new Rectangle(26, 183, 180, 38));
        Classic176FrontEndSkin.Button(login.CloseButton, "退出游戏", new Rectangle(226, 183, 180, 38));
        Classic176FrontEndSkin.Button(login.AccountButton, "注册账号", new Rectangle(26, 237, 116, 34));
        Classic176FrontEndSkin.Button(login.PassButton, "修改密码", new Rectangle(158, 237, 116, 34));
        Classic176FrontEndSkin.Button(login.ViewKeyButton, "安全键盘", new Rectangle(290, 237, 116, 34));
        Classic176FrontEndSkin.Label(login, "请使用原有账号登录", new Rectangle(24, 278, 384, 22), 9);
        return true;
    }

    public static bool TryBeginOpening(MirAnimatedControl background) => Classic176GateTransition.TryBegin(background);

    public static void ApplyAccount(AccountDialog dialog)
    {
        if (!Settings.UseClassic176Login || !Classic176FrontEndSkin.Available || !Classic176FrontEndSkin.Begin(dialog)) return;
        Classic176FrontEndSkin.Panel(dialog, new Size(560, 492), "注册新账号");
        dialog.Location = Classic176FrontEndSkin.Center(dialog.Size);
        MirTextBox[] inputs = [dialog.AccountIDTextBox, dialog.Password1TextBox, dialog.Password2TextBox,
            dialog.UserNameTextBox, dialog.BirthDateTextBox, dialog.QuestionTextBox, dialog.AnswerTextBox, dialog.EMailTextBox];
        string[] captions = ["账号", "密码", "确认密码", "姓名", "生日", "密保问题", "密保答案", "电子邮箱"];
        for (int i = 0; i < inputs.Length; i++)
        {
            int y = 58 + i * 39;
            Classic176FrontEndSkin.Label(dialog, captions[i], new Rectangle(24, y, 122, 27));
            Classic176FrontEndSkin.Input(inputs[i], new Rectangle(158, y, 374, 27));
        }
        dialog.Description.Location = new Point(24, 377);
        dialog.Description.Size = new Size(508, 60);
        dialog.Description.ForeColour = Classic176FrontEndSkin.Ink;
        dialog.Description.BackColour = Color.FromArgb(255, 15, 13, 10);
        Classic176FrontEndSkin.Button(dialog.OKButton, "提交注册", new Rectangle(100, 446, 156, 34));
        Classic176FrontEndSkin.Button(dialog.CancelButton, "返回登录", new Rectangle(304, 446, 156, 34));
    }

    public static void ApplyPassword(PasswordDialog dialog)
    {
        if (!Settings.UseClassic176Login || !Classic176FrontEndSkin.Available || !Classic176FrontEndSkin.Begin(dialog)) return;
        Classic176FrontEndSkin.Panel(dialog, new Size(480, 332), "修改账号密码");
        dialog.Location = Classic176FrontEndSkin.Center(dialog.Size);
        MirTextBox[] inputs = [dialog.AccountIDTextBox, dialog.CurrentPasswordTextBox, dialog.NewPassword1TextBox, dialog.NewPassword2TextBox];
        string[] captions = ["账号", "当前密码", "新密码", "确认新密码"];
        for (int i = 0; i < inputs.Length; i++)
        {
            int y = 65 + i * 43;
            Classic176FrontEndSkin.Label(dialog, captions[i], new Rectangle(20, y, 116, 28));
            Classic176FrontEndSkin.Input(inputs[i], new Rectangle(146, y, 308, 28));
        }
        Classic176FrontEndSkin.Button(dialog.OKButton, "确认修改", new Rectangle(62, 264, 156, 38));
        Classic176FrontEndSkin.Button(dialog.CancelButton, "返回登录", new Rectangle(262, 264, 156, 38));
    }

    public static void ApplyKeyboard(KeyboardDialog dialog, LoginDialog login = null)
    {
        if (!Settings.UseClassic176Login || !Classic176FrontEndSkin.Available || !Classic176FrontEndSkin.Begin(dialog)) return;
        Classic176FrontEndSkin.Panel(dialog, new Size(220, 280), "");
        // Keep the original six-column key positions and original editing/Enter handlers.
        dialog.Location = new Point(Math.Max(0, Math.Min(Settings.ScreenWidth - 220, (Settings.ScreenWidth + 432) / 2 + 8)),
            Math.Max(0, Math.Min(Settings.ScreenHeight - 280, (Settings.ScreenHeight - 280) / 2)));
        if (login != null && Settings.ScreenWidth < 1000)
        {
            Point oldLocation = login.Location;
            login.Location = new Point((Settings.ScreenWidth - 664) / 2, oldLocation.Y);
            dialog.Location = new Point(login.Location.X + login.Size.Width + 12,
                Math.Min(Settings.ScreenHeight - dialog.Size.Height, login.Location.Y));
            dialog.Disposing += (_, _) => { if (!login.IsDisposed) login.Location = oldLocation; };
        }
        ApplyKeyboardKeys(dialog);
    }

    public static void ApplyKeyboardKeys(KeyboardDialog dialog)
    {
        if (dialog.Library != Libraries.Classic176FrontEnd) return;
        foreach (var button in dialog.Controls.OfType<MirButton>())
        {
            if (!Classic176FrontEndSkin.Begin(button)) continue;
            string caption = button.Controls.OfType<MirLabel>().First().Text;
            var bounds = new Rectangle(button.Location, button.Size);
            if (ReferenceEquals(button, dialog.KeyEscButton)) bounds = new Rectangle(12, 8, 64, 28);
            if (ReferenceEquals(button, dialog.KeyDelButton)) bounds = new Rectangle(140, 76, 64, 28);
            if (ReferenceEquals(button, dialog.KeyRandButton)) bounds = new Rectangle(76, 236, 60, 32);
            if (ReferenceEquals(button, dialog.KeyEnterButton)) bounds = new Rectangle(140, 236, 64, 32);
            Classic176FrontEndSkin.Button(button, caption, bounds);
        }
    }
}
