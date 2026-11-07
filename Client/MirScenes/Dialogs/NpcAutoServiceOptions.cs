using Client.MirControls;
using Client.MirGraphics;

namespace Client.MirScenes.Dialogs
{
    // Keep the custom options outside the upstream dialog's baked artwork.
    public sealed class NpcAutoServiceOptions : MirControl
    {
        public MirCheckBox AutoSellBox { get; }
        public MirCheckBox AutoRepairBox { get; }
        public MirCheckBox AutoPickWeightProtectBox { get; }

        public static void Attach(OptionDialog owner) => _ = new NpcAutoServiceOptions(owner);

        private NpcAutoServiceOptions(OptionDialog owner)
        {
            Size original = owner.Size;
            if (original.Width <= 0 || original.Height <= 0) original = new Size(259, 354);
            owner.AutoSize = false;
            owner.Size = new Size(original.Width, original.Height + 106);
            owner.Location = new Point(Math.Max(0, owner.Location.X), Math.Max(0, owner.Location.Y - 53));
            void KeepInsideScreen() => owner.Location = new Point(
                Math.Clamp(owner.Location.X, 0, Math.Max(0, Settings.ScreenWidth - owner.Size.Width)),
                Math.Clamp(owner.Location.Y, 0, Math.Max(0, Settings.ScreenHeight - owner.Size.Height)));
            // MirImageControl.TrueSize still describes the original art during dragging.
            owner.OnMoving += (_, _) => KeepInsideScreen();
            KeepInsideScreen();

            Parent = owner;
            Location = new Point(8, original.Height - 3);
            Size = new Size(original.Width - 16, 102);
            BackColour = Color.FromArgb(255, 22, 19, 15);
            Border = true;
            BorderColour = Color.FromArgb(92, 78, 56);
            DrawControlTexture = true;

            new MirLabel
            {
                Parent = this, Location = new Point(10, 3), AutoSize = true, NotControl = true,
                ForeColour = Color.FromArgb(235, 210, 155),
                Text = DbLanguage.Chinese ? "NPC 与拾取辅助设置" : "NPC & pickup options"
            };
            AutoSellBox = CreateBox(27,
                DbLanguage.Chinese ? "自动出售普通装备" : "Auto-sell normal equipment",
                DbLanguage.Chinese ? "点击可出售装备的商人时自动出售背包普通装备；跳过极品、强化、镶嵌、绑定及任务物品。" :
                    "Sell eligible normal inventory equipment at a suitable merchant. Protect upgraded, socketed, bound and quest items.");
            AutoRepairBox = CreateBox(51,
                DbLanguage.Chinese ? "自动特修身上装备" : "Auto special-repair equipped gear",
                DbLanguage.Chinese ? "点击支持特修的商人时维修身上装备，消耗金币但保留持久上限；不支持特修时跳过，不做普通修理。" :
                    "Pay for special repair of equipped gear, preserving maximum durability. Skip merchants without special repair; never use normal repair.");
            AutoPickWeightProtectBox = CreateBox(75,
                DbLanguage.Chinese ? "4×4拾取严格负重保护" : "4x4 pickup weight guard",
                DbLanguage.Chinese ? "开启后，4×4范围自动拾取在接近背包负重上限时自动停止捡装备，防止超重跑不动。" :
                    "Stop 4x4 range auto-pickup before exceeding bag weight limit so you can always run.");
            AutoSellBox.Click += (_, _) => { Settings.AutoSellNormalEquipment = AutoSellBox.Checked; NpcAutoService.PreferencesChanged(); Settings.Save(); };
            AutoRepairBox.Click += (_, _) => { Settings.AutoSpecialRepairEquipment = AutoRepairBox.Checked; NpcAutoService.PreferencesChanged(); Settings.Save(); };
            AutoPickWeightProtectBox.Click += (_, _) =>
            {
                Settings.AutoPickWeightProtection = AutoPickWeightProtectBox.Checked;
                Settings.Save();
                NpcAutoService.SyncWeightProtection(silent: false);
            };
            owner.VisibleChanged += (_, _) => Synchronize();
            owner.BeforeDraw += (_, _) => Synchronize();
            Synchronize();
        }

        private MirCheckBox CreateBox(int y, string text, string hint) => new()
        {
            Parent = this, Location = new Point(10, y), AutoSize = false, Size = new Size(Size.Width - 20, 19),
            Library = Libraries.Prguse, UnTickedIndex = 2086, TickedIndex = 2087,
            LabelText = text, Hint = hint
        };

        private void Synchronize()
        {
            if (AutoSellBox.Checked != Settings.AutoSellNormalEquipment) AutoSellBox.Checked = Settings.AutoSellNormalEquipment;
            if (AutoRepairBox.Checked != Settings.AutoSpecialRepairEquipment) AutoRepairBox.Checked = Settings.AutoSpecialRepairEquipment;
            if (AutoPickWeightProtectBox.Checked != Settings.AutoPickWeightProtection) AutoPickWeightProtectBox.Checked = Settings.AutoPickWeightProtection;
        }
    }
}
