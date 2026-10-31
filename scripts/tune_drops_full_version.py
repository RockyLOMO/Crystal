#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import re
import sys

DROPS_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join(".", "Envir", "Drops")

# 1. 全职业技能书与英雄合击书 (大幅提高爆率，最高不超过 1/35)
SKILL_BOOK_KEYWORDS = [
    "Book", "HalfMoon", "CrossHalfMoon", "FlamingSword", "TwinDrakeBlade", "LionRoar", "BladeAvalanche", "SlashingBurst", "ProtectionField", "Rage",
    "IceStorm", "MagicShield", "FlameDisruptor", "MeteorStrike", "Blizzard", "FlameField", "Vampirism", "Mirroring",
    "SummonShinsu", "SummonHolyDeva", "SoulShield", "PoisonCloud", "Reincarnation", "UltimateEnhancer", "EnergyRepulsor", "Purification", "Curse", "EnergyShield", "Hallucination",
    "DoubleSlash", "Haste", "LightBody", "HeavenlySword", "FireBurst", "DarkBody", "FlashDash", "PoisonSword", "MoonLight", "MPEater", "SwiftFeet",
    "TrapHexagon", "ExplosiveTrap", "VampireShot", "OneWithNature",
    "半月", "烈火", "逐日", "开天", "冰咆哮", "流星火雨", "魔法盾", "召唤神兽", "召唤月灵", "噬血术", "护体神盾"
]

# 2. 全版本养成材料：觉醒灵魂、祝福油、精炼矿石、宝石、宝珠
MATERIAL_KEYWORDS = [
    "BenedictionOil", "BlessingOil", "AwakeningSoul", "Jade", "Gem", "Orb", "Crystal", "Stone", "Relic", "Heart", "Piece",
    "BraveryOrb", "MagicOrb", "SoulOrb", "ZumaRelic", "ZumaHeart",
    "祝福油", "觉醒灵魂", "宝石", "神石", "灵珠", "矿", "精魄", "遗物"
]

# 3. 沃玛与祖玛过渡装 (单人主号+英雄双份需求，上限锁在 1/60)
MID_TIER_EQUIP = [
    "Wooma", "Horn", "RubyRing", "PlatinumRing", "DragonRing", "RedEyeRing", "ExpelRing", "CoralRing", "GaleRing",
    "Zuma", "JudgementMace", "BoneWand", "WarMageStaff", "DragonlessSword", "DragonSword", "SoulSpringWand", "BladesOfDarkness", "FiendBow",
    "WarSpiritBlade", "MagicScythe", "StoneBambooFan", "FreezingBlades", "LitheBow",
    "GreenNecklace", "GreenBead", "EvilBell", "DemonicBells", "SoulNecklace", "KnightBracelet", "DragonBracelet", "SoulBracelet", "SoulSpringBracelet",
    "ForceRing", "PowerRing", "PurpleRing", "VioletRing", "TitanRing",
    "裁决", "骨玉", "龙纹", "无极棍", "力量戒指", "紫碧螺", "泰坦戒指", "绿色项链", "恶魔铃铛", "灵魂项链", "骑士手镯", "龙之手镯", "心灵手镯"
]

# 4. 赤月、魔龙、狐月、雪域高阶神装 (上限锁在 1/120 ~ 1/180)
HIGH_TIER_EQUIP = [
    "Saint", "Fashin", "Tianzun", "Heaven", "Mir", "RedMoon", "RedMoonSword", "FanOfCrane",
    "Thunder", "Flame", "Radiant", "DragonSlayer", "DragonStaff", "SoulSabre", "RoyalBlades", "MaleficBow",
    "SwordOfWarGod", "BladeOfSorcery", "HeavenSword", "BladesOfMagi", "BowOfForce",
    "HeavenArmour", "DragonBoots", "RedScaleBoots", "GoldBelt", "AdamantineBelt",
    "Fox", "Snow", "Ice", "Tarragon", "BlackDragon", "IceDragon",
    "圣战", "法神", "天尊", "雷霆", "烈焰", "光芒", "战神", "圣魔", "真魂", "屠龙", "嗜魂", "霸者", "怒斩", "龙牙", "逍遥扇", "开天", "镇天", "玄天"
]


def calc_new_denom(denom: int, item_name: str) -> int:
    if denom <= 3:
        return denom

    # 1. 技能书：平滑压缩，保底 1/35
    if any(k.lower() in item_name.lower() for k in SKILL_BOOK_KEYWORDS):
        return max(3, min(int(denom * 0.12), 35))

    # 2. 沃玛/祖玛级装备（主号+英雄双倍消耗）：保底 1/60
    if any(k.lower() in item_name.lower() for k in MID_TIER_EQUIP):
        return max(5, min(int(denom * 0.15), 60))

    # 3. 赤月/魔龙/雪域高阶装备：保底 1/150
    if any(k.lower() in item_name.lower() for k in HIGH_TIER_EQUIP):
        return max(15, min(int(denom * 0.12), 150))

    # 4. 养成材料（祝福油、觉醒石、宝石）：大幅提产，保底 1/25
    if any(k.lower() in item_name.lower() for k in MATERIAL_KEYWORDS):
        return max(2, min(int(denom * 0.15), 25))

    # 5. 通用爆率阶梯压缩（杜绝万分之一死胡同）
    if denom <= 30:
        return max(2, int(denom * 0.5))
    elif denom <= 200:
        return max(8, int(denom * 0.25))
    elif denom <= 1000:
        return max(25, int(denom * 0.15))
    elif denom <= 5000:
        return min(int(denom * 0.08), 160)
    else:
        # 哪怕原版是 1/50000 的特戒或终极装，单人服最高封顶 1/260
        return min(int(denom * 0.03), 260)


def process_drop_file(filepath: str) -> bool:
    try:
        with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
    except Exception as e:
        print(f"读取失败 {filepath}: {e}")
        return False

    new_lines = []
    modified = False
    pattern = re.compile(r"^1/(\d+)\s+(.+)$")

    for line in lines:
        stripped = line.strip()
        if not stripped or stripped.startswith(";"):
            new_lines.append(line)
            continue

        match = pattern.match(stripped)
        if match:
            old_denom = int(match.group(1))
            item_rest = match.group(2)
            new_denom = calc_new_denom(old_denom, item_rest)
            if new_denom != old_denom:
                modified = True
            new_lines.append(f"1/{new_denom} {item_rest}\n")
        else:
            new_lines.append(line)

    if modified:
        with open(filepath, "w", encoding="utf-8") as f:
            f.writelines(new_lines)
    return modified


def main():
    if not os.path.exists(DROPS_DIR):
        print(f"[错误] 找不到目录: {DROPS_DIR}，请将脚本放在 Server 根目录下运行或传入 Envir/Drops 路径！")
        return

    total_files = 0
    modified_files = 0
    for root, _, files in os.walk(DROPS_DIR):
        for file in files:
            if file.lower().endswith(".txt"):
                total_files += 1
                if process_drop_file(os.path.join(root, file)):
                    modified_files += 1

    print("=" * 60)
    print("Crystal 全版本单人+英雄慢节奏爆率重塑完成！")
    print(f"扫描怪物爆率文件总数: {total_files}")
    print(f"已优化压缩爆率文件数: {modified_files}")
    print("=" * 60)


if __name__ == "__main__":
    main()
