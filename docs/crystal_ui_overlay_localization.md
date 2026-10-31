> **2026-10-31 实施记录**：本方案已按当前 Crystal 代码适配落地。集中调用位置采用 `MirImageControl.DrawControl()`，覆盖延迟创建与换图控件；并非照抄下文不存在的字段或三处场景调用示例。角色与英雄标题区域留给人物名、公会名，改为覆盖原有页签和属性标题。坐标尺改为默认关闭的 INI 调试选项。实际范围、备份、下载包和验证限制以 [README](README.md) 最新章节及 [验证记录](ui-overlay-validation.json) 为准；下文为原始设计参考。

# Crystal (CrystalM2) 免改 .lib 贴图・C# 渲染层“黑金铭牌遮罩”汉化方案

> **工程原理**：  
> 不解包、不修改任何 `.lib` 二进制图片文件。利用 Crystal 客户端 `MirControl` 原生支持的 `DrawControlTexture`（纯色底板渲染）、`Border`（1像素金属描边）以及 `NotControl = true`（鼠标事件100%穿透），在带英文的贴图区域上方动态挂载一层**“暗金边框+深褐底色+微软雅黑淡金字”的仿原生铭牌**。  
> 
> **核心优势**：  
> 1. **零侵入集中管理**：仅需新增 1 个 `ChineseUIOverlay.cs` 文件 + 3 行场景调用代码，不污染原有 20 多个 Dialog 类。  
> 2. **保留原版交互**：由于开启了鼠标穿透（`NotControl = true`），窗口顶部标题栏依然可以正常按住拖拽移动，图形按钮依然保留原版的悬停高亮、按下位移与点击音效。

---

## 一、 实施步骤总览 (3 步曲，耗时约 15 分钟)

| 步骤 | 动作 | 修改文件 | 说明 |
| :--- | :--- | :--- | :--- |
| **Step 1** | 新建遮罩引擎类 | `Client/MirControls/ChineseUIOverlay.cs` | 封装窗口标题铭牌、按钮内部贴片、任意区域遮罩三个核心方法与全窗口映射表 |
| **Step 2** | 注入三大场景钩子 | `LoginScene.cs` / `SelectScene.cs` / `GameScene.cs` | 在三个场景的构造函数末尾各加 1 行代码，一键激活所有贴图遮罩 |
| **Step 3** | 坐标微调（可选） | 按住 `Alt` 键开启内置坐标尺 | 针对个别非居中标题，2秒读取坐标并在 `ChineseUIOverlay.cs` 中微调偏移量 |

---

## 二、 Step 1：新建核心遮罩引擎 (`Client/MirControls/ChineseUIOverlay.cs`)

在 Visual Studio 的 `Client` 项目中，右键点击 **`MirControls`** 文件夹 -> **添加 -> 新建项 -> 类**，命名为 **`ChineseUIOverlay.cs`**，将以下完整代码粘贴进去：

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using Client.MirScenes;
using Client.MirScenes.Dialogs;

namespace Client.MirControls
{
    /// <summary>
    /// 免修改 .lib 贴图的 C# 渲染层中文铭牌遮罩引擎
    /// </summary>
    public static class ChineseUIOverlay
    {
        // 贴合 Euro Mir 暗黑金属 UI 风格的标准配色
        private static readonly Color PlaqueBackColor = Color.FromArgb(248, 22, 19, 15);    // 深黑褐底板（彻底遮住底下英文像素）
        private static readonly Color PlaqueBorderColor = Color.FromArgb(200, 92, 78, 56);  // 暗金铜描边（融入原版金属框）
        private static readonly Color TitleTextColor = Color.FromArgb(255, 235, 210, 155);  // 标题淡金字
        private static readonly Color ButtonTextColor = Color.FromArgb(255, 225, 205, 170); // 按钮常规字色
        private static readonly Color ButtonHoverColor = Color.FromArgb(255, 255, 240, 180);// 按钮悬停高亮字色

