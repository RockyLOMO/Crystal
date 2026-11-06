using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using S = ServerPackets;

namespace Server.SoloExtension
{
    public static class TownAndCampModule
    {
        public const uint CampSetGoldCost = 2000;

        // 禁止扎营的终极 Boss 巢穴地图标识或标题关键词
        private static readonly string[] ForbiddenBossMapKeywords =
        {
            "D717", "B7_AngledStoneTomb", "石墓七层",
            "D515", "ZumaTaurusRoom", "祖玛教主之家", "教主之家",
            "D10061", "D10062", "RedMoonRoom", "Great_TaoTomb", "赤月魔穴", "恶魔祭坛",
            "冰宫底层", "IcePalaceBottom", "KingRoom"
        };

        // 沃玛及以上名牌装备保护词根（一键统卖时 100% 自动跳过保护）
        private static readonly string[] FamousGearKeywords =
        {
            "Wooma", "Zuma", "Saint", "Fashin", "Tianzun", "Heaven", "Mir", "RedMoon", "Thunder", "Flame", "Radiant",
            "JudgementMace", "BoneWand", "SoulSabre", "DragonSword", "DragonSlayer", "DragonStaff", "WarMageStaff",
            "DragonlessSword", "SoulSpringWand", "BladesOfDarkness", "FiendBow", "RoyalBlades", "MaleficBow",
            "RubyRing", "PlatinumRing", "DragonRing", "RedEyeRing", "ForceRing", "PowerRing", "PurpleRing", "VioletRing", "TitanRing",
            "GreenNecklace", "EvilBell", "DemonicBells", "SoulNecklace", "KnightBracelet", "DragonBracelet", "SoulBracelet", "3rdEyeBracelet",
            "SteelGlove", "ClearRing", "TeleportRing", "ParalysisRing", "RevivalRing", "ProtectionRing", "MuscleRing",
            "SteelArmour", "DragonRobe", "TitanArmour", "FireRobe", "IronArmour", "PearlArmour",
            "沃玛", "祖玛", "赤月", "圣战", "法神", "天尊", "雷霆", "烈焰", "光芒", "战神", "圣魔", "真魂",
            "裁决", "骨玉", "龙纹", "无极棍", "井中月", "血饮", "屠龙", "嗜魂", "霸者", "怒斩", "龙牙", "逍遥扇", "开天", "镇天", "玄天",
            "红宝石", "铂金", "龙之", "思贝儿", "心灵", "幽灵", "生命项链", "天竺", "绿色项链", "恶魔铃铛", "灵魂项链", "骑士手镯", "阎罗", "力量戒指", "紫碧螺", "泰坦戒指",
            "天魔神甲", "圣战宝甲", "法神披风", "霓裳羽衣", "天尊道袍", "天师长袍"
        };

        public static bool TryHandleNpcCommand(PlayerObject player, string key)
        {
            if (player == null || string.IsNullOrWhiteSpace(key)) return false;
            string cmd = key.Trim().TrimStart('[').TrimEnd(']').ToUpperInvariant();
            switch (cmd)
            {
                case "@CAMP_MENU":
                    OpenCampTentDialog(player);
                    return true;
                case "@CAMP_SET":
                    HandleCampSet(player);
                    return true;
                case "@CAMP_RETURN":
                    HandleCampReturn(player);
                    return true;
                case "@CAMP_TOWN":
                case "@SOLO_QUARTERMASTER":
                    OpenQuartermasterDialog(player);
                    return true;
                case "@SOLO_QUICKSELL":
                    HandleQuickSell(player);
                    return true;
                case "@SOLO_CLEANLOWGEAR":
                    HandleCleanLowGear(player);
                    return true;
                case "@SOLO_QUICKREPAIR":
                    HandleQuickRepair(player);
                    return true;
                case "@SOLO_REFORGE3":
                    HandleThreeInOneReforge(player);
                    return true;
                default:
                    return false;
            }
        }

        private static void DeductGold(PlayerObject player, uint amount)
        {
            if (player?.Account == null || amount == 0) return;
            player.Account.Gold = player.Account.Gold >= amount ? player.Account.Gold - amount : 0;
            player.Enqueue(new S.LoseGold { Gold = amount });
        }

