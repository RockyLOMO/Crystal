# Crystal (CrystalM2) 全版本全特性开启・单人+原生Hero 慢节奏肉鸽化服务端方案

> **版本形态**：Euro Mir 全盛完全体（不锁 1.76）。全面开放 **战、法、道、刺客（Assassin）、弓箭手（Archer）五职业**、原生 **Hero（英雄副将）**、**坐骑系统（Mounts）**、**装备觉醒与宝石镶嵌（Awakening & Sockets）**、**38~60级高阶技能** 以及 **赤月之后的冰宫、火龙、狐月山、真天宫等全套后期终局地图**。  
> **物理法则**：严格开启全图物理碰撞（`SafeZoneWalkThrough=False`），还原最硬核的网格卡位与身位阻挡。  
> **寿命预期**：300 ~ 500+ 小时（约 4 ~ 6 个月）。把 1~35 级做成爽快过渡，35~45 级做成经典 1.76 沉淀，45~65 级做成暗黑破坏神式的终局（Endgame）深渊开荒。

---

## 一、 全局基础配置 (`Setup.ini`) —— 全特性解锁

在 `Server` 根目录下修改 `Setup.ini`。开启所有官方高版本模块，同时维持紧缩型经济（因为后期坐骑饲料、装备觉醒、宝石合成全部是吞金大户）：

```ini
[Rates]
; 基础经验 2 倍（配合 C# 六段式全版本经验曲线，兼顾前期出山与 50级+ 终局深度）
ExperienceRate=2

; 全局掉落倍率 1 倍（保持原版药水与材料产出节奏，让觉醒石、祝福油、镶嵌宝石保持稀缺）
DropRate=1

; 金币倍率 3 倍（全版本相比 1.76 多了坐骑养护、装备打孔镶嵌、高级装备觉醒，开销极大，3倍金币刚好收支平衡）
GoldRate=3

; 技能熟练度 6 倍（全版本每个职业多出四五个 38~55 级高阶技能，主号+Hero 双份技能树需适度减负）
SkillRate=6

[Game]
; 1. 开启原生 Hero（英雄副将）系统（主号+Hero 可从五职业中任意排列组合出 25 种流派！）
AllowHeroes=True
HeroLevelRequirement=22

; 2. 严格开启物理碰撞（不穿人、不穿怪）
SafeZoneWalkThrough=False

; 3. 死亡保护：关闭穿戴装备与坐骑掉落，仅掉落背包杂物（保护后期高觉醒/满插槽孤品）
DropEquippedItems=False

; 4. 确保全版本扩展模块处于激活状态（默认开启，检查确认）
AllowAssassin=True
AllowArcher=True
AllowMounts=True
AllowAwakening=True
AllowFishing=True
```

### 💡 全版本“五职业 主号 + Hero”特色组合推荐
不再局限于传统的“战法道”，全版本下你可以尝试极具新鲜感的双人搭配：
* **法/道（主） + 刺客（Hero）**：刺客拥有极高的攻速、双持暴击与潜行切入能力（自带鬼灵步、炎龙波、火镰狂舞）。你主号在远处拉怪上毒/铺火，刺客 Hero 开启自动技能像绞肉机一样瞬秒精英怪。
* **战士/刺客（主） + 弓箭手（Hero）**：弓箭手是纯粹的远程物理炮台与陷阱大师，能在你近战抗怪时在脚下铺满爆炸陷阱并提供恐怖的远程蓄力火力支援。

---

## 二、 核心源码改造 1：全版本“六段式”动态经验曲线 (1 ~ 65+ 级)

**痛点**：1.76 的经验曲线到 45 级就断崖了，但 Crystal 全版本在 40 级之后还有 **寒冰掌、灭天火、分身术、无极真气、气功波、狮子吼、开天斩、噬血术** 等大量神技以及高级地图。如果 40 级后直接卡死在 0.5 倍经验，你永远体验不到后半程的精彩内容。  
**改造目标**：将等级上限的体感天花板从 45 级平滑延展至 **60~65 级**。

* **修改文件**：`Server/MirObjects/PlayerObject.cs`
* **定位方法**：`public virtual void GainExp(uint amount)`

