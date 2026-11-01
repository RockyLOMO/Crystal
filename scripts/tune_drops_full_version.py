#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
from typing import Dict, List, Tuple

MANIFEST_FILENAME = ".tuned-manifest.json"

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
    "Wooma", "WoomaHorn", "RubyRing", "PlatinumRing", "DragonRing", "RedEyeRing", "ExpelRing", "CoralRing", "GaleRing",
    "Zuma", "JudgementMace", "BoneWand", "WarMageStaff", "DragonlessSword", "DragonSword", "SoulSpringWand", "BladesOfDarkness", "FiendBow",
    "WarSpiritBlade", "MagicScythe", "StoneBambooFan", "FreezingBlades", "LitheBow",
    "GreenNecklace", "GreenBead", "EvilBell", "DemonicBells", "SoulNecklace", "KnightBracelet", "DragonBracelet", "SoulBracelet", "SoulSpringBracelet",
    "ForceRing", "PowerRing", "PurpleRing", "VioletRing", "TitanRing",
    "裁决", "骨玉", "龙纹", "无极棍", "力量戒指", "紫碧螺", "泰坦戒指", "绿色项链", "恶魔铃铛", "灵魂项链", "骑士手镯", "龙之手镯", "心灵手镯"
]

# 4. 赤月、魔龙、狐月、雪域高阶神装 (上限锁在 1/120 ~ 1/180)
HIGH_TIER_EQUIP = [
    "Saint", "Fashin", "Tianzun", "Heaven", "Mir", "RedMoon", "RedMoonSword", "FanOfCrane",
    "ThunderArmour", "ThunderNecklace", "ThunderBracelet", "ThunderRing", "ThunderBelt", "ThunderBoots",
    "Flame", "Radiant", "DragonSlayer", "DragonStaff", "SoulSabre", "RoyalBlades", "MaleficBow",
    "SwordOfWarGod", "BladeOfSorcery", "HeavenSword", "BladesOfMagi", "BowOfForce",
    "HeavenArmour", "DragonBoots", "RedScaleBoots", "GoldBelt", "AdamantineBelt",
    "Fox", "Snow", "Ice", "Tarragon", "BlackDragon", "IceDragon",
    "圣战", "法神", "天尊", "雷霆", "烈焰", "光芒", "战神", "圣魔", "真魂", "屠龙", "嗜魂", "霸者", "怒斩", "龙牙", "逍遥扇", "开天", "镇天", "玄天"
]

# 5. 金币、药水、肉类/采集物保持原版爆率，避免普通怪满地药水肉块撑爆背包
CONSUMABLE_OR_GOLD_KEYWORDS = [
    "Drug", "Potion", "Meat", "Venison", "Chicken", "Beef", "Pork",
    "Cherry", "Chestnut", "Timber", "Cannibal", "SpiderTeeth", "SpiderWeb",
    "DeerHorn", "DeerBlood", "Torch", "Amulet", "Bundle", "Scroll", "Teleport", "Escape",
    "GoldBar", "GoldChest"
]


