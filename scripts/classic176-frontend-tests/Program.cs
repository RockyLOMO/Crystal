using System.Reflection;
using System.Runtime.CompilerServices;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using SlimDX.Direct3D9;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Pass repo root, runtime client directory and independent atlas.");
        args = args.Select(TestWorkspace.ResolvePath).ToArray();
        foreach (string file in new[] { "msvcr100.dll", "msvcp100.dll", "d3dx9_43.dll", "d3dcompiler_43.dll" })
            File.Copy(Path.Combine(args[1], file), Path.Combine(AppContext.BaseDirectory, file), true);
        Run(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string[] args)
    {
        string fixture = Path.Combine(TestWorkspace.RunDirectory, "fixture");
        Directory.CreateDirectory(Path.Combine(fixture, "Data"));
        Directory.CreateDirectory(Path.Combine(fixture, "Localization"));
        File.Copy(Path.Combine(args[0], "Client", "Localization", "Overlay.zh-CN.json"), Path.Combine(fixture, "Localization", "Overlay.zh-CN.json"));
        File.Copy(Path.Combine(args[0], "Client", "Localization", "Chinese.json"), Path.Combine(fixture, "Localization", "Chinese.json"));
        Directory.SetCurrentDirectory(fixture);
        using var pixel = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(pixel);
        using var form = new Client.CMain();
        using var nativeHost = new Form { ShowInTaskbar = false, Opacity = 0, Location = new Point(-32000,-32000), StartPosition=FormStartPosition.Manual };
        nativeHost.Show(); // Invisible native edit host only; no CMain.Load or server/network loop.
        var assembly = typeof(LoginScene).Assembly;
        assembly.GetType("Client.Program")!.GetField("Form")!.SetValue(null, form);
        Client.CMain.Graphics = graphics;
        Client.CMain.Cursors = Enumerable.Repeat(Cursors.Default, 256).ToArray();
        DbLanguage.Load(Path.Combine(args[0], "Localization"), "Chinese");
        GameLanguage.LoadClientLanguage(Path.Combine(fixture,"Localization","Chinese.json"));
        var settings = assembly.GetType("Client.Settings")!;
        void Set(string key, object value) => settings.GetField(key)!.SetValue(null, value);
        Set("FontName", "Microsoft YaHei"); Set("FullScreen", false);
        Set("ScreenWidth", 1366); Set("ScreenHeight", 768); Set("Resolution", 1366);
        Set("AccountID", ""); Set("Password", ""); Set("UseClassic176Login", true); Set("UseClassic176Select", true);
        int passed = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine($"PASS {++passed}: {name}"); }
        T Get<T>(object o, string key) => (T)o.GetType().GetField(key, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
        void Click(MirButton b) => b.InvokeMouseClick(EventArgs.Empty);
        string Caption(MirButton b) => b.Controls.OfType<MirLabel>().First().Text;
        bool Enabled(MirControl c) => (bool)typeof(MirControl).GetField("_enabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!;
        bool Inside(MirControl c) => new Rectangle(Point.Empty, new Size((int)settings.GetField("ScreenWidth")!.GetValue(null)!, (int)settings.GetField("ScreenHeight")!.GetValue(null)!)).Contains(c.DisplayRectangle);
        using (var missing = new LoginScene()) Check(Get<LoginScene.LoginDialog>(missing, "_login").Library == Libraries.Prguse, "missing atlas preserves original login");
        using (var missing = new SelectScene([])) Check(missing.Background.Library == Libraries.Prguse, "missing atlas preserves original selection");
        File.Copy(args[2], Path.Combine(fixture, "Data", "Classic176FrontEnd.Lib"));
        Libraries.Classic176FrontEnd.Initialize();
        var dx = assembly.GetType("Client.MirGraphics.DXManager")!;
        dx.GetMethod("Create")!.Invoke(null, null);
        foreach (string name in new[] { "Prguse", "Title", "ChrSel" })
        {
            File.Copy(Path.Combine(args[1], "Data", name + ".Lib"), Path.Combine(fixture, "Data", name + ".Lib"));
            ((MLibrary)typeof(Libraries).GetField(name)!.GetValue(null)!).Initialize();
        }
        var device = (Device)dx.GetField("Device")!.GetValue(null)!;
        var sprite = (Sprite)dx.GetField("Sprite")!.GetValue(null)!;
        var gateFrame = assembly.GetType("Client.MirControls.Classic176GateTransition")!
            .GetMethod("Frame", BindingFlags.Static | BindingFlags.NonPublic)!;
        void Render(MirControl background, string name)
        {
            void KeepNativeHostHidden(MirControl c)
            {
                if (c is MirTextBox text)
                {
                    typeof(MirControl).GetField("HasShown", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, true);
                    var visibilityHandler=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),text,typeof(MirTextBox).GetMethod("TextBox_VisibleChanged",BindingFlags.Instance|BindingFlags.NonPublic)!);
                    text.TextBox.VisibleChanged-=visibilityHandler; text.TextBox.ParentChanged-=visibilityHandler;
                    text.TextBox.Parent=nativeHost;
                    text.TextBox.Visible=text.Visible;
                }
                foreach (var child in c.Controls) KeepNativeHostHidden(child);
            }
            // Textboxes render their regular DX textures; prevent native edit hosts from attaching to an invisible Form.
            KeepNativeHostHidden(background);
            device.Clear(ClearFlags.Target, Color.Black, 0, 0);
            device.BeginScene(); sprite.Begin(SpriteFlags.AlphaBlend);
            // Never call LoginScene.Draw: its Shown event connects to a server.
            background.Draw(); sprite.End(); device.EndScene();
            Surface.ToFile((Surface)dx.GetField("MainSurface")!.GetValue(null)!, Path.Combine(TestWorkspace.RunDirectory, name + ".png"), ImageFileFormat.Png);
            Check(true, "real DX9 preview: " + name);
        }
        var completeField=typeof(MLibrary).GetField("_images",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var completeFrames=(MImage[])completeField.GetValue(Libraries.Classic176FrontEnd)!;
        // Retain all existing gate/four-card fallback regressions on the previous atlas shape.
        completeField.SetValue(Libraries.Classic176FrontEnd,completeFrames.Take(13).ToArray());
        foreach (var size in new[] { new Size(800,600), new Size(1024,768), new Size(1280,720), new Size(1366,768), new Size(1920,1080) })
        {
            Set("ScreenWidth", size.Width); Set("ScreenHeight", size.Height); Set("Resolution", size.Width);
            using var scene = new LoginScene(); MirScene.ActiveScene = scene;
            var login = Get<LoginScene.LoginDialog>(scene, "_login");
            var bg = Get<MirAnimatedControl>(scene, "_background");
            Check(!login.Visible, $"{size}: login waits for accepted ClientVersion");
            scene.ProcessPacket(new ServerPackets.ClientVersion { Result = 1 });
            Check(login.Visible && login.Library == Libraries.Classic176FrontEnd && Inside(login), $"{size}: accepted version shows classic login inside screen");
            Check(bg.Controls.Distinct().Count() == bg.Controls.Count && bg.Controls[0].GetType().Name == "BackgroundArt", $"{size}: art appears once behind native controls");
            int count = bg.Controls.Count; Classic176LoginLayout.Apply(scene,bg,login);
            Check(count == bg.Controls.Count, $"{size}: login layout is idempotent");
            Check(login.PasswordTextBox.Password && login.AccountIDTextBox.MaxLength == Globals.MaxAccountIDLength, $"{size}: native password masking and limits retained");
            foreach(var button in new[]{login.AccountButton,login.PassButton,login.ViewKeyButton})
            {
                Client.CMain.MPoint=button.DisplayLocation.Add(new Point(button.Size.Width/2,button.Size.Height/2));
                scene.OnMouseMove(new MouseEventArgs(MouseButtons.None,0,Client.CMain.MPoint.X,Client.CMain.MPoint.Y,0));
                Check(MirControl.MouseControl==button,$"{size}: login entry reachable through real scene routing");
            }
            if (size.Width == 1366)
            {
                Render(bg, "login-1366");
                login.AccountIDTextBox.Text="testuser"; login.PasswordTextBox.Text="FakePass123";
                Check(Enabled(login.OKButton), "dummy valid fields enable original login action");
                Render(bg, "login-filled-1366");
                var network=assembly.GetType("Client.MirNetwork.Network")!;
                var queue=new System.Collections.Concurrent.ConcurrentQueue<Packet>();
                var send=network.GetField("_sendList",BindingFlags.Static|BindingFlags.NonPublic)!;
                send.SetValue(null,queue);
                Click(login.OKButton);
                login.TextBox_KeyPress(login.PasswordTextBox.TextBox,new KeyPressEventArgs((char)Keys.Enter));
                Check(queue.Count==1 && queue.TryPeek(out var submitted) && submitted is ClientPackets.Login && !Enabled(login.OKButton), "original submit enqueues one packet and blocks a second Enter");
                scene.ProcessPacket(new ServerPackets.Login { Result=2 });
                Check(Enabled(login.OKButton),"server login rejection restores original retry action");
                send.SetValue(null,null);
                foreach(var box in scene.Controls.OfType<MirMessageBox>().ToArray()) box.Dispose();
                login.AccountIDTextBox.Text=""; login.PasswordTextBox.Text="";
                Set("AccountID", ""); Set("Password", "");
            }
            Click(login.AccountButton);
            var account = Get<LoginScene.NewAccountDialog>(scene,"_account");
            Check(!login.Visible && Inside(account) && account.Library == Libraries.Classic176FrontEnd && account.Controls.OfType<MirTextBox>().Count() == 8, $"{size}: original register event opens all eight fields");
            if (size.Width == 1366) Render(bg, "register-1366");
            Click(account.CancelButton); Check(login.Visible && account.IsDisposed, $"{size}: original registration cancel returns to login");
            Click(login.PassButton); var password = Get<LoginScene.ChangePasswordDialog>(scene,"_password");
            Check(!login.Visible && Inside(password) && password.Controls.OfType<MirTextBox>().Count() == 4, $"{size}: password event keeps four fields");
            if (size.Width == 1366) Render(bg,"password-1366");
            Click(password.CancelButton); Check(login.Visible && password.IsDisposed, $"{size}: original password cancel returns to login");
            Point oldLogin = login.Location;
            Click(login.ViewKeyButton); var keyboard = Get<LoginScene.InputKeyDialog>(scene,"_ViewKey");
            Check(Inside(keyboard) && !keyboard.DisplayRectangle.IntersectsWith(login.DisplayRectangle), $"{size}: keyboard and login do not overlap");
            var oldKeys = keyboard.Controls.OfType<MirButton>().Where(b => Caption(b).Length == 1).ToArray();
            for (int i=0;i<5;i++) Click(keyboard.KeyRandButton);
            var keys = keyboard.Controls.OfType<MirButton>().Where(b => Caption(b).Length == 1).ToArray();
            Check(oldKeys.All(k=>k.IsDisposed) && keys.Length == 36 && keys.Select(Caption).Distinct().Count()==36 && keys.All(k=>k.Library==Libraries.Classic176FrontEnd), $"{size}: random keyboard replaces keys without losing labels or styling");
            Check(keyboard.Controls.OfType<MirButton>().All(b=>keyboard.DisplayRectangle.Contains(b.DisplayRectangle)), $"{size}: keyboard keys fit panel");
            if (size.Width == 1366) Render(bg,"keyboard-1366");
            Click(keyboard.KeyEscButton); Check(keyboard.IsDisposed && login.Location == oldLogin, $"{size}: keyboard escape restores login position");
            var chars = Enumerable.Range(0,4).Select(i=>new SelectInfo { Index=i+1, Name="玛法勇士"+i, Level=(ushort)(20+i), Class=(MirClass)i, Gender=(MirGender)(i%2), LastAccess=DateTime.Now.AddMinutes(-i) }).ToList();
            int completions = 0;
            bg.AfterAnimation += (_, _) => completions++;
            var success = new ServerPackets.LoginSuccess { Characters=chars };
            scene.ProcessPacket(success);
            Check(!Enabled(scene) && bg.Animated && login.IsDisposed, $"{size}: LoginSuccess retains original finite transition");
            scene.ProcessPacket(success);
            Check(bg.AnimationCount == 12 && bg.AnimationDelay == 120 && completions == 0,
                $"{size}: duplicate success does not restart the 1440ms gate transition");
            for(int step=0;step<12;step++)
            {
                Check(!scene.IsDisposed && (int)gateFrame.Invoke(null,new object[]{bg})! == 3+Math.Min(step,9),
                    $"{size}: opening step {step} uses its real door frame and holds the final frame");
                if(size.Width==1366) Render(bg,$"gate-{step:D2}");
                Client.CMain.Time+=120; bg.UpdateOffSet();
            }
            Check(scene.IsDisposed && MirScene.ActiveScene is SelectScene && completions==1, $"{size}: transition reaches selection exactly once");
            scene.ProcessPacket(success);
            for(int repeat=0;repeat<3;repeat++) { Client.CMain.Time+=120; bg.UpdateOffSet(); }
            Check(completions==1, $"{size}: disposed login cannot replay the completion callback");
            using var select = (SelectScene)MirScene.ActiveScene;
            Check(select.CharacterButtons.Length==4 && select.CharacterButtons.All(Inside) && select.CharacterButtons.All(b=>b.Library==Libraries.Classic176FrontEnd), $"{size}: four original character cards stay in bounds");
            Check(new[]{select.StartGameButton,select.NewCharacterButton,select.DeleteCharacterButton,select.CreditsButton,select.ExitGameButton}.All(Inside), $"{size}: all five native selection actions stay in bounds");
            foreach(var button in select.CharacterButtons.Cast<MirControl>().Append(select.StartGameButton))
            {
                Client.CMain.MPoint=button.DisplayLocation.Add(new Point(button.Size.Width/2,button.Size.Height/2));
                select.OnMouseMove(new MouseEventArgs(MouseButtons.None,0,Client.CMain.MPoint.X,Client.CMain.MPoint.Y,0));
                Check(MirControl.MouseControl==button,$"{size}: select entry reachable through real scene routing");
            }
            Check(select.CharacterButtons.Select(b=>b.NameLabel.Text).Distinct().Count()==4 && Enabled(select.StartGameButton), $"{size}: original character metadata and start enabled state retained");
            if (size.Width == 1366) { select.CharacterDisplay.Opacity=1; Render(select.Background,"select-1366"); }
            Click(select.NewCharacterButton); var create=select.NewCharacterDialog;
            Check(create.Visible && Inside(create) && create.Library==Libraries.Classic176FrontEnd, $"{size}: native create event opens classic popup");
            Check(!select.CharacterDisplay.Visible,$"{size}: creation popup hides underlying selection preview");
            var classes=new[]{create.WarriorButton,create.WizardButton,create.TaoistButton,create.AssassinButton,create.ArcherButton};
            for(int i=0;i<5;i++) foreach(var gender in new[]{MirGender.Male,MirGender.Female})
            {
                Click(classes[i]); Click(gender==MirGender.Male?create.MaleButton:create.FemaleButton);
                Check((int)create.Class==i && create.Gender==gender && create.CharacterDisplay.Library==Libraries.ChrSel && create.CharacterDisplay.AnimationCount==16, $"{size}: class {i} gender {gender} keeps original preview/events");
            }
            if(size.Width==1366) Render(select,"create-1366");
            Click(create.CancelButton);
            Check(select.CharacterDisplay.Visible,$"{size}: cancel restores original selection preview");
            Click(select.NewCharacterButton);
            Check(create.Class==MirClass.Warrior && create.Gender==MirGender.Male, $"{size}: reopening resets to native defaults");
            if(size.Width==1366) Render(select,"create-reopen-1366");
        }
        // Simulate the previously deployed three-entry atlas without overwriting a file
        // or changing the original libraries. Optional door frames must remain optional.
        var imageField=typeof(MLibrary).GetField("_images",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var images=(MImage[])imageField.GetValue(Libraries.Classic176FrontEnd)!;
        try
        {
            imageField.SetValue(Libraries.Classic176FrontEnd,images.Take(3).ToArray());
            using var legacy=new LoginScene(); MirScene.ActiveScene=legacy;
            var bg=Get<MirAnimatedControl>(legacy,"_background");
            int completed=0;bg.AfterAnimation+=(_,_)=>completed++;
            var success=new ServerPackets.LoginSuccess{Characters=[]};
            legacy.ProcessPacket(success);legacy.ProcessPacket(success);
            Check((int)gateFrame.Invoke(null,new object[]{bg})! == 0,
                "legacy three-entry atlas keeps its static gate when optional animation frames are absent");
            for(int step=0;step<12;step++){Client.CMain.Time+=120;bg.UpdateOffSet();}
            Check(completed==1 && legacy.IsDisposed && MirScene.ActiveScene is SelectScene,
                "legacy three-entry atlas still reaches selection once without a black screen or stuck transition");
            MirScene.ActiveScene.Dispose();
        }
        finally {imageField.SetValue(Libraries.Classic176FrontEnd,images);}
        Set("UseClassic176Login",false); Set("UseClassic176Select",false);
        using(var original=new LoginScene()) Check(Get<LoginScene.LoginDialog>(original,"_login").Library==Libraries.Prguse,"login flag independently restores original");
        using(var original=new SelectScene([])) Check(original.Background.Library==Libraries.Prguse,"selection flag independently restores original");
        Set("UseClassic176Login",true);
        using(var classic=new LoginScene()) Check(Get<LoginScene.LoginDialog>(classic,"_login").Library==Libraries.Classic176FrontEnd,"login can be enabled while select is disabled");
        using(var original=new SelectScene([])) Check(original.Background.Library==Libraries.Prguse,"select stays original while login enabled");
        Set("UseClassic176Select",true);
        using(var empty=new SelectScene([])) Check(!Enabled(empty.StartGameButton) && empty.CharacterButtons.All(b=>b.NameLabel.Text==""),"empty slots preserve disabled enter and no invented characters");
        completeField.SetValue(Libraries.Classic176FrontEnd,completeFrames);
        NativeRoleChecks.Run(Set,Check,Render);
        Console.WriteLine($"Classic front-end: {passed} checks passed; PNGs and isolated fixture: {TestWorkspace.RunDirectory}");
    }
}
