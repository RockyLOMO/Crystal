using System.Collections.Concurrent;
using System.Reflection;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;

internal static class NativeRoleChecks
{
    internal static void Run(Action<string,object> set, Action<bool,string> check, Action<MirControl,string> render)
    {
        bool Enabled(MirControl c) => (bool)typeof(MirControl).GetField("_enabled",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!;
        var assembly=typeof(SelectScene).Assembly;
        var queue=new ConcurrentQueue<Packet>();
        assembly.GetType("Client.MirNetwork.Network")!.GetField("_sendList",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,queue);
        Libraries.Loaded=true;
        set("UseClassic176Select",true);
        foreach(var size in new[]{new Size(800,600),new Size(1024,768),new Size(1280,720),new Size(1366,768),new Size(1920,1080)})
        {
            set("ScreenWidth",size.Width);set("ScreenHeight",size.Height);set("Resolution",size.Width);
            var roles=Enumerable.Range(0,4).Select(i=>new SelectInfo{Index=700+i,Name="复古角色"+(i+1),Level=(ushort)(20+i),Class=(MirClass)(i%3),Gender=(MirGender)(i%2),LastAccess=DateTime.Now.AddMinutes(-i)}).ToList();
            roles[0].Class=roles[1].Class=MirClass.Wizard;roles[0].Gender=roles[1].Gender=MirGender.Male;
            using var scene=new SelectScene(roles); MirScene.ActiveScene=scene;
            check(scene.CharacterButtons.Count(b=>b.Visible)==2 && Classic176RoleLayout.PageIndex(scene)==0,$"{size}: native cave shows only its first two of four preserved role slots");
            check(scene.Background.Controls.Count(c=>c.GetType().Name=="Portrait" && c.Visible)==2 && !scene.CharacterDisplay.Visible,$"{size}: native cave has two independent role portraits instead of the original single preview");
            int count=scene.Background.Controls.Count;
            Classic176RoleLayout.Apply(scene);Classic176RoleLayout.Refresh(scene);
            check(count==scene.Background.Controls.Count,$"{size}: repeated native hook does not duplicate portraits or paging controls");
            foreach(var button in scene.Background.Controls.OfType<MirButton>().Where(b=>b.Visible &&
                (b.Hint=="选择这个角色" || b.Hint=="上一页角色" || b.Hint=="下一页角色")))
            {
                Client.CMain.MPoint=button.DisplayLocation.Add(new Point(button.Size.Width/2,button.Size.Height/2));
                scene.OnMouseMove(new MouseEventArgs(MouseButtons.None,0,Client.CMain.MPoint.X,Client.CMain.MPoint.Y,0));
                check(MirControl.MouseControl==button,$"{size}: native role selection/paging reaches its real button through scene routing");
            }
            scene.CharacterButtons[1].InvokeMouseClick(EventArgs.Empty);
            Client.CMain.Time+=1400;
            if(size.Width==1366)render(scene.Background,"native-select-page1");
            Classic176RoleLayout.SetPage(scene,1);
            check(scene.CharacterButtons.Skip(2).All(b=>b.Visible) && scene.CharacterButtons.Take(2).All(b=>!b.Visible),$"{size}: second page exposes roles three/four and hides the first pair");
            check(scene.CharacterButtons[2].Selected,$"{size}: page navigation reuses the original third-role selection event");
            scene.CharacterButtons[3].InvokeMouseClick(EventArgs.Empty);
            Client.CMain.Time+=1400;
            if(size.Width==1366)render(scene.Background,"native-select-page2");
            scene.StartGameButton.InvokeMouseClick(EventArgs.Empty);
            check(queue.TryDequeue(out var p) && p is ClientPackets.StartGame start && start.CharacterIndex==703 && queue.IsEmpty,$"{size}: entering from page two sends exactly the original fourth-role ID");
            Classic176RoleLayout.SetPage(scene,0);
            check(scene.CharacterButtons[0].Selected && scene.CharacterButtons[0].Visible,$"{size}: returning to first page restores the correct original role selection");
            scene.NewCharacterButton.InvokeMouseClick(EventArgs.Empty);
            var create=scene.NewCharacterDialog;
            check(create.Size==new Size(300,417) && create.Visible && scene.Background.Controls.Where(c=>c.GetType().Name=="Portrait").All(c=>!c.Visible),$"{size}: native 300x417 creation frame hides underlying portraits");
            var buttons=new[]{create.WarriorButton,create.WizardButton,create.TaoistButton,create.AssassinButton,create.ArcherButton};
            foreach(var type in Enumerable.Range(0,5))foreach(var gender in new[]{MirGender.Male,MirGender.Female})
            {
                buttons[type].InvokeMouseClick(EventArgs.Empty);
                (gender==MirGender.Male?create.MaleButton:create.FemaleButton).InvokeMouseClick(EventArgs.Empty);
                Client.CMain.MPoint=buttons[type].DisplayLocation.Add(new Point(15,15));
                scene.OnMouseMove(new MouseEventArgs(MouseButtons.None,0,Client.CMain.MPoint.X,Client.CMain.MPoint.Y,0));
                check((int)create.Class==type && create.Gender==gender && buttons.All(b=>b.Index==-1 && b.TrueSize==b.Size) && MirControl.MouseControl==buttons[type],$"{size}: native creation keeps class {type}/{gender} events and valid hit rectangles after index refresh");
            }
            if(size.Width==1366)render(scene,"native-create");
            create.CancelButton.InvokeMouseClick(EventArgs.Empty);
            check(!create.Visible && scene.Background.Controls.Count(c=>c.GetType().Name=="Portrait" && c.Visible)==2,$"{size}: creation cancel restores the pair without altering account data");
            scene.NewCharacterButton.InvokeMouseClick(EventArgs.Empty);
            check(create.Class==MirClass.Warrior && create.Gender==MirGender.Male,$"{size}: native creation reopening keeps the original class/gender defaults");
            create.CancelButton.InvokeMouseClick(EventArgs.Empty);
        }
        set("ScreenWidth",1366);set("ScreenHeight",768);set("Resolution",1366);
        using(var empty=new SelectScene([]))
        {
            MirScene.ActiveScene=empty;Classic176RoleLayout.SetPage(empty,1);
            empty.OnKeyPress(new KeyPressEventArgs((char)Keys.Enter));
            check(!Enabled(empty.StartGameButton) && !Enabled(empty.DeleteCharacterButton) && queue.IsEmpty,"empty page cannot start or delete an invisible previously selected character");
        }
        using(var one=new SelectScene([new SelectInfo{Index=812,Name="唯一角色",Class=MirClass.Warrior}]))
        {
            Classic176RoleLayout.SetPage(one,1);
            check(!Enabled(one.StartGameButton) && !Enabled(one.DeleteCharacterButton),"paging a single character to an empty pair disables destructive/enter actions");
            Classic176RoleLayout.SetPage(one,0);
            check(Enabled(one.StartGameButton) && Enabled(one.DeleteCharacterButton) && one.CharacterButtons[0].Selected,"returning from an empty page restores the original valid role");
        }
        foreach(var type in new[]{MirClass.Warrior,MirClass.Wizard,MirClass.Taoist})foreach(var gender in new[]{MirGender.Male,MirGender.Female})
        {
            int statue=Classic176RoleLayout.PortraitFrame(type,gender,false,100000);
            int reveal=Classic176RoleLayout.PortraitFrame(type,gender,true,0);
            int end=Classic176RoleLayout.PortraitFrame(type,gender,true,960);
            int action=Classic176RoleLayout.PortraitFrame(type,gender,true,1040);
            check(statue==reveal && end==reveal+12 && action==reveal-20 &&
                Classic176RoleLayout.PortraitFrame(type,gender,true,1040+16*120)==action,
                $"{type}/{gender}: gray statue reveals once, then colorful animation loops without flashing gray");
        }
        check(Classic176RoleLayout.PortraitFrame(MirClass.Archer,MirGender.Female,true,0)==-1 &&
            Classic176RoleLayout.PortraitFrame(MirClass.Assassin,MirGender.Male,true,0)==-1,
            "additional Crystal classes retain their original model family instead of being removed");
        using(var extra=new SelectScene([new SelectInfo{Index=900,Name="刺客示例",Class=MirClass.Assassin},new SelectInfo{Index=901,Name="弓手示例",Class=MirClass.Archer,Gender=MirGender.Female}]))
        {
            MirScene.ActiveScene=extra;render(extra.Background,"native-extra-classes");
            check(extra.CharacterButtons.Take(2).All(c=>c.Visible) && extra.Characters.Count==2,"native cave renders both additional classes without dropping character data");
        }
    }
}