def calc_new_denom(denom: int, item_name: str) -> int:
    if denom <= 3:
        return denom

    tokens = item_name.split()
    base_token = tokens[0] if tokens else item_name
    # 任务物品 (带 Q 标记) 或金币堆保持原版掉率
    if "Q" in tokens[1:] or base_token.lower() == "gold":
        return denom

    lower_name = item_name.lower()

    # 1. 技能书：平滑压缩，上限 1/35，下限不低于 1/25（避免低级技能书变成 1/3 满地爆）
    if any(k.lower() in lower_name for k in SKILL_BOOK_KEYWORDS):
        if denom <= 25:
            return denom
        return max(25, min(int(denom * 0.12), 35))

    # 2. 沃玛/祖玛级装备（主号+英雄双倍消耗）：上限 1/60，下限 1/25
    if any(k.lower() in lower_name for k in MID_TIER_EQUIP):
        if denom <= 25:
            return denom
        return max(25, min(int(denom * 0.15), 60))

    # 3. 赤月/魔龙/雪域高阶装备：上限 1/150，下限 1/45
    if any(k.lower() in lower_name for k in HIGH_TIER_EQUIP):
        if denom <= 45:
            return denom
        return max(45, min(int(denom * 0.12), 150))

    # 4. 养成材料（祝福油、觉醒石、宝石）：大幅提产，上限 1/25，下限 1/12
    if any(k.lower() in lower_name for k in MATERIAL_KEYWORDS):
        if denom <= 12:
            return denom
        return max(12, min(int(denom * 0.15), 25))

    # 5. 金币、药水、肉类及基础补给品：保持原版掉率，不额外放大
    if any(k.lower() in lower_name for k in CONSUMABLE_OR_GOLD_KEYWORDS):
        return denom

    # 6. 通用爆率阶梯：
    # - 低价值普通白装 (原分母 <= 400，如木剑、布衣、轻盔、青铜头、铁手镯、牛角戒指等)：
    #   稀释分母 (×4，保底 1/180，封顶 1/800)，解决出门刷怪几分钟包袱塞满超重跑不动的问题
    # - 中高稀有度未命中词条的特殊掉落 (> 400)：平滑压缩，杜绝万分之一死胡同
    if denom <= 400:
        return min(max(denom * 4, 180), 800)
    elif denom <= 1000:
        return max(80, int(denom * 0.25))
    elif denom <= 5000:
        return min(int(denom * 0.08), 160)
    else:
        return min(int(denom * 0.03), 260)


DROP_LINE_PATTERN = re.compile(r"^(\s*)1/(\d+)(\s+)(.+?)(\r?\n?)$")


def transform_drop_content(raw_bytes: bytes) -> Tuple[bytes, int, int]:
    text = raw_bytes.decode("utf-8", errors="surrogateescape")
    lines = text.splitlines(keepends=True)
    new_lines: List[str] = []
    total_entries = 0
    changed_entries = 0

    for line in lines:
        stripped = line.strip()
        if not stripped or stripped.startswith(";") or stripped.startswith("#"):
            new_lines.append(line)
            continue

        match = DROP_LINE_PATTERN.match(line)
        if match:
            leading_ws, denom_str, sep_ws, item_rest, line_ending = match.groups()
            old_denom = int(denom_str)
            total_entries += 1
            new_denom = calc_new_denom(old_denom, item_rest.strip())
            if new_denom != old_denom:
                changed_entries += 1
            ending = line_ending if line_ending else "\n"
            new_lines.append(f"{leading_ws}1/{new_denom}{sep_ws}{item_rest}{ending}")
        else:
            new_lines.append(line)

    out_bytes = "".join(new_lines).encode("utf-8", errors="surrogateescape")
    return out_bytes, total_entries, changed_entries