        public static void OpenCampTentDialog(PlayerObject player)
        {
            if (player?.CurrentMap == null) return;

            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            string curMapTitle = DbLanguage.Get("Maps", player.CurrentMap.Info.Title);
            string campDesc = string.IsNullOrEmpty(profile.CampMapFileName)
                ? "{尚未扎营/Red}"
                : $"{{{profile.CampMapTitle} ({profile.CampX}, {profile.CampY})/LimeGreen}}";

            var lines = new List<string>
            {
                "{【玛法行军帐 · 篝火营地】/Gold}",
                $"当前所在：{curMapTitle} ({player.CurrentLocation.X}, {player.CurrentLocation.Y})",
                $"记录营地：{campDesc}",
                "----------------------------------------"
            };

            if (player.InSafeZone)
            {
                lines.Add("你当前处于城镇安全区，可整顿行装后直接返回洞穴营地：");
                lines.Add("<[整理行装，携英雄返回营地]/@Camp_Return>");
                lines.Add(" ");
                lines.Add("{【安全区随身军需与神匠快捷服务】/Khaki}");
                lines.Add("<[一键出售普通战利品 (自动锁死极品与名牌)]/@Solo_QuickSell>");
                lines.Add("<[一键清理15级以下过渡装 (回收新手红紫装减负)]/@Solo_CleanLowGear>");
                lines.Add("<[主号与英雄全身装备一键特修]/@Solo_QuickRepair>");
                lines.Add("<[同名装备三合一极品重铸]/@Solo_Reforge3>");
            }
            else
            {
                lines.Add("你在野外或洞穴深处可升起篝火扎营，回城补给后从安全区瞬间返场。");
                lines.Add("{提示：野外回城请使用【回城卷 / 地牢逃脱卷】，终极Boss房禁止扎营。/Silver}");
                lines.Add(" ");
                lines.Add($"<[在此处升起篝火扎营 (消耗 {CampSetGoldCost:N0} 金币)]/@Camp_Set>");
                lines.Add("----------------------------------------");
                lines.Add("{【行军随身减负服务 (立刻解除背包超重)】/Khaki}");
                lines.Add("<[随身统卖普通白装 (自动锁死极品与名牌)]/@Solo_QuickSell>");
                lines.Add("<[一键清理15级以下过渡装 (回收新手红紫装减负)]/@Solo_CleanLowGear>");
            }

            lines.Add(" ");
            lines.Add("<关闭窗口/@exit>");
            SendCustomDialog(player, lines);
        }

        public static void OpenQuartermasterDialog(PlayerObject player)
        {
            if (player == null) return;
            var lines = new List<string>
            {
                "{【玛法军需官 · 战利品统卖与全身特修】/Gold}",
                "老兵，把包袱里沉甸甸的普通铁器交给我统一折算吧！",
                "{凡是【精良】及以上、带极品点数、幸运/暴击/吸血/插槽或沃玛名牌装备，我都会为你自动锁死保护！/LimeGreen}",
                "----------------------------------------",
                "<[一键出售普通战利品 (100% 锁死保护极品与名牌)]/@Solo_QuickSell>",
                "<[主号与英雄全身装备一键特修 (不掉持久上限)]/@Solo_QuickRepair>",
                "<[同名装备三合一重铸 (3件同名合成极品/传世)]/@Solo_Reforge3>",
                "----------------------------------------",
                "<[打开玛法行军帐面板]/@Camp_Menu>",
                "<[打开玛法猎魔手札面板]/@Tome_Main>",
                "<离开/@exit>"
            };
            SendCustomDialog(player, lines);
        }

