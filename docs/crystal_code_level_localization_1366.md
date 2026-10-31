> **2026-10-31 执行校正**：已按本方案目标完成本机双端代码显示补漏与 1366×768 游戏视口设定，译名向盛大 1.76 靠拢。当前分支实际使用 JSON，且名称包含逻辑用途，因此未执行下文删除 INI、全局源码字符串替换、覆盖内部 Name/只读 FriendlyName 的示例。现有代码原本也支持 1920；1366 是本次发行设置。原生登录/选人布局与远端服务端上限保留。实现、备份、测试与未验收范围见 [README.md](README.md) 文首；下文保留为原始方案参考，不能直接当成当前源码的可运行补丁。

# Crystal (CrystalM2) 游戏内全代码层零死角汉化方案 (排除 .lib 贴图)

> **工程目标**：  
> 1. **分辨率设定**：直接锁定官方原生支持的最大宽屏分辨率 **`1366 × 768`**（无需魔改视野格子，零黑边 Bug）。  
> 2. **彻底清除 5 大代码硬编码死角**：解决只改 `GameLanguage.cs` 后，装备悬浮窗（Tooltip）、面板标签、技能名字、地面掉落名字、服务端广播依然是英文的问题。  
> 3. **边界保持**：不碰 `.lib` 贴图素材、不碰 `Sound` 语音、不碰 `Server.exe` 后台管理 GUI 按钮。

---

## 一、 客户端直接锁定官方最大分辨率 (`1366 × 768`)

Crystal 官方原生完美适配的最大分辨率为 **`1366 × 768`（16:9 宽屏）**。在此分辨率下，UI 坐标、小地图、技能栏以及服务端 `DataRange`（18格视野）均处于原生对齐状态，无需修改地图渲染半径。

### 1.1 代码默认分辨率锁定 (`Client/Settings.cs`)
打开 `Client/Settings.cs`，找到 `Resolution` 与 `FontName` 变量，直接修改默认值：

```csharp
// 文件：Client/Settings.cs
public static int Resolution = 1366; // 原默认为 1024 或 800，直接锁死官方最大 1366 (即 1366x768)
public static bool FullScreen = false; // 建议窗口化运行，若需全屏拉伸可设为 true
public static string FontName = "Microsoft YaHei"; // 全局替换为微软雅黑，解决中文方块字与发虚
```

### 1.2 配置文件同步 (`Client/Config.ini`)
如果客户端目录已经生成过 `Config.ini`，它会覆盖代码里的默认值。打开客户端运行目录下的 `Config.ini` 修改：
```ini
[Graphics]
Resolution=1366
FullScreen=False
```

---

## 二、 为什么之前汉化不彻底？（5 大英文残留死角全景排查）

在 `Suprcode/Crystal` 源码中，除 `.lib` 图片外，玩家在游戏画面里看到的所有英文分布在以下 **5 个位置**（之前的方案只改了第 1 项，导致 2~5 项依然显示英文）：

| 死角编号 | 残留英文现象 | 真实源码位置 | 根治手段 |
| :--- | :--- | :--- | :--- |
| **死角 1** | 基础聊天提示、属性名 | `Shared/GameLanguage.cs` | 替换双端的 `GameLanguage.cs` 并**删除旧的 `Language.ini`** |
| **死角 2** | **鼠标悬停装备/技能时的属性框 (Tooltip)**<br>*(如 `Physical Attack`, `Durability`, `Need Level`, `Awakening`)* | **`Client/MirScenes/GameScene.cs`**<br>(`CreateItemLabel` & `CreateMagicLabel` 方法) | **100% 硬编码在源码里**！必须直接替换这两个方法里的字符串 |
| **死角 3** | **所有弹窗面板里的文字标签与按钮**<br>*(如设置界面、坐骑、英雄、排行、寄售、合成)* | **`Client/MirScenes/Dialogs/*.cs`**<br>(约 25 个 Dialog 类中的 `Text = "..."`) | 使用后附的 Python 脚本一键批量替换所有 `Dialogs` 下的硬编码字符串 |
| **死角 4** | **背包物品、地面掉落、技能栏的名字仍是英文**<br>*(即使服务端挂了字典，客户端依然显示英文)* | **`Shared/ItemInfo.cs` & `MagicInfo.cs`**<br>(网络封包 `Save(BinaryWriter)` 序列化处) | 在服务端发包序列化时，直接将中文名写入网络流下发给客户端 |
| **死角 5** | **服务端下发的动态黄字/红字广播**<br>*(如沙巴克提示、副本倒计时、Boss 刷新、交易提示)* | **`Server/MirObjects/PlayerObject.cs`** 等<br>(`ReceiveChat("...", ChatType.System)`) | 批量替换服务端 `ReceiveChat` 中的硬编码英文提示 |

