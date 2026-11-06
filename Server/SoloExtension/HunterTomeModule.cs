using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace Server.SoloExtension
{
    public static class HunterTomeModule
    {
        public sealed class CodexChapter
        {
            public string Id { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string[] MonsterKeywords { get; init; } = Array.Empty<string>();
            public int TargetKills { get; init; }
            public int BonusOffense { get; init; }
            public int BonusDefense { get; init; }
            public int BonusDropPercent { get; init; }
            public string RewardDesc { get; init; } = string.Empty;
        }

        public static readonly CodexChapter[] Chapters =
        {
            new()
            {
                Id = "BichonWilds",
                Title = "第一章·比奇荒野 (钉耙猫/多角虫/半兽人)",
                MonsterKeywords = new[] { "Cat", "Keratoid", "Oma", "ShellNipper", "Spider", "钉耙猫", "多角虫", "盔甲虫", "半兽" },
                TargetKills = 50,
                BonusOffense = 0,
                BonusDefense = 1,
                BonusDropPercent = 0,
                RewardDesc = "防御/魔御 +1"
            },
            new()
            {
                Id = "DeadMine",
                Title = "第二章·废矿尸群 (僵尸/尸王)",
                MonsterKeywords = new[] { "Zombie", "Ghoul", "僵尸", "尸王" },
                TargetKills = 80,
                BonusOffense = 1,
                BonusDefense = 0,
                BonusDropPercent = 0,
                RewardDesc = "攻/魔/道 +1"
            },
            new()
            {
                Id = "DeathValley",
                Title = "第三章·死亡山谷 (蜈蚣/钳虫/跳跳蜂/触龙神)",
                MonsterKeywords = new[] { "Worm", "Centipede", "Tongs", "Maggot", "Bee", "Moth", "蜈蚣", "钳虫", "跳跳蜂", "恶蛆", "触龙神" },
                TargetKills = 120,
                BonusOffense = 0,
                BonusDefense = 2,
                BonusDropPercent = 0,
                RewardDesc = "防御/魔御 +2"
            },
            new()
            {
                Id = "WoomaTemple",
                Title = "第四章·沃玛神殿 (沃玛战士/沃玛卫士/沃玛教主)",
                MonsterKeywords = new[] { "Wooma", "BugBat", "沃玛", "暗黑战士", "火焰沃玛" },
                TargetKills = 120,
                BonusOffense = 2,
                BonusDefense = 0,
                BonusDropPercent = 0,
                RewardDesc = "攻/魔/道 +2"
            },
            new()
            {
                Id = "StoneTomb",
                Title = "第五章·石墓烈焰 (红黑野猪/白野猪/蝎蛇)",
                MonsterKeywords = new[] { "Boar", "KingHog", "SnakeScorpion", "KingScorpion", "野猪", "白野猪", "蝎蛇", "邪恶毒蛇" },
                TargetKills = 100,
                BonusOffense = 2,
                BonusDefense = 0,
                BonusDropPercent = 5,
                RewardDesc = "攻/魔/道 +2，爆率 +5%"
            },
            new()
            {
                Id = "ZumaTemple",
                Title = "第六章·祖玛阁楼 (祖玛卫士/雕像/弓箭手/教主)",
                MonsterKeywords = new[] { "Zuma", "BigRat", "祖玛" },
                TargetKills = 150,
                BonusOffense = 3,
                BonusDefense = 2,
                BonusDropPercent = 0,
                RewardDesc = "攻/魔/道 +3，防御/魔御 +2"
            },
            new()
            {
                Id = "RedMoonGorge",
                Title = "第七章·赤月魔穴 (血巨人/双头金刚/月魔蜘蛛/赤月恶魔)",
                MonsterKeywords = new[] { "Ape", "RedMoon", "CrystalSpider", "VenomSpider", "LureSpider", "GreatSpider", "赤月", "月魔", "血巨人", "双头", "黑锷", "幻影蜘蛛" },
                TargetKills = 150,
                BonusOffense = 3,
                BonusDefense = 0,
                BonusDropPercent = 8,
                RewardDesc = "攻/魔/道 +3，爆率 +8%"
            },
            new()
            {
                Id = "EndgameRealms",
                Title = "第八章·神龙雪域 (牛魔/狐月/雷炎/冰宫/雪域魔物)",
                MonsterKeywords = new[] { "Minotaur", "Fox", "Frozen", "Snow", "Glacier", "Hell", "Flame", "Tucson", "Horned", "牛魔", "狐", "冰", "雪", "魔龙", "雷炎" },
                TargetKills = 200,
                BonusOffense = 5,
                BonusDefense = 4,
                BonusDropPercent = 12,
                RewardDesc = "攻/魔/道 +5，双防 +4，爆率 +12%"
            }
        };

        public static bool TryHandleNpcCommand(PlayerObject player, string key)
        {
            if (player == null || string.IsNullOrWhiteSpace(key)) return false;
            string cmd = key.Trim().TrimStart('[').TrimEnd(']').ToUpperInvariant();
            switch (cmd)
            {
                case "@TOME_MAIN":
                    OpenHunterTomeDialog(player);
                    return true;
                case "@TOME_TOGGLEPICK":
                    HandleToggleRangePickup(player);
                    return true;
                case "@TOME_SENSE":
                    HandleSenseMapEcology(player);
                    return true;
                case "@TOME_CODEX":
                    HandleShowMonsterCodex(player);
                    return true;
                default:
                    return false;
            }
        }

        public static void OpenHunterTomeDialog(PlayerObject player)
        {
            if (player?.CurrentMap == null) return;

            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            string pickModeStr = GetPickModeLabel(profile.AutoPickRangeMode);
            int heat = EcologyAndRarityModule.GetMapHeat(player.CurrentMap);
            var (offense, defense, dropRate, completedCount) = CalculateCodexBonuses(profile);

            var lines = new List<string>
            {
                "{【玛法猎魔手札 · 随身奇物】/Gold}",
                $"当前 4×4 范围自动拾取：{{{pickModeStr}/LimeGreen}}",
                $"本层魔物惊扰度进度：{{{heat}/100/DeepSkyBlue}} (满50出精英 / 满100出猩红领主)",
                $"已激活猎魔图鉴加成：{{攻魔道+{offense}、双防+{defense}、爆率+{dropRate}% ({completedCount}/{Chapters.Length}章)/Gold}}",
                "----------------------------------------",
                "<[切换] 4×4 范围自动拾取模式 (智能极品/全部/关闭)/@Tome_TogglePick>",
                "<[感知] 探查本层魔物惊扰度与存活领主方位/@Tome_Sense>",
                "<[图鉴] 查阅玛法猎魔图鉴进度与永久属性加成/@Tome_Codex>"
            };

            if (player.InSafeZone)
            {
                lines.Add("----------------------------------------");
                lines.Add("{【安全区军需与营地快捷通道】/Khaki}");
                lines.Add("<[营地] 打开玛法行军帐返场/@Camp_Menu>");
                lines.Add("<[统卖] 一键出售普通战利品 (锁极品)/@Solo_QuickSell>");
                lines.Add("<[特修] 主号与英雄全身特修/@Solo_QuickRepair>");
            }

            lines.Add(" ");
            lines.Add("<合上手札/@exit>");
            TownAndCampModule.SendCustomDialog(player, lines);
        }

        public static void HandleToggleRangePickup(PlayerObject player)
        {
            if (player == null) return;
            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            profile.AutoPickRangeMode = profile.AutoPickRangeMode switch
            {
                2 => 1,
                1 => 0,
                _ => 2
            };
            profile.Save(force: true);

            string label = GetPickModeLabel(profile.AutoPickRangeMode);
            player.ReceiveChat($"📖 [玛法猎魔手札] 4×4 范围自动拾取已切换为：{label}。", ChatType.Hint);
            OpenHunterTomeDialog(player);
        }

        public static void HandleSenseMapEcology(PlayerObject player)
        {
            if (player?.CurrentMap == null) return;

            Map map = player.CurrentMap;
            int heat = EcologyAndRarityModule.GetMapHeat(map);
            string mapTitle = DbLanguage.Get("Maps", map.Info.Title);

            var rareMobs = new List<MonsterObject>();
            foreach (var obj in Envir.Main.Objects)
            {
                if (obj is MonsterObject mob && mob.CurrentMap == map && !mob.Dead && mob.Master == null && mob.Rarity >= MonsterRarity.Rare)
                {
                    rareMobs.Add(mob);
                }
            }

            rareMobs.Sort((a, b) => b.Rarity.CompareTo(a.Rarity));

            var lines = new List<string>
            {
                $"{{【本层猎魔感知 · {mapTitle}】/Gold}}",
                $"当前区域杀戮惊扰度：{{{heat}/100/DeepSkyBlue}}",
                heat < 50
                    ? $"再累积 {50 - heat} 点惊扰度将引出【精英】魔物！"
                    : $"再累积 {100 - heat} 点惊扰度将惊动猩红【领主】霸主！",
                "----------------------------------------"
            };

            if (rareMobs.Count == 0)
            {
                lines.Add("{当前层暂未感知到存活的【精英/统领/领主】气息，继续清剿魔物可提高惊扰度！/Silver}");
            }
            else
            {
                lines.Add($"感知到本层共有 {{{rareMobs.Count}/LimeGreen}} 头高阶变异魔物存活：");
                foreach (var mob in rareMobs.Take(6))
                {
                    string dir = GetCompassDirection(player.CurrentLocation, mob.CurrentLocation);
                    int dist = Functions.MaxDistance(player.CurrentLocation, mob.CurrentLocation);
                    string cnName = DbLanguage.Monster(mob.Name);
                    string color = mob.Rarity == MonsterRarity.Lord ? "Red" : (mob.Rarity == MonsterRarity.Elite ? "Gold" : "DeepSkyBlue");
                    lines.Add($"· {{{cnName}/{color}}} 位于 ({mob.CurrentLocation.X},{mob.CurrentLocation.Y}) [{dir} {dist}步]");
                }
            }

            lines.Add("----------------------------------------");
            lines.Add("<返回手札首页/@Tome_Main>    <关闭/@exit>");
            TownAndCampModule.SendCustomDialog(player, lines);
        }

        public static void HandleShowMonsterCodex(PlayerObject player)
        {
            if (player == null) return;
            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            var (offense, defense, dropRate, completedCount) = CalculateCodexBonuses(profile);

            var lines = new List<string>
            {
                "{【玛法猎魔图鉴 · 永久属性加持】/Gold}",
                $"当前总加成：{{攻/魔/道 +{offense} | 防御/魔御 +{defense} | 掉宝率 +{dropRate}%/LimeGreen}}",
                "----------------------------------------"
            };

            foreach (var ch in Chapters)
            {
                int kills = profile.MonsterKills.TryGetValue(ch.Id, out int k) ? k : 0;
                bool done = kills >= ch.TargetKills;
                string status = done ? "{[已圆满]/LimeGreen}" : $"({kills}/{ch.TargetKills})";
                lines.Add($"{ch.Title} {status} -> {{{ch.RewardDesc}/Khaki}}");
            }

            lines.Add("----------------------------------------");
            lines.Add("<返回手札首页/@Tome_Main>    <关闭/@exit>");
            TownAndCampModule.SendCustomDialog(player, lines);
        }

        public static void OnMonsterKilled(MonsterObject mob, MapObject expOwner)
        {
            PlayerObject killer = EcologyAndRarityModule.ResolvePlayer(expOwner);
            if (killer == null || mob?.Info == null || mob.Master != null) return;

            var profile = SoloPlayerProfile.GetOrLoad(killer.Name);
            string rawName = mob.Info.Name ?? string.Empty;
            string gameName = mob.Info.GameName ?? string.Empty;
            string cnName = DbLanguage.Monster(gameName) ?? string.Empty;

            // 精英/统领/领主击杀额外计入多倍图鉴进度（变异+2、精英+3、统领+5、领主+10）
            int progressDelta = mob.Rarity switch
            {
                MonsterRarity.Lord => 10,
                MonsterRarity.Elite => 5,
                MonsterRarity.Rare => 3,
                MonsterRarity.Uncommon => 2,
                _ => 1
            };

            bool milestoneUnlocked = false;
            foreach (var ch in Chapters)
            {
                if (!MatchesChapter(ch, rawName, gameName, cnName)) continue;

                int prev = profile.MonsterKills.TryGetValue(ch.Id, out int cur) ? cur : 0;
                int next = prev + progressDelta;
                profile.MonsterKills[ch.Id] = next;
                profile.IsDirty = true;

                if (prev < ch.TargetKills && next >= ch.TargetKills)
                {
                    milestoneUnlocked = true;
                    killer.ReceiveChat($"🏆 【猎魔图鉴圆满】恭喜完成《{ch.Title}》！永久激活图鉴属性：{ch.RewardDesc}！", ChatType.System2);
                }
            }

            if (milestoneUnlocked)
            {
                profile.Save(force: true);
                SyncCodexBonuses(killer, forceRefresh: true);
            }
            else if (profile.IsDirty && (Envir.Main.Time - profile.LastSavedTick >= 30000))
            {
                profile.Save(force: false);
            }
        }

        public static void SyncCodexBonuses(PlayerObject player, bool forceRefresh = false)
        {
            if (player?.Info == null || Envir.Main.BuffInfoList.Count == 0) return;

            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            var (offense, defense, dropRate, completedCount) = CalculateCodexBonuses(profile);
            int score = offense * 1000 + defense * 100 + dropRate;

            if (!forceRefresh && profile.LastSyncedCodexScore == score)
                return;

            profile.LastSyncedCodexScore = score;
            if (score <= 0) return;

            var codexStats = new Stats
            {
                [Stat.MinDC] = offense,
                [Stat.MaxDC] = offense,
                [Stat.MinMC] = offense,
                [Stat.MaxMC] = offense,
                [Stat.MinSC] = offense,
                [Stat.MaxSC] = offense,
                [Stat.MinAC] = defense,
                [Stat.MaxAC] = defense,
                [Stat.MinMAC] = defense,
                [Stat.MaxMAC] = defense,
                [Stat.ItemDropRatePercent] = dropRate
            };

            player.RemoveBuff(BuffType.General);
            player.AddBuff(BuffType.General, player, Settings.Minute * 1440, codexStats, refreshStats: true);
        }

        public static void ProcessRangeAutoPickup(PlayerObject player)
        {
            if (player == null || player.Dead || player.CurrentMap == null || player.Info?.Inventory == null)
                return;

            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            if (profile.AutoPickRangeMode <= 0) return;

            Map map = player.CurrentMap;
            Point center = player.CurrentLocation;
            int pickedItems = 0;

            // 4x4 范围自动拾取（以角色为中心半径 2 格范围）
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int x = center.X + dx;
                    int y = center.Y + dy;
                    if (!map.ValidPoint(x, y)) continue;

                    Cell cell = map.GetCell(x, y);
                    if (cell?.Objects == null || cell.Objects.Count == 0) continue;

                    for (int i = cell.Objects.Count - 1; i >= 0; i--)
                    {
                        if (cell.Objects[i] is not ItemObject itemObj) continue;

                        if (itemObj.Owner != null && itemObj.Owner != player && itemObj.Owner != player.Hero)
                        {
                            if (player.GroupMembers == null || !(itemObj.Owner is PlayerObject ownerPlayer && player.GroupMembers.Contains(ownerPlayer)))
                                continue;
                        }

                        if (itemObj.Gold > 0)
                        {
                            if ((ulong)player.Account.Gold + itemObj.Gold <= uint.MaxValue)
                            {
                                player.GainGold(itemObj.Gold);
                                map.RemoveObject(itemObj);
                                itemObj.Despawn();
                            }
                            continue;
                        }

                        UserItem item = itemObj.Item;
                        if (item?.Info == null) continue;

                        if (profile.AutoPickRangeMode == 2 && !ShouldSmartPickupItem(item))
                            continue;

                        if (player.CanGainItem(item))
                        {
                            item.Identified = true;
                            player.GainItem(item);
                            map.RemoveObject(itemObj);
                            itemObj.Despawn();
                            pickedItems++;
                            if (pickedItems >= 6) return; // 每秒最多批量拾取 6 件物品，保持帧率丝滑
                        }
                    }
                }
            }
        }

        public static bool ShouldSmartPickupItem(UserItem item)
        {
            if (item?.Info == null) return false;

            // 1. 精良及以上品质极品装备（或带任何附加词条/名牌装备）必捡
            if (item.GetSoloRarityTier() >= 1 || TownAndCampModule.IsProtectedEquipment(item))
            {
                if (TownAndCampModule.IsEquipmentType(item.Info.Type))
                    return true;
            }

            // 2. 普通白板垃圾装备跳过不捡，彻底解决包袱超重痛点！
            if (TownAndCampModule.IsEquipmentType(item.Info.Type))
                return false;

            // 3. 矿石仅拾取纯度 >= 10 的高纯度矿石
            if (item.Info.Type == ItemType.Ore)
                return item.CurrentDura >= 10000;

            // 4. 肉块/低价值任务杂物过滤，药水、卷轴、技能书、材料、宝石、护身符正常拾取
            if (item.Info.Type == ItemType.Meat)
                return false;

            return true;
        }

        public static (int Offense, int Defense, int DropRate, int CompletedCount) CalculateCodexBonuses(SoloPlayerProfile profile)
        {
            int offense = 0, defense = 0, drop = 0, completed = 0;
            if (profile?.MonsterKills == null) return (0, 0, 0, 0);

            foreach (var ch in Chapters)
            {
                if (profile.MonsterKills.TryGetValue(ch.Id, out int kills) && kills >= ch.TargetKills)
                {
                    offense += ch.BonusOffense;
                    defense += ch.BonusDefense;
                    drop += ch.BonusDropPercent;
                    completed++;
                }
            }
            return (offense, defense, drop, completed);
        }

        private static bool MatchesChapter(CodexChapter chapter, string rawName, string gameName, string cnName)
        {
            foreach (string kw in chapter.MonsterKeywords)
            {
                if (rawName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    gameName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    cnName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetPickModeLabel(int mode)
        {
            return mode switch
            {
                1 => "开启 (4×4 全部拾取)",
                2 => "智能 (4×4 仅拾取金币/药水/技能书与精良以上极品)",
                _ => "已关闭"
            };
        }

        private static string GetCompassDirection(Point from, Point to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx == 0 && dy == 0) return "脚下";

            string ns = dy < -2 ? "北" : (dy > 2 ? "南" : "");
            string ew = dx < -2 ? "西" : (dx > 2 ? "东" : "");
            if (string.IsNullOrEmpty(ns) && string.IsNullOrEmpty(ew))
                return "身旁";
            if (string.IsNullOrEmpty(ns)) return "正" + ew;
            if (string.IsNullOrEmpty(ew)) return "正" + ns;
            return ew + ns;
        }
    }
}
