# Crystal 引擎向盛大 1.76 操作手感靠拢：全方位对比与源码改造计划

Crystal（基于 C# 开发的开源传奇引擎）默认采用的是韩服/欧服早期的硬核设定，加上外服开发者加入的大量 RPG 限制机制，导致习惯了国内盛大 1.76（以及后续 GOM/GEE/BLUE 引擎）的玩家在上手时会感到极度“卡手、拖沓、不跟手”。

本文档从底层逻辑对比两者的核心差异，并提供精确到代码文件与配置项的改造施工计划。

---

## 第一部分：Crystal 与盛大 1.76 核心操作差异深度对比

| 操作维度 | 盛大 1.76（及国内主流内挂端） | Crystal 引擎默认表现 | 导致“别扭”的底层代码原因 |
| :--- | :--- | :--- | :--- |
| **1. 起跑机制 (Run)** | **右键即跑**：只要按住右键，第一步直接跨两格起跑，指哪跑哪，零前摇。 | **助跑机制**：停下后按右键，必须先“走”一步（1格），第2步才能跑起来。 | 客户端 `MapControl.cs` 中存在 `CanRun` 布尔锁，每次停止或动作后重置为 `false`，需执行一次 `Walk` 才激活为 `true`。 |
| **2. 近距离右键判定** | **无视距离尝试跑**：哪怕鼠标就在人物脚边 1 格，按右键也是往那个方向跑两格。 | **近距离强行变走**：鼠标距离人物中心只有 1 格时，按住右键也会被强制判定为“走一步”。 | `CanMove` / 鼠标逻辑中判断了 `Functions.MaxDistance(User.CurrentLocation, MapLocation) == 1` 时强制调用 `Walk`。 |
| **3. 障碍物滑步 (绕障)** | **丝滑滑步**：跑步时正前方有树或怪，系统会自动向左右偏转 45 度顺滑绕过去继续跑。 | **撞墙死卡**：正前方两格内只要有障碍物，角色直接原地罚站，甚至不会自动退化为走一步。 | 跑动方向检测只检查了正方向（或极窄角度），缺乏“跑不通转侧向跑 -> 侧向跑不通转走步”的多级回退算法。 |
| **4. 战士攻击与追击** | **免 Shift / 锁定顺滑**：点中怪后自动追砍；开了免 Shift 或隔位刺杀后，走位砍怪一气呵成。 | **丢失目标 / 挥空**：怪移动半步鼠标没跟上就原地发呆；按住 Shift 点歪一点就对着空气挥刀。 | 缺少持续性的 `TargetObject` 粘滞追击逻辑，且近战缺少自动寻位（如自动找刺杀位）辅助。 |
| **5. 法道施法体验** | **自动朝向 + 智能锁定**：鼠标指怪按 F1-F8，人物自动转向并锁定释放；移开鼠标也有短暂锁定记忆。 | **施法后摇重、判定严**：动作锁（ActionLock）时间长，跑动中甩雷电术/贴符极易因为动作未完成而吞技能。 | 客户端施法指令队列（`NextAction`）缓存时间短，且服务端对动作帧间隔校验过于严苛。 |
| **6. 物品拾取与显示** | **踩上秒捡 / 内挂显名**：路过即自动进包；地面物品名字常驻显示，极品高亮。 | **罚站按空格**：必须精准走到物品格子上按 `Space` 或点小字；默认不按 Alt 不显示地名。 | 缺少移动完成后的自动 `PickUp` 触发器；地面 UI 渲染默认关闭了常驻文本绘制。 |
| **7. 快捷键与UI交互** | **F9~F12 经典肌肉记忆**：F9包裹、F10装备、F11技能、F12内挂；1-6 秒吃药无冷却感。 | **美式 MMO 键位**：I/B包裹、C属性、S技能；F12 没内挂；吃药有明显延迟感。 | 默认键位表（`Keybind`）按欧美 RPG 设定；普通药水与太阳水共用或受限于缓慢的物品使用封包。 |

---

## 第二部分：向盛大 1.76 靠拢的分阶段改造计划

本计划分为三个阶段：**阶段一（解决最难受的移动与起跑）**、**阶段二（战斗手感与免Shift丝滑度）**、**阶段三（内挂级便利性与键位还原）**。

---

### 阶段一：移动与起跑丝滑化改造（最高优先级）