```csharp
public virtual void GainExp(uint amount)
{
    if (amount == 0 || Dead) return;

    float expMultiplier = Settings.ExperienceRate; // 基础为 2.0f

    // ===== 全版本 (1~65级) 六段式长线经验曲线 =====
    if (Level < 22)
        expMultiplier *= 2.5f;      // [第1段] 1~21级 (实际 5.0x)：比奇新手期，1.5小时快速抵达22级领取 Hero
    else if (Level < 35)
        expMultiplier *= 1.5f;      // [第2段] 22~34级 (实际 3.0x)：沃玛/蜈蚣洞成型期，顺滑拿到35级三职业标志技能
    else if (Level < 42)
        expMultiplier *= 1.0f;      // [第3段] 35~41级 (实际 2.0x)：经典 1.76 黄金期（猪洞/祖玛/赤月），稳步解锁 38~40 级新技能
    else if (Level < 50)
        expMultiplier *= 0.6f;      // [第4段] 42~49级 (实际 1.2x)：步入神舰/牛魔/狐月山/真天宫，冲击47级开天斩/噬血术/流星火雨
    else if (Level < 58)
        expMultiplier *= 0.35f;     // [第5段] 50~57级 (实际 0.7x)：冰宫/火龙洞深渊攻坚期，全套装备觉醒与高级宝石成型
    else
        expMultiplier *= 0.15f;     // [第6段] 58级以上 (实际 0.3x)：终局养老天花板

    // ===== 主号与 Hero 协同分流 (总经验 120%：主号 75% + 英雄 45%) =====
    if (Hero != null && !Hero.Dead && Hero.CurrentMap == CurrentMap)
    {
        uint masterExp = (uint)Math.Max(1, amount * expMultiplier * 0.75f);
        uint heroExp   = (uint)Math.Max(1, amount * expMultiplier * 0.45f);
        
        base_GainExp(masterExp);
        Hero.GainExp(heroExp);
    }
    else
    {
        uint soloExp = (uint)Math.Max(1, amount * expMultiplier);
        base_GainExp(soloExp);
    }
}
```

---

## 三、 核心源码改造 2：全版本变异精英怪 + 觉醒/宝石材料专属掉落

**设计逻辑**：全版本 Crystal 最迷人的养成线是 **“装备觉醒（Awakening）”** 和 **“插槽宝石（Gems/Orbs）”**。我们将变异精英怪与这套原生高版本系统深度绑定——击杀蓝名**`[精英]`**和金名**`[领主]`**不仅连爆装备，还会额外产出觉醒石与高阶属性宝珠！

* **修改文件**：`Server/MirObjects/MonsterObject.cs`

### 3.1 怪物刷新变异判定（适配后期高血量地图）
全版本后期地图（如冰宫、火龙神殿）的小怪基础血量可能达到 3000~6000，因此我们将不参与变异的 Boss 血量阈值上调至 `15000`（仅排除真正的教主级 Boss）：

```csharp
public byte EliteTier = 0; // 0:普通怪, 1:稀有精英(蓝), 2:远古领主(金)

public void RollEliteMutation()
{
    // 排除原生 Boss、城门守卫、玩家宠物/Hero，以及血量 >= 15000 的终极魔神
    if (Info.AI == 6 || Info.Boss || Master != null || MaxHP >= 15000) return;

    int roll = Envir.Random.Next(1000);
    if (roll < 4) // 0.4% 概率：【远古领主 (金名)】
    {
        EliteTier = 2;
        Name = $"[领主]{Info.GameName}";
        NameColour = Color.Gold;
        MaxHP = (uint)(Info.HP * 5.5);      // 5.5倍血量，配合坐骑冲锋或主英双控击杀
        MinDC = (ushort)(Info.MinDC * 1.5);
        MaxDC = (ushort)(Info.MaxDC * 1.7);
        ActionTime = Math.Max(300, Info.ActionTime - 150);
    }
    else if (roll < 28) // 2.4% 概率：【稀有精英 (蓝名)】
    {
        EliteTier = 1;
        Name = $"[精英]{Info.GameName}";
        NameColour = Color.DeepSkyBlue;
        MaxHP = (uint)(Info.HP * 2.5);
        MaxDC = (ushort)(Info.MaxDC * 1.25);
    }
    HP = MaxHP;
}
```

### 3.2 连爆奖池 + 全版本高阶词条（吸血/暴击/插槽）触发
在 `MonsterObject.cs` 的 **`Drop()`** 方法中：

```csharp
protected virtual void Drop()
{
    // 普通怪 1 次，蓝名精英连爆 2 次，金名领主连爆 6 次！
    int dropLoops = EliteTier == 2 ? 6 : (EliteTier == 1 ? 2 : 1);

    for (int loop = 0; loop < dropLoops; loop++)
    {
        // ... 原有 foreach (DropInfo drop in Info.Drops) 逻辑 ...
        if (item != null)
        {
            if (EliteTier == 2)
                item.RollFullVersionRareStats(forceRare: true, minAdd: 2, maxAdd: 6); // 领主必出高极品+高概率带孔！
            else if (EliteTier == 1)
                item.RollFullVersionRareStats(forceRare: true, minAdd: 1, maxAdd: 3);
            else
                item.RollFullVersionRareStats(forceRare: false, minAdd: 1, maxAdd: 4);
        }
    }
}
```