---

## 三、 死角逐个击破：核心源码修改指南

### 3.1 【死角 4 根治】让物品、怪物、地图、技能 100% 给客户端下发中文
之前挂载 `ChineseNames.txt` 如果只改了 `FriendlyName`，客户端很多地方依然读的是 `Info.Name`。最彻底、且**绝对不破坏 `Envir/Drops/*.txt` 英文爆率表**的改法如下：

#### 第一步：在服务端 `Envir.cs` 加载完数据库并读取完爆率表后，再执行汉化覆盖！
打开 **`Server/MirEnvir/Envir.cs`**，找到 `Start()` 或 `LoadDB()` 与 `MonsterInfo.LoadDrops()` 调用的位置：

> **核心原理**：服务端只有在启动那一秒执行 `LoadDrops()` 时才需要用到怪物的英文名去匹配 `WhiteEvilBoar.txt`，以及用到物品的英文名去匹配爆率表里的 `JudgementMace`！**只要在 `LoadDrops()` 执行完毕之后，直接把内存里的名字覆盖成中文，爆率表已经绑定好了内存对象引用，后续客户端连进来看到的就 100% 全是纯中文！**

在 `Server/MirEnvir/Envir.cs` 中定位到所有 `LoadDrops()` 和 `LoadNPCs()` 执行完毕之后的位置，加入以下代码：

```csharp
// 文件：Server/MirEnvir/Envir.cs
// 务必放在 LoadDB() 以及所有 MonsterInfo.LoadDrops() 执行完毕之后调用！
public void ApplyFullInGameChinese()
{
    string dictPath = Path.Combine(Settings.EnvirPath, "ChineseNames.txt");
    if (!File.Exists(dictPath)) return;

    var dict = File.ReadAllLines(dictPath, Encoding.UTF8)
                   .Where(l => l.Contains('=') && !l.TrimStart().StartsWith(";"))
                   .Select(l => l.Split('=', 2))
                   .ToDictionary(k => k[0].Trim(), v => v[1].Trim(), StringComparer.OrdinalIgnoreCase);

    // 1. 汉化怪物显示名 (GameName 控制客户端头顶显示，Name 留给脚本)
    foreach (var mob in MonsterInfoList)
        if (dict.TryGetValue(mob.Name, out string cnMob)) mob.GameName = cnMob;

    // 2. 汉化地图右上角与小地图显示名 (Title)
    foreach (var map in MapInfoList)
        if (dict.TryGetValue(map.Title, out string cnMap)) map.Title = cnMap;

    // 3. 汉化物品显示名 (将中文存入 FriendlyName)
    foreach (var item in ItemInfoList)
        if (dict.TryGetValue(item.Name, out string cnItem)) item.FriendlyName = cnItem;

    // 4. 汉化五职业所有技能名称 (MagicInfo.Name)
    foreach (var magic in MagicInfoList)
        if (dict.TryGetValue(magic.Name, out string cnMagic)) magic.Name = cnMagic;
}
```

#### 第二步：修改 `Shared/ItemInfo.cs` 发包逻辑（1行代码让客户端所有地方的物品名变中文）
打开 **`Shared/ItemInfo.cs`**，找到 `public void Save(BinaryWriter writer)` 方法：
* **注意**：这个方法有两个重载，或者被服务端用来写 `Server.MirDB` 和发网络包。为了不污染 `Server.MirDB` 本地文件，我们直接在 **`Client` 端接收构造函数 `public ItemInfo(BinaryReader reader)`** 或 **`ItemInfo.FriendlyName` 属性** 处拦截：