**目标**：彻底干掉“走两步才跑”、“脚下右键不跑”、“撞怪卡死”三大毒瘤，还原 1.76 指哪跑哪的爽快感。

#### 任务 1.1：彻底移除“助跑”限制（右键直接跨两格起跑）
* **涉及文件**：`Client/MirScenes/Dialogs/MapControl.cs`
* **改造步骤**：
  1. 在 `MapControl.cs` 中搜索 `CanRun`。
  2. 找到控制移动的主方法（通常在 `Process()` 或 `CheckInput()` 中处理鼠标按住移动的部分）。
  3. **修改方案**：
     * 将 `CanRun` 属性修改为常驻返回 `true`，或者把所有 `CanRun = false;` 的赋值语句注释掉。
     * 定位到右键跑步判断处（类似 `if (CanRun && User.CanRun(direction))`），去掉前置的 `CanRun` 限制，只要人物状态允许（没中麻痹、负重够），按右键第一下直接触发 `User.CanRun(direction)`。

#### 任务 1.2：解除“鼠标靠近人物 1 格内右键强制变走”的限制
* **涉及文件**：`Client/MirScenes/Dialogs/MapControl.cs`
* **改造步骤**：
  1. 在处理鼠标右键（`MouseButtons.Right`）的代码块中，查找距离判断代码（通常包含 `Functions.MaxDistance` 或比较 `User.CurrentLocation` 与 `MapLocation` 的距离）。
  2. **修改方案**：
     * 当检测到玩家按下的是**鼠标右键**时，无视鼠标与角色的格子距离（哪怕距离只有 1 格），一律只提取方向 `direction = Functions.DirectionFromPoint(User.CurrentLocation, MapLocation)`，并直接尝试向该方向跑两格（`Run`）。

#### 任务 1.3：加入“跑不通自动降级为走 + 侧向滑步”算法
* **涉及文件**：`Client/MirScenes/Dialogs/MapControl.cs`
* **改造步骤**：
  1. 原版右键逻辑如果前方第 2 格被挡住（但第 1 格是空的），有时会直接卡住不动。
  2. **修改方案**：重构右键移动的回退链条（Fallback Chain）：
     * **第 1 优先级**：尝试正方向跑 2 格 `User.CanRun(dir)`。
     * **第 2 优先级（侧向滑步）**：若正方向跑不通，尝试顺时针/逆时针偏转 1 个方向（`MirDirection` +1 或 -1，取决于鼠标偏左还是偏右）跑 2 格。
     * **第 3 优先级（降级走步）**：若跑 2 格全部被挡（比如贴墙或隔着 1 格有怪），自动降级调用 `User.CanWalk(dir)` 走 1 格，绝不让角色原地罚站。

---

### 阶段二：战斗手感、锁定与职业微调（还原 1.76 PK 与打怪节奏）

**目标**：解决战士挥空、按 Shift 手酸、法道甩技能卡顿的问题。

#### 任务 2.1：实现国服“免 Shift”开关与持续追砍
* **涉及文件**：`Client/MirScenes/Dialogs/MapControl.cs`、`Client/MirScenes/GameScene.cs`、`Client/Settings.cs`
* **改造步骤**：
  1. 在 `Settings.cs` 中新增配置项：`public static bool SmartShift = true;`（默认开启免 Shift）。
  2. 在 `MapControl.cs` 的左键点击/按住逻辑中：
     * 当鼠标左键点击怪物（`TargetObject != null`）时，如果开启了 `SmartShift`，无需检测键盘 `Shift` 键是否按下，直接进入持续锁定攻击状态。
     * 当怪物移动拉开 1 格距离时，若为战士职业且学了《刺杀剑术》，可根据开关决定是“直接隔位刺杀”还是“自动走一步贴脸平砍”。

#### 任务 2.2：优化技能动作队列（解决跑动中放不出技能/吃药卡顿）
* **涉及文件**：`Client/MirObjects/UserObject.cs`、`Server/MirObjects/PlayerObject.cs`
* **改造步骤**：
  1. **动作锁微调**：检查 `UserObject.cs` 中的 `SetAction` 与 `NextAction` 逻辑。原版在跑步未完全结束的最后几十毫秒内按技能键，有时会被直接丢弃。放宽 `NextAction` 的缓存窗口期，让玩家在跑步动画后半段按下的雷电术/烈火能无缝衔接在落地瞬间释放。
  2. **独立吃药通道**：确保喝药指令（`UseItem`）不受攻击/跑步动画硬直的完全阻塞，还原 1.76 边跑边丝滑喝强效太阳水的手感。