        public static void HandleCampSet(PlayerObject player)
        {
            if (player?.CurrentMap == null) return;

            if (player.InSafeZone)
            {
                player.ReceiveChat("⚠️ 你正处于城镇安全区内，无需在此扎营！请前往野外或洞穴探险时扎营。", ChatType.Hint);
                return;
            }

            if (IsForbiddenBossRoom(player.CurrentMap))
            {
                player.ReceiveChat("⚠️ 终极首领巢穴杀机四伏，禁止直接扎营！请退回上一层入口处升起行军帐。", ChatType.System2);
                return;
            }

            if (player.Account.Gold < CampSetGoldCost)
            {
                player.ReceiveChat($"⚠️ 升起篝火扎营需要 {CampSetGoldCost:N0} 金币，你的金币不足！", ChatType.System);
                return;
            }

            DeductGold(player, CampSetGoldCost);

            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            profile.CampMapFileName = player.CurrentMap.Info.FileName;
            profile.CampMapTitle = DbLanguage.Get("Maps", player.CurrentMap.Info.Title);
            profile.CampX = player.CurrentLocation.X;
            profile.CampY = player.CurrentLocation.Y;
            profile.CampUpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            profile.Save(force: true);

            player.ReceiveChat($"⛺ [玛法行军帐] 已在 {profile.CampMapTitle}({profile.CampX},{profile.CampY}) 升起篝火！回城补给后可在安全区双击行军帐直接返场。", ChatType.Hint);
            OpenCampTentDialog(player);
        }

        public static void HandleCampReturn(PlayerObject player)
        {
            if (player?.CurrentMap == null) return;

            if (!player.InSafeZone)
            {
                player.ReceiveChat("⚠️ 必须在城镇安全区内才可整理行装返回营地！野外回城请使用【回城卷】或【地牢逃脱卷】。", ChatType.System2);
                return;
            }

            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            if (string.IsNullOrEmpty(profile.CampMapFileName))
            {
                player.ReceiveChat("⚠️ 你尚未在野外或洞穴记录任何营地坐标！请先在探险途中双击【玛法行军帐】扎营。", ChatType.Hint);
                return;
            }

            Map targetMap = Envir.Main.MapList.FirstOrDefault(m =>
                string.Equals(m.Info.FileName, profile.CampMapFileName, StringComparison.OrdinalIgnoreCase));

            if (targetMap == null)
            {
                player.ReceiveChat("⚠️ 原营地所在地图当前不可达，请重新扎营。", ChatType.System);
                return;
            }

            Point targetPt = new(profile.CampX, profile.CampY);
            if (!targetMap.ValidPoint(targetPt))
                targetPt = EcologyAndRarityModule.FindNearbyWalkablePoint(targetMap, targetPt, 3);

            if (player.Teleport(targetMap, targetPt))
            {
                if (player.Hero != null && player.HeroSpawned && !player.Hero.Dead)
                {
                    Point heroPt = EcologyAndRarityModule.FindNearbyWalkablePoint(targetMap, targetPt, 1);
                    player.Hero.Teleport(targetMap, heroPt);
                }
                player.ReceiveChat($"⛺ [玛法行军帐] 已携英雄重返营地：{profile.CampMapTitle} ({targetPt.X}, {targetPt.Y})！", ChatType.Hint);
            }
        }

        public static void HandleQuickSell(PlayerObject player)
        {
            if (player?.Info?.Inventory == null) return;

            int soldCount = 0;
            int protectedCount = 0;
            ulong totalGoldEarned = 0;

            for (int i = 0; i < player.Info.Inventory.Length; i++)
            {
                UserItem item = player.Info.Inventory[i];
                if (item?.Info == null) continue;
                if (!IsEquipmentType(item.Info.Type)) continue;

                if (IsProtectedEquipment(item))
                {
                    protectedCount++;
                    continue;
                }

                uint sellValue = Math.Max(1u, item.Price() / 2u);
                if ((ulong)player.Account.Gold + totalGoldEarned + sellValue > uint.MaxValue)
                    break;

                player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                player.Info.Inventory[i] = null;
                totalGoldEarned += sellValue;
                soldCount++;
            }

            if (soldCount > 0)
            {
                player.GainGold((uint)totalGoldEarned);
                player.RefreshBagWeight();
                player.ReceiveChat($"💰 [军需统卖] 已批量出售 {soldCount} 件普通白板装备，结算获得 {totalGoldEarned:N0} 金币（已自动锁定保护 {protectedCount} 件极品/名牌装备）。", ChatType.Hint);
            }
            else
            {
                player.ReceiveChat($"🛡️ [军需统卖] 背包中无可出售的普通白装（已为你自动锁定保护 {protectedCount} 件极品/名牌装备）。", ChatType.Hint);
            }
        }

