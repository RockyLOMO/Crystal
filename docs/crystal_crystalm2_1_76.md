# Crystal (CrystalM2) 全栈汉化与 1.76 复古化工程方案

> **2026-10-30 实施校正**：本文原方案保留作讨论材料，实际实施与验证以 [README.md](README.md) 为准。当前代码使用 `Shared/Language.cs` 和 `Localization/Chinese.json`，并不读取本文示例的 `Language.ini`。本轮采用显示名称字典，保留数据库英文 Name、掉落文件名、脚本命令参数和地图 FileName；不执行全文替换或删地图。`DbLanguage.json` 原常量没有加载实现，现已新增独立的 `Shared/DbLanguage.cs` 加载器及 `*.zh-CN.json` 字典。官方开源来源不等于已证明零后门；未完成全源码安全审计。关闭额外职业/新英雄也不等于已完成纯 1.76 规则和内容裁剪，未知第三方底包不能保证无缝兼容。

> **适用项目**：`Suprcode/Crystal` (LOMCN 官方 C# 开源引擎)
> **目标效果**：保持官方最新主分支源码的零后门与高稳定性，实现**客户端 UI + 系统提示 + 物品/怪物/地图/技能 + NPC 脚本/爆率表** 100% 盛大标准中文汉化，且不破坏掉落与脚本引用链路。

---

## 一、 核心架构拆解：为什么直接改名字会导致“怪不爆装备”？

很多新手在汉化 Crystal 时，直接打开服务端可视化后台把 `WhiteEvilBoar` 改成了 `白野猪`，把 `JudgementMace` 改成了 `裁决之杖`，结果进游戏发现：**怪物一个都不刷新，或者打死怪什么都不爆！**

这是因为 Crystal 的数据层采用了**“名称强绑定（Name-Key Binding）”**机制。全套汉化必须同步处理以下 **4 个核心层级**：

| 层级 | 核心文件位置 | 作用说明 | 联动耦合关系（避坑重点） |
| :--- | :--- | :--- | :--- |
| **1. 语言字典与字体层** | `Shared/GameLanguage.cs`<br>`Client/Language.ini` | 客户端面板按钮、属性名、系统红字/黄字提示 | **完全独立**，直接替换即可立即生效 |
| **2. 核心二进制数据库** | `Server/Envir/Server.MirDB` | 存放所有物品(`ItemInfo`)、怪物(`MonsterInfo`)、地图(`MapInfo`)、技能(`MagicInfo`) | **核心数据源**，修改其中的 `Name` 字段会影响下方所有文本脚本 |
| **3. 怪物爆率表** | `Server/Envir/Drops/*.txt` | 文件名为**怪物名**，文件内容为**物品名** | **强耦合**：文件名必须与 `MonsterInfo.Name` 一致；内部道具名必须与 `ItemInfo.Name` 一致 |
| **4. NPC 与任务脚本** | `Server/Envir/NPCs/*.txt`<br>`Server/Envir/Quests/*.txt` | NPC 对话文本、传送指令、给予物品(`GIVE`)、检查物品(`CHECKITEM`) | **强耦合**：脚本中的物品名、怪物名必须与 `Server.MirDB` 严格一致，文件编码必须为 **UTF-8** |

---

## 二、 第一阶段：客户端字体修正与 UI 语言包汉化（10分钟搞定）

### 1.1 修复中文字体渲染发虚/方框问题
官方原版客户端默认使用英文字体 `MS Sans Serif` 或 `Tahoma`，在渲染中文时字号偏小且容易发虚。
1. 用 Visual Studio 打开 `Legend of Mir.sln`。
2. 全局搜索（`Ctrl + Shift + F`）关键词：`new Font(` 或在 `Client/MirControls/MirLabel.cs` 中找到默认字体定义：
   ```csharp
   // 原版代码通常位于 Client/Settings.cs 或 Client/MirControls/MirLabel.cs
   public static string FontName = "MS Sans Serif";
   ```
3. 将其修改为国内 Windows 原生自带的高清中文字体（推荐 **微软雅黑** 或复古感强的 **新宋体**）：
   ```csharp
   // 推荐现代清晰风格：
   public static string FontName = "Microsoft YaHei";
   // 若追求老盛大 1.76 像素点阵感，可改为：
   // public static string FontName = "NSimSun";
   ```

### 1.2 一键替换 `Language.ini`（客户端与服务端系统提示全汉化）
Crystal 原生支持外部语言配置文件。启动一次客户端和服务端后，会在根目录自动生成 `Language.ini`（或者直接修改源码中的 `Shared/GameLanguage.cs`）。

将以下整理好的标准中文配置直接覆盖到**客户端根目录**与**服务端根目录**的 `Language.ini` 中（注意保存为 **UTF-8** 编码）：

```ini
[Language]
; --- 客户端基础提示 ---
PetMode_Both=[宠物模式: 攻击并移动]
PetMode_MoveOnly=[宠物模式: 仅跟随移动]
PetMode_AttackOnly=[宠物模式: 原地攻击]
PetMode_None=[宠物模式: 休息待命]
PetMode_FocusMasterTarget=[宠物模式: 锁定主人目标]

AttackMode_Peace=[攻击模式: 和平攻击]
AttackMode_Group=[攻击模式: 编组攻击]
AttackMode_Guild=[攻击模式: 行会攻击]
AttackMode_EnemyGuild=[攻击模式: 敌对行会]
AttackMode_RedBrown=[攻击模式: 善恶攻击]
AttackMode_All=[攻击模式: 全体攻击]

LogOutTip=正在返回角色选择界面，请稍候...
ExitTip=正在退出传奇世界，请稍候...
DiedTip=你已经死亡，可在城内复活或等待道士救治。
DropTip=确定要丢弃 {0} 吗？
GoldTip=请输入要丢弃的金币数量:

; --- 背包与装备穿戴校验 ---
LowLevel=你的等级不足，无法穿戴该装备。
LowDC=你的攻击力不足，无法穿戴该装备。
LowMC=你的魔法力不足，无法穿戴该装备。
LowSC=你的道术力不足，无法穿戴该装备。
NotEnoughBagSpace=背包空间不足！
NotEnoughGold=你的金币不足！
WeaponLuck=你的武器被祝福了，幸运值+1！
WeaponCurse=你的武器被诅咒了，诅咒值+1！
WeaponNoEffect=使用祝福油没有任何效果。

; --- 技能与战斗提示 ---
LevelUp=恭喜你升级了！当前等级: {0}
GainExp=获得经验值: {0}
SafeZone=安全区
CannotUseInSafeZone=在安全区内无法使用该物品或技能！
NotEnoughMp=魔法值不足，无法施展技能。
SkillLevelUp=技能 [{0}] 熟练度提升，当前等级: {1} 级！
GroupSwitch=编组开关: {0}
TradeSwitch=交易开关: {0}
WhisperSwitch=私聊开关: {0}
```

---

## 三、 第二阶段：数据库 (`Server.MirDB`) 与 `Drops` 联动批量汉化方案

这是最核心的一步。**切勿在后台手工一个一个改**，既慢又会导致爆率表断链。最靠谱的工程方案是：**利用 Python 映射字典，同步改写导出的数据库文件与全套 `Drops/*.txt`、`NPCs/*.txt` 脚本**。

### 2.1 方案 A：直接使用 C# 注入式映射汉化（最稳妥、不改文件名）
如果你不想破坏官方 `Standard Server Files` 里几千个复杂的英文爆率文件和英文 NPC 脚本引用，**最聪明的做法是在 C# 源码里加一个 `LocalName`（中文显示名）属性，或者在客户端接收封包时做字典映射！**

하지만如果你希望服务端后台看到的也是纯中文，请按下面的 **方案 B（全量替换法）** 执行。

### 2.2 方案 B：全量导出 + Python 字典同步替换法（彻底中文化）

#### 第一步：从服务端导出明文数据或添加简易汉化接口
在 `Server` 项目的 `MirDatabase/Enivronment.cs`（或 `ServerForm.cs`）中，你可以添加一段不到 30 行的 C# 批量汉化方法，在服务端启动加载完 `Server.MirDB` 后执行一次：

```csharp
// 在 ServerForm.cs 中添加一个按钮事件或启动调用，读取外部的 cn_dict.csv 进行一键全库汉化
public void LocalizeDatabase(string dictFilePath)
{
    if (!File.Exists(dictFilePath)) return;

    // 读取中英对照表: 格式为 "Type,EnglishName,ChineseName"
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadAllLines(dictFilePath, Encoding.UTF8))
    {
        var parts = line.Split(',');
        if (parts.Length >= 3)
            dict[parts[1].Trim()] = parts[2].Trim();
    }

    // 1. 汉化物品表 (ItemInfo)
    foreach (var item in Envir.ItemInfoList)
    {
        if (dict.TryGetValue(item.Name, out string cnName))
            item.Name = cnName;
    }

    // 2. 汉化怪物表 (MonsterInfo)
    foreach (var mob in Envir.MonsterInfoList)
    {
        if (dict.TryGetValue(mob.Name, out string cnName))
            mob.Name = cnName;
    }

    // 3. 汉化地图表 (MapInfo)
    foreach (var map in Envir.MapInfoList)
    {
        if (dict.TryGetValue(map.Title, out string cnTitle))
            map.Title = cnTitle;
    }

    // 保存回 Server.MirDB
    Envir.SaveDB();
    MessageBox.Show("Server.MirDB 数据库汉化完成并已保存！");
}
```

#### 第二步：使用 Python 脚本同步重命名 `Envir/Drops` 与 `Envir/NPCs`
当 `Server.MirDB` 里的名字变成中文后，在服务端根目录运行以下 Python 脚本，它会自动读取同一份 `cn_dict.csv` 中英对照表，把所有爆率文件名、爆率内容、NPC脚本里的英文道具名**一秒钟全部对齐为中文**：

```python
# localize_envir.py - 同步汉化 Drops 爆率表与 NPCs 脚本
import os
import csv

DICT_FILE = "cn_dict.csv"
ENVR_DIR = "./Envir"

def load_dictionary(csv_path):
    mapping = {}
    with open(csv_path, mode='r', encoding='utf-8') as f:
        reader = csv.reader(f)
        for row in reader:
            if len(row) >= 3 and not row[0].startswith('#'):
                en_name = row[1].strip()
                cn_name = row[2].strip()
                mapping[en_name] = cn_name
    # 按英文长度降序排列，防止短词误替换长词的一部分（如先把 "Sword" 替换导致 "WoodenSword" 损坏）
    return dict(sorted(mapping.items(), key=lambda item: len(item[0]), reverse=True))

def process_drops(drops_dir, mapping):
    for root, _, files in os.walk(drops_dir):
        for filename in files:
            if not filename.endswith(".txt"):
                continue
            filepath = os.path.join(root, filename)

            # 1. 替换爆率文件内部的物品名称
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()

            for en, cn in mapping.items():
                content = content.replace(en, cn)

            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)

            # 2. 如果文件名本身是怪物英文名，则将文件名也重命名为中文怪物名
            mob_name = filename[:-4] # 去掉 .txt
            if mob_name in mapping:
                new_filepath = os.path.join(root, mapping[mob_name] + ".txt")
                if filepath != new_filepath:
                    os.replace(filepath, new_filepath)
                    print(f"[爆率表重命名] {filename} -> {mapping[mob_name]}.txt")

if __name__ == "__main__":
    mapping = load_dictionary(DICT_FILE)
    process_drops(os.path.join(ENVR_DIR, "Drops"), mapping)
    process_drops(os.path.join(ENVR_DIR, "NPCs"), mapping)
    print("Envir 爆率与 NPC 脚本全部汉化对齐完毕！")
```

---

## 四、 附：盛大 1.76 核心字典对照表 (`cn_dict.csv` 精选种子模板)

将以下内容保存为 `cn_dict.csv`，即可作为上述 C# 与 Python 脚本的输入字典（涵盖了 1.76 最核心的地图、怪物与装备）：

```csv
# 类型,EuroMir英文原名,盛大1.76标准中文名
Map,BichonProvince,比奇省
Map,MongchonProvince,盟重省
Map,SerpentValley,毒蛇山谷
Map,WoomaForest,沃玛森林
Map,WoomaTemple,沃玛寺庙
Map,ZumaTemple,祖玛寺庙
Map,RedMoonGorge,赤月峡谷
Map,PrajnaIsland,苍月岛
Map,sealedValley,封魔谷
Map,WhiteSunGate,白日门
Monster,Deer,鹿
Monster,Hen,鸡
Monster,Scarecrow,稻草人
Monster,RakingCat,钉耙猫
Monster,HookingCat,多钩猫
Monster,Oma,半兽人
Monster,OmaFighter,半兽战士
Monster,OmaWarrior,半兽勇士
Monster,SpittingSpider,毒蜘蛛
Monster,CaveMaggot,洞蛆
Monster,Skeleton,骷髅
Monster,BoneFighter,骷髅战士
Monster,BoneElite,骷髅精灵
Monster,WoomaGuardian,沃玛卫士
Monster,WoomaTaurus,沃玛教主
Monster,RedBoar,红野猪
Monster,BlackBoar,黑野猪
Monster,WhiteEvilBoar,白野猪
Monster,EvilCentipede,邪恶钳虫
Monster,TouchDragonGod,触龙神
Monster,ZumaArcher,祖玛弓箭手
Monster,ZumaStatue,祖玛雕像
Monster,ZumaGuardian,祖玛卫士
Monster,ZumaTaurus,祖玛教主
Monster,RedMoonEvil,赤月恶魔
Monster,TwinHeadBeast,双头金刚
Monster,TwinHeadBlood,双头血魔
Item,WoodenSword,木剑
Item,BronzeSword,青铜剑
Item,IronSword,铁剑
Item,ShortSword,短剑
Item,octagonalRing,八角戒指
Item,GlassRing,玻璃戒指
Item,TraditionalNecklace,传统项链
Item,EbonySword,乌木剑
Item,EightDiagramSword,八荒
Item,HalfMoon,半月
Item,SeaSoul,海魂
Item,CondorSword,凝霜
Item,FrostWind,凌风
Item,Executioner,修罗
Item,WoomaHorn,沃玛号角
Item,RefineSword,炼狱
Item,SilverSnake,银蛇
Item,MagicStaff,魔杖
Item,JudgementMace,裁决之杖
Item,BoneJadeStaff,骨玉权杖
Item,DragonPatternSword,龙纹剑
Item,DragonSlayer,屠龙
Item,SoulStaff,嗜魂法杖
Item,CarefreeFan,逍遥扇
Item,BaseDress(M),布衣(男)
Item,BaseDress(F),布衣(女)
Item,LightArmor(M),轻型盔甲(男)
Item,LightArmor(F),轻型盔甲(女)
Item,HeavyArmor(M),重盔甲(男)
Item,HeavyArmor(F),重盔甲(女)
Item,SoulArmor(M),灵魂战衣(男)
Item,SoulArmor(F),灵魂战衣(女)
Item,MagicRobe(M),魔法长袍(男)
Item,MagicRobe(F),魔法长袍(女)
Item,WarGodArmor,天魔神甲
Item,SacredWarArmor,圣战宝甲
Item,DharmaGodCloak,法神披风
Item,霓裳羽衣,霓裳羽衣
Item,TaoistGodRobe,天尊道袍
Item,MasterRobe,天师长袍
Item,SunPotion,(小)太阳水
Item,SunPotion(M),强效太阳水
Item,BlessingOil,祝福油
Item,TownTeleport,回城卷
Item,RandomTeleport,随机传送卷
Item,DungeonEscape,地牢逃脱卷
```

---

## 五、 第三阶段：三步将 Crystal 锁定为纯粹“1.76 重装上阵”

完成汉化后，只需在源码和配置里做以下 3 处改动，即可屏蔽后期内容，还原最纯粹的盛大 1.76 体验：

### 1. 屏蔽刺客与弓箭手（锁定战法道三职业）
打开 `Client/MirScenes/SelectScene.cs`（创建角色界面），找到职业切换按钮逻辑，注释掉刺客与弓箭手按钮的点击或可见性；同时在服务端 `Server/MirNetwork/MirConnection.cs` 的 `NewCharacter` 方法中加入一行拦截：
```csharp
if (p.Class == MirClass.Assassin || p.Class == MirClass.Archer) return; // 严禁创建刺客与弓手
```

### 2. 关闭坐骑、觉醒与后期系统
在客户端主界面 `Client/MirScenes/GameScene.cs` 中，将底部 UI 上的 **Mount（坐骑）**、**Awakening（觉醒）**、**Mentor（师徒）** 按钮设置 `Visible = false`，界面瞬间变得极其干净清爽。

### 3. 锁定 1.76 地图边界
打开服务端 `Server.exe` -> 顶部菜单 `System` -> `Map Info`：
* 按住 `Shift` 选中排在 **赤月峡谷（RedMoonGorge）** 和 **六大重装暗之 Boss 房间** 之后的所有后期韩服地图（如狐月山、冰宫、火龙洞等），直接点击删除，或者在 `Movement` 页签中删除通往这些地图的传送门坐标。

---

## 六、 懒人捷径：如何直接获取国内现成的汉化底包？

如果你不想自己跑脚本转换数据库，可以直接采用**“官方最新源码 + 国内现成汉化底包”**的组合：
1. **拉取最新引擎源码**：直接从 GitHub `Suprcode/Crystal` 拉取最新代码，按本文 **第一阶段** 改好字体与 `Language.ini`。
2. **获取中文 1.76 底包**：在 Gitee（码云）搜索 `CrystalM2` 或 `Mir2 C#`，下载国内开发者已经翻译并裁剪好的 `Envir` 文件夹（包含全中文的 `Server.MirDB`、`Drops`、`NPCs`）。
3. **版本对齐小技巧**：如果老底包的 `Server.MirDB` 版本号低于最新源码，不用担心，Crystal 的 `Envir.LoadDB()` 内置了**向下兼容自动升级机制**（`switch (Version)` 分支会自动补齐新字段并升级存档），直接丢进去启动 `Server.exe` 即可无缝升级运行！