def run_tuning(source_dir: str, output_dir: str, apply_changes: bool = False) -> Dict[str, object]:
    src_real = os.path.realpath(source_dir)
    out_real = os.path.realpath(output_dir)

    if not os.path.isdir(src_real):
        raise ValueError(f"原始掉落基线目录不存在: {source_dir}")

    if src_real.lower() == out_real.lower():
        raise ValueError("输入基线目录 (--source) 与输出目录 (--output) 不能相同，禁止原地覆盖！")

    if os.path.exists(os.path.join(src_real, MANIFEST_FILENAME)):
        raise ValueError(f"输入目录包含 {MANIFEST_FILENAME}，说明它是已调优输出目录，禁止作为原始基线重复输入！")

    txt_files: List[str] = []
    for root, _, files in os.walk(src_real):
        for file in sorted(files):
            if file.lower().endswith(".txt"):
                rel = os.path.relpath(os.path.join(root, file), src_real)
                txt_files.append(rel)
    txt_files.sort()

    if not txt_files:
        raise ValueError(f"输入基线目录中未找到任何 .txt 掉落文件: {source_dir}")

    src_hasher = hashlib.sha256()
    out_hasher = hashlib.sha256()

    total_files = len(txt_files)
    modified_files = 0
    total_entries = 0
    modified_entries = 0
    staged_outputs: List[Tuple[str, bytes]] = []

    for rel in txt_files:
        src_path = os.path.join(src_real, rel)
        with open(src_path, "rb") as f:
            raw_bytes = f.read()

        norm_rel = rel.replace("\\", "/")
        src_hasher.update(norm_rel.encode("utf-8") + b"\0" + raw_bytes + b"\0")

        out_bytes, entries, changed = transform_drop_content(raw_bytes)
        total_entries += entries
        modified_entries += changed
        if out_bytes != raw_bytes:
            modified_files += 1

        out_hasher.update(norm_rel.encode("utf-8") + b"\0" + out_bytes + b"\0")
        staged_outputs.append((rel, out_bytes))

    source_sha256 = src_hasher.hexdigest()
    output_sha256 = out_hasher.hexdigest()

    if apply_changes:
        for rel, out_bytes in staged_outputs:
            dest_path = os.path.join(out_real, rel)
            dest_parent = os.path.dirname(dest_path)
            os.makedirs(dest_parent, exist_ok=True)
            fd, tmp_path = tempfile.mkstemp(prefix=".drop_tmp_", dir=dest_parent)
            try:
                with os.fdopen(fd, "wb") as tf:
                    tf.write(out_bytes)
                os.replace(tmp_path, dest_path)
            except Exception:
                if os.path.exists(tmp_path):
                    os.unlink(tmp_path)
                raise

        manifest = {
            "source_sha256": source_sha256,
            "output_sha256": output_sha256,
            "total_files": total_files,
            "modified_files": modified_files,
            "total_entries": total_entries,
            "modified_entries": modified_entries,
        }
        manifest_path = os.path.join(out_real, MANIFEST_FILENAME)
        with open(manifest_path, "w", encoding="utf-8") as mf:
            json.dump(manifest, mf, indent=2, ensure_ascii=False)

    return {
        "mode": "apply" if apply_changes else "dry-run",
        "source_dir": src_real,
        "output_dir": out_real,
        "source_sha256": source_sha256,
        "output_sha256": output_sha256,
        "total_files": total_files,
        "modified_files": modified_files,
        "total_entries": total_entries,
        "modified_entries": modified_entries,
    }


def main(argv: List[str] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Crystal 单人+英雄经济平衡爆率生成工具（只读基线 -> 目标目录，幂等可重复执行）"
    )
    parser.add_argument("--source", "-s", required=True, help="原始只读 Envir/Drops 基线目录路径")
    parser.add_argument("--output", "-o", required=True, help="生成后的 Envir/Drops 输出目录路径")
    parser.add_argument("--apply", action="store_true", help="实际写入输出目录（默认仅 dry-run 预览）")
    args = parser.parse_args(argv)

    try:
        summary = run_tuning(args.source, args.output, apply_changes=args.apply)
    except Exception as exc:
        print(f"[错误] {exc}", file=sys.stderr)
        return 1

    print("=" * 64)
    print(f"模式: {summary['mode'].upper()}")
    print(f"原始基线目录: {summary['source_dir']}")
    print(f"目标输出目录: {summary['output_dir']}")
    print(f"扫描掉落文件总数: {summary['total_files']} (变更文件数: {summary['modified_files']})")
    print(f"扫描掉落条目总数: {summary['total_entries']} (变更条目数: {summary['modified_entries']})")
    print(f"原始基线 SHA-256: {summary['source_sha256']}")
    print(f"生成结果 SHA-256: {summary['output_sha256']}")
    if not args.apply:
        print("提示: 当前为预览模式 (dry-run)，未写入文件；如需写入请追加 --apply 参数。")
    print("=" * 64)
    return 0


if __name__ == "__main__":
    sys.exit(main())