```csharp
// 最稳妥改法：打开 Shared/ItemInfo.cs，找到 ToClient() 或 BinaryReader 读取构造函数：
public ItemInfo(BinaryReader reader, int version, int customVersion)
{
    Index = reader.ReadInt32();
    Name = reader.ReadString();
    // ... 原有读取逻辑 ...
    FriendlyName = reader.ReadString();

    // ===== 新增这 1 行：在客户端内存中，如果存在中文 FriendlyName，直接覆盖 Name！ =====
    // 这样服务端 Name 永远是英文（NPC脚本/GM命令全兼容），而客户端所有UI拿到的 Name 全是中文！
    #if CLIENT
    if (!string.IsNullOrEmpty(FriendlyName)) Name = FriendlyName;
    #endif
}
```
*(若项目未定义 `CLIENT` 宏，可直接打开 `Client/MirNetwork/MirConnection.cs` 在收到 `S.NewItemInfo` 封包时加上 `if (!string.IsNullOrEmpty(pkt.Info.FriendlyName)) pkt.Info.Name = pkt.Info.FriendlyName;`)*。

---

### 3.2 【死角 2 根治】汉化装备与技能悬浮属性窗 (`Client/MirScenes/GameScene.cs`)

你进游戏后把鼠标移到武器、衣服、戒指、坐骑、技能图标上弹出的半透明黑色属性框，里面的英文**全部硬编码在 `Client/MirScenes/GameScene.cs` 中**！

打开 `Client/MirScenes/GameScene.cs`，搜索 **`CreateItemLabel`** 和 **`CreateMagicLabel`**，将里面的硬编码英文对应替换为以下中文：

| `GameScene.cs` 中的原版硬编码英文 | 替换为中文 |
| :--- | :--- |
| `"Weight: "` / `"Durability: "` | `"重量: "` / `"持久: "` |
| `"Accuracy: +"` / `"Agility: +"` | `"准确: +"` / `"敏捷: +"` |
| `"Attack Speed: +"` / `"Luck: +"` / `"Curse: +"` | `"攻击速度: +"` / `"幸运: +"` / `"诅咒: +"` |
| `"Physical Attack: "` / `"Magic Attack: "` / `"Taoist Attack: "` | `"攻击: "` / `"魔法: "` / `"道术: "` |
| `"Physical Defence: "` / `"Magic Defence: "` | `"防御: "` / `"魔御: "` |
| `"Health: +"` / `"Mana: +"` | `"体力值: +"` / `"魔力值: +"` |
| `"Critical Rate: +"` / `"Critical Damage: +"` | `"暴击率: +"` / `"暴击伤害: +"` |
| `"Life Steal: +"` / `"Poison Recover: +"` / `"Spell Recover: +"` | `"吸血: +"` / `"中毒恢复: +"` / `"魔法恢复: +"` |
| `"Holy: +"` / `"Freezing: +"` / `"Poison Attack: +"` | `"神圣: +"` / `"冰冻: +"` / `"毒素攻击: +"` |
| `"Required Level: "` / `"Required Class: "` | `"需要等级: "` / `"职业要求: "` |
| `"Required DC: "` / `"Required MC: "` / `"Required SC: "` | `"需要攻击力: "` / `"需要魔法力: "` / `"需要道术力: "` |
| `"Warrior "` / `"Wizard "` / `"Taoist "` / `"Assassin "` / `"Archer "` | `"战士 "` / `"法师 "` / `"道士 "` / `"刺客 "` / `"弓箭手 "` |
| `"Cannot be Dropped"` / `"Cannot be Traded"` / `"Cannot be Sold"` | `"不可掉落"` / `"不可交易"` / `"不可出售"` |
| `"Awakening Level "` / `"Max Awakening Level"` | `"觉醒等级 "` / `"已达最大觉醒等级"` |
| `"Socket"` / `"Empty Socket"` | `"镶嵌槽"` / `"空插槽"` |
| `"Skill Level: "` / `"Skill Points: "` | `"技能等级: "` / `"熟练度: "` |

---

### 3.3 【死角 3 根治】全自动扫描汉化 `Client/MirScenes/Dialogs/` 下所有弹窗面板

客户端 `Client/MirScenes/Dialogs/` 下有 20 多个窗口代码文件（如 `HelpDialog.cs` 帮助、`MenuDialog.cs` 设置菜单、`RelationshipDialog.cs` 社交、`GuildDialog.cs` 行会、`MentorDialog.cs` 师徒、`TrustMerchantDialog.cs` 寄售拍卖行、`MountDialog.cs` 坐骑、`FishingDialog.cs` 钓鱼、`HeroDialogs.cs` 英雄面板等），里面有数百处 `Text = "English"`。

将以下 Python 脚本保存为 **`localize_client_code.py`**，放在解决方案根目录运行一次，即可自动把客户端源码里的所有硬编码 UI 文字替换为中文：

