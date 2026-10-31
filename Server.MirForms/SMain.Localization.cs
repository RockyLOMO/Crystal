namespace Server
{
    public partial class SMain
    {
        // Operator UI stays English independently of Chinese game messages.
        // Keep this gate separate from Settings.Language, which controls text
        // sent to players and must remain available for Chinese localization.
        private static bool ChineseMainWindow => false;

        // Presentation only. Never translate database keys or editable record names.
        private static readonly Dictionary<string, string> MainWindowChinese = new(StringComparer.Ordinal)
        {
            ["Logs"] = "运行日志",
            ["Debug Logs"] = "调试日志",
            ["Chat Logs"] = "聊天日志",
            ["Send Message"] = "发送全服消息",
            ["Send"] = "发送",
            ["Players Online"] = "在线玩家",
            ["Guilds"] = "行会",
            ["Index"] = "编号",
            ["Name"] = "名称",
            ["Level"] = "等级",
            ["Class"] = "职业",
            ["Gender"] = "性别",
            ["Current Map"] = "当前地图",
            ["Leader"] = "会长",
            ["Members"] = "成员",
            ["Gold"] = "金币",
            ["GT"] = "行会领地租期",
            ["Players: 0"] = "玩家：0",
            ["Monsters: 0"] = "怪物：0",
            ["Connections: 0"] = "连接：0",
            ["Blocked IPs: 0"] = "封禁 IP：0",
            ["CycleDelay: 0"] = "循环耗时：0",
            ["Uptime:"] = "运行时间：",
            ["Control"] = "控制",
            ["Start Server"] = "启动服务端",
            ["Stop Server"] = "停止服务端",
            ["Reboot Server"] = "重启服务端",
            ["Clear Blocked IPs"] = "清除封禁 IP",
            ["Close Server"] = "关闭服务端",
            ["Reload"] = "重新加载",
            ["NPCs"] = "NPC 脚本",
            ["Drops"] = "掉落表",
            ["Line Message"] = "定时公告",
            ["Account"] = "账号",
            ["Accounts"] = "账号管理",
            ["Market"] = "交易市场",
            ["Namelists"] = "名称列表",
            ["Database"] = "数据库",
            ["Map"] = "地图",
            ["Item (OLD- HIDDEN)"] = "物品（旧版，隐藏）",
            ["Monster(OLD - HIDDEN)"] = "怪物（旧版，隐藏）",
            ["Item"] = "物品",
            ["Monster"] = "怪物",
            ["NPC"] = "NPC 角色",
            ["Quest"] = "任务",
            ["Magic"] = "技能",
            ["Gameshop"] = "游戏商城",
            ["Recipe"] = "配方",
            ["Config"] = "配置",
            ["Server"] = "服务端",
            ["Balance"] = "数值平衡",
            ["System"] = "系统",
            ["Dragon"] = "龙系统",
            ["Mining"] = "挖矿",
            ["Fishing"] = "钓鱼",
            ["Mail"] = "邮件",
            ["Goods"] = "商品",
            ["Refining"] = "精炼",
            ["Relationship"] = "婚姻关系",
            ["Mentor"] = "师徒",
            ["Gem"] = "宝石",
            ["Conquest"] = "攻城",
            ["SpawnTick"] = "刷怪时钟",
            ["Heroes"] = "英雄",
            ["Monster Tuner"] = "怪物调试",
            ["Drop Builder"] = "掉落表编辑器",
            ["Characters"] = "角色管理",
            ["Warrior"] = "战士",
            ["Wizard"] = "法师",
            ["Taoist"] = "道士",
            ["Assassin"] = "刺客",
            ["Archer"] = "弓箭手",
            ["Male"] = "男",
            ["Female"] = "女",
            ["DELETED"] = "已删除",
            ["None"] = "无",
            ["Unknown"] = "未知",
            ["Notice"] = "提示",
            ["Server must be running to tune monsters"] = "请先启动服务端，再调试怪物。",
        };

        private static string MainText(string text) =>
            ChineseMainWindow && MainWindowChinese.TryGetValue(text, out string translated) ? translated : text;

        private void LocalizeMainWindow()
        {
            if (!ChineseMainWindow) return;
            Text = "传奇 2 服务端";
            LocalizeMainControl(this);
        }

        private static void LocalizeMainControl(Control control)
        {
            control.Text = MainText(control.Text);

            if (control is ListView list)
                foreach (ColumnHeader column in list.Columns)
                    column.Text = MainText(column.Text);

            if (control is ToolStrip strip)
                LocalizeMainMenuItems(strip.Items);

            foreach (Control child in control.Controls)
                LocalizeMainControl(child);
        }

        private static void LocalizeMainMenuItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.Text = MainText(item.Text);
                if (item is ToolStripDropDownItem dropdown)
                    LocalizeMainMenuItems(dropdown.DropDownItems);
            }
        }
    }
}