---

## 四、 核心源码改造 3：全版本专属词条孵化器（含暴击、吸血、神圣、攻速）

**设计逻辑**：在 1.76 中只有“攻魔道防魔御”五种基础属性。但在 Crystal 全版本中，`UserItem` 原生支持 **暴击率（CriticalRate）、暴击伤害（CriticalDamage）、吸血（HpDrainRate）、神圣攻击（Holy）、毒物躲避、负重、体力/魔力上限百分比** 等极其丰富的暗黑式词条！我们在极品鉴定中将这些原生字段全部激活：

* **修改文件**：`Shared/UserItem.cs`

```csharp
public void RollFullVersionRareStats(bool forceRare = false, int minAdd = 1, int maxAdd = 5)
{
    // 涵盖全版本所有可穿戴部位（含腰带、靴子、坐骑装备、护身符等）
    if (Info.Type != ItemType.Weapon && Info.Type != ItemType.Armour &&
        Info.Type != ItemType.Helmet && Info.Type != ItemType.Necklace &&
        Info.Type != ItemType.Bracelet && Info.Type != ItemType.Ring &&
        Info.Type != ItemType.Belt && Info.Type != ItemType.Boots &&
        Info.Type != ItemType.Torch && Info.Type != ItemType.Amulet)
        return;

    // 普通怪 10% 概率出极品，精英与领主 100% 必出
    if (!forceRare && RandomUtils.Next(100) >= 10) return;

    // 决定附加词条数量：75%单词条，20%双词条，5%三词条神装
    int rCount = RandomUtils.Next(100);
    int statCount = rCount < 75 ? 1 : (rCount < 95 ? 2 : 3);

    for (int i = 0; i < statCount; i++)
    {
        // 加权点数：+1(55%), +2(25%), +3(12%), +4(5%), +5(2%), +6(1%)
        int roll = RandomUtils.Next(100);
        byte val = (byte)(roll < 55 ? 1 : roll < 80 ? 2 : roll < 92 ? 3 : roll < 97 ? 4 : roll < 99 ? 5 : 6);
        val = (byte)Math.Clamp((int)val, minAdd, maxAdd);

        switch (RandomUtils.Next(6))
        {
            case 0: MaxDC += val; break;  // 物理攻击 (战/刺/弓通用，Crystal中刺客弓手根据DC或MC加成)
            case 1: MaxMC += val; break;  // 魔法攻击
            case 2: MaxSC += val; break;  // 道术攻击
            case 3: MaxAC += val; break;  // 物理防御
            case 4: MaxMAC += val; break; // 魔法防御
            case 5:                       // 生命/法力上限直接加成
                HP += (ushort)(val * 15);
                MP += (ushort)(val * 15);
                break;
        }
    }

    // ===== 全版本专属：3% 概率激活【高阶暗金特效词条】 =====
    if (RandomUtils.Next(100) < 3 || (forceRare && minAdd >= 2 && RandomUtils.Next(100) < 20))
    {
        switch (RandomUtils.Next(5))
        {
            case 0: CriticalRate += (byte)RandomUtils.Next(1, 3); break;   // 暴击率 +1% ~ 2% (刺客/弓手/战士神词条)
            case 1: CriticalDamage += (byte)RandomUtils.Next(2, 6); break; // 暴击伤害 +2% ~ 5%
            case 2: HpDrainRate += (byte)RandomUtils.Next(1, 4); break;    // 物理吸血 +1% ~ 3% (近战续航神器)
            case 3: AttackSpeed += 1; break;                               // 攻击速度 +1
            case 4: Luck += 1; break;                                      // 幸运 +1
        }
    }
}
```

---

## 五、 核心源码改造 4：全版本召唤物阶梯进化 (1 ~ 60 级跨度)

在全版本中，道士除《召唤骷髅》和《召唤神兽》外，后期还能学习 **《召唤月灵 / 圣兽 (SummonHolyDeva)》**。我们将阶梯成长一直延伸到 55 级：

* **修改文件**：`Server/MirObjects/PlayerObject.cs`

```csharp
// 在召唤神兽 / 圣兽逻辑处修改上限：
int maxDevaCount = 1;
byte startPetLevel = 0;

if (Level >= 42) { maxDevaCount = 2; startPetLevel = 3; } // 42级：双神兽（3级）
if (Level >= 48) { maxDevaCount = 2; startPetLevel = 7; } // 48级：双满级神兽（7级）
if (Level >= 54) { maxDevaCount = 3; startPetLevel = 7; } // 54级（终局冰宫/火龙地图）：三只7级神兽护体！

// 法师诱惑之光：保持 5 只，50级后解锁抓捕更高级别精英怪，叛变时间固定为 240 分钟（4小时）
target.TameTime = Envir.Time + (Settings.Minute * 240);
```