        /// <summary>
        /// 1. 遮罩窗口顶部的英文图片标题（生成一块居中的暗金铭牌，且不影响鼠标拖拽窗口）
        /// </summary>
        /// <param name="dialog">目标窗口对象</param>
        /// <param name="cnTitle">中文标题（建议2~4字，两字中间可加空格如 "背 包"）</param>
        /// <param name="width">铭牌宽度（默认 92px，长标题可设为 120px）</param>
        /// <param name="height">铭牌高度（默认 18px）</param>
        /// <param name="offsetX">水平居中基础上的微调偏移（正数向右，负数向左）</param>
        /// <param name="offsetY">距离窗口顶部的 Y 坐标（Crystal 标准窗口通常在 6 ~ 10 之间）</param>
        public static MirControl MaskTitle(MirControl dialog, string cnTitle, int width = 92, int height = 18, int offsetX = 0, int offsetY = 7)
        {
            if (dialog == null) return null;

            var plaque = new MirControl
            {
                Parent = dialog,
                Size = new Size(width, height),
                Location = new Point((dialog.Size.Width - width) / 2 + offsetX, offsetY),
                BackColour = PlaqueBackColor,
                DrawControlTexture = true,
                Border = true,
                BorderColour = PlaqueBorderColor,
                NotControl = true // 关键：鼠标穿透，保证玩家依然可以按住标题栏拖动窗口！
            };

            new MirLabel
            {
                Parent = plaque,
                AutoSize = false,
                Size = plaque.Size,
                Location = new Point(0, -1),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                ForeColour = TitleTextColor,
                Font = new Font(Settings.FontName, 9.5F, FontStyle.Bold),
                Text = cnTitle,
                NotControl = true
            };

            return plaque;
        }

        /// <summary>
        /// 2. 遮罩带英文贴图的图形按钮（保留原按钮四周金属立体边框与点击下陷动画）
        /// </summary>
        /// <param name="btn">目标 MirButton</param>
        /// <param name="cnText">中文按钮文字</param>
        /// <param name="padX">左右保留的原版边框像素宽度（默认 5px）</param>
        /// <param name="padY">上下保留的原版边框像素高度（默认 4px）</param>
        public static void MaskButton(MirButton btn, string cnText, int padX = 5, int padY = 4)
        {
            if (btn == null || btn.Size.Width <= padX * 2 || btn.Size.Height <= padY * 2) return;

            var mask = new MirControl
            {
                Parent = btn,
                Location = new Point(padX, padY),
                Size = new Size(btn.Size.Width - padX * 2, btn.Size.Height - padY * 2),
                BackColour = Color.FromArgb(242, 30, 25, 20),
                DrawControlTexture = true,
                NotControl = true // 关键：穿透点击事件给底下的 MirButton
            };

            var label = new MirLabel
            {
                Parent = mask,
                AutoSize = false,
                Size = mask.Size,
                Location = new Point(0, -1),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                ForeColour = ButtonTextColor,
                Font = new Font(Settings.FontName, 8.5F, FontStyle.Bold),
                Text = cnText,
                NotControl = true
            };

            // 联动按钮悬停与按下状态，提供媲美原生的视觉反馈
            btn.MouseEnter += (s, e) => label.ForeColour = ButtonHoverColor;
            btn.MouseLeave += (s, e) => { label.ForeColour = ButtonTextColor; label.Location = new Point(0, -1); };
            btn.MouseDown += (s, e) => label.Location = new Point(1, 0); // 按下时文字同步微移 1px
            btn.MouseUp += (s, e) => label.Location = new Point(0, -1);
        }

        /// <summary>
        /// 3. 遮罩背景图上任意固定坐标的英文标签（如面板内部的子栏目艺术字）
        /// </summary>
        public static void MaskRect(MirControl parent, string cnText, Rectangle rect, float fontSize = 8.5F, bool showBorder = false)
        {
            if (parent == null) return;

            var mask = new MirControl
            {
                Parent = parent,
                Location = rect.Location,
                Size = rect.Size,
                BackColour = PlaqueBackColor,
                DrawControlTexture = true,
                Border = showBorder,
                BorderColour = PlaqueBorderColor,
                NotControl = true
            };

            new MirLabel
            {
                Parent = mask,
                AutoSize = false,
                Size = mask.Size,
                Location = new Point(0, -1),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                ForeColour = TitleTextColor,
                Font = new Font(Settings.FontName, fontSize, FontStyle.Regular),
                Text = cnText,
                NotControl = true
            };
        }

        // =========================================================================
        // 三大场景一键批量挂载入口（所有坐标微调均在此处集中管理）
        // =========================================================================