```python
import os
import re

CLIENT_DIR = "./Client"
SHARED_DIR = "./Shared"

# 核心 UI 硬编码精准替换字典 (覆盖 GameScene.cs 与所有 Dialogs/*.cs)
REPLACEMENTS = {
    # 角色与属性面板
    '"Character"': '"角色信息"',
    '"Inventory"': '"背包"',
    '"Equipment"': '"装备"',
    '"Skills"': '"技能"',
    '"Quests"': '"任务"',
    '"Hero"': '"英雄(副将)"',
    '"Mount"': '"坐骑"',
    '"Fishing"': '"垂钓"',
    '"Awakening"': '"装备觉醒"',
    '"Market"': '"寄售行"',
    '"Trust Merchant"': '"寄售商行"',
    '"Guild"': '"行会"',
    '"Friends"': '"好友"',
    '"Mail"': '"邮件"',
    '"Mentor"': '"师徒"',
    '"Relationship"': '"伴侣"',
    '"Ranking"': '"排行榜"',
    '"Belt"': '"快捷栏"',
    
    # Tooltip 核心词条 (GameScene.cs)
    '"Weight: "': '"重量: "',
    '"Durability: "': '"持久: "',
    '"Accuracy: +"': '"准确: +"',
    '"Agility: +"': '"敏捷: +"',
    '"Attack Speed: +"': '"攻击速度: +"',
    '"Luck: +"': '"幸运: +"',
    '"Curse: +"': '"诅咒: +"',
    '"Physical Attack: "': '"攻击: "',
    '"Magic Attack: "': '"魔法: "',
    '"Taoist Attack: "': '"道术: "',
    '"Physical Defence: "': '"防御: "',
    '"Magic Defence: "': '"魔御: "',
    '"Critical Rate: +"': '"暴击率: +"',
    '"Critical Damage: +"': '"暴击伤害: +"',
    '"Life Steal: +"': '"吸血: +"',
    '"Holy: +"': '"神圣: +"',
    '"Freezing: +"': '"冰冻: +"',
    '"Poison Attack: +"': '"毒素攻击: +"',
    '"Required Level: "': '"需要等级: "',
    '"Required DC: "': '"需要攻击: "',
    '"Required MC: "': '"需要魔法: "',
    '"Required SC: "': '"需要道术: "',
    '"Required Class: "': '"职业限制: "',
    '"Warrior"': '"战士"',
    '"Wizard"': '"法师"',
    '"Taoist"': '"道士"',
    '"Assassin"': '"刺客"',
    '"Archer"': '"弓箭手"',
    '"Skill Level: "': '"技能等级: "',
    
    # 按钮与通用对话框
    '"OK"': '"确定"',
    '"Cancel"': '"取消"',
    '"Close"': '"关闭"',
    '"Yes"': '"是"',
    '"No"': '"否"',
    '"Buy"': '"购买"',
    '"Sell"': '"出售"',
    '"Repair"': '"修理"',
    '"Special Repair"': '"特殊修理"',
    '"Storage"': '"仓库"',
    '"Search"': '"搜索"',
    '"Refresh"': '"刷新"',
    '"Collect"': '"领取"',
    '"Send"': '"发送"',
    '"Delete"': '"删除"',
    '"Reply"': '"回复"',
    '"Exit Game"': '"退出游戏"',
    '"Log Out"': '"返回选人"',
    '"Settings"': '"系统设置"',
    '"Options"': '"游戏选项"',
}

def replace_in_file(filepath):
    with open(filepath, "r", encoding="utf-8-sig", errors="ignore") as f:
        content = f.read()
    
    original = content
    for eng, chs in REPLACEMENTS.items():
        content = content.replace(eng, chs)
        
    if content != original:
        # 使用 utf-8-sig (带BOM) 保存，防止 Visual Studio 编译器把中文识别为乱码！
        with open(filepath, "w", encoding="utf-8-sig") as f:
            f.write(content)
        print(f"[已汉化] {filepath}")

def main():
    for root, _, files in os.walk(CLIENT_DIR):
        for file in files:
            if file.endswith(".cs"):
                replace_in_file(os.path.join(root, file))
    print("Client 端硬编码 UI 汉化扫描完成！请重新生成解决方案。")

if __name__ == "__main__":
    main()
```