---

### 阶段三：内挂级便利功能与经典键位重构

**目标**：不用每次低头按空格捡东西，不用重新适应反人类的快捷键。

#### 任务 3.1：实现“踩上自动捡物（Auto PickUp）”
* **涉及文件**：`Client/MirObjects/UserObject.cs` 或 `GameScene.cs`
* **改造步骤**：
  1. 在角色每次完成移动（`Walk` 或 `Run` 动作结束，到达新坐标 `CurrentLocation`）的瞬间，或者在 `GameScene.Process()` 中增加一个每 200ms 触发一次的检测：
  2. 遍历当前视野内地面物品列表（`MapControl.Objects` 中类型为 `ItemObject` 的对象）。
  3. 如果发现 `item.CurrentLocation == User.CurrentLocation`（脚下有东西）且背包未满，客户端自动发送 `new C.PickUp()` 封包给服务端。

#### 任务 3.2：地面物品名、怪物血条常驻显示
* **涉及文件**：`Client/Settings.cs`、`Client/MirScenes/Dialogs/MapControl.cs`
* **改造步骤**：
  1. 将 `Settings.cs` 中的显名、显血开关默认值全部改为 `true`。
  2. 修改 `MapControl.cs` 中的 `DrawItemNames` 逻辑，无需按住 `Alt` 键即可常驻绘制地面物品名称，并为极品/高级装备加上颜色区分。

#### 任务 3.3：盛大 1.76 经典键位默认化
* **涉及文件**：`Client/KeyBind.cs`
* **改造步骤**：
  1. 打开 `KeyBind.cs` 中的默认键位初始化方法（`LoadDefaultKeybinds` 等）。
  2. 修改以下核心默认映射：
     * **打开背包 (Inventory)**：改为 `Keys.F9`（保留 `B`/`I` 作为备选）。
     * **人物装备/状态 (Equipment/Status)**：改为 `Keys.F10`。
     * **技能面板 (Skills)**：改为 `Keys.F11`。
     * **辅助/内挂设置 (Options)**：改为 `Keys.F12`（将原本绑定在 `O` 键的设置面板绑到 `F12`，并在里面集中放置“免Shift”、“自动捡物”、“显血显名”等内挂勾选框）。
  3. 删除客户端目录下的旧 `Keybinds.ini` 配置文件，重新运行使新默认键位生效。

---

## 第三部分：服务端参数协同调优清单 (Server Side)

仅改客户端还不够，Crystal 服务端默认带有很多生存类限制，需要一并关闭才能彻底找回 1.76 的爽快感：

- [x] **取消饥饿度/体力限制**：在客户端 `Client/MirScenes/GameScene.cs`（`MapControl.CheckInput`）中移除 `User.HP >= 10` 的低血量禁跑限制；服务端 `Server/MirObjects/HumanObject.cs` 无额外饥饿度减速。
- [x] **放宽反外挂起跑预热校验（防拉回）**：在服务端 `Server/MirObjects/HumanObject.cs` 的 `CanRun` 属性中移除 `(_stepCounter > 0 || FastRun)` 限制，使角色在静止起跑第一帧（`_stepCounter == 0`）直接发送 `C.Run` 时服务端正常放行，不再触发 `S.UserLocation` 拉回（Rollback）。
- [x] **调整怪物尸体碰撞清理**：已核验服务端 `Server/MirObjects/MonsterObject.cs` 的 `Blocking => !Dead`，怪物死亡瞬间立即释放格子阻挡（`Blocking = false`）。

---

## 第四部分：改造验收测试表

完成上述代码修改后，进游戏按以下 4 条标准验收手感：

1. **安全区蝴蝶步测试**：在比奇安全区围绕一个中心点，用鼠标右键高频画圈跑动。合格标准：**每一步都是直接跨 2 格跑，全程不出现走 1 格的动作，且不被周围站立的 NPC 卡停。**
2. **森林绕树测试**：在比奇森林对着树林按住右键不松手。合格标准：**角色遇到大树自动向两侧滑步绕开继续跑，不会顶着树原地踏步。**
3. **战士刺杀/追砍测试**：开启免 Shift，鼠标左键点中一只鹿不松手。合格标准：**角色自动追上去连续挥刀，无需按住键盘 Shift。**
4. **打怪爆物测试**：打死怪物爆出一地金币和药水，直接用右键从物品堆上跑过去。合格标准：**跑过瞬间脚下物品自动进包，无需停下来按空格。**

