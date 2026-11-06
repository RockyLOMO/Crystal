using System;
using System.Collections.Concurrent;
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace Server.SoloExtension
{
    public static class EcologyAndRarityModule
    {
        // 记录每张地图当前的杀戮惊扰度 (Map Heat: 0 ~ 100)
        private static readonly ConcurrentDictionary<Map, int> MapHeatDict = new();

        public static int GetMapHeat(Map map)
        {
            if (map == null) return 0;
            return MapHeatDict.TryGetValue(map, out int heat) ? heat : 0;
        }

        public static void SetMapHeat(Map map, int heat)
        {
            if (map == null) return;
            MapHeatDict[map] = Math.Clamp(heat, 0, 100);
        }

        // 1. 怪物生成时的四阶变异判定
        public static void OnMonsterSpawn(MonsterObject mob, MonsterRarity? forcedRarity = null)
        {
            if (mob?.Info == null || mob.Race != ObjectType.Monster)
                return;

            if (mob.Info.AI == 6 || mob.Info.IsBoss || mob.Master != null || mob.Info.Stats[Stat.HP] >= 15000)
            {
                mob.Rarity = MonsterRarity.None;
                mob.EliteTier = 0;
                mob.EliteSubType = -1;
                mob.CustomDisplayName = null;
                return;
            }

            // 统一由四阶变异系统接管，避免原生 MonsterType 双重叠加前缀
            mob.SetMonsterType(MonsterType.Normal);
            mob.LordGuardsSummoned = false;
            mob.LordEnraged = false;

            if (forcedRarity.HasValue)
            {
                mob.Rarity = forcedRarity.Value;
            }
            else if (mob.Rarity == MonsterRarity.None)
            {
                int roll = Envir.Main.Random.Next(10000);
                if (roll < 10)       mob.Rarity = MonsterRarity.Lord;     // 0.10% 猩红[领主]
                else if (roll < 35)  mob.Rarity = MonsterRarity.Elite;    // 0.25% 暗金[统领]
                else if (roll < 115) mob.Rarity = MonsterRarity.Rare;     // 0.80% 天蓝[精英]
                else if (roll < 365) mob.Rarity = MonsterRarity.Uncommon; // 2.50% 翠绿[变异]
                else                 mob.Rarity = MonsterRarity.None;
            }

            switch (mob.Rarity)
            {
                case MonsterRarity.Uncommon:
                    mob.EliteTier = 0;
                    mob.EliteSubType = -1;
                    mob.Name = $"[变异]{mob.Info.GameName}";
                    mob.NameColour = Color.LimeGreen;
                    break;

                case MonsterRarity.Rare:
                    // 随机赋予三大功能性副词条：[富饶](必爆大堆金币)、[坚韧](必掉纯度15+黑铁)、[狂暴](高攻掉武器)
                    if (mob.EliteSubType < 0 || mob.EliteSubType > 2)
                        mob.EliteSubType = Envir.Main.Random.Next(3);
                    int subType = mob.EliteSubType;
                    string tag = subType == 0 ? "精英・富饶" : (subType == 1 ? "精英・坚韧" : "精英・狂暴");
                    mob.EliteTier = 1;
                    mob.Name = $"[{tag}]{mob.Info.GameName}";
                    mob.NameColour = subType == 0 ? Color.Gold : (subType == 1 ? Color.MediumSpringGreen : Color.DeepSkyBlue);
                    break;

                case MonsterRarity.Elite:
                    mob.EliteTier = 2;
                    mob.EliteSubType = -1;
                    mob.Name = $"[统领]{mob.Info.GameName}";
                    mob.NameColour = Color.Gold;
                    break;

                case MonsterRarity.Lord:
                    mob.EliteTier = 2;
                    mob.EliteSubType = -1;
                    mob.Name = $"[领主]血煞{mob.Info.GameName}";
                    mob.NameColour = Color.Crimson;
                    mob.CoolEye = true; // 破除隐身术
                    break;

                default:
                    mob.EliteTier = 0;
                    mob.EliteSubType = -1;
                    mob.CustomDisplayName = null;
                    mob.NameColour = Color.White;
                    break;
            }
        }

        // 在 MonsterObject.RefreshBase() 中应用四阶属性倍率，保证 Buff/毒素刷新时不丢失变异属性
        public static bool ApplyRarityStats(MonsterObject mob)
        {
            if (mob == null || mob.Master != null || mob.Rarity == MonsterRarity.None)
                return false;

            switch (mob.Rarity)
            {
                case MonsterRarity.Uncommon:
                    ScaleStat(mob, Stat.HP, 2.0);
                    ScaleStat(mob, Stat.MaxDC, 1.20);
                    return true;

                case MonsterRarity.Rare:
                    ScaleStat(mob, Stat.HP, 3.5);
                    ScaleStat(mob, Stat.MaxDC, mob.EliteSubType == 2 ? 1.50 : 1.30);
                    if (mob.EliteSubType == 1)
                    {
                        ScaleStat(mob, Stat.MinAC, 1.35);
                        ScaleStat(mob, Stat.MaxAC, 1.35);
                    }
                    return true;

                case MonsterRarity.Elite:
                    ScaleStat(mob, Stat.HP, 5.5);
                    ScaleStat(mob, Stat.MinDC, 1.40);
                    ScaleStat(mob, Stat.MaxDC, 1.60);
                    mob.AttackSpeed = (ushort)Math.Max(300, mob.Info.AttackSpeed - 140);
                    mob.MoveSpeed = (ushort)Math.Max(350, mob.Info.MoveSpeed - 120);
                    return true;

                case MonsterRarity.Lord:
                    ScaleStat(mob, Stat.HP, 9.0);
                    ScaleStat(mob, Stat.MinDC, 1.70);
                    ScaleStat(mob, Stat.MaxDC, 1.95);
                    mob.AttackSpeed = (ushort)Math.Max(250, mob.Info.AttackSpeed - (mob.LordEnraged ? 280 : 200));
                    mob.MoveSpeed = (ushort)Math.Max(350, mob.Info.MoveSpeed - 200);
                    mob.CoolEye = true;
                    return true;

                default:
                    return false;
            }
        }

        public static Color? GetRarityNameColour(MonsterObject mob)
        {
            if (mob == null || mob.Master != null) return null;
            return mob.Rarity switch
            {
                MonsterRarity.Uncommon => Color.LimeGreen,
                MonsterRarity.Rare => mob.EliteSubType == 0
                    ? Color.Gold
                    : (mob.EliteSubType == 1 ? Color.MediumSpringGreen : Color.DeepSkyBlue),
                MonsterRarity.Elite => Color.Gold,
                MonsterRarity.Lord => Color.Crimson,
                _ => null
            };
        }

        public static double GetExpMultiplier(MonsterObject mob)
        {
            if (mob == null) return 1.0;
            return mob.Rarity switch
            {
                MonsterRarity.Lord => 15.0,
                MonsterRarity.Elite => 9.0,
                MonsterRarity.Rare => 5.0,
                MonsterRarity.Uncommon => 2.5,
                _ => mob.EliteTier == 2 ? 15.0 : (mob.EliteTier == 1 ? 5.0 : 1.0)
            };
        }

        public static double GetGoldMultiplier(MonsterObject mob)
        {
            if (mob == null) return 1.0;
            return mob.Rarity switch
            {
                MonsterRarity.Lord => 3.5,
                MonsterRarity.Elite => 2.5,
                MonsterRarity.Rare => mob.EliteSubType == 0 ? 3.0 : 1.8,
                MonsterRarity.Uncommon => 1.4,
                _ => mob.EliteTier == 2 ? 3.0 : (mob.EliteTier == 1 ? 1.8 : 1.0)
            };
        }

        public static int GetDropBonusPercent(MonsterObject mob)
        {
            if (mob == null) return 0;
            return mob.Rarity switch
            {
                MonsterRarity.Lord => 100,
                MonsterRarity.Elite => 60,
                MonsterRarity.Rare => 40,
                MonsterRarity.Uncommon => 20,
                _ => mob.EliteTier == 2 ? 100 : (mob.EliteTier == 1 ? 40 : 0)
            };
        }

        public static int GetDropLoops(MonsterObject mob)
        {
            if (mob == null) return 1;
            return mob.Rarity switch
            {
                MonsterRarity.Lord => 4,
                MonsterRarity.Elite => 3,
                MonsterRarity.Rare => 2,
                MonsterRarity.Uncommon => 1,
                _ => mob.EliteTier == 2 ? 4 : (mob.EliteTier == 1 ? 2 : 1)
            };
        }

        // 2. 红名 [领主] 两段式 AI（50%血量召唤亲卫 + 20%残血狂暴且禁绝自然回血）
        public static void OnMonsterTick(MonsterObject mob)
        {
            if (mob == null || mob.Rarity != MonsterRarity.Lord || mob.Dead || mob.Target == null)
                return;

            PlayerObject targetPlayer = ResolvePlayer(mob.Target) ?? ResolvePlayer(mob.EXPOwner);
            string displayName = DbLanguage.Monster(mob.Name);

            // 阶段一：50% 血量召唤 2 只 [变异] 同族亲卫护驾，考验主号+Hero走位拉扯
            if (mob.HealthPercent <= 50 && !mob.LordGuardsSummoned)
            {
                mob.LordGuardsSummoned = true;
                if (mob.CurrentMap != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var guard = MonsterObject.GetMonster(mob.Info);
                        if (guard != null)
                        {
                            OnMonsterSpawn(guard, MonsterRarity.Uncommon);
                            Point spawnPt = FindNearbyWalkablePoint(mob.CurrentMap, mob.CurrentLocation, 2);
                            if (guard.Spawn(mob.CurrentMap, spawnPt))
                            {
                                guard.Target = mob.Target;
                            }
                        }
                    }
                }

                targetPlayer?.ReceiveChat($"⚠️ [领主咆哮] {displayName} 发出震天怒吼，召唤了 2 名变异亲卫前来护驾！", ChatType.System2);
            }

            // 阶段二：20% 血量困兽犹斗（攻速提升，但彻底封锁自然回血，防止单人近战打不死）
            if (mob.HealthPercent <= 20 && !mob.LordEnraged)
            {
                mob.LordEnraged = true;
                mob.AttackSpeed = (ushort)Math.Max(200, mob.AttackSpeed - 80);
                mob.RegenTime = long.MaxValue; // 彻底锁死自然回血计时器！
                targetPlayer?.ReceiveChat($"🔥 [困兽死斗] {displayName} 已陷入濒死狂暴，攻速激增，但已彻底丧失生命恢复能力！", ChatType.System2);
            }
        }

        // 3. 击杀怪物累积【地图惊扰度 (Map Heat)】并主动涌现精英/领主
        public static void OnMonsterKilled(MonsterObject mob, MapObject expOwner)
        {
            PlayerObject killer = ResolvePlayer(expOwner);
            if (killer == null || mob?.CurrentMap == null || mob.Master != null || mob.Rarity != MonsterRarity.None)
                return;

            Map map = mob.CurrentMap;
            int prevHeat = GetMapHeat(map);
            int delta = 1; // 每杀1只普通怪 +1 点惊扰度（50只引出精英，100只引出领主）
            int heat = prevHeat + delta;

            if (heat >= 100) // 满 100 点：清零并在附近刷出猩红 [领主]！
            {
                MapHeatDict[map] = 0;
                var lord = MonsterObject.GetMonster(mob.Info);
                if (lord != null)
                {
                    OnMonsterSpawn(lord, MonsterRarity.Lord);
                    Point spawnPt = FindNearbyWalkablePoint(map, mob.CurrentLocation, 3);
                    if (lord.Spawn(map, spawnPt))
                    {
                        killer.ReceiveChat($"🚨 【洞穴异动】你的持续杀戮彻底惊动了本层霸主——鲜红的 {DbLanguage.Monster(lord.Name)} 已在附近({spawnPt.X},{spawnPt.Y})现身！", ChatType.System2);
                    }
                }
            }
            else if (prevHeat < 50 && heat >= 50) // 刚突破 50 点：刷出一只 [精英]！
            {
                MapHeatDict[map] = heat;
                var rareMob = MonsterObject.GetMonster(mob.Info);
                if (rareMob != null)
                {
                    OnMonsterSpawn(rareMob, MonsterRarity.Rare);
                    Point spawnPt = FindNearbyWalkablePoint(map, mob.CurrentLocation, 2);
                    if (rareMob.Spawn(map, spawnPt))
                    {
                        killer.ReceiveChat($"⚡ 【气息躁动】当前区域惊扰度已达 {heat}/100，一只 {DbLanguage.Monster(rareMob.Name)} 被血腥味吸引而来！", ChatType.Hint);
                    }
                }
            }
            else
            {
                MapHeatDict[map] = heat;
            }
        }

        // 4. 精英副词条专属掉落（[富饶]必爆金币堆、[坚韧]必掉纯度15+黑铁矿）
        public static void ApplySubTypeBonusDrops(MonsterObject mob, Action<uint> dropGold, Func<UserItem, bool> dropItem)
        {
            if (mob == null || mob.CurrentMap == null) return;

            if (mob.Rarity == MonsterRarity.Rare && mob.EliteSubType == 0)
            {
                // [精英・富饶] 额外必爆 2 堆丰厚金币
                uint richGold = (uint)(Math.Max(1, (int)mob.Level) * 90 * Settings.GoldRate);
                for (int i = 0; i < 2; i++)
                    dropGold(richGold);
            }
            else if (mob.Rarity == MonsterRarity.Rare && mob.EliteSubType == 1)
            {
                // [精英・坚韧] 必掉 1~2 块纯度 15~22 的黑铁矿石
                ItemInfo oreInfo = Envir.Main.GetItemInfo(Settings.RefineOreName) ?? Envir.Main.GetItemInfo("BlackIronOre");
                if (oreInfo != null)
                {
                    int oreCount = Envir.Main.Random.Next(1, 3);
                    for (int i = 0; i < oreCount; i++)
                    {
                        UserItem ore = Envir.Main.CreateDropItem(oreInfo);
                        if (ore != null)
                        {
                            ushort purity = (ushort)(Envir.Main.Random.Next(15, 23) * 1000);
                            ore.CurrentDura = purity;
                            ore.MaxDura = purity;
                            dropItem(ore);
                        }
                    }
                }
            }
        }

        internal static PlayerObject ResolvePlayer(MapObject obj)
        {
            return obj switch
            {
                PlayerObject p => p,
                HeroObject h => h.Owner,
                MonsterObject m when m.Master is PlayerObject mp => mp,
                MonsterObject m when m.Master is HeroObject mh => mh.Owner,
                _ => null
            };
        }

        internal static Point FindNearbyWalkablePoint(Map map, Point origin, int radius)
        {
            if (map == null) return origin;
            for (int r = 1; r <= radius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        Point pt = new(origin.X + dx, origin.Y + dy);
                        if (map.ValidPoint(pt))
                            return pt;
                    }
                }
            }
            return origin;
        }

        public static int GetDropRateBonusPercent(MonsterObject mob) => GetDropBonusPercent(mob);

        private static void ScaleStat(MonsterObject mob, Stat stat, double multiplier)
        {
            int val = mob.Stats[stat];
            if (val == 0) return;
            int scaled = (int)Math.Round(val * multiplier);
            mob.Stats[stat] = stat == Stat.HP ? Math.Max(1, scaled) : scaled;
        }
    }
}
