> **2026-10-31 实际执行说明**：已按本方案的“玩家中文、后台英文、音效原版”边界完成显示层改造与本机发布；经典译名尽量靠拢盛大 1.76。当前源码实际使用 JSON 语言包，`FriendlyName` / `GameName` 为派生属性，以下 `Language.ini`、直接赋值、`ChineseNames.txt` 和“几分钟/100%”为原方案示意，未照抄执行。具体文件、覆盖范围、备份、测试与限制以 [README.md](README.md) 文首为准。铁匠脚本 `#ELSEIF` 导致的 `0159` 地图加载异常已修复并通过重启验证；实际游戏画面仍待进图人工验收。

# Crystal (CrystalM2) 游戏内文字精准汉化工程方案 (服务端与语音保持英文)

> **工程目标**：  
> 1. **100% 汉化（玩家视野）**：客户端 UI 面板、属性悬浮窗（Tooltips）、聊天框系统提示、物品名、怪物名、地图名、技能名、NPC 对话与任务文本。  
> 2. **0% 改动（运维与听觉）**：`Server.exe` 后台管理界面保持全英文原貌；`Sound/` 语音与音效保持原版；底层数据库配置键名尽量保持稳定。

---

## 一、 汉化范围边界划分表

| 模块层级 | 涉及文件 / 目录 | 汉化策略 | 预期效果 |
| :--- | :--- | :--- | :--- |
| **1. 客户端字体渲染** | `Client/MirControls/MirLabel.cs` | **必改（1行代码）** | 将默认英文字体替换为 `Microsoft YaHei`，彻底消除中文方块字与乱码 |
| **2. 客户端 UI 与系统提示** | `Language.ini` (或 `GameLanguage.cs`) | **全量汉化** | 登录界面、属性栏、设置项、快捷键说明、系统报错与战斗提示全中文 |
| **3. 装备/怪物/地图/技能名** | `Server.MirDB` + `Envir/Drops/*.txt` | **精准汉化（见第三节双轨方案）** | 地上爆出中文装备名、怪物头顶显示中文、右上角小地图显示中文 |
| **4. NPC 对话与任务文本** | `Envir/NPCs/*.txt` & `Envir/Quests/` | **仅汉化显示文本段** | 点击老兵、药店老板显示中文对话，但保留 `#IF / #ACT` 英文逻辑指令 |
| **5. 服务端 GUI 管理后台** | `Server/Forms/*.cs` | **保持英文（不碰）** | 后台菜单（`ItemInfo`, `MonsterInfo`, `Config`）保持英文，干净易维护 |
| **6. 游戏音效与语音** | `Client/Sound/*` | **保持原版（不碰）** | 挥刀、惨叫、技能音效原汁原味 |

---

## 二、 第一阶段：客户端中文字体渲染与 UI 字典汉化（耗时：10 分钟）

### Step 1：修复客户端 DirectX 中文字体显示
Crystal 客户端默认使用 `FontName = "MS Sans Serif"` 或 `"Arial"`，在渲染中文字符时会出现字体发虚或“口口”乱码。
* **修改文件**：`Client/MirScenes/GameScene.cs` 及 `Client/MirControls/MirLabel.cs`（或全局搜索 `"MS Sans Serif"`）
* **操作**：将默认字体名称统一替换为 **`"Microsoft YaHei"`**（微软雅黑）或 **`"SimSun"`**（宋体，更有老传奇像素味）。

```csharp
// 在 Client/Settings.cs 中找到 FontName 定义：
public static string FontName = "Microsoft YaHei"; // 原为 "MS Sans Serif" 或 "Arial"
```

### Step 2：注入客户端与服务端下发提示语字典 (`GameLanguage.cs`)
Crystal 把所有游戏内看得到的系统提示（如“升级了”、“背包已满”、“攻击力 10-20”）全部集中在 `Shared/GameLanguage.cs` 中，并在启动时自动生成/读取 `Language.ini`。
* **操作**：无需修改 `Server` 后台窗口代码，直接修改 **`Shared/GameLanguage.cs`** 中的默认字符串赋值（然后删除旧的 `Language.ini` 让程序重新生成一份中文版）：

```csharp
// 文件路径：Shared/GameLanguage.cs (节选核心游戏内高频词条)
public static class GameLanguage
{
    // 客户端 UI 与角色面板
    public static string Attack = "攻击";
    public static string Magic = "魔法";
    public static string Taoism = "道术";
    public static string Defence = "防御";
    public static string MagicDefence = "魔御";
    public static string Accuracy = "准确";
    public static string Agility = "敏捷";
    public static string Luck = "幸运";
    public static string CriticalRate = "暴击率";
    public static string CriticalDamage = "暴击伤害";
    public static string HpDrainRate = "吸血";
    public static string BagWeight = "背包负重";
    public static string WearWeight = "穿戴负重";
    public static string HandWeight = "腕力";

    // 游戏内系统广播与战斗提示 (Server 下发给 Client 聊天框的文字)
    public static string LevelUp = "恭喜！你的等级提升到了 {0} 级！";
    public static string GainExp = "获得经验值: {0}";
    public static string NotEnoughGold = "你的金币不足！";
    public static string BagFull = "你的背包已满，无法拾取更多物品。";
    public static string SafeZone = "安全区";
    public static string WeaponLuckUp = "你的武器受到祝福，幸运值提升了！";
    public static string WeaponCurse = "你的武器受到了诅咒！";
    // ... 其余约 150 行提示词可直接交由 AI 一键批量替换为中文
}
```

