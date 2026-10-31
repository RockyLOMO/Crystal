# Crystal Mir2 改动、汉化与本机部署记录

日期：2026-10-30（Asia/Singapore）。当前为本机试验部署，安卓兼容环境适配另行验证。

## GitHub fork 与改动汇总

- 用户 fork：[RockyLOMO/Crystal](https://github.com/RockyLOMO/Crystal)，父仓库为 [Suprcode/Crystal](https://github.com/Suprcode/Crystal)。
- 本地 `origin` 指向用户 fork，`upstream` 保留上游地址；维护分支为 `rx-server`。
- 改动基于上游提交 `0e315fe327192afe52c3d7357ddd1f5b7e26c5b8`，不是无源码修改的外挂配置方案。CPU 优化和显示字典接入都需要重新编译；字典译文本身是外置 JSON。
- 本次按请求整理并执行本地 Git 提交；没有向上游提交 PR，也没有自动推送维护分支。

| 改动 | 文件 | 目的与边界 |
| --- | --- | --- |
| 到期调度、工作线程等待与安全停止 | `Server/MirEnvir/Envir.cs` | 有界索引堆替代全量扫描，野怪保留原线程分区，宠物和其他对象由主线程处理 |
| 运行时对象的调度通知 | `Server/MirObjects/MapObject.cs` | 生成/消失、宠物归属和 `OperateTime` 更新同步调整队列，外部提前唤醒继续生效 |
| 中文显示字典加载 | `Shared/DbLanguage.cs`、客户端/服务端 `Settings.cs` 及发布项目文件 | Chinese 时加载三份 `*.zh-CN.json`，未知项回退原文，发布时复制字典 |
| 物品、技能、任务名称显示 | `Shared/Data/ItemData.cs`、`Shared/Data/ClientData.cs` | 保留物品内部 `Name`；提供 `RawFriendlyName`，避免精炼逻辑依赖译文 |
| 精炼逻辑兼容 | `Server/MirObjects/PlayerObject.cs` | 黑铁矿石判断改用原始名称，不使用中文显示名 |
| 世界物件、地图与 NPC 文字 | `Client/MirObjects/{Item,Monster,NPC}Object.cs`、`Client/MirScenes/GameScene.cs`、`Client/MirScenes/Dialogs/NPCDialogs.cs` | 显示名称和精确菜单文字翻译，玩家姓名、脚本跳转、颜色和变量不作全文替换 |
| 上游中文语言包补漏纠错 | `Client/Localization/Chinese.json`、`Server.MirForms/Localization/Chinese.json` | 在上游自带中文基础上补齐缺键、纠正属性提示，移除客户端已无对应键的旧条目 |
| 服务端主窗口中文化 | `Server.MirForms/SMain.cs`、`SMain.Localization.cs` | 菜单、页签、列头与动态状态；不是全数据库编辑器汉化 |
| 新增译文与覆盖记录 | `Localization/` | 本地整理的名称、菜单和短句；无第三方汉化整包，具体来源与未译范围见下文 |
| 回归检查 | `scripts/performance-tests/`、`scripts/localization-tests/`、`scripts/package-tests/` | 35 项调度检查、13 项汉化检查及 39 项小样本打包检查，不启动实际游戏服；不依赖本机 `.ai` 或运行数据库 |
| 构建、运行与分发维护 | `scripts/` 内 PowerShell、CMD、Python 脚本 | 独立暂存构建、正常启停/备份、资源下载、干净客户端打包、协议检查与 CPU 测量；不会自行创建计划任务或修改防火墙 |
| 本地文件边界与证据 | `.gitignore`、`docs/`、根 `README.md` | 忽略 `.ai/`；记录测试、哈希、分发信息和部署边界，不提交运行存档、密钥、资源大文件及缓存 |

汉化来源必须区分：界面/系统提示以**上游已有 `Chinese.json`** 为基础；新增名称、菜单和部分对话由本次工作根据实际底库的英文标识、常见传奇术语整理，并用 Python 脚本展开楼层、编号和词根变体。没有引入第三方汉化整包，也没有逐条对照盛大官方文本校对；后期名称部分为暂定语义/音译。Jev 内容底库和 MirFiles 资源是另行下载的社区数据，不是本次原创汉化文件，也不随源码提交。

数据库导出的 `.ai/localization-data.json`、NPC 语料 `.ai/npc-visible-text.json` 只用于此次生成与覆盖统计，未纳入版本控制。`scripts/build-*-localization.py` **重新生成译文时需要这些本地输入**，不是克隆后可直接运行的测试命令；不能用一份不同底库的导出结果覆盖当前字典后仍沿用本次覆盖率。正常构建、发布和回归测试直接使用已跟踪的 `Localization/`，不需要这些导出文件、真实数据库、8GB 客户端资源或本机 TEMP 工具。

在 Windows/.NET 8 SDK 环境，从仓库根目录验证（无需启动运行服）：

```powershell
dotnet build Server.MirForms/Server.csproj -c Release
dotnet build Client/Client.csproj -c Release
dotnet run --project scripts/performance-tests/PerformanceTests.csproj -c Release
dotnet run --project scripts/localization-tests/Localization.Tests.csproj -c Release -- "$PWD"
```

构建仍有上游既有警告：服务端成员隐藏，以及客户端 WindowsBase 依赖版本冲突、未使用字段等；本轮构建为 0 错误，并未把这些警告描述为已修复。详细性能数值、验收范围和备份路径见后文。升级上游时应重点复核上述两个调度文件、共享名称显示与精炼判断，并重跑测试。

## 代码审查与进一步优化（2026-10-30）

对空载调度与中文显示字典代码完成全面审查，并落地以下改进（源码已同步，验证命令与回归测试已更新）：

### 1. 降 CPU 与并发调度优化

| 优化点 | 涉及位置 | 问题背景与改进细节 |
| --- | --- | --- |
| 单对象异常隔离与限流退避 | `Server/MirEnvir/Envir.cs` (`ProcessScheduledObject` / `ReportObjectProcessError`) | 原实现中单个怪物或对象 `Process()` 抛出未捕获异常会直达 `ThreadLoop.finally` 并将 `Running = false`，导致全服停机。现改为单对象级捕获：累计 `ObjectProcessErrors`、将异常对象的 `OperateTime` 推迟 `1000ms` 后继续处理并限流输出日志。一次性延迟动作在执行前出队，发生异常后不会重放已部分执行的动作；首次已产生的奖励等效果不自动回滚，其他待执行动作保留。仅在调度器自身结构异常时才触发安全停服。 |
| 批处理锁合并（减少 50% 入出队锁争用） | `Server/MirEnvir/Envir.cs` (`ProcessMobBatch`) | 原实现每处理一个对象需分别加锁调用 `TakeDue` 与 `Complete`。现将“完成上一对象入堆（`Complete`）”与“取出下一到期对象（`TakeDue`）”合并至同一次 `MobThread.ScheduleLock` 临界区内执行。 |
| `OperateTime` 无变化写跳过 | `Server/MirObjects/MapObject.cs` (`OperateTime.set`) | 在进入 `MobThread.ScheduleLock` 前先通过 `Interlocked.Read(ref OperateTimeValue) == value` 判断值是否变化；未变化时直接返回，避免无意义的加锁与堆修复。 |
| 真实繁忙耗时与锁竞争监控指标 | `Server/MirEnvir/Envir.cs` (`WorkLoop` / `ProcessMobBatch`) | 修复因 `Monitor.Wait` 休眠导致 `LastRunTime` 恒为固定节拍（10–16ms）的问题：`Envir.LastRunTime` 改为统计每秒内单 Tick 最大实际繁忙耗时（ms），`MobThread.LastRunTime` 统计当前批次实际执行耗时，并新增每秒全局锁争用增量 `LastLockContentionPerSecond`（基于 `Monitor.LockContentionCount`）与异常计数 `ObjectProcessErrors`。 |

### 2. 汉化与显示/逻辑边界优化

| 优化点 | 涉及位置 | 问题背景与改进细节 |
| --- | --- | --- |
| GM 按物品查角色支持中英双语 | `Server/MirEnvir/Envir.cs` (`MatchPlayerbyItem` / `MatchesItemName`) | 服务端启用中文后 `Item.FriendlyName` 返回中文，导致 GM 输入英文原名查询失效。现同时匹配 `UserItem.FriendlyName`、`ItemInfo.FriendlyName`、`ItemInfo.RawFriendlyName` 与 `ItemInfo.Name`（精确与模糊模式均支持）。 |
| 地面堆叠物品名称翻译保留数量 | `Shared/DbLanguage.cs` (`Item`)、`Client/MirObjects/ItemObject.cs`、`Client/MirScenes/GameScene.cs` | 服务端下发地面堆叠物品名称格式为 `"名称 (数量)"`，直接查表无法命中带数量的键。新增 `DbLanguage.Item(string)` 识别并剥离末尾 `" (数字)"` 后翻译基础物品名，再拼接原数量后缀（支持跨语言服务端/客户端混用）。 |
| 商城搜索支持中英双语 | `Client/MirScenes/Dialogs/GameshopDialog.cs` (`MatchesSearch`) | 客户端商城搜索原仅匹配翻译后的 `FriendlyName`；现同时匹配中文显示名、`RawFriendlyName` 与数据库原始英文名 `Name`。 |

### 3. 核心监控指标与后续压测建议

- **核心监控指标**：
  - `Envir.LastRunTime`（每秒单 Tick 峰值繁忙耗时，ms）與各 `MobThread.LastRunTime`（工作线程批处理实际耗时，ms）。
  - `Envir.LastCount`（每秒实际执行 `Process()` 的到期对象总数）。
  - `Envir.LastLockContentionPerSecond`（每秒 `.NET Monitor` 锁争用增量）与 `Envir.ObjectProcessErrors`（调度器隔离的对象异常总数）。
  - 进程工作集、GC 代际回收次数，以及网络与图形层面的非托管/堆外内存占用（Private Bytes 减去 GC Heap Size）。
- **已知边界与压测建议**：
  - 主循环末尾 `Monitor.Wait(_locker, remaining)` 受 Windows 默认时钟粒度影响，空载或轻载下实际 Tick 间隔约 `10–15.6ms`，玩家网络包处理与该节拍同步，建议在多人战斗、攻速与移动加速判定场景下做实机验收。

## 审查问题修复（2026-10-31）

- **P1：NPC 奖励重复执行与同帧多动作漏跳**。`MapObject.Process()` 将到期动作在调用 `Process(action)` 前通过 `ActionList.RemoveAt(i--)` 移除：既保证脚本先发奖励、后抛异常时不会重放该动作，又修复正序移除未回退索引导致同帧连续到期动作被跳过的问题。新增真实 `PlayerObject → CompleteNPC → NPCScript → NPCSegment` 回归：发 100 金币后除零，首次金币为 100；重试后执行剩余 7 金币奖励保持 107；同帧连续两笔 7 金币动作在同一 Tick 内全部执行完毕（金币达 121）。
- **P2：迁移客户端目录后无法打包**。`Package-Client.ps1` 不再要求输入目录叫 `Mir2`；按真实客户端目录名构造 7z 输入及排除规则，再将 ZIP 顶层统一为 `Mir2`。校验客户端入口、拒绝常见服务端程序/存档，保留干净配置、用户数据排除、CRC/SHA 校验和旧包保护。`-ReportPath` 可把小样本测试报告输出到 TEMP，避免覆盖真实分发记录。

验证命令：

```powershell
dotnet run --project scripts/performance-tests/PerformanceTests.csproj -c Release
pwsh -NoProfile -File scripts/package-tests/Test-PackageClient.ps1
```

调度 35 项、实际 7z 小样本打包/解包 39 项检查通过。打包测试覆盖 `mir2-client`、中文/空格及其他目录名、固定归档根目录、干净配置、个人数据排除、文件哈希、错误输入与已有包保护。没有重新生成真实 8GB 客户端 ZIP，也没有部署或重启运行服；当前已发布包与此前的实测记录不因源码修复而自动更新。实际重打包需用一个不存在的新 `-PackageName`，已有同名包会按原保护规则拒绝覆盖。

## 目录与分支迁移（2026-10-30）

按用户要求，源码目录由 `D:\projs\Mir2` 仅调整大小写为 `D:\projs\mir2`，本地维护分支由 `codex/zh-cn-idle-cpu` 重命名为 `rx-server`；完整运行根目录由 `D:\Mir2` 同盘移动到 `D:\private-server\mir2`，包括 `Server/`、启动脚本和全部历史备份。

移动前确认服务端/客户端均未运行，无引用旧路径的 Windows 服务、计划任务、快捷方式或用户环境变量，目标目录没有冲突。源码通过中间目录名完成 Windows 大小写重命名；运行目录采用同盘目录移动。移动完成、修改管理脚本及启动验证之前，32,247 个文件的相对路径/大小清单一致，数据库、配置和关键程序文件 SHA-256 一致。

源码和运行根目录的 `Manage-Mir2.ps1` 默认路径、`Measure-Server.ps1` 默认程序路径、当前部署清单、程序哈希记录及地图备份位置已同步更新。CMD 启动器原本按自身目录定位，无需写死新路径；管理脚本原副本保留在新运行目录的 `backups/directory-migration-20261030/`。

从新源码目录 Release 构建通过，31 项调度、12 项汉化检查通过；新运行目录实际启动并监听 `127.0.0.1:7000/3000`，版本和 KeepAlive 检查通过，随后正常保存关闭，保持迁移前的停止状态。客户端运行目录及客户端 ZIP 不涉及本次移动；GitHub fork 和上游远程地址保持不变，本地迁移提交未推送。

`performance-20261030.json` 中的旧程序路径是此前采样时的真实路径，作为历史证据保留，不代表当前启动位置。后文所有当前启动/备份操作使用迁移后的目录。

## 目录与选型

| 用途 | 路径 |
| --- | --- |
| Crystal 源码、部署脚本与本说明 | `D:\projs\mir2` |
| Windows 运行入口 | `D:\private-server\mir2` |
| 实际服务端、配置与角色存档 | `D:\private-server\mir2\Server` |
| 客户端运行程序与资源 | `D:\private-server\mir2-client` |
| 客户端打包分发 | `E:\rxdev\webhost\mir2` |
| 停服一致性备份 | `D:\private-server\mir2\backups` |
| 数据库参考源码（Git 忽略） | `D:\projs\mir2\.ai\database-source` |

采用本机 Windows。原版服务端实际入口 `Server.MirForms/Server.csproj` 为 WinForms，目标 `net8.0-windows7.0`；`Server/Server.Library.csproj` 才是可跨平台的核心库。当前未引入第三方 Linux 宿主，也未修改 Linux 虚拟机。

本机已有 .NET SDK 8.0.423、Windows Desktop Runtime 8.0.29。服务端使用框架依赖发布；客户端为 win-x64 自包含发布，减少后续安卓 Wine 环境对 .NET 安装的依赖。安卓实际运行、触控与性能尚未验证。

## 版本与来源

- 源码：<https://github.com/Suprcode/Crystal>，提交 `0e315fe327192afe52c3d7357ddd1f5b7e26c5b8`。
- 构建与资源来源指南：<https://www.lomcn.net/wiki/index.php/Getting_Started>。
- 数据库：<https://github.com/Suprcode/Crystal.Database/releases/download/29.01.25/29.01.25.rar>，归档 68,652,922 bytes，内部 Jev 数据库原始版本 111。
- 客户端资源：<https://mirfiles.com/resources/mir2/crystal/patch/>。
- 资源清单 `PList.gz`：4,698 个文件，8,981,153,619 bytes；SHA-256 `c7a1f29ab769e1ef52a257c0ccc52ab527010d4846193f41051cf2438e23248f`。
- 该资源清单实际为未压缩二进制；资源主要是 `.Lib`、地图与声音，不包含客户端程序。程序由同一源码提交编译。
- VC++ 2010 x64 依赖来源：<https://www.microsoft.com/en-us/download/details.aspx?id=26999>。下载包与提取的 DLL 均核对 Microsoft 有效签名。

原始下载、解包与构建缓存放在 `%TEMP%\ai\20261030\mir2-crystal`，不依赖这些缓存启动现有服务。

## 当前配置与内容边界

- 游戏地址：服务端对外监听 `0.0.0.0:7000`，客户端与分发包默认连接 `s.f-li.cn:7000`（`Client/Settings.cs` 支持 `IPAddress=host:port` 或 `[ipv6]:port` 并校验端口范围 `1..65535`）；原版状态监听：`127.0.0.1:3000`。
- 未添加路由器映射、防火墙规则或开机计划任务，HTTP 管理服务关闭。
- 仅开放战士、法师、道士创建：`AllowCreateAssassin=False`、`AllowCreateArcher=False`。
- `Configs/HeroSettings.ini`：`AllowNewHero=False`。
- `EnforceDBChecks=True`，保留原版启动检查；版本检查开启，`VersionPath=.\ClientVersion\Client.exe`。
- GM 口令已替换为随机值，保存在服务端 `Configs/Setup.ini`；文档与客户端不包含该值。
- 服务端与客户端界面设为 Chinese；客户端字体为 Microsoft YaHei，1024 分辨率窗口模式。
- 客户端 `Mir2Config.ini` 和 `Mir2Test.ini` 均指向本机，自动更新器关闭，避免第三方资源源替换本地程序或配置。
- 基础库含后期地图、技能与机制。禁用额外职业和英雄不等于完成纯 1.76 裁剪；本轮先建立可运行基础。
- 修复社区基础库 `Envir/Drops/HiGreatGhoul.txt:32` 与 `HiGreatGhoul0.txt:71` 的缺空格：`1/1RedDagger Q` 改为 `1/1 RedDagger Q`，物品 RedDagger 在数据库中存在。
- 对服务端数据库实际引用的 463 张地图逐一核对 SHA-256；其中 `DogYoHyun.map` 与资源源版本不同，已备份原客户端地图并同步为服务端版本。现 463/463 匹配，详情见 `map-sync.json`。重新获取全套资源后也应保留这项同步。

## 空载 CPU 排查与修复（2026-10-30）

用户报告无人在线时 CPU 25%+。本轮未复现 25%，但修改前的上游原版程序在当前完整底库下确有持续 CPU 消耗。`Multithreaded=True`、`ThreadLimit=2`、`MonsterProcessWhenAlone=False` 与本提交上游默认值一致，并非部署时额外增加的线程或无人区 AI。

在本机 16 逻辑处理器、零玩家、相同配置和 463 张地图、正常刷怪保留的条件下测得：

| 版本/测法 | 时长 | 平均 CPU（占整机） | 最高单秒 CPU | 平均工作集 |
| --- | --- | --- | --- | --- |
| 修改前原版，System.Runtime counters | 45 秒 | 9.65% | 10.25% | 3,390 MB |
| 最终到期调度版，System.Runtime counters | 45 秒 | 1.18% | 3.13% | 3,332 MB |
| 最终版同期，进程 CPU 时间差分 | 45.64 秒 | 1.21% | 3.11% | 3,332 MB |
| 最终版第二轮，进程 CPU 时间差分 | 45.69 秒 | 1.35% | 3.37% | 3,333 MB |

计数器首个无区间的 CPU 0 样本不计入平均，保留后续真实零样本；进程时间法按 16 个逻辑处理器归一化。原版与最终版计数器平均值相比下降约 87.8%。这是短期本机空载验证，不代表在线战斗或长期运行负载；用户报告的 25% 没有被当作本轮基线。

运行采样显示主要耗时在全量对象链表扫描和重复预算计时，实际 AI 耗时仅占很小部分。原版主线程空闲等待被注释，工作线程在未到期时仍反复扫描并忙等。先只增加等待、再合并预算读取的两轮修改分别仍有 7.78% 和 5.81% 平均 CPU，因此最终改为有界索引最小堆：只取到期对象，野怪沿用原分区，宠物和其他对象由主线程处理。`OperateTime` 提前/延期、生成/消失和宠物归属变化同步调整位置；每对象最多一个堆节点，没有持续堆积的过期条目。对象处理不持调度锁，保留 10ms 节拍、严格 `Time > OperateTime` 和每帧最多执行一次，停服先唤醒并等待工作线程退出，再清理队列。

没有删除地图、关闭刷怪、延长无人区 AI 间隔或修改地图内存结构。新增每对象约 32 bytes 调度元数据，工作集仍在用户接受的约 3.3GB 范围。31 项实际 DLL 调度回归通过，覆盖生命周期、并发提前唤醒、宠物迁移、预算续跑、单线程模式、堆有界性与停止流程；协议版本/KeepAlive 通过，最终正常关闭通过。真实角色移动、战斗、交易和长期稳定性尚未验收。

统计与样本保存在 `performance-20261030.json`，程序哈希在 `binary-sha256.json`。复测无需安装诊断工具：

```powershell
pwsh -NoProfile -File D:\projs\mir2\scripts\Measure-Server.ps1 -Seconds 45
```

本轮开始时服务端/客户端均未运行；验证后恢复为停止状态。可用 `D:\private-server\mir2\Start-Server.cmd` 启动新版本。临时 dotnet-counters/dotnet-trace 工具已卸载，未修改 PATH 或用户环境变量。

停服数据备份：`D:\private-server\mir2\backups\20261030-121901Z`；原版程序备份：`D:\private-server\mir2\backups\before-localization-cpu-20261030`；第一版汉化客户端/服务端升级前备份：`D:\private-server\mir2\backups\before-zhCN-release2-20261030`；上一个过渡版服务端程序：`D:\private-server\mir2\backups\before-due-queue-20261030`。回退时先正常停服并备份当前状态，再恢复成套程序与匹配的 Localization；不要用程序回退覆盖账号存档。上述备份已生成，尚未做真实恢复演练。

## 汉化范围（2026-10-30）

采用显示层字典，内部英文数据库名称、地图文件名、掉落文件名、任务和 NPC 命令参数保持原样。原讨论文档 `crystal_crystalm2_1_76.md` 的 `Language.ini`、直接重命名和删除后期地图步骤没有执行；当前代码实际使用 JSON 语言包。

- 补齐客户端仓库密码、删除确认等 19 个语言键和服务端 2 个语言键，修正攻击/魔法/道术不足提示。
- `Shared/DbLanguage.cs` 在启动时加载 `Localization/*.zh-CN.json`，Chinese 启用，未知名称回退原文；English 恢复原显示。字典不会写回内容数据库。
- 物品唯一名称 1,626/1,626；怪物非空显示名 371/371，原始名称及显示名联合 561/562（只跳过 `Name=00, GameName=空` 的占位记录）。
- 地图标题 297/297、NPC 名称 271/271、技能名称 109/109、任务标题 141/141；NPC 名称中的下划线换行保持原数。
- 怪物名字、宠物名字中的怪物部分、物品栏/地面物品、地图标题、技能和任务标题已接入。玩家和宠物主人的姓名不参与翻译。精炼黑铁矿石判断继续使用原始名称，避免中文显示影响逻辑。
- 服务端主窗口菜单、日志/在线/行会页签、列头、职业性别及动态状态已中文化；数据库编辑器内的内部名称和其他编辑窗口尚未完整汉化。
- NPC 文本起步包有 321 条菜单 caption、386 条完整行。语料中菜单出现次数覆盖 6,485/8,000（81.1%），完整行覆盖 7,751/16,813（46.1%）；两项重叠，不能相加。比奇/盟重优先正文仅 181/1,126 个唯一行，长篇对话、任务正文和动态变量句仍有英文。只改精确可见文字，跳转目标、色码、变量标记和命令不改。

部分后期/韩服名称为暂定中文语义或音译，并非全部盛大标准译名；名称覆盖不代表纯 1.76 内容或游戏内显示验收。详细统计在 `../Localization/*coverage.json`，生成器在 `scripts/build-*-localization.py`。

验证：`dotnet run --project scripts/localization-tests/Localization.Tests.csproj -c Release -- D:\projs\mir2` 的 12 项检查通过，覆盖内部标识、精炼名称、宠物主人、未知项回退、NPC 跳转/色码保留与名称覆盖。新客户端本机隐藏启动 10 秒保持运行、正常关闭，新增错误日志 0 bytes；尚未登录角色验证所有窗口和对话。

## 启动、停止与备份

双击 `D:\private-server\mir2\Start-Server.cmd` 启动管理窗口。若服务已在后台运行，重复启动会报告现有进程；先正常停止后再次启动可显示窗口。

```powershell
# 后台启动
pwsh -NoProfile -File D:\private-server\mir2\Manage-Mir2.ps1 -Action Start
# 状态
pwsh -NoProfile -File D:\private-server\mir2\Manage-Mir2.ps1 -Action Status
# 正常停止，等待原版关闭流程完成并保存存档
pwsh -NoProfile -File D:\private-server\mir2\Manage-Mir2.ps1 -Action Stop
# 停服后生成一致性备份（运行中会拒绝）
pwsh -NoProfile -File D:\private-server\mir2\Manage-Mir2.ps1 -Action Backup
```

客户端双击 `D:\private-server\mir2-client\Start-Client.cmd`，或在该目录运行 `Client.exe`。正常用户可在客户端注册；没有预置测试账号或管理员账号。

原版每次接受新连接后，对相同 IP 设置 5 秒连接冷却。刚启动客户端就立即运行协议探针，可能在握手前被关闭连接；应等待冷却后再测。本轮复核已通过，未关闭这项保护。

不要直接强杀服务端。管理脚本按完整程序路径定位进程，向其隐藏的 WinForms 窗口发出正常关闭消息；超时保留进程并报告错误，不自动强杀。

角色与账号位于 `Server.MirADB`；游戏配置、内容库为 `Configs/`、`Envir/`、`Server.MirDB`，另有 `Guilds/`、`Conquests/`、`Archive/`。停服备份保存这些目录和文件。程序、静态地图及客户端资源可按记录的版本重新获取。

恢复时先正常停服，另备份当前状态，再把所选备份恢复到相同版本的完整服务端目录，最后启动并检查日志。配置与账号文件不得放入客户端 Web 目录。当前尚未执行真实回滚恢复演练。

## 构建与资源续传

所有源码和维护脚本在 `D:\projs\mir2`。构建先输出到新的暂存目录，不直接覆盖运行中的服务端：

```powershell
pwsh -NoProfile -File D:\projs\mir2\scripts\Build-Mir2.ps1 -OutputDirectory "$env:TEMP\ai\20261030\mir2-rebuild"
```

服务端发布命令为 `dotnet publish Server.MirForms/Server.csproj -c Release -r win-x64 --self-contained false`；客户端对应 `Client/Client.csproj --self-contained true`。

客户端额外在程序目录提供 `msvcr100.dll`、`msvcp100.dll`（10.0.40219.1），以及已存在于本机、有效 Microsoft 签名的 x64 `d3dx9_43.dll`、`d3dcompiler_43.dll` 的副本。未安装系统级运行库。重新部署客户端时保留这四个应用本地依赖。

```powershell
python D:\projs\mir2\scripts\download-client-resources.py --destination 'D:\private-server\mir2-client' --workspace D:\projs\mir2 --temp "$env:TEMP\ai\20261030\mir2-crystal\resources" --workers 16
python D:\projs\mir2\scripts\probe-server.py
```

下载器按上游清单核对文件长度，并记录本地 SHA-256；同长度文件会复用，不覆盖冲突的既有文件。清单不提供每文件上游校验值，本地哈希用于后续基线对比。没有把资源清单当作可执行代码运行。

升级服务端前停服备份，保留旧程序，再部署暂存程序；客户端版本文件同步到服务端 `ClientVersion/Client.exe` 后重启。更新后执行协议检查、实际登录与保存测试。

资源下载成功后，用 `scripts/Package-Client.ps1` 打包。脚本在任务 TEMP 目录生成 ZIP，插入干净的本机连接配置，排除账号偏好、截图、日志与用户数据；`7z t` 通过后才复制到 `E:\rxdev\webhost\mir2`，并再次核对 SHA-256。最终分发信息见 `client-package.json`。

当前汉化分发包：`E:\rxdev\webhost\mir2\Crystal-Mir2-Client-win-x64-zhCN-20261030.zip`，8,059,900,590 bytes，5,199 个文件，SHA-256 `1e42c45c96cc26175a2f8c66fc87b2b43f2d2c6031f4bba4fdd3673850fb3209`。原始未接入名称字典的旧包保留。旁边提供 `.zip.sha256` 与 `README.txt`。ZIP 内部完整性、分发副本哈希、干净默认配置、敏感/个人数据排除、同步地图内容均已检查通过。运行目录与分发目录已分离，E 盘目录不保留展开的客户端。

## 验证与限制

- 原版服务端与客户端 Release 发布均成功；源码迁移后再次构建成功。
- 上游现有编译警告包括 WindowsBase/WebView2 引用冲突、未使用变量/字段等，没有修改游戏源码来消除这些警告。
- 服务端加载 463 张地图；修复掉落脚本后重启未再出现该掉落错误。
- 只读协议检查：Connected、正确客户端版本、KeepAlive 往返通过，不创建账号或角色。
- 本机 SlimDX/Direct3D9 在 .NET 8.0.29 中初始化成功，识别 1 个显示适配器。
- 用户在本轮确认客户端正常显示登录界面。启动/显示切换期间记录过 `D3DERR_DEVICELOST`，该日志不作为显示持续失败的结论；尚未完成进图验收。
- 初次部署完成全量资源后已重启客户端并由用户确认登录界面。本轮汉化客户端再次启动检查后正常关闭。
- 正常停服、再启动、重复启动保护、状态检查与停服备份已执行。
- 客户端 4,698 个资源文件、8,981,153,619 bytes 全部下载成功，失败数 0；详情见 `client-resource-download.json`。每文件本地 SHA-256 位于 Git 忽略的 `.ai/client-resource-sha256.json`，地图的部署覆盖另记在 `map-sync.json`。
- 尚未验收实际创建角色、战斗、交易、存档恢复与安卓真机；服务启动、协议握手和图形初始化不代表完整玩法验收。
- 本机存储空间虚拟磁盘仍显示 Warning/Degraded；D/E 卷报告 Healthy。账号与角色保留在 D 盘，E 盘用于按用户要求存放打包后的客户端文件。未调整存储池或触发修复。