---

## 第五部分：源码落地与验证记录（2026-10-31）

| 阶段 / 任务 | 实际修改文件 | 落地实现摘要 |
| --- | --- | --- |
| **Phase 1.1：无助跑瞬时起跑** | `Client/MirScenes/GameScene.cs`、`Client/MirObjects/PlayerObject.cs`、`Server/MirObjects/HumanObject.cs` | 客户端 `GameScene.CanRun` 默认常驻 `true`，移除站立/转向/受击/低血量（`User.HP >= 10`）将 `CanRun` 置 `false` 的预热锁；服务端 `HumanObject.CanRun` 同步移除 `(_stepCounter > 0 \|\| FastRun)` 校验，消除起跑首帧拉回。 |
| **Phase 1.2：近距离右键起跑** | `Client/MirScenes/GameScene.cs` (`MapControl.CheckInput`) | 将右键距离判定从 `Functions.InRange(MapLocation, User.CurrentLocation, 2)` 放宽为 `MapLocation == User.CurrentLocation`；鼠标在角色身旁 1~2 格按住右键即可朝鼠标方向连续跑步。 |
| **Phase 1.3：跑步遇阻滑步与降级** | `Client/MirScenes/GameScene.cs` (`MapControl.CanRun(dir, out outDir)`) | 新增 `CanRun(MirDirection dir, out MirDirection outDir)` 重载，按“正前方 2 格跑 -> 左右 ±45° 扇区滑步 2 格跑 -> 自动降级 `CanWalk(direction, out direction)` 1 格走步”三级回退，解决树林、墙角、NPC 旁跑步卡死。 |
| **Phase 2.1：免 Shift 攻击与智能追击** | `Client/Settings.cs`、`Client/MirScenes/GameScene.cs`、`Client/MirScenes/Dialogs/MainDialogs.cs` | 新增 `Settings.SmartShift = true`（持久化至 `Mir2Config.ini` 并在 `F12` 选项面板提供“免Shift攻击”勾选框）；支持开启刺杀剑术（`User.Thrusting`）时 2 格直线刺杀位免 Shift 自动出刀，以及目标距离 > 2 格时自动跑步追击、距离 ≤ 2 格时自动走步贴身。 |
| **Phase 2.2：预输入窗口与独立吃药** | `Client/MirObjects/UserObject.cs`、`Client/MirControls/MirItemCell.cs` | 在 `UserObject.ProcessFrames()` 中保留动作末尾 2 帧（约最后 30% 进度）的 `QueuedAction` 预输入缓存，消除跑动/挥刀中转向与技能吞指令；将药水（`ItemType.Potion`）的 `UseItemTime` 冷却缩短为 `200ms`，与移动/攻击动作锁完全解耦。 |
| **Phase 3.1~3.3：踩点自动捡物与 1.76 内挂** | `Client/Settings.cs`、`Client/MirScenes/GameScene.cs`、`Client/MirScenes/Dialogs/MainDialogs.cs` | 新增 `Settings.AutoPickUp = true`（`F12` 面板可开关），角色所在格存在地面物品或金币且背包有空位时按 `250ms` 节流自动发送 `C.PickUp`；常驻地面物品名（极品染色 `Cyan`）、显血显名与 `F9~F12` 经典键位默认生效，并已同步部署至 `D:\private-server\mir2\Server` 与 `D:\private-server\mir2-client`。 |

### 构建与回归测试结果

- `dotnet build Server.MirForms/Server.csproj -c Release`：**0 Error(s)**
- `dotnet build Client/Client.csproj -c Release`：**0 Error(s)**
- `dotnet run --project scripts/performance-tests/PerformanceTests.csproj -c Release`：**37/37 全部通过**（含新增 `PASS 34: 1.76 instant-run: HumanObject.CanRun is true from standstill without 1-step walk warm-up`）
- `dotnet run --project scripts/localization-tests/Localization.Tests.csproj -c Release`：**29/29 全部通过**