---

## 三、 第二阶段：物品、怪物、地图与技能名称汉化（核心架构决策）

在 Crystal 中，如果你直接在后台把 `MonsterInfo` 里的 `WhiteEvilBoar` 改名成 `白野猪`，或者把 `ItemInfo` 里的 `JudgementMace` 改名成 `裁决之杖`，会导致 `Envir/Drops/` 爆率文件和 `Envir/NPCs/` 脚本里写的英文物品名全部失效（找不到物品）。

为了做到**“后台和脚本继续优雅地用英文，游戏画面里玩家看到的纯是中文”**，为你提供以下两种工程方案（**强烈推荐【方案 A】**）：

### 🌟 方案 A（架构师首选）：在源码中增加 `FriendlyName`（显示名与索引名分离）
**原理**：只需要改 **15 行 C# 代码**，让服务端的逻辑脚本（Drops/NPCs）继续用原版英文 `Name` 索引，但下发给客户端渲染画面时，自动下发中文名！**零爆率断链风险，服务端后台还能同时看到中英文对照！**

#### 1. 物品表实现“英文索引 + 中文显示” (`ItemInfo.cs`)
打开 `Shared/ItemInfo.cs`（或对应的数据结构定义文件）：
* 原版 `ItemInfo` 中已经有一个字段叫 `FriendlyName`（或者我们可以直接利用它）！
* 如果你在后台把 `Name` 保持为 `"JudgementMace"`（供爆率表读取），把 `FriendlyName` 填为 `"裁决之杖"`。
* 在客户端绘制物品名称的 `Client/MirObjects/ItemObject.cs` 及背包 Tooltip 处，将显示逻辑改为：

```csharp
// 客户端显示物品名称时：优先显示中文 FriendlyName，若为空则回退显示英文 Name
public string DisplayName => !string.IsNullOrEmpty(Info.FriendlyName) ? Info.FriendlyName : Info.Name;
```

#### 2. 怪物表实现“英文索引 + 中文头顶显示” (`MonsterInfo.cs`)
同样在 `MonsterInfo` 中，它原生就完美区分了两个字段：
* **`Name`（内部英文名）**：例如 `WhiteEvilBoar`（用于匹配 `Envir/Drops/WhiteEvilBoar.txt` 爆率文件，**保持英文不动**！）。
* **`GameName`（游戏内显示名）**：这正是怪物在游戏画面里头顶显示的名字！直接将 `GameName` 改为 **`白野猪`**！
  * **优势**：前面我们写的变异精英怪代码 `Name = $"[领主]{Info.GameName}"` 会自动在游戏里显示为金色的 **`[领主]白野猪`**，而后台加载爆率时依然去读 `WhiteEvilBoar.txt`，完美闭环！

#### 3. 地图表实现“英文文件名 + 中文右上角显示” (`MapInfo.cs`)
`MapInfo` 原生同样自带分离字段：
* **`FileName`**：地图文件索引（如 `0`、`3`、`D717`，**保持不动**）。
* **`Title`**：游戏内右上角小地图与进图提示显示的名称！直接将 `Title` 从 `BichonProvince` 改为 **`比奇省`**，`StoneTomb7` 改为 **`石墓七层`**！

---

### 方案 B（传统替换法）：导出 CSV 批量汉化 + 同步替换 Drops 文本
如果你不想用双字段，想把 `ItemInfo.Name` 也彻底改成中文：
1. 打开 `Server.exe` 后台（英文界面），点击顶部 **`Export` -> `ItemInfo` / `MonsterInfo` / `MapInfo` / `MagicInfo`**，导出为 4 个 CSV/Excel 文件。
2. 运行 Python 脚本，读取中英文对照字典（如 `JudgementMace -> 裁决之杖`），**同时替换导出表格中的名称，以及 `Envir/Drops/*.txt` 爆率文件里的对应英文单词**。
3. 在后台点击 **`Import`** 重新导回生成中文版 `Server.MirDB`。

> 💡 **对比结论**：对于自己有源码编译能力的开发者，**【方案 A】完胜**。因为 `MonsterInfo.GameName`、`MapInfo.Title`、`ItemInfo.FriendlyName` 是 Crystal 官方原生就已经为你预留好的“显示名”字段，根本不需要去改那几百个 `.txt` 爆率文件里的英文名！

---

## 四、 第三阶段：自动化注入中文显示名脚本（一键汉化 `MirDB`）