---

## 六、 全版本经济回收与终局 NPC 脚本 (`Envir/NPCs/`)

因为开放了全版本地图与五职业，装备池扩大了数倍。铁匠铺的重铸系统按阶段覆盖 **沃玛 $\rightarrow$ 祖玛 $\rightarrow$ 赤月 $\rightarrow$ 魔龙/火龙级**。

### 6.1 【盟重神匠】全阶装备重铸 & 觉醒材料兑换
```text
[@Main]
无论是战法道还是刺客、弓箭手的装备，只要你有3件同名装备，我都能为你重铸出逆天词条（含暴击、吸血、攻速）！\
<【沃玛级三合一重铸】消耗3件 + 10万金币/@ReforgeTier1> \
<【祖玛级三合一重铸】消耗3件 + 40万金币/@ReforgeTier2> \
<【赤月/雷霆级重铸】消耗3件 + 120万金币/@ReforgeTier3> \
<【多余高级技能书回收】兑换装备觉醒石 (Awakening Stone)/@ExchangeAwaken> \
```

### 6.2 【险恶传送官】分阶段买路钱（适配全版本后期地图）
```text
[@Main]
全大陆的险恶禁地均已开启！每次传送仅送达中转层，底层魔窟需你与英雄徒步杀入：\
<【初级历练 - 3万金币】石墓五层 / 蜈蚣死亡棺材/@Tier1Map> \
<【中级攻坚 - 8万金币】祖玛五层 / 封魔疾风殿 / 牛魔五层/@Tier2Map> \
<【高阶禁地 - 25万金币】赤月抉择之地 / 神舰三层/@Tier3Map> \
<【终局深渊 - 60万金币】狐月秘境 / 冰宫走廊 / 火龙洞窟入口/@Tier4Map> \
```

---

## 七、 全版本 `Envir/Drops` 爆率调优脚本（兼顾五职业 + 觉醒宝石）

在全版本 `Envir/Drops/` 中，除了常规装备，还有大量的 **制作材料（Crafting Materials）、觉醒石（Awakening）、属性宝珠（Orbs）、坐骑装备以及刺客/弓箭手专属武器**。

将以下脚本保存为 `tune_drops_full_version.py` 在服务端根目录运行：

```python
import os
import re

DROPS_DIR = "./Envir/Drops"

def adjust_denominator(match):
    denom = int(match.group(1))
    item_name = match.group(2).strip()
    
    # 1. 药水、金币、基础弓箭/毒药补给保持原样
    if denom <= 25:
        return f"1/{denom} {item_name}"
    
    # 2. 觉醒石(Awakening)、镶嵌宝珠(Orb/Gem)、祝福油：适度提高掉率，支撑全版本养成乐趣
    if any(k in item_name.lower() for k in ["awaken", "orb", "gem", "blessing", "mount"]):
        new_denom = max(15, int(denom / 4))
        return f"1/{new_denom} {item_name}"

    # 3. 沃玛级、35~38级五职业技能书 (原版 1/100 ~ 1/350) -> 调整为 1/30 ~ 1/90
    # 原因：五职业技能书池子变深了，稍微调高以确保主号+Hero能顺利学全技能
    if denom <= 350:
        new_denom = max(30, int(denom / 3.5))
        return f"1/{new_denom} {item_name}"
    
    # 4. 祖玛级、赤月级、40~47级高阶技能书 (原版 1/400 ~ 1/2500) -> 调整为 1/120 ~ 1/500
    if denom <= 2500:
        new_denom = max(120, int(denom / 4.5))
        return f"1/{new_denom} {item_name}"
    
    # 5. 全版本终极神兵（屠龙/开天/玄天/镇天/顶级刺客双刃/神弓）与特戒 -> 保底 1/600
    # 配合金名领主 6 连爆，实际在后期大Boss或金名怪身上约有 1/100 机会圆梦
    new_denom = max(600, int(denom / 5))
    return f"1/{new_denom} {item_name}"

def process_all_drops():
    pattern = re.compile(r"^1/(\d+)\s+(.+)$")
    count = 0
    for root, _, files in os.walk(DROPS_DIR):
        for file in files:
            if not file.endswith(".txt"):
                continue
            path = os.path.join(root, file)
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
            
            new_lines = [
                adjust_denominator(pattern.match(line.strip())) + "\n"
                if pattern.match(line.strip()) else line
                for line in lines
            ]
            with open(path, "w", encoding="utf-8") as f:
                f.writelines(new_lines)
            count += 1
    print(f"已完成 {count} 个怪物爆率文件的【Crystal 全版本五职业+Hero】慢节奏调优！")

if __name__ == "__main__":
    process_all_drops()
```