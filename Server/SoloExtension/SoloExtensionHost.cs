using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using Shared;
using S = ServerPackets;

namespace Server.SoloExtension
{
    /// <summary>
    /// 玛法独行扩展宿主总入口 (SoloExtensionHost)
    /// 统一收敛 8 处 One-Line Hook，对主工程保持零侵入、单行转发。
    /// </summary>
    public static class SoloExtensionHost
    {
        public const int CampTentItemIndex = 990001;
        public const int HunterTomeItemIndex = 990002;
        public const short CampTentShape = 901;
        public const short HunterTomeShape = 902;

        public const string CampTentItemName = "玛法行军帐";
        public const string HunterTomeItemName = "玛法猎魔手札";

        /// <summary>
        /// Hook 1: Envir.StartEnvir() 启动初始化
        /// - 注入扩展专属奇物（【玛法行军帐】、【玛法猎魔手札】）
        /// - 确保比奇/盟重安全区军需官与盟重神匠 NPC 脚本及实体就绪
        /// </summary>
        public static void ApplyInGameChinese(Envir envir)
        {
            if (envir == null) return;

            try
            {
                EnsureCustomItems(envir);
                EnsureSoloNpcScriptsAndSpawns(envir);
            }
            catch (Exception ex)
            {
                MessageQueue.Instance.Enqueue($"[SoloExtension] ApplyInGameChinese 异常: {ex.Message}");
            }
        }

        private static void EnsureCustomItems(Envir envir)
        {
            ushort scrollImage = envir.ItemInfoList.FirstOrDefault(x => x != null && x.Type == ItemType.Scroll)?.Image ?? 250;
            ushort bookImage = envir.ItemInfoList.FirstOrDefault(x => x != null && x.Type == ItemType.Book)?.Image ?? scrollImage;

            const BindMode permanentBind = BindMode.DontDrop | BindMode.DontDeathdrop | BindMode.DontSell |
                                           BindMode.DontTrade | BindMode.DontStore | BindMode.DontUpgrade;

            ItemInfo campTent = envir.ItemInfoList.FirstOrDefault(x => x != null && (x.Index == CampTentItemIndex || x.Name == CampTentItemName));
            if (campTent == null)
            {
                campTent = new ItemInfo
                {
                    Index = CampTentItemIndex,
                    Name = CampTentItemName,
                    Type = ItemType.Scroll,
                    Shape = CampTentShape,
                    Image = scrollImage,
                    Weight = 0,
                    Durability = 1,
                    StackSize = 1,
                    StartItem = true,
                    Bind = permanentBind,
                    ToolTip = "【玛法独行奇物】双击呼出行军营帐界面：地宫扎营记录、主将与英雄安全区休整、返回行军帐点（不占负重，死亡不掉落）。"
                };
                envir.ItemInfoList.Add(campTent);
            }

            if (!envir.StartItems.Contains(campTent))
            {
                envir.StartItems.Add(campTent);
            }

            ItemInfo hunterTome = envir.ItemInfoList.FirstOrDefault(x => x != null && (x.Index == HunterTomeItemIndex || x.Name == HunterTomeItemName));
            if (hunterTome == null)
            {
                hunterTome = new ItemInfo
                {
                    Index = HunterTomeItemIndex,
                    Name = HunterTomeItemName,
                    Type = ItemType.Scroll,
                    Shape = HunterTomeShape,
                    Image = bookImage,
                    Weight = 0,
                    Durability = 1,
                    StackSize = 1,
                    StartItem = true,
                    Bind = permanentBind,
                    ToolTip = "【玛法独行奇物】双击翻阅猎魔手札：开启4格自动拾取、感知全图精英/领主方位与地图热度、检视玛法生态图鉴永久属性加成。"
                };
                envir.ItemInfoList.Add(hunterTome);
            }

            if (!envir.StartItems.Contains(hunterTome))
            {
                envir.StartItems.Add(hunterTome);
            }
        }