为了省去你在 `Server.exe` 后台手动改几千个怪物和装备的 `GameName / FriendlyName / Title`，我们可以直接在 `Server` 启动加载 `Server.MirDB` 的代码处（`Server/MirEnvir/Envir.cs` -> `LoadDB()` 末尾），挂一个 **“首次启动自动挂载中文词典 (`zh_CN_Dict.csv`)”** 的钩子函数！

### Step 1：在 `Envir.cs` 的 `LoadDB()` 末尾加入 20 行自动汉化器
```csharp
// 文件：Server/MirEnvir/Envir.cs -> LoadDB() 方法加载完数据后调用：
public void ApplyChineseDisplayNames()
{
    string dictPath = Path.Combine(Settings.EnvirPath, "ChineseNames.txt");
    if (!File.Exists(dictPath)) return;

    // 读取格式为：英文原名=中文名 的字典文件
    var dict = File.ReadAllLines(dictPath, Encoding.UTF8)
                   .Where(l => l.Contains('=') && !l.StartsWith(";"))
                   .Select(l => l.Split('=', 2))
                   .ToDictionary(k => k[0].Trim(), v => v[1].Trim(), StringComparer.OrdinalIgnoreCase);

    // 1. 自动汉化所有怪物的游戏内显示名 (GameName)，保留英文 Name 供 Drops 使用！
    foreach (var mob in MonsterInfoList)
        if (dict.TryGetValue(mob.Name, out string cnName)) mob.GameName = cnName;

    // 2. 自动汉化所有地图的游戏内标题 (Title)
    foreach (var map in MapInfoList)
        if (dict.TryGetValue(map.Title, out string cnTitle)) map.Title = cnTitle;

    // 3. 自动汉化所有物品的 FriendlyName (配合客户端优先渲染 FriendlyName)
    foreach (var item in ItemInfoList)
        if (dict.TryGetValue(item.Name, out string cnItem)) item.FriendlyName = cnItem;
}
```
* **神级优势**：你甚至不需要修改 `Server.MirDB` 二进制文件本身！只需要在 `Envir/` 目录下放一个纯文本的 `ChineseNames.txt`（里面写着 `WhiteEvilBoar=白野猪`、`BichonProvince=比奇省`、`JudgementMace=裁决之杖`），服务端每次启动内存加载时**自动1秒完成全量游戏内汉化**，随时想改哪个译名直接记事本打开改完重启即生效！

---

## 五、 第四阶段：NPC 对话与任务文本汉化 (`Envir/NPCs/`)

游戏内点击 NPC 弹出的窗口内容存放在 `Envir/NPCs/*.txt` 中。
* **规则**：保留所有 `[@Main]` 标签、`#IF`、`#ACT` 以及里面的英文物品名参数（因为底层物品内部名还是英文），**只翻译 `#SAY` 下方的自然语言对话和 `<按钮文字/@跳转标签>` 斜杠左边的文字**！

#### 示例（盟重药店老板脚本汉化前后对比）：
```text
; 汉化前：
[@Main]
#SAY
Welcome to Mongchon Potion Shop. How can I help you?\
<Buy Potions/@Buy>\
<Sell Potions/@Sell>\

; 汉化后（注意 @Buy 和内部物品代码保持英文，仅汉化玩家看到的字）：
[@Main]
#SAY
欢迎来到盟重药店，请问有什么可以帮您？\
<购买药水/@Buy>\
<出售药水/@Sell>\

[Types]
1

[Trade]
(HP)DrugSmall
(MP)DrugSmall
```
* **批量处理技巧**：`Envir/NPCs/` 下通常只有几十个常用 NPC 文本，写一个小型 Python 脚本读取每个 `.txt`，只提取 `#SAY` 区块内的文本以及 `<EnglishText/@Tag>` 左半部分交由大模型 API 翻译并保存为 **UTF-8 with BOM** 编码，5 分钟即可全部搞定。

---

## 六、 落地执行 Checklist（按顺序打勾即可）

- [ ] **Step 1（改字体）**：将 `Client/Settings.cs` 中的 `FontName` 改为 `"Microsoft YaHei"`。
- [ ] **Step 2（改物品显示优先级）**：在客户端绘制物品名与 Tooltip 处，将 `Info.Name` 改为优先读取 `Info.FriendlyName`。
- [ ] **Step 3（挂字典加载器）**：在 `Server/MirEnvir/Envir.cs` 的 `LoadDB()` 末尾粘贴上面的 `ApplyChineseDisplayNames()` 方法，并放入 `Envir/ChineseNames.txt` 中英对照词库。
- [ ] **Step 4（汉化系统提示）**：将 `Shared/GameLanguage.cs` 中的客户端 UI 与聊天框提示词替换为中文。
- [ ] **Step 5（汉化核心 NPC）**：将比奇、盟重、白日门等主城常用传送员、药店、铁匠的 `#SAY` 文本替换为中文（UTF-8 编码）。
- [ ] **Step 6（编译启动）**：按 F5 编译，服务端后台依然是清爽的原版英文，进游戏后看到的画面、地图、怪物、掉落、UI 100% 是纯正中文，挥刀砍怪依然是经典原版配音！