        /// <summary>
        /// 挂载【主游戏场景 GameScene】所有弹窗标题与核心图形按钮
        /// </summary>
        public static void ApplyToGameScene(GameScene scene)
        {
            if (scene == null) return;

            // --- A. 核心高频主窗口顶部标题遮罩 ---
            MaskTitle(scene.InventoryDialog, "背  包", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.CharacterDialog, "角色信息", width: 96, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.SkillDialog, "技能列表", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.QuestDialog, "任务日志", width: 92, height: 18, offsetX: 0, offsetY: 7);
            
            // --- B. 原生 Hero (英雄副将) 与坐骑/觉醒窗口 ---
            // (注：若你的分支字段名略有不同，根据 VS 智能提示对应修改即可)
            MaskTitle(scene.HeroCharacterDialog, "英雄状态", width: 96, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.HeroInventoryDialog, "英雄背包", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.MountDialog, "坐  骑", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.AwakeningDialog, "装备觉醒", width: 96, height: 18, offsetX: 0, offsetY: 7);

            // --- C. 商店、仓库、寄售行、社交与系统菜单窗口 ---
            MaskTitle(scene.NPCGoodsDialog, "商  店", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.StorageDialog, "仓  库", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.TrustMerchantDialog, "寄售商行", width: 100, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.GuildDialog, "行  会", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.FriendDialog, "好  友", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.MentorDialog, "师徒系统", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.RelationshipDialog, "伴侣系统", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.MailListDialog, "信  箱", width: 88, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.RankingDialog, "排行榜", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.FishingDialog, "垂  钓", width: 80, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.HelpDialog, "游戏帮助", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.OptionDialog, "系统设置", width: 92, height: 18, offsetX: 0, offsetY: 7);
            MaskTitle(scene.MenuDialog, "主菜单", width: 84, height: 18, offsetX: 0, offsetY: 7);

            // --- D. 角色面板 (CharacterDialog) 顶部的 4 个图形切换页签按钮 ---
            if (scene.CharacterDialog != null)
            {
                MaskButton(scene.CharacterDialog.CharacterTabButton, "装备", padX: 3, padY: 3);
                MaskButton(scene.CharacterDialog.StatusTabButton, "属性", padX: 3, padY: 3);
                MaskButton(scene.CharacterDialog.StateTabButton, "状态", padX: 3, padY: 3);
                MaskButton(scene.CharacterDialog.SkillTabButton, "技能", padX: 3, padY: 3);
            }

            // --- E. 系统菜单 (MenuDialog) 内的纯图片按钮 ---
            if (scene.MenuDialog != null)
            {
                MaskButton(scene.MenuDialog.ExitButton, "退出游戏");
                MaskButton(scene.MenuDialog.LogOutButton, "返回选人");
                MaskButton(scene.MenuDialog.OptionButton, "游戏设置");
                MaskButton(scene.MenuDialog.HelpButton, "帮助说明");
            }
        }
    }
}
```

> 💡 **智能提示适配说明**：不同时期的 `Crystal` 分支中，`GameScene` 里的个别窗口属性名可能略有差异（例如有的分支把 `OptionDialog` 写在 `SettingsDialog`，或把 `HeroCharacterDialog` 集成在 `CharacterDialog` 内）。粘贴后如果 VS 提示某一行属性不存在，先将该行注释掉或按 `.` 查看它的实际变量名即可。

---

## 三、 Step 2：在三大场景中注入 1 行调用代码

### 1. 主游戏场景 (`Client/MirScenes/GameScene.cs`)
打开 `Client/MirScenes/GameScene.cs`，找到构造函数 **`public GameScene()`** 的**最末尾（所有 Dialog 均已 `new` 出来之后）**，加上 1 行代码：

```csharp
public GameScene()
{
    // ... 原有的上百行 Dialog 初始化代码 ...

    // ===== 新增：一键挂载所有窗口与按钮的中文铭牌遮罩 =====
    ChineseUIOverlay.ApplyToGameScene(this);
}
```

### 2. 选人界面 (`Client/MirScenes/SelectScene.cs`)
选人界面的四个大按钮（`Start`、`New Character`、`Delete`、`Exit`）也是 `.lib` 图片字。打开 `SelectScene.cs`，在构造函数末尾加上：

```csharp
// 在 SelectScene 构造函数末尾添加：
ChineseUIOverlay.MaskButton(StartGameButton, "开始游戏", padX: 8, padY: 5);
ChineseUIOverlay.MaskButton(NewCharacterButton, "创建角色", padX: 8, padY: 5);
ChineseUIOverlay.MaskButton(DeleteCharacterButton, "删除角色", padX: 8, padY: 5);
ChineseUIOverlay.MaskButton(ExitGameButton, "退出游戏", padX: 8, padY: 5);
```

### 3. 登录界面 (`Client/MirScenes/LoginScene.cs`)
如果登录框、注册框上的按钮也是英文图片，同样在 `LoginScene.cs` 内部对应的 `LoginDialog` 构造函数末尾调用 `ChineseUIOverlay.MaskButton(...)` 即可。

---

## 四、 Step 3：内置“指哪打哪”2秒坐标校准器（解决个别贴图不居中问题）

由于 `.lib` 里有些老窗口的英文标题并不是严格水平居中的（比如有的偏左 20 像素，有的高度在 `Y = 12`），或者是窗口背景图中间有一块写着英文的底纹（比如背包左下角的 `Gold` 字样、商店里的 `Buy / Sell` 标语），怎么快速知道它的精确坐标 `(X, Y, Width, Height)`？

不要去打开 `LibraryEditor` 算像素！直接在 `ChineseUIOverlay.cs` 的 `MaskTitle` 或任意你想测量的 `Dialog` 上加下面这段 **6 行代码的“Alt 键坐标探测仪”**：

```csharp
// 在 ChineseUIOverlay.cs 中新增此调试工具方法：
public static void EnableCoordInspector(MirControl dialog, string dialogName)
{
    if (dialog == null) return;
    dialog.MouseMove += (s, e) =>
    {
        // 当你在游戏里按住键盘 Alt 键并在窗口上移动鼠标时，实时计算鼠标相对于该窗口左上角的精确坐标！
        if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
        {
            Point relPos = new Point(CMain.MPoint.X - dialog.DisplayRectangle.X, 
                                     CMain.MPoint.Y - dialog.DisplayRectangle.Y);
            GameScene.Scene?.ChatDialog?.ReceiveChat(
                $"[坐标尺-{dialogName}] 相对X={relPos.X}, 相对Y={relPos.Y} (窗口总宽={dialog.Size.Width})", 
                ChatType.Hint);
        }
    };
}
```

### 🎯 怎么用这个坐标尺 10 秒消除任何残留的 `.lib` 英文？
1. 在 `ApplyToGameScene` 里把你想测量的窗口加上 `EnableCoordInspector(scene.InventoryDialog, "背包");`。
2. 进游戏打开背包，看到比如左下角金币框旁边有个图片写的英文 `Gold` 没盖住：
   * 按住键盘 **`Alt` 键**，把鼠标移到 `Gold` 的**左上角**，看一眼聊天框提示：`相对X=18, 相对Y=265`；
   * 再把鼠标移到 `Gold` 的**右下角**，看一眼提示：`相对X=52, 相对Y=281`（算出宽 `34`，高 `16`）。
3. 回到 `ChineseUIOverlay.cs` 的 `ApplyToGameScene` 里加一行：
   ```csharp
   ChineseUIOverlay.MaskRect(scene.InventoryDialog, "金币", new Rectangle(18, 265, 34, 16));
   ```
4. 按 F5 重新启动，那个位置的英文图片就被严丝合缝地替换成中文“金币”了！

---

## 五、 常见残留 `.lib` 英文区域速查与遮罩清单

除了上面已经自动覆盖的 20 个窗口主标题外，你在游戏里还会遇到以下 4 处高频的局部图片英文，可以直接用 `MaskRect` 或 `MaskButton` 顺手干掉：

| 所在界面 | 残留的图片英文内容 | 遮罩方式 (`ChineseUIOverlay` 调用方法) |
| :--- | :--- | :--- |
| **底部主状态栏 (`MainDialog`)** | 等级 `Lv`、经验 `EXP`、重量 `Weight` 小贴图字 | 若觉得刺眼，调用 `MaskRect(scene.MainDialog, "经验", new Rectangle(...))` 覆盖 |
| **背包窗口 (`InventoryDialog`)** | 左下角 `Gold`（金币）艺术字与右上角关闭 `X` 旁边的装饰字 | 调用 `MaskRect(scene.InventoryDialog, "金 币", new Rectangle(...))` |
| **创建角色界面 (`SelectScene`)** | 五职业选择按钮旁的英文职业艺术字 (`Warrior`, `Wizard`...) | 在 `NewCharacterDialog` 中对 5 个职业图标旁边的英文说明区调用 `MaskRect` |
| **死亡复活弹窗 (`ReviveDialog`)** | 弹窗标题与 `Revive in Town`（回城复活）按钮 | 对复活按钮直接调用 `MaskButton(ReviveButton, "回城复活")` |

---

## 六、 落地执行 Checklist

- [ ] **1. 创建文件**：在 `Client/MirControls/` 下新建 `ChineseUIOverlay.cs` 并粘贴上述代码。
- [ ] **2. 处理编译报错字段**：如果 VS 提示 `scene.某某Dialog` 不存在（分支命名差异），在 `GameScene.cs` 中搜一下该窗口的实际变量名并对齐。
- [ ] **3. 挂载钩子**：在 `GameScene()` 构造函数最末尾添加 `ChineseUIOverlay.ApplyToGameScene(this);`，在 `SelectScene()` 末尾挂载 4 个主按钮。
- [ ] **4. 启动验收**：按 F5 启动客户端，打开背包（F9）、角色（F10）、技能（F11），观察顶部暗金中文铭牌是否完美盖住底下英文；若有个别位置偏移，按住 `Alt` 键读取坐标微调 `offsetX / offsetY` 即可收工！