> ⚠️ **极度重要的编译避坑提示（UTF-8 with BOM）**：  
> Visual Studio 的 C# 编译器在读取 `.cs` 源码时，如果文件是无 BOM 的 UTF-8，有时会把源码里的中文字符串按系统 GBK 编译导致进游戏乱码。上面的脚本专门使用了 `encoding="utf-8-sig"`（带 BOM 的 UTF-8），确保编译出来的中文 100% 正常！

---

### 3.4 【死角 1 & 5 根治】`Language.ini` 重新生成机制与服务端 `ReceiveChat`

很多玩家改了 `Shared/GameLanguage.cs` 里的英文，重新编译后进游戏发现**还是英文**！
* **原因**：因为你的客户端和服务端根目录下已经存在了一个旧的 **`Language.ini`**！程序启动时发现有 `Language.ini`，就会直接读取 `.ini` 里的旧英文，完全忽略你在 `GameLanguage.cs` 里改的中文！
* **解决方法**：
  1. 修改完 `Shared/GameLanguage.cs` 后；
  2. **务必手动删除 `Client/` 和 `Server/` 运行目录下的 `Language.ini` 文件**！
  3. 重新启动客户端和服务端，程序会自动根据你汉化后的 `GameLanguage.cs` 重新生成一份纯中文的 `Language.ini`。

#### 服务端动态广播汉化 (`Server/MirObjects/PlayerObject.cs`)
服务端后台界面（`Server/Forms/`）我们完全不碰，但在 **`Server/MirObjects/PlayerObject.cs`**、**`HeroObject.cs`** 和 **`Map.cs`** 中，凡是调用 **`ReceiveChat("...", ChatType.System)`** 或 **`ReceiveChat("...", ChatType.Hint)`** 的地方，都是发给玩家客户端左下角聊天框看的提示（例如：
* `"Your hero has reached level {0}."` $\rightarrow$ `"你的英雄等级提升到了 {0} 级！"`
* `"You cannot summon your hero in this map."` $\rightarrow$ `"当前地图禁止召唤英雄。"`
* `"Could not find a suitable location."` $\rightarrow$ `"无法找到合适的传送落脚点。"`）
可在 VS 中按 `Ctrl+Shift+F` 在 `Server/MirObjects` 文件夹下全局搜索 `ReceiveChat("`，仅约 30 处直接带双引号的英文提示，花 3 分钟替换为中文即可。

---

## 四、 全版本 `Envir/ChineseNames.txt` 完整结构模板

将以下内容保存为 `Server/Envir/ChineseNames.txt`（UTF-8 编码），配合 `3.1` 中的 `ApplyFullInGameChinese()` 方法，即可覆盖五职业技能、核心地图、常见怪物与装备：