        private static void EnsureSoloNpcScriptsAndSpawns(Envir envir)
        {
            if (string.IsNullOrWhiteSpace(Settings.NPCPath) || !Directory.Exists(Settings.NPCPath))
                return;

            string quartermasterScript = "00SoloQuartermaster";
            string quartermasterPath = Path.Combine(Settings.NPCPath, quartermasterScript + ".txt");
            if (!File.Exists(quartermasterPath))
            {
                File.WriteAllText(quartermasterPath,
                    "[@MAIN]\r\n" +
                    "#SAY\r\n" +
                    "欢迎来到【玛法军需处】！独行玛法，后勤至关重要。\\\r\n" +
                    "我可为你安全回收包袱中的白板杂物（自动保护精良以上、极品、铭文与名牌装备），\\\r\n" +
                    "也可一键满耐久全身特修主将与英雄装备，或呼叫行军帐与猎魔手札。\\\r\n\\\r\n" +
                    "<【一键安全回收杂物】/@Solo_QuickSell>    <【主将+英雄全身特修】/@Solo_QuickRepair>\\\r\n" +
                    "<【打开玛法行军帐】/@CAMP_MENU>            <【翻阅玛法猎魔手札】/@TOME_MAIN>\\\r\n" +
                    "<离开/@exit>\r\n");
            }

            string smithScript = "00SoloMasterSmith";
            string smithPath = Path.Combine(Settings.NPCPath, smithScript + ".txt");
            if (!File.Exists(smithPath))
            {
                File.WriteAllText(smithPath,
                    "[@MAIN]\r\n" +
                    "#SAY\r\n" +
                    "老夫乃【盟重神匠】！打到多余的同名祖玛、赤月或沃玛装备莫要丢弃。\\\r\n" +
                    "只需在背包中放入 3 件同名装备与 50,000 金币，\\\r\n" +
                    "老夫便可为你三合一重铸，保底赋予【稀有】及以上极品词条，更有 20% 概率觉醒【神话・传世】特殊属性！\\\r\n\\\r\n" +
                    "<【同名装备三合一重铸 (50,000金币)】/@Solo_Reforge3>\\\r\n" +
                    "<【一键安全回收杂物】/@Solo_QuickSell>    <【主将+英雄全身特修】/@Solo_QuickRepair>\\\r\n" +
                    "<离开/@exit>\r\n");
            }

            SpawnSoloNpcIfAbsent(envir, "0", "玛法军需官", quartermasterScript, new Point(328, 265), 15);
            SpawnSoloNpcIfAbsent(envir, "3", "玛法军需官", quartermasterScript, new Point(333, 325), 15);
            SpawnSoloNpcIfAbsent(envir, "3", "盟重神匠", smithScript, new Point(326, 325), 12);
        }

        private static void SpawnSoloNpcIfAbsent(Envir envir, string mapFileName, string npcName, string scriptFile, Point preferredPoint, ushort image)
        {
            Map map = envir.MapList.FirstOrDefault(m =>
                m?.Info != null && string.Equals(m.Info.FileName, mapFileName, StringComparison.OrdinalIgnoreCase));
            if (map == null || map.Cells == null) return;

            if (map.NPCs.Any(n => n?.Info != null && n.Info.Name == npcName))
                return;

            Point spawnLocation = FindValidNpcPoint(map, preferredPoint);
            if (!map.ValidPoint(spawnLocation)) return;

            NPCInfo info = new NPCInfo
            {
                Index = 990000 + map.Info.Index * 10 + map.NPCs.Count,
                MapIndex = map.Info.Index,
                Name = npcName,
                FileName = scriptFile,
                Location = spawnLocation,
                Image = image,
                Rate = 100
            };

            NPCObject npcObj = new NPCObject(info) { CurrentMap = map };
            map.AddObject(npcObj);
        }

        private static Point FindValidNpcPoint(Map map, Point center)
        {
            if (map.ValidPoint(center) && map.NPCs.All(n => n.CurrentLocation != center))
                return center;

            for (int r = 1; r <= 6; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        Point candidate = new Point(center.X + dx, center.Y + dy);
                        if (map.ValidPoint(candidate) && map.NPCs.All(n => n.CurrentLocation != candidate))
                            return candidate;
                    }
                }
            }

            if (map.Info.SafeZones.Count > 0)
            {
                Point sz = map.Info.SafeZones[0].Location;
                for (int r = 1; r <= 6; r++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        for (int dy = -r; dy <= r; dy++)
                        {
                            Point candidate = new Point(sz.X + dx, sz.Y + dy);
                            if (map.ValidPoint(candidate) && map.NPCs.All(n => n.CurrentLocation != candidate))
                                return candidate;
                        }
                    }
                }
            }

            return center;
        }

        /// <summary>
        /// Hook 2: PlayerObject.GainExp(uint amount)
        /// 接管 6 段动态经验衰减曲线与双英雄（主将 75% + 英雄 45% = 120%）协同分润。
        /// </summary>
        public static uint ApplySmoothExpMultiplier(ushort level, uint amount)
        {
            if (amount == 0) return 0;
            float stageMultiplier = GetStageExpMultiplier(level);
            return (uint)Math.Max(1, amount * stageMultiplier);
        }

        public static float GetStageExpMultiplier(ushort level)
        {
            if (level < 22) return 2.5f;   // [第1段] 1~21级: 新手筑基与英雄解锁期
            if (level < 35) return 1.5f;   // [第2段] 22~34级: 沃玛/蜈蚣洞成型期
            if (level < 42) return 1.0f;   // [第3段] 35~41级: 祖玛/赤月攻坚期
            if (level < 50) return 0.6f;   // [第4段] 42~49级: 魔龙/新技能进阶期
            if (level < 58) return 0.35f;  // [第5段] 50~57级: 狐月/雪域/雷炎觉醒期
            return 0.15f;                  // [第6段] 58级+: 传世养老期
        }

        /// <summary>
        /// Hook 3: PlayerObject.Process() 每秒心跳
        /// - 存量老号自动补发【玛法行军帐】与【玛法猎魔手札】
        /// - 同步图鉴永久属性 Buff
        /// - 执行 4×4 范围自动拾取金币/药水/高级装备
        /// </summary>
        public static void OnPlayerTick(PlayerObject player)
        {
            if (player == null || player.Dead || player.Info == null || player.CurrentMap == null)
                return;

            long now = Envir.Main.Time;
            if (now < player.SoloNextTickTime)
                return;

            player.SoloNextTickTime = now + 1000;

            EnsureStarterCurios(player);
            HunterTomeModule.SyncCodexBonuses(player);
            HunterTomeModule.ProcessRangeAutoPickup(player);
        }

        private static void EnsureStarterCurios(PlayerObject player)
        {
            if (player?.Info?.Inventory == null) return;
            var profile = SoloPlayerProfile.GetOrLoad(player.Name);
            if (profile.StarterItemsGranted) return;

            GiveCurioIfMissing(player, CampTentItemIndex, CampTentItemName);
            GiveCurioIfMissing(player, HunterTomeItemIndex, HunterTomeItemName);

            profile.StarterItemsGranted = true;
            profile.Save(force: true);
        }

        private static void GiveCurioIfMissing(PlayerObject player, int itemIndex, string itemName)
        {
            bool hasItem = player.Info.Inventory.Any(x =>
                x?.Info != null && (x.Info.Index == itemIndex || x.Info.Name == itemName));
            if (hasItem) return;

            ItemInfo info = Envir.Main.ItemInfoList.FirstOrDefault(x =>
                x != null && (x.Index == itemIndex || x.Name == itemName));
            if (info == null) return;

            UserItem curio = Envir.Main.CreateFreshItem(info);
            if (player.CanGainItem(curio))
            {
                player.GainItem(curio);
            }
        }

        /// <summary>
        /// Hook 4: PlayerObject.UseItem(ulong id)
        /// 拦截【玛法行军帐】与【玛法猎魔手札】的双击使用，弹出对应功能对话框且不消耗道具。
        /// </summary>
        public static bool TryUseCustomItem(PlayerObject player, UserItem item)
        {
            if (player == null || item?.Info == null) return false;

            bool isCampTent = item.Info.Index == CampTentItemIndex ||
                              (item.Info.Type == ItemType.Scroll && item.Info.Shape == CampTentShape) ||
                              string.Equals(item.Info.Name, CampTentItemName, StringComparison.OrdinalIgnoreCase);

            bool isHunterTome = item.Info.Index == HunterTomeItemIndex ||
                                (item.Info.Type == ItemType.Scroll && item.Info.Shape == HunterTomeShape) ||
                                string.Equals(item.Info.Name, HunterTomeItemName, StringComparison.OrdinalIgnoreCase);

            if (!isCampTent && !isHunterTome) return false;

            // 解锁客户端背包格（Success = false 表示不扣除该永久奇物）
            player.Enqueue(new S.UseItem
            {
                UniqueID = item.UniqueID,
                Grid = MirGridType.Inventory,
                Success = false
            });

            if (isCampTent)
                TownAndCampModule.OpenCampTentDialog(player);
            else
                HunterTomeModule.OpenHunterTomeDialog(player);

            return true;
        }

        /// <summary>
        /// Hook 5: NPCScript.Call / PlayerObject.CallNPC / CallDefaultNPC
        /// 统一拦截自定义对话按钮指令（行军帐、军需官、盟重神匠、猎魔手札）。
        /// </summary>
        public static bool TryHandleNpcAction(PlayerObject player, string key)
        {
            if (player == null || string.IsNullOrWhiteSpace(key)) return false;

            if (TownAndCampModule.TryHandleNpcCommand(player, key))
                return true;

            if (HunterTomeModule.TryHandleNpcCommand(player, key))
                return true;

            return false;
        }

        /// <summary>
        /// Hook 6: MonsterObject.Spawn()
        /// 4 阶怪物生态变异判定与属性挂载。
        /// </summary>
        public static void OnMonsterSpawn(MonsterObject mob, MonsterRarity? forcedRarity = null)
        {
            EcologyAndRarityModule.OnMonsterSpawn(mob, forcedRarity);
        }

        /// <summary>
        /// Hook 7: MonsterObject.ProcessAI()
        /// [领主] 2 阶段动态 AI 心跳（50% HP 召唤亲卫、20% HP 狂暴死战禁回血）。
        /// </summary>
        public static void OnMonsterTick(MonsterObject mob)
        {
            EcologyAndRarityModule.OnMonsterTick(mob);
        }

        /// <summary>
        /// Hook 8: MonsterObject.Die()
        /// 地图狩猎热度演进（50 刷精英、100 刷深红领主）与玛法猎魔图鉴击杀计数。
        /// </summary>
        public static void OnMonsterKilled(MonsterObject mob, MapObject expOwner)
        {
            EcologyAndRarityModule.OnMonsterKilled(mob, expOwner);
            HunterTomeModule.OnMonsterKilled(mob, expOwner);
        }
    }
}