        public static void HandleCleanLowGear(PlayerObject player)
        {
            if (player?.Info?.Inventory == null) return;

            int soldCount = 0;
            ulong totalGoldEarned = 0;

            for (int i = 0; i < player.Info.Inventory.Length; i++)
            {
                UserItem item = player.Info.Inventory[i];
                if (item?.Info == null) continue;
                if (!IsEquipmentType(item.Info.Type)) continue;
                if (IsFamousOrHighTierGear(item.Info)) continue;

                // 仅清理 15 级及以下的新手过渡装备（如布衣、轻型盔甲、木剑、铁手镯、皮手套等）
                bool isLowLevelStarter = item.Info.RequiredType != RequiredType.Level || item.Info.RequiredAmount <= 15;
                if (!isLowLevelStarter) continue;

                uint sellValue = Math.Max(10u, item.Price() / 2u);
                if ((ulong)player.Account.Gold + totalGoldEarned + sellValue > uint.MaxValue)
                    break;

                player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                player.Info.Inventory[i] = null;
                totalGoldEarned += sellValue;
                soldCount++;
            }

            if (soldCount > 0)
            {
                player.GainGold((uint)totalGoldEarned);
                player.RefreshBagWeight();
                player.ReceiveChat($"🪶 [行军减负] 已一键清理 {soldCount} 件15级以下新手过渡装备，折算获得 {totalGoldEarned:N0} 金币，包袱负重已立即恢复！", ChatType.Hint);
            }
            else
            {
                player.ReceiveChat("🪶 [行军减负] 背包中没有需要清理的15级以下新手过渡装备。", ChatType.Hint);
            }
        }

        public static void HandleQuickRepair(PlayerObject player)
        {
            if (player?.Info?.Equipment == null) return;

            int repairedCount = 0;
            uint totalCost = 0;

            // 1. 特修主号全身穿戴装备
            for (int i = 0; i < player.Info.Equipment.Length; i++)
            {
                UserItem item = player.Info.Equipment[i];
                if (!CanSpecialRepairItem(item)) continue;

                uint cost = Math.Max(1u, item.RepairPrice() * 2u);
                if (player.Account.Gold < cost) continue;

                DeductGold(player, cost);
                item.CurrentDura = item.MaxDura;
                item.DuraChanged = false;
                player.Enqueue(new S.ItemRepaired { UniqueID = item.UniqueID, MaxDura = item.MaxDura, CurrentDura = item.CurrentDura });
                repairedCount++;
                totalCost += cost;
            }

            // 2. 特修 Hero 副将全身穿戴装备
            UserItem[] heroEquip = player.Hero?.Info?.Equipment;
            if (heroEquip != null)
            {
                for (int i = 0; i < heroEquip.Length; i++)
                {
                    UserItem item = heroEquip[i];
                    if (!CanSpecialRepairItem(item)) continue;

                    uint cost = Math.Max(1u, item.RepairPrice() * 2u);
                    if (player.Account.Gold < cost) continue;

                    DeductGold(player, cost);
                    item.CurrentDura = item.MaxDura;
                    item.DuraChanged = false;
                    player.Enqueue(new S.ItemRepaired { UniqueID = item.UniqueID, MaxDura = item.MaxDura, CurrentDura = item.CurrentDura });
                    repairedCount++;
                    totalCost += cost;
                }
            }

            if (repairedCount > 0)
            {
                player.RefreshStats();
                player.Hero?.RefreshStats();
                player.ReceiveChat($"🔨 [军需特修] 主号与英雄全身共 {repairedCount} 件装备已特殊修理完毕，消耗 {totalCost:N0} 金币！", ChatType.Hint);
            }
            else
            {
                player.ReceiveChat("✨ [军需特修] 主号与英雄全身穿戴装备持久均已满值（或金币不足），无需修理。", ChatType.Hint);
            }
        }

        public static void HandleThreeInOneReforge(PlayerObject player)
        {
            if (player?.Info?.Inventory == null) return;

            // 查找背包中拥有 >= 3 件同名可重铸的装备组（优先消耗附加点数最低的 3 件）
            var candidateGroup = player.Info.Inventory
                .Select((item, idx) => (item, idx))
                .Where(x => x.item?.Info != null
                            && IsEquipmentType(x.item.Info.Type)
                            && !x.item.Cursed
                            && x.item.RentalInformation == null
                            && x.item.ExpireInfo == null
                            && x.item.Count == 1)
                .GroupBy(x => x.item.Info.Index)
                .FirstOrDefault(g => g.Count() >= 3);

            if (candidateGroup == null)
            {
                player.ReceiveChat("⚒️ [盟重神匠] 背包中未找到 3 件同名装备！请集齐 3 件相同的装备（如 3 把井中月或 3 枚红宝石戒指）再来重铸。", ChatType.Hint);
                return;
            }

            var toConsume = candidateGroup
                .OrderBy(x => x.item.GetSoloRarityTier())
                .ThenBy(x => x.item.GetPrimaryAddedPoints())
                .Take(3)
                .ToList();

            ItemInfo baseInfo = toConsume[0].item.Info;
            uint reforgeCost = baseInfo.RequiredAmount >= 35 ? 50000u : (baseInfo.RequiredAmount >= 22 ? 30000u : 15000u);

            if (player.Account.Gold < reforgeCost)
            {
                player.ReceiveChat($"⚒️ [盟重神匠] 重铸 3 件【{baseInfo.FriendlyName}】需要 {reforgeCost:N0} 金币，你的金币不足！", ChatType.System);
                return;
            }

            DeductGold(player, reforgeCost);

            foreach (var entry in toConsume)
            {
                player.Enqueue(new S.DeleteItem { UniqueID = entry.item.UniqueID, Count = entry.item.Count });
                player.Info.Inventory[entry.idx] = null;
            }

            UserItem reforged = Envir.Main.CreateFreshItem(baseInfo);
            int roll = Envir.Main.Random.Next(100);
            if (roll < 18)
            {
                // 18% 概率直接洗出鲜红【神话・传世】孤品（必出 3 条 +3~+6 且必带特殊词条）
                reforged.RollSoloRarityStats(MonsterRarity.Lord);
                if (!reforged.HasSpecialAddedStats())
                    reforged.RollSpecialAddedStat();
            }
            else if (roll < 55)
            {
                // 37% 概率洗出暗金【史诗】
                reforged.RollSoloRarityStats(MonsterRarity.Elite);
            }
            else
            {
                // 保底必出天蓝【稀有】以上
                reforged.RollSoloRarityStats(MonsterRarity.Rare);
                if (reforged.GetPrimaryAddedPoints() < 3)
                    reforged.AddedStats[Stat.MaxDC] += 2;
            }

            player.GainItem(reforged);
            player.RefreshBagWeight();

            string qualityTag = reforged.GetSoloQualityTag();
            int pts = reforged.GetPrimaryAddedPoints();
            if (reforged.GetSoloRarityTier() >= 4)
            {
                player.ReceiveChat($"🔥 【神匠传世重铸】金光冲天！你用 3 件【{baseInfo.FriendlyName}】重铸出了绝世孤品 {qualityTag}{baseInfo.FriendlyName}（总极品 +{pts} 点）！", ChatType.System2);
            }
            else
            {
                player.ReceiveChat($"⚒️ [盟重神匠] 重铸成功！消耗 3 件同名装备与 {reforgeCost:N0} 金币，获得 {qualityTag}{baseInfo.FriendlyName}（总极品 +{pts} 点）。", ChatType.Hint);
            }
        }

        public static bool IsEquipmentType(ItemType type)
        {
            return type is ItemType.Weapon or ItemType.Armour or ItemType.Helmet
                or ItemType.Necklace or ItemType.Bracelet or ItemType.Ring
                or ItemType.Belt or ItemType.Boots;
        }

        public static bool IsProtectedEquipment(UserItem item)
        {
            if (item?.Info == null) return true;
            if (!IsEquipmentType(item.Info.Type)) return true;

            // 1. 品质 >= 精良 (AddedStats > 0) 或 原生稀有度 >= Rare
            if (item.GetSoloRarityTier() >= 1 || item.Info.Grade >= ItemGrade.Rare)
                return true;

            // 2. 带有任何极品附加属性、幸运/暴击/吸血/攻速、插槽、宝石、觉醒或精炼
            if (item.AddedStats.Count > 0 || item.IsAdded || item.HasSpecialAddedStats())
                return true;
            if (item.GemCount > 0 || item.RefineAdded > 0 || item.RefinedValue != RefinedValue.None)
                return true;
            if (item.Awake != null && (item.Awake.Type != AwakeType.None || item.Awake.GetAwakeLevel() > 0))
                return true;
            if (item.Info.Slots > 0 || (item.Slots != null && item.Slots.Any(s => s != null)))
                return true;

            // 3. 绑定、特殊戒指、套装、任务、灵魂绑定、婚戒、GM制造
            if (item.Info.Bind != BindMode.None || item.Info.Unique != SpecialItemMode.None || item.Info.Set != ItemSet.None)
                return true;
            if (item.Info.NeedIdentify || item.SoulBoundId != -1 || item.WeddingRing != -1 || item.GMMade || item.Cursed)
                return true;
            if (item.Info.Name != null && (item.Info.Name.Contains('[') || item.Info.Name.Contains(']')))
                return true;

            // 4. 沃玛及以上名牌装备保护
            if (IsFamousOrHighTierGear(item.Info))
                return true;

            return false;
        }

        public static bool IsFamousOrHighTierGear(ItemInfo info)
        {
            if (info == null) return false;
            if (info.Price >= 15000 || (info.RequiredType == RequiredType.Level && info.RequiredAmount >= 28))
                return true;

            string rawName = info.Name ?? string.Empty;
            string cnName = info.FriendlyName ?? string.Empty;
            foreach (string kw in FamousGearKeywords)
            {
                if (rawName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    cnName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool IsForbiddenBossRoom(Map map)
        {
            if (map?.Info == null) return true;
            if (map.Info.NoReconnect) return true;

            string fileName = map.Info.FileName ?? string.Empty;
            string title = map.Info.Title ?? string.Empty;
            string cnTitle = DbLanguage.Get("Maps", title) ?? string.Empty;

            foreach (string kw in ForbiddenBossMapKeywords)
            {
                if (fileName.Equals(kw, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    cnTitle.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 若地图固定刷新表包含终极 Boss (IsBoss)，同样视为 Boss 房禁止直接扎营
            if (map.Info.Respawns != null)
            {
                for (int i = 0; i < map.Info.Respawns.Count; i++)
                {
                    MonsterInfo mInfo = Envir.Main.GetMonsterInfo(map.Info.Respawns[i].MonsterIndex);
                    if (mInfo != null && mInfo.IsBoss)
                        return true;
                }
            }

            return false;
        }

        internal static void SendCustomDialog(PlayerObject player, List<string> pageLines)
        {
            if (player == null) return;
            uint npcId = Envir.Main?.DefaultNPC?.LoadedObjectID ?? 1u;
            player.NPCObjectID = npcId;
            player.NPCScriptID = Envir.Main?.DefaultNPC?.ScriptID ?? 0;
            player.NPCDelayed = false;
            player.Enqueue(new S.NPCUpdate { NPCID = npcId });
            player.Enqueue(new S.NPCResponse { Page = pageLines });
        }

        private static bool CanSpecialRepairItem(UserItem item)
        {
            return item?.Info != null
                && item.Info.Durability > 0
                && item.MaxDura > 0
                && item.CurrentDura < item.MaxDura
                && (item.Info.Bind & (BindMode.DontRepair | BindMode.NoSRepair)) == 0
                && (item.RentalInformation?.BindingFlags & (BindMode.DontRepair | BindMode.NoSRepair)) is null or BindMode.None;
        }
    }
}