```ini
; ====== 1. 核心地图 (MapInfo.Title) ======
BichonProvince=比奇省
BorderVillage=边界村
SerpentValley=毒蛇山谷
MongchonProvince=盟重省
WoomaForest=沃玛森林
WoomaTemple=沃玛寺庙
BugCave=蜈蚣洞(地牢)
StoneTomb=石墓(猪洞)
ZumaTemple=祖玛寺庙
RedMoonValley=赤月峡谷
WhiteSunGate=白日门
FengMoValley=封魔谷
CangYueIsland=苍月岛
BullDevilTemple=牛魔寺庙
PrajnaTemple=般若寺
FoxMountain=狐月山
IcePalace=冰宫
DragonAbysm=火龙魔窟

; ====== 2. 五职业全版本技能 (MagicInfo.Name) ======
; --- 战士 (Warrior) ---
Fencing=基本剑术
Slaying=攻杀剑术
Thrusting=刺杀剑术
HalfMoon=半月弯刀
ShoulderDash=野蛮冲撞
TwinDrakeBlade=双龙斩
Entrapment=捕绳剑
FlamingSword=烈火剑法
LionRoar=狮子吼
CrossHalfMoon=圆月弯刀
BladeAvalanche=开天斩
ProtectionField=护体神盾
Rage=狂暴冲锋
SlashingBurst=逐日剑法

; --- 法师 (Wizard) ---
FireBall=火球术
Repulsion=抗拒火环
ElectricShock=诱惑之光
GreatFireBall=大火球
HellFire=地狱火
ThunderBolt=雷电术
Teleport=瞬息移动
FireBang=爆裂火焰
FireWall=火墙
Lightning=疾光电影
FrostCrunch=冰咆哮
ThunderStorm=地狱雷光
MagicShield=魔法盾
TurnUndead=圣言术
Vampirism=噬魂术
IcePillar=寒冰掌
FlameDisruptor=灭天火
Mirroring=分身术
FlameField=火龙气焰
Blizzard=天霜冰环
MeteorStrike=流星火雨

; --- 道士 (Taoist) ---
Healing=治愈术
SpiritSword=精神力战法
Poisoning=施毒术
SoulFireBall=灵魂火符
SummonSkeleton=召唤骷髅
Hiding=隐身术
MassHiding=集体隐身术
SoulShield=幽灵盾
Revelation=心灵启示
BlessedArmour=神圣战甲术
EnergyRepulsor=气功波
TrapHexagon=困魔咒
Purification=净化术
MassHealing=群体治愈术
Hallucination=迷魂术
UltimateEnhancer=无极真气
SummonShinsu=召唤神兽
Reincarnation=复活术
PoisonCloud=噬血毒云
Curse=诅咒术
Plague=瘟疫
SummonHolyDeva=召唤月灵(圣兽)

; --- 刺客 (Assassin) ---
FatalSword=致命刺剑
DoubleSlash=双剑合璧
Haste=急速鬼步
FlashDash=鬼灵步
LightBody=轻身步
HeavenlySword=天机剑法
FireBurst=爆炎术
Trap=缚足陷阱
PoisonSword=施毒刺杀
MoonLight=月影潜行
MPEater=吸魔剑
SwiftFeet=神行百变
DarkBody=暗影替身
Hemorrhage=割裂出血
CrescentSlash=霜月弯刀
MoonMist=月华乱舞

; --- 弓箭手 (Archer) ---
Focus=精准射击
StraightShot=穿云箭
DoubleShot=二连射
ExplosiveTrap=爆裂陷阱
DelayedExplosion=延时爆破
Meditation=凝神聚气
BackStep=后跃步
ElementalShot=元素射击
Concentration=心眼专注
Stonetrap=石化陷阱
ElementalBarrier=元素护盾
SummonVampire=召唤血蝠
VampireShot=吸血箭
SummonToad=召唤剧毒蟾蜍
PoisonShot=毒雾箭
CrippleShot=致残射击
SummonSnakes=召唤灵蛇图腾
NapalmShot=爆炎箭

; ====== 3. 经典怪物与 Boss (MonsterInfo.Name) ======
Deer=鹿
Hen=鸡
Scarecrow=稻草人
RakingCat=钉耙猫
HookingCat=多钩猫
Oma=半兽人
OmaFighter=半兽战士
OmaWarrior=半兽勇士
CaveMaggot=洞蛆
Skeleton=骷髅
BoneFighter=骷髅战士
BoneElite=骷髅精灵
Zombie=僵尸
ShamanZombie=尸王
WoomaSoldier=沃玛战士
WoomaFighter=沃玛勇士
WoomaGuardian=沃玛卫士
WoomaTaurus=沃玛教主
RedBoar=红野猪
BlackBoar=黑野猪
WhiteEvilBoar=白野猪
EvilCentipede=邪恶钳虫
BugBat=蝙蝠
Tongs=钳虫
Centipede=蜈蚣
EvilTongs=触龙神
ZumaMice=祖玛雕像
ZumaGuardian=祖玛卫士
ZumaArcher=祖玛弓箭手
ZumaTaurus=祖玛教主
MoonSpider=月魔蜘蛛
RedMoonEvil=赤月恶魔
```

---

## 五、 最终检查清单 (Checklist)

1. [ ] **`Settings.cs` & `Config.ini`**：确认 `Resolution = 1366`，`FontName = "Microsoft YaHei"`。
2. [ ] **删除旧 `Language.ini`**：替换 `Shared/GameLanguage.cs` 后，务必删掉 `Client/` 和 `Server/` 目录下的旧 `Language.ini` 让其重新生成。
3. [ ] **运行 `localize_client_code.py`**：一键替换 `Client/MirScenes/` 下所有 Tooltip 与弹窗面板的硬编码英文。
4. [ ] **注入 `ApplyFullInGameChinese()`**：在 `Server/MirEnvir/Envir.cs` 的 `LoadDrops()` 之后调用字典挂载，并在 `Shared/ItemInfo.cs` 读取时用 `FriendlyName` 覆盖客户端 `Name`。
5. [ ] **重新生成解决方案 (`Rebuild Solution`)**：启动游戏，除 `.lib` 美术贴图按钮外，游戏内所有动态文本、悬浮窗、技能、地图、掉落均已 100% 汉化！