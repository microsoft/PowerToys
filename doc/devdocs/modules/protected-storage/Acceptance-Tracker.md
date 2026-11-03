# Workspaces Protected Storage 验收台账

最后更新：2026-11-04（UTC+08:00；E45无caller-DACL替代机制试验因UAC取消受阻，未得到可行性结论）

**这是验收状态的唯一维护入口。每执行完一项，就更新本文件；不能只在聊天里报告通过。**

本台账合并了 [原生产验收矩阵](../workspaces-protected-storage-acceptance.md)、
`HUMAN-TESTS-Workspaces-ProtectedStorage-0.101.3001.0.md`，以及后续本地 E2E、
只读 Data、迁移/重置、性能和 PR #51114 集成的验证要求。保留原生产矩阵的用例 ID；
旧人工清单的同名 ID 以 `H:` 为前缀引用，避免把两个不同的 `DATA-01` 混为一项。

## 1. 当前基线与结论

### 2026-11-03统计快照（E44诊断未改变正式用例状态）

按下方126个唯一正式验收ID统计整项状态；不重复计入E33/E36子项、风险编号、
历史执行记录和环境矩阵行。该比例不是独立测试数、剩余工作量或风险消除比例。

| 状态 | 条数 |
|---|---:|
| PASS | 26 |
| PARTIAL | 22 |
| FAIL | 2 |
| RETEST | 0 |
| BLOCKED | 1 |
| NOT RUN | 75 |
| 合计 | 126 |

26条PASS由AUTO 14条、UNIT 6条、LOCAL-01、PERF-02/03及E37关闭的
DATA-09、DATA-17、CLI-01组成；仍不代表核心架构和正式安装验收已完成。
显式标P0的45条中，整项PASS 3条、PARTIAL 16条、FAIL 2条、RETEST 0条、BLOCKED 1条、
NOT RUN 23条。当前正式BLOCKED为REL-01（PM决策）；E40将DATA-14补到PARTIAL，
E40/E41分别记录CLI-03/CLI-07的实际FAIL；
不要与E33的三个BLOCKED子项混淆。
R06已有E36定向实机PASS，但DATA-10包含更多条件，仍只计PARTIAL。

### 分类总览：126条条件，不是126场独立测试

目前有100条尚未整项关闭，但其中只有75条记为NOT RUN；另有22条已部分覆盖、
2条FAIL、1条受阻，不能全部当作“从零开始”。E39已关闭原来的2条RETEST。台账混合了工程门禁、
自动化合同、实机行为、错误分支、环境矩阵和产品决策，因此26/126不是工作量完成百分比。

下面按**主要验证目标**将每个正式ID归入一个类别，互不重复、没有删减或新增要求。
“未关闭”包含PARTIAL/FAIL/RETEST/BLOCKED/NOT RUN；“未跑”仅指NOT RUN。

| 类别 | 总条数 | 整项PASS | 未关闭 | 其中未跑 |
|---|---:|---:|---:|---:|
| 工程基线与自动化 | 27 | 20 | 7 | 3 |
| 日常功能、交互和性能 | 19 | 4 | 15 | 6 |
| 跨权限执行与运行上下文 | 7 | 0 | 7 | 4 |
| 写保护、授权和用户隔离 | 9 | 0 | 9 | 5 |
| 数据迁移、一致性与恢复 | 17 | 1 | 16 | 14 |
| 安装、升级、卸载与生命周期 | 36 | 1 | 35 | 34 |
| 生产信任、环境与发布门槛 | 11 | 0 | 11 | 9 |
| 合计 | 126 | 26 | 100 | 75 |

| 类别 | 唯一归属的正式ID |
|---|---|
| 工程基线与自动化 | AUTO-01～14；BUILD-01～05；UNIT-01～08 |
| 日常功能、交互和性能 | LOCAL-03；DATA-10～13、16～17；CLI-01～02、05～06；UX-01～05；PERF-01～03 |
| 跨权限执行与运行上下文 | DATA-14；CLI-03～04、07；INST-07～08；ENV-02 |
| 写保护、授权和用户隔离 | ACL-01～04；SEC-01～05 |
| 数据迁移、一致性与恢复 | DATA-01～09、15、18；BLOB-01～03；CLEAN-01～03 |
| 安装、升级、卸载与生命周期 | LOCAL-01～02、04；INST-01～06；LIFE-01；UPD-01～15；REM-01～11 |
| 生产信任、环境与发布门槛 | SEC-06～11；ENV-01、03～04；REL-01～02 |

剩余最多的是安装生命周期35条、数据生命周期16条、日常功能15条。安装类中
INST-01～06、UPD-01～15、REM-01～11共32条属于正式安装/升级/卸载条件，
不能要求当前Debug样本先把它们全部关闭。

已通过的26条集中在：工程/自动化20条；日常功能4条（DATA-17、CLI-01、
PERF-02/03）；数据一致性1条（DATA-09）；部署核对1条（LOCAL-01）。
“整项0 PASS”不等于完全没测：写保护类已有4条PARTIAL，普通owner拒写和
未注册caller拒绝已实测；日常功能另有9条PARTIAL。跨权限、跨owner及生产生命周期
仍然是真实缺口，不能仅靠调整分类消除。

后续执行按第12节的工作包组织，原ID只作为每个阶段的验收检查点。

| 项目 | 当前记录 |
|---|---|
| 待验收代码 | `feature/workspaces-protected-storage-main-20261028`；产品基线 `b132ee3fbf` 加E35时间保留修复 `890cc68af1`；E36已增量发布到本地Debug6006 Editor并实际验证，managed DLL SHA256前缀 `6207D44B809AD83A` |
| 正式签名构建 | E38/E42：[162529587](https://dev.azure.com/microsoft/Dart/_build/results?buildId=162529587)于11-02 17:00:23结束，整体FAILED；代码`6bd56af147`、版本`0.101.6007.0`。x64在Carrier ICE阶段失败；ARM64成功并发布machine/user installer及carrier。缺x64安装包及已安装验收，不关闭B02 |
| 主线基线 | `8056ba09721708bceee6176291b24af7ebabb0b3`；包含 PR #51114，合并提交 `06e6ae43e225cac51c2f282b0eebc42c6f995c97` |
| 集成 worktree | `D:\PowerToys-Workspaces-Integration` |
| 待测本地包 | x64 Debug **`0.101.6006.0`，已匹配部署**；入口 `D:\PowerToys-Workspaces-Integration\x64\Debug\PowerToys.exe`；E33 核对实际客户端和 runtime，不再受旧6005部署阻塞 |
| 原代码备份 | `backup/workspaces-protected-storage-20261028`，提交 `c576e95f70d4247efeb68afe1c50bc5f7e57f4e2`；含原 worktree 全部已跟踪/未跟踪源码改动 |
| 原 worktree | `D:\PowerToys-Workspaces-E2E`；原 HEAD `146518e3a3c3120257799b89e061b304f81fdab7`、索引和源码保留 |
| 最近确认的运行环境 | E40，11-02：客户端/catalog/runtime6006匹配，Editor DLL仍为时间修复版；普通控制器Session2，用户明确授权同账号UAC。实测普通CLI、High CLI、Medium host及High Arranger/目标；目标窗口144 DPI，不是混合DPI/OTS验收 |
| 新包部署状态 | 本轮开始前已完成6006部署；本轮没有 Repair/Reset/Purge、重建VA、迁移或再次请求UAC。E31 的1223仅保留为历史记录 |
| 数据保护结果 | E40先完整备份，收尾经Cancel冲突/Reload/真实Import恢复原两条记录，全字段及payload字节一致；settings、桌面恢复，测试进程/夹具目录无残留，服务和Debug文件未变。没有直接覆盖protected blob/index |
| 当前验收结论 | E44在未修改6006二进制的情况下复现High launch：Medium Launcher读取前的client身份准备要求WRITE_DAC打开自身token，被拒绝5，最终包装成exit1。与E41 High list的linked-token 1346是两个不同前置条件缺口。CLI-03/CLI-07仍FAIL，26条PASS不变；没有实施架构重构或修复 |

`6003`、`6004`、`6005`、`6006` 都是合入 main 后的**本地 E2E 测试构建**，使用
`ProtectedStorage.LocalLifecycle.exe` 和本地测试 bundle，不是正式 carrier MSI 部署。
本地保留数据的 `Repair` 不能替代生产常规 MSI 升级、无新增 UAC、卸载或 servicing 验收。

两个 worktree 当前使用同一个本地测试服务身份和设置目录；源码隔离不代表运行环境隔离。
不能同时运行新旧客户端并假定它们都匹配同一份服务 catalog。6006 已完成匹配部署；
后续换包仍必须核对服务/catalog；旧 `Debug-6003-before-snapshot` 仅作历史备份。
仅切换构建时，应走经授权的保留数据更新；不要误用默认 `Prepare`、`Reset` 或 `Clean`。

## 2. 状态和逐项更新规则

| 状态 | 使用条件 |
|---|---|
| `PASS` | 本行所有通过标准在记录的版本、环境和范围内满足，并有可定位证据 |
| `FAIL` | 已实际执行，观察到与通过标准不符；填写失败步骤、错误码和证据 |
| `BLOCKED` | 必要前置条件未满足或授权取消；**不是已运行并失败** |
| `NOT RUN` | 尚未执行；源码存在、编译成功、没有复现都不能改成 PASS |
| `PARTIAL` | 只覆盖部分要求或组合；列明已经覆盖和缺失的部分，不计为整项通过 |
| `RETEST` | 曾有通过证据，但相关实现、构建或环境变化，需要重新确认；旧记录仍保留 |
| `N/A` | 明确不适用，必须记录原因及范围决定，不能用来隐藏未完成项目 |

更新一项时同时完成：

1. 更新该行的状态、日期、适用环境/版本、证据编号；有阻塞则引用阻塞编号。
2. 在第 13 节追加执行记录，写明实际步骤、结果、错误码及数据/环境恢复情况。
3. 不覆盖旧的失败/通过证据。修复后写新记录；无法复现记 NOT RUN 或 PARTIAL，不记 PASS。
4. 部分组合通过时保持 PARTIAL，或者拆分带环境后缀的条目，例如 `DATA-14/L64`、
   `DATA-14/P-XU`。不能用一个普通权限案例代表 High/OTS，也不能用 x64 代表 ARM64。
5. 产品代码、catalog 或服务版本变化后，重新检查受影响条目是否应转为 RETEST。
   纯文档更新不改变产品代码基线，也不要求无意义地重跑全部测试。

每次实际运行至少记录：用例 ID、执行时间、代码/二进制版本、OS build、架构、安装 scope、
真实业务用户 A/B、进程 token/IL、实际服务/worker 版本、操作/请求 ID、退出码、证据位置。
SID 等本机身份信息保留在受控本地证据中，公开文档用 A/B 代称；不记录密码、token、
真实工作区命令行或其他敏感业务内容。

### 环境范围

| 环境 ID | 范围 |
|---|---|
| `AUTO` | 非安装的编译、单元/协议/授权 fixture、发布 authoring/mock 检查 |
| `L64` | 当前为已匹配部署的 x64 Debug 6006（E33）；旧6003/6004/6005结果单独保留 |
| `P-XU` / `P-XM` | 包含本次集成代码的正式签名 x64 per-user / per-machine 安装 |
| `P-AU` / `P-AM` | 包含本次集成代码的正式签名 ARM64 per-user / per-machine 安装及 ARM64 运行 |

下方业务、CLI、ACL、UX、性能表的当前实机状态针对 L64；正式验收时必须按相同 ID
另记适用的 P 环境结果，**不得继承 L64 的 PASS**。安装、MSI 升级和卸载表属于 P 环境。
优先级 P0 表示首轮主路径，P1 表示常见异常，P2 表示专项/故障注入；P2 并不自动免于发布要求。

## 3. 阻塞和风险

| 编号 | 事实 | 影响与解除条件 |
|---|---|---|
| B01（已解除） | 早前提权取消后服务留在 6002；14:39 发现新 6003 客户端反复被旧 catalog 拒绝 | 2026-10-28 14:49 已授权保留数据更新，核对新 ready/catalog 和不变的数据哈希（E13/E15）。未执行的用例转为 NOT RUN，不自动变为 PASS |
| B02 | E42已核对6007 ARM64发布manifest和版本/提交元数据；x64 job失败，尚缺x64 installer、升级配对及实际已安装资格 | ARM64产物存在不代表P-AU/P-AM已执行；P环境安装/升级/卸载不能用本地helper或历史3000/3001结果替代 |
| B03（历史探测取消） | 6004 隔离 High Arranger 探测曾返回 1223；之后用户已完成 6004 服务更新（E21） | High/OTS 完整资格仍需独立证据；不再把部署列为 6004 阻塞。6005 更新单独记录 |
| B04（已解除） | E31 的6006更新曾返回1223；E33 开始时已观察到匹配的6006客户端/runtime | 本轮实际业务验证已执行；不把历史授权取消继续当作当前部署阻塞 |
| B05 | E33 当前会话与Runner均非管理员；WIP skill要求跳过管理员变体 | High目标/OTS仍需另行授权的适用环境；未为覆盖率发起UAC |
| B06（E33缺口保留；E36新一轮已补足） | E33未保留完整事前字节，旧结果不能追溯改PASS；E36在任何变更前保存完整blob和逐记录详情 | E36清理后原记录全字段及2225字节业务payload一致；blob整体变化来自revision/operationId/requestDigest信封元数据。此证据仅证明E36相对其自身起点的保真 |
| B07 | E33 canonical resource receipt保留4个未解析launch记录及旧PID31508查询拒绝 | 已实测当前5个自有fixture退出、无剩余fixture进程、借用UI恢复；不手改旧receipt消除hold，不终止无法确认身份的PID |
| R01（6003 失败；6004 已完成实机会话） | 6003 的额外握手失败保留 E16；6004 两次 shared session 正常完成（E22），没有再卡在 protected-plan | 不把未采集 token 的已退出 Arranger 当作 High 证明；目标应用的 elevation 复用缺陷见 R05 |
| R02 | 通用 “protected storage unavailable” 曾掩盖启动会话错误；授权不匹配还曾提供无效 Retry | 后一问题已修复并实测（E11/E12）；UX-01 的其他错误分类仍需逐项验证，不能根据弹窗直接诊断服务坏了 |
| R03（已绕开慢路径，快慢差异尚未闭环） | 6005清单47.152秒；独立x64诊断定位到AppsFolder首次Next等待。10-29 23:10最新main仍使用相同枚举循环（E32），不是已找到main的另一套快速实现 | E29/E32：新路径分别697/2924ms，旧路径36958/37383ms；没有改服务timeout。未对原版main二进制做同机同权限A/B，不能据源码相同断言原版必慢，也不能把6006优化说成已证明的分支回归根因修复 |
| R04 | 服务删除不等于 VA profile 已卸载；旧测试曾报告 profile 仍 loaded | 清理应报告准确残留，不强制卸载 hive；REM-06/CLEAN-02 单独判断 |
| R05 | 6004 `move-existing-windows=true` 时只匹配名称/路径/PWA，没有比较实际 token elevation；普通 Notepad14436 可满足 elevated 条目，绕过新启动路径 | 6005 在既有窗口选择和新窗口认领处均要求 elevation 匹配；仅用 limited process query，查询失败不当作普通窗口，不引入身份握手或 DACL 修改 |
| R06（L64定向实机PASS） | E35修复复制构造函数；E36实际新建自有工作区，再连续两次改名、Save、Reload、重开 | 创建时间三次均 `1793347374`，last-launched三次均0，除改名外全部字段一致；不是只读内存或单测结论。Debug6006 Editor已包含修复；既有零值不自动恢复 |
| R08（E44已定位当前同路径失败） | E40原始exit1保留；E44同一6006二进制的High/no-app复现捕获`open own token DACL: access denied (win32=5)`。Medium Launcher的主token内容IL为Medium，但token对象label仍High/NW、owner为Administrators，普通身份不能WRITE_DAC | `ConnectOwnerPipe -> AllowIdentityQuery -> GrantIdentityQuery`在建立pipe前失败；Unauthorized/5经host通用catch丢失为internalError/exit1。创建Medium进程本身不足以满足SDK身份准备；未修改ACL绕过或修复产品，详见E44 |
| R09（E41定位） | High CLI直接执行list，没有Medium读取宿主；本机同账号linked token为TokenImpersonation/SecurityIdentification，真实native客户端构造返回OwnerContextRequired/1346。CLI统一映射成storageUnavailable，Resources又缺该码映射，最终显示unexpected error | CLI-07 FAIL。不能只加现有Medium宿主便认为已修复：E44证明该宿主仍可能不满足SDK token对象权限要求。应共同明确通用owner-context/身份准备契约及保真错误映射，不放宽token检查、不回退明文、不重装服务 |
| R07 | E33 重新捕获后快捷方式复选框显示未勾选，现有快捷方式仍在；Revert后恢复勾选 | `DataContext` 赋值后才调用 `CheckShortcutPresence`，对应属性无变更通知；该顺序可追溯到2024-08-23初始PR #34324（`579619952d`），当前main仍保留。本轮未证明快捷方式被删除，也未修产品代码 |
| Q02 | 首次管理员授权时机、fail-closed 范围和 UX 仍需 PM 结论 | 当前不得启用不受保护 fallback；技术检查通过不等于 PM 已批准 |

## 4. 已取得的自动化与环境保留证据

这些条目只关闭其明确写出的检查范围，不关闭后续的已安装场景。

| ID | 检查与通过标准 | 当前状态 | 最近结果 / 证据 |
|---|---|---|---|
| AUTO-01 | 新 worktree 完整构建同版本 Runner、Settings、Quick Access、Editor、Launcher、LauncherUI、SnapshotTool、Arranger、CLI 和本地服务 bundle | PASS | 2026-10-29；6006，E30；不宣称全包零警告/完整符号，仍有既有XAML警告 |
| AUTO-02 | 本次集成的默认原生 Workspaces 单测全部通过，含 repository、CLI 合同、元数据及缺少 protected preview 的拒绝行为 | PASS | E39：当前提交重新构建x64 Release默认套件，193 passed、0 failed/skipped；显式关闭opt-in live slice。旧E18/E24记录保留 |
| AUTO-03 | 托管存储/协议/迁移/repository 测试通过 | PASS | 2026-10-29；49 passed，0 failed/skipped，E19；旧 E03/E11 记录保留 |
| AUTO-04 | 最终目标代码的 native storage 套件及 20,000 次 framing mutation 全部通过 | PASS | 2026-10-29；3,018 assertions + 20,000 mutations，E19。少的一条是已删除 peer helper 的参数断言；服务端映像和权限检查没有删除 |
| AUTO-05 | 原生 Setup/Lifecycle/Broker/MsiAction 编译及安装器契约检查通过 | PASS | E39：四个native helper重新编译，4,129 installer contracts通过；另核对非安装fixture MSI的RemoveFile/Media表。WIX1105明确保留：本机ICE未执行，不替代生产签名/安装或REL-02 |
| AUTO-06 | 发布流程的 mocked gates、最终文件字节/哈希、固定 policy/version、篡改拒绝检查通过 | PASS | 2026-10-29；E19，含新 inventory；仍是 process-scoped mock，不是实际发布/servicing |
| AUTO-07 | 本地测试 CMS 正常签名接受，内容改变或不同 signer 拒绝 | PASS | 2026-10-29；6006 E30，`expiredFixture=False`；不代替生产时间戳/吊销检查 |
| AUTO-08 | 新 CLI 的 `--version`、`--help` 正常退出且报告当前候选版本 | PASS | 2026-10-29；6004，E20；未连接 6004 服务执行 list/launch |
| AUTO-09 | 原源码完整保存、新集成提交持久化；新客户端文件哈希匹配新 catalog；维护不改变原业务数据 | PASS | E08备份保留；E30/E31证明6006构建匹配bundle及构建/取消更新未改业务哈希；这些检查本身不是运行证据，实际6006客户端/服务业务路径另见E33/E36 |
| AUTO-10 | 真实 Arranger 用既有认证 IPC 获取快照；拒绝错误 operation；无需完整进程查询 | PASS | E18：旧 6003 因要求额外 plan 参数而失败，6004 通过；限权案例仅修改可丢弃 Medium 子进程 DACL，并实际确认 full-query 拒绝、limited-query 成功，不是 High/MIC 验收 |
| AUTO-11 | Capture 等待可见、禁用重复操作/关闭、完成后恢复；开始前取消不启动 native capture | PASS | E23：Release WorkspacesCsharpLibrary.UnitTests 10 passed，其中 2 个新增真实 WPF/STA 窗口用例；模拟 pending Task，不代表已部署 native Capture E2E |
| AUTO-12 | 既有/新窗口匹配要求实际 elevation 一致；查询失败不按普通权限处理 | PASS | E24：Release AppUtils 21 passed，包含当前真实 token 的匹配/相反 elevation 拒绝、无效 PID 两种请求均拒绝；不是实际 High/OTS 新应用启动 |
| AUTO-13 | Capture 快路径保留当前可见应用的名称、路径、包身份与提升能力；快捷方式/Steam/PWA 不丢失必要启动信息 | PASS | E29：5 项定向 Release 回归通过，11 个可见应用元数据与旧枚举一致；快捷方式覆盖环境变量路径、显式 AUMID、Steam URL、不可读链接；PWA/Steam 既有计划回归通过。不是所有安装应用/所有机器的完整目录等价证明 |
| AUTO-14 | 编辑副本经实际ToWrappers保存转换后，创建时间不变，最近启动时间含0哨兵均保留 | PASS | E35：新增2个参数化回归先红后绿；整个WorkspaceWriteTests 8/8通过。仅Release构建及保存payload路径，不替代GUI/服务端端到端复测 |

### 原生产构建与单测要求的保留状态

| ID | 要求 / 通过标准 | 当前状态 | 范围或缺口 |
|---|---|---|---|
| BUILD-01 | x64 production Common/Client/Bootstrap/Runtime 全部编译链接，无测试授权旁路 | PARTIAL | AUTO-01/05 有组件证据；需补齐本次生产配置图，不能拿 LocalTest verifier 代替生产 verifier |
| BUILD-02 | Setup/Broker/Lifecycle/MsiAction/carrier 构建顺序、资源嵌入、正式签名和最终产物一致 | PARTIAL | native compile/mock 已有；真实 carrier/MSI/ESRP 最终链路待 B02 |
| BUILD-03 | 所有 native/managed 消费方及分析器通过同一合同，含 CLI、CmdPal/ModuleServices | PARTIAL | 本地 UI/CLI 包已构建；本次完整生产消费方图仍待核验 |
| BUILD-04 | 两种主安装 scope 的实际 MSI tables/嵌入 Lifecycle 正确；全机清理仅在正确条件下执行 | NOT RUN | AUTO-05/06 不能替代新包实际 MSI tables/字节和运行验证 |
| BUILD-05 | ARM64 编译、打包和 ARM64 机器运行 | NOT RUN | 上游 PR 或旧 ADO 的 ARM64 绿色不能代表本次集成 |
| UNIT-01 | 非法长度、溢出、UTF-8、版本/命令、尾随/截断响应 fail-closed | PASS | AUTO；E19 的 native/managed 套件 |
| UNIT-02 | native/managed golden vectors 一致，含 Unicode、64 位值和操作 ID | PASS | AUTO-02/03；仅 AUTO 范围 |
| UNIT-03 | 未初始化、合法空集合和已初始化后损坏明确区分 | PASS | AUTO；E19；实机 DATA-07/08 另验 |
| UNIT-04 | CAS 旧 revision 拒绝，operation ID 重放/内容冲突行为正确 | PASS | AUTO；E19；实际并发 writer 竞争另验 |
| UNIT-05 | 中断 intent/record/journal 后恢复或明确 recovery-required | PASS | AUTO fixture；E19；不代替真实 MSI/VA 提交边界断电验收 |
| UNIT-06 | 改字节、错 signer/product/architecture/catalog、降级、混版均拒绝 | PARTIAL | 本地 pin/tamper、catalog 和 authoring 有证据；生产信任/时间戳/安装矩阵未完成 |
| UNIT-07 | framing 和业务解析畸形输入受限，不崩溃、不挂死、不过量分配 | PASS | AUTO；E18/E19；限定已执行 mutation/解析 corpus，不宣称覆盖所有输入 |
| UNIT-08 | 主更新器区分成功、需重启、需重试和未知结果；模块失败不回滚主产品成功 | NOT RUN | 旧 Workspaces/updater 测试结果只留在历史；本次 Workspaces 范围不是 updater 全套 |

## 5. 本地切换、首次设置与安装

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 阻塞 |
|---|---|---|---|
| LOCAL-01 / P0 | 核对新目录全部 PE 版本和 catalog；不得夹带旧 worktree 客户端 | PASS | E30 的6006构建/bundle检查；E33实际客户端版本和runtime6006匹配，旧“服务仍6005”只属E31历史 |
| LOCAL-02 / P0 | 退出旧 UI；保留数据更新服务；记录 Code 前后版本、ready、index/blob 哈希及回退证据 | PARTIAL | E33已确认6006部署后状态，B04解除；本轮没有执行或完整见证该次更新的前后数据/回退流程，不能据此把整项判PASS。6004 -> 6005 的PASS保留E25 |
| LOCAL-03 / P0 | 从新 Runner 的 Settings、Quick Access、热键打开 Editor；实际进程全部来自新包，读到正确集合 | PARTIAL | E33真实Settings入口恢复同一6006 Editor并读取原集合；当前版本的Quick Access/热键入口未全部重测，不再归因于部署阻塞 |
| LOCAL-04 / P1 | 开发构建的 settings/log/mutex/event 与正式安装隔离；不改正式配置、不自动更新/卸载正式版本 | NOT RUN | 部署前置已满足；新旧 LocalE2E 本身并非不同命名空间 |
| INST-01 / P0 | A 的真实 per-user 首次预配；carrier 为 A 的 USERUNMANAGED/context 2，服务为对应 VA，不是 SYSTEM/管理员 owner | NOT RUN | P；B02；本地 helper 无 carrier MSI，不能替代 |
| INST-02 / P0 | 标准用户 B 在 Setup UAC 输入另一管理员凭据；业务 owner 全程仍是 B | NOT RUN | P；真实交互 OTS，不用 Session 0 合成 token 代替 |
| INST-03 / P0 | 在**首次 Setup UI**取消/无法授权；受影响功能明确阻断、旧源保留、其他模块仍可用 | NOT RUN | P；本次开发 PowerShell 提权取消不等于本项已通过 |
| INST-04 / P0 | A/B 同版本分别预配；同产品包、不同注册/VA/服务/Data，互不覆盖，无 MST | NOT RUN | P；需要真实 A/B 登录 |
| INST-05 / P0 | 已预配的普通用户正常使用；不重复预配、不反复请求服务授权 | NOT RUN | P；数据/应用启动 UAC 单独计数 |
| INST-06 / P1 | 首次设置部分失败后 retry；识别自有 partial state，不删 established/peer 实例 | NOT RUN | P；先保留操作日志，未知结果先 inspect |
| INST-07 / P0 | PowerToys 已提升时仍取得正确 owner 的普通 Editor/维护/执行上下文；不借用其他用户 shell | NOT RUN | L64：部署前置已满足；正式两种 scope 另记 |
| INST-08 / P0 | 安装/启用模块、后台枚举或普通 inspect/sync 不隐式首次预配或弹设置 UAC | NOT RUN | L64：部署前置已满足；P 范围还需 B02 |
| LIFE-01 / P0 | 退出重开 PowerToys，重启 Windows 后重新登录；服务 Auto、数据保留、不重复迁移/首次授权 | NOT RUN | L64：部署前置已满足；重启需安排，不自动打断用户工作 |

## 6. 数据、迁移与日常业务

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| DATA-01 / P0 | 真正未初始化的 store + 两个旧工作区；完整校验、提交并重开；持久化后才删除匹配旧源 | NOT RUN | 部署前置已满足；6002 的迁移回执仅历史 H04；不得把已有 store 当首次迁移 |
| DATA-02 / P1 | 旧 JSON 损坏、版本/条目不支持；整体拒绝，无部分发布/成功标记，源保留且可修复重试 | NOT RUN | 部署前置已满足；需要独立迁移 fixture |
| DATA-03 / P1 | 旧数据含 elevated/参数等敏感字段；按 module 规则完整验证/迁移，service 不解释业务 JSON | NOT RUN | 部署前置已满足；不能为迁移成功静默丢弃字段 |
| DATA-04 / P2 | 读取/迁移/清理之间替换源文件；提交验证过的同一快照，不能删除后来替换的源 | NOT RUN | 部署前置已满足；独立 fixture，记录文件 identity/hash |
| DATA-05 / P2 | 提交后响应丢失；查询原 operation ID，不能重复导入覆盖成功迁移 | NOT RUN | 部署前置已满足；需确定性故障注入 |
| DATA-06 / P1 | 阻止旧源清理；target 仍权威，显示 pending；Retry cleanup 只删除匹配源，不重复迁移 | NOT RUN | 部署前置已满足；不是 blob GC/服务修复 |
| DATA-07 / P0 | 已初始化后重启/更新/KeepData 重装，或放回旧文件；不得再次自动导入旧副本 | PARTIAL | E37：真实Import合法空集合，放回不同ID的legacy marker，关闭并重新启动Editor、Reload后仍为0条，无再导入；marker恢复。更新/KeepData重装及OS重启组合未测 |
| DATA-08 / P2 | 初始化记录存在但 record 缺失/损坏；明确 recovery-required，不回退明文、不初始化为空 | NOT RUN | 部署前置已满足；仅 disposable VM/数据 |
| DATA-09 / P1 | 编辑器和受支持 writer 产生真实 CAS 竞争；冲突可见、草稿保留、不覆盖无关修改 | PASS | L64 E37：Editor保留待保存改名草稿，外部CLI实际启动并写回history后Save触发Conflict；最新保存记录未变，取消错误仍保留草稿，Cancel draft/Reload后有效编辑成功。范围为同owner Editor↔CLI；不宣称跨owner矩阵或CAS Retry有效 |
| DATA-10 / P0 | 创建、改名/参数/布局、保存、重开、删除；完整持久化；保存失败保留可恢复草稿 | PARTIAL | E36创建/两次改名保存重开/删除及本轮原记录保真PASS，R06关闭；完整参数/布局、失败恢复等组合仍未全部对新版本复测。E33历史FAIL和旧B06证据缺口保留 |
| DATA-11 / P0 | Capture/取消；preview 归属正确、有大小/生命周期限制、可回收，不写可篡改临时计划 | PARTIAL | E33：6006实际Capture/取消/重新捕获；四个自有应用身份、DPI、最小化/最大化状态匹配，包含晚创建窗口；无LocalE2E legacy live文件。跨owner/大小/过期仍未测 |
| DATA-12 / P0 | 已保存/未保存工作区 Launch & Edit；使用准确 preview，重新捕获/保存/取消符合预期 | PARTIAL | E33：已保存自有卡Launch & edit完成后重新捕获新增窗口，Revert恢复4行，Cancel返回列表；未保存卡/重新捕获后Save等完整组合未覆盖，显示问题见R07 |
| DATA-13 / P0 | Editor/快捷方式/CLI 启动普通工作区；目标、参数、布局、move-existing、最小化/最大化正确 | PARTIAL | E33：自有快捷方式启动3个Win32 fixture及包Paint，实际普通token、PNG参数、DPI布局/最小化正确；move-existing开关的同一原窗口行为正确。全入口/全部状态组合未覆盖 |
| DATA-14 / P0 | Medium 执行核心 -> High Arranger；同账号提升和 OTS 分别测试；原 owner/会话/精确计划不变，目标应用 elevation 正确 | PARTIAL | E40：用户授权后，普通CLI20828 -> High Arranger24616，普通目标8420/High目标18376均同owner/session2并按保存布局排列，exit0；已有普通同映像29704保持原窗/位置，没有冒充High目标。OTS及其他入口未测；E27/E22历史保留 |
| DATA-15 / P0 | 安装元数据与 last-launched 经可观察 CAS 写回；不覆盖新编辑，不在 destructor 静默 I/O，不因重试重复启动 | PARTIAL | E37：实际CLI报告persistenceStatus=updated并产生非零last-launched；随后编辑Save/Reload保留创建时间和该非零时间，且真实CAS冲突未覆盖native写回。安装元数据变更、写回失败/未知结果等组合未完整覆盖 |
| DATA-16 / P1 | CmdPal/ModuleServices 枚举与启动使用同一 repository/owner；读取不预配，无 legacy escape | NOT RUN | 部署前置已满足；相关正式 host 还需新 catalog/构建证据 |
| DATA-17 / P1 | Export 得到一致显式副本；Import 确认后完整替换，坏 JSON 整体拒绝；副本不是 live source | PASS | L64 E37：实际Export全字段一致，修改那个导出文件不改live；真实确认Import完整替换自有fixture；截断JSON和错误集合类型均拒绝，index/记录不变；修正同一输入后Retry成功。原集合最后也经Import恢复并验证完整payload |
| DATA-18 / P2 | 已接受 save 时进入维护/停止/重启；已接受写入结果可确定，未接受请求可重试，不假成功 | NOT RUN | 部署前置已满足；保留 operation ID，未知结果先查询 |

## 7. PR #51114 的共享执行与 CLI

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| CLI-01 / P0 | 普通终端 list/filter/details/JSON 与 Editor 同一集合；不修改 index/blob，不读旧明文 | PASS | L64 E37：text/JSON及按ID/名称details匹配；最终6次读取前后全部index/blob文件名与字节完全相同。错误选项/缺失名称另验证exit2/3；合法空集合放回legacy后CLI仍读protected空集合 |
| CLI-02 / P0 | GUI 和 CLI 的 Medium 入口使用共享执行核心；CLI 同步等最终 arrangement 结果，失败/部分成功不报全成功 | PARTIAL | E37：CLI普通启动exit0/completed/arranged/updated，真实新目标为普通token、参数及144 DPI位置匹配；deadline为exit9/timeout，未伪报成功。全部GUI入口和部分应用失败矩阵仍未覆盖 |
| CLI-03 / P0 | High GUI/CLI launch 先交给同用户/会话的 Medium host；按保存的 elevation 标志启动，不因高权限终端把所有目标提权 | FAIL | E40目标未启动；E44用不存在ID作无应用诊断，普通exit3，而High直接及调试复现均exit1。Medium进程已进入client，但为VA准备token只读查询权限时无法取得自身token的WRITE_DAC，native5；root见R08/E44。未修复，也不是High GUI/完整启动已通过 |
| CLI-04 / P1 | 拒绝错误 owner/会话、失效进程/映像、非法 private host 输入；普通调用不能注入任意 workspace snapshot | NOT RUN | 部署前置已满足；保护存储和 plan 的身份要求不得因合并而悄悄移除 |
| CLI-05 / P1 | GUI Cancel/Ctrl+C/deadline/owner exit/第二次启动；停止后续应用，保留已启动应用，释放锁，结果未知不自动重放 | PARTIAL | E37的deadline/已有进程保留/新会话证据保留。E40计划取消UAC，但实际exit0并启动High目标，用户操作无法确认，未发生可证实的denial；不能改为取消PASS。该新启动仅证明此前High失败后会话可再次使用 |
| CLI-06 / P1 | 控制台应用独立控制台；CLI stdin/stdout/JSON、退出码、code page、隐私遥测合同保持主线行为 | NOT RUN | 部署前置已满足；错误/警告不得污染 JSON 流，参数/工作区内容不进遥测 |
| CLI-07 / P0 | 普通与提升终端分别验证 list 的 owner 上下文和错误反馈 | FAIL | E41：同一Debug6006 CLI，普通exit0读到两条；同owner/session2 High exit8、storageUnavailable/unexpected error。匹配native库诊断确认构造期OwnerContextRequired/1346，linked token只有SecurityIdentification；不是服务停机。尚未修复，见R09 |

## 8. 权限、隔离与信任

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| ACL-01 / P0 | 所属普通用户和管理员直接读取/list Data 的 index、blob、日志；其他普通用户/其他 VA 不获得读取权限 | PARTIAL | E37普通owner读取Data目录/index/blob成功；管理员与跨owner/VA矩阵未测。Code不属于此可读承诺，实际Code读取被拒；本轮额外要求Code可读的清单定义被判错，不放宽ACL |
| ACL-02 / P0 | owner/admin 正常文件访问不能写、追加、删除、替换、更改 DACL/owner；SYSTEM/对应 VA 仍可写 | PARTIAL | E37普通owner对Data的写/追加/删除及WRITE_DAC/WRITE_OWNER全部返回AccessDenied5；只申请权限、未写入，非SharingViolation/不存在。合法服务保存仍成功；管理员常规访问和完整替换矩阵未覆盖 |
| ACL-03 / P1 | 既有 Data 启动迁移、嵌套文件、新 blob、原子 index 替换均保持只读策略；内容哈希/对象 owner 不变 | NOT RUN | 部署前置已满足；AUTO fixture 有覆盖但不等于完整服务实测 |
| ACL-04 / P1 | Code/Policy/SCM 权限不因 Data 可读而放宽；排障以共享 read/write/delete 打开，不能阻塞正常提交 | NOT RUN | 部署前置已满足；不测试管理员主动 take ownership/backup privilege 并宣称 ACL 可阻止机器管理员 |
| SEC-01 / P0 | 普通用户不能修改 Code/Data/control；另一 VA 不能改其他 owner 的树 | PARTIAL | E37普通owner对Data/固定active Code目录及已知运行映像/catalog的40次修改/删除/ACL权限请求全为AccessDenied5；control及跨owner/VA矩阵仍未完成 |
| SEC-02 / P0 | 错 SID、未授权映像/角色/目标、伪造 owner 请求被拒绝；CLI 身份列入正确 catalog | PARTIAL | E37未注册PowerShell进程使用真实本地client/验证server身份的transport，读与错误revision条件写均收到服务端Unauthorized/native5/retryNone，index未变；正确CLI可读/启动。只证明caller拒绝，不冒充已通过认证后的role/target/SID隔离测试 |
| SEC-03 / P1 | 客户端拒绝伪造 VA/server image、错误 PID 或 PID 重用 | NOT RUN | 部署前置已满足；记录实际拒绝点 |
| SEC-04 / P2 | reparse/hardlink/源替换不能重定向特权写入或执行 | NOT RUN | 部署前置已满足；仅隔离 fixture，正常权限不能放宽来完成测试 |
| SEC-05 / P1 | VA 无 SCM ChangeConfig/WriteDac/WriteOwner；SYSTEM 不执行 VA 可写 live Code 作为维护入口 | NOT RUN | P；实际服务/维护路径验证 |
| SEC-06 / P0 | 缺失/错签名/错 catalog/混包 fail-closed；无生产 Debug/test-root 旁路 | NOT RUN | P；本地 exact-pin 不能代替正式信任策略 |
| SEC-07 / P1 | 不同有效 Microsoft production leaf 可独立接受；不要求跨文件 leaf 相同或手工 allowlist | NOT RUN | P；历史签名成功只作参考 |
| SEC-08 / P2 | 用户根、仿 publisher、自签名、错误 EKU/test root 不能授权生产代码/catalog | NOT RUN | P；不往工作机信任库导入测试根 |
| SEC-09 / P2 | CMS 缺失/替换/重复/非法 timestamp 拒绝；签名绑定真实 RFC3161，不信任自报 signingTime | NOT RUN | P；需要正式/专用密码学 fixture |
| SEC-10 / P2 | 有效签名时间戳跨 signer 过期仍有效；已吊销拒绝，离线未知与无效信任区分；正常离线数据访问可用 | NOT RUN | P；不改工作机时钟；本地 cert lease 修复不是本项证据 |
| SEC-11 / P1 | CI 与产品共用生产 verifier；未知/旧 policy 明确拒绝，mock/local key 不弱化正式包 | PARTIAL | AUTO 仅部分证据；P 的最终打包字节/配置仍待核验 |

## 9. UX、性能、blob 与重置工具

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| UX-01 / P0 | 区分未设置、服务停机、存储恢复、arranger 身份/通信、UAC 取消、应用启动失败；日志保留阶段/native error | PARTIAL | E37结果保留。E41 list的OwnerContextRequired/1346被泛化；E44 Medium host内的token DACL native5先映射Unauthorized，再被host的std::exception catch丢为internalError/exit1。原始错误传递仍需修复，不能把它们诊断成runtime停机 |
| UX-02 / P0 | Launch & Edit 连点不产生第二次启动；页面 busy 可恢复，失败保留草稿，busy/failed 非“坏数据” | NOT RUN | 部署前置已满足；历史 H03 不直接沿用 |
| UX-03 / P1 | 进度 UI 冷启动能收到状态；关闭/取消无挂死；Import/Export/Reload/Retry cleanup 作用明确 | NOT RUN | 部署前置已满足；Reload 不重新预配，Retry cleanup 不做 blob GC |
| UX-04 / P1 | 中英文、长路径/错误文本等可读且本地化；无 console/JSON 内容泄漏或无响应错误循环 | NOT RUN | 部署前置已满足；真实 UI 观察另留证据 |
| PERF-01 / P1 | 正常 launch/arranger 不重复全量枚举；记录 metadata/首应用/arrangement 分段耗时，legacy fallback 有说明 | NOT RUN | 部署前置已满足；另见 R03；用同机同数据比较，不凭印象认定更快 |
| PERF-02 / P1 | Capture 独立量测全量枚举和 UI 响应；不得因 launch 优化宣称 Capture 已改善 | PASS | 量测范围：E26，清单 47.152 秒/整体约 47.293 秒，进度可见。**不表示性能达标或已加速**；R03 性能问题保留 |
| PERF-03 / P0 | Capture恢复秒级可用，不靠只显示进度或丢失必要应用元数据；记录真实点击Capture到可编辑界面耗时 | PASS | L64 E33：6006实际点击到可编辑页面4982ms（含驱动开销）；自有Win32/包应用身份及状态匹配。限本次实机样本，不是固定延迟、P95或原版main二进制A/B；E29/E32函数计时单独保留 |
| UX-05 / P0 | Create/Capture 等待期间显示进度和说明，不允许第二次 Capture；完成/失败后恢复可操作状态，进度窗不遮挡错误对话框 | PARTIAL | E37补齐正常Capture pending：Capture/Cancel禁用、progress可见、原生owner禁用；重复Invoke未完成，WM_CLOSE同步处理后窗口仍在且仅观察到一个snapshot子进程，随后正常完成/取消。实际Capture失败及错误窗遮挡/Retry仍未完整测 |
| BLOB-01 / P1 | 正常保存先 durable 新 generation，再原子发布 index；旧 generation 正常可删除，保留 live 数据 | NOT RUN | 部署前置已满足；blob 不是每个 workspace 一份，也不是备份历史 |
| BLOB-02 / P2 | 人为阻止旧 blob 删除/中断提交后重启；Recover 验证有效 index/live generations 后回收 orphan | NOT RUN | 部署前置已满足；当前无周期性 GC/每次删除失败诊断，不把即时全部删除当已实现承诺 |
| BLOB-03 / P2 | index/live 数据损坏时先报告恢复状态，不在验证前误删仍需保留的文件 | NOT RUN | 部署前置已满足；仅 disposable 数据 |
| CLEAN-01 / P1 | reset 的 WhatIf/备份/范围验证正确；备份成功后才删准确 owner 的测试服务/Code/Data；重复执行安全 | NOT RUN | 部署前置已满足；历史 H05 不证明新 helper 完整通过；默认不清所有历史 prototype |
| CLEAN-02 / P1 | profile loaded、服务未停、Code 无法删、结果未知分别报告；不强卸 hive、不吞错误、不误伤其他用户/原型 | NOT RUN | 部署前置已满足；另见 R04；权限拒绝不是“对象不存在” |
| CLEAN-03 / P1 | PrepareMigration 生成合法旧格式、缺 ID 等真实 fixture 到 local legacy 位置；不预先消费迁移 | NOT RUN | 部署前置已满足；当前 6002 已有迁移记录，不能把 10-23 的准备状态当作现在仍未初始化 |

## 10. 正式升级、兼容和恢复

本节必须使用正式包及真实 Windows Installer 状态；本地 helper 的 repair 不提供替代证据。

| ID / 优先级 | 操作与通过标准 | 当前状态 | 前置 / 注意事项 |
|---|---|---|---|
| UPD-01 / P0 | A v1 -> v2 真实 MSI major upgrade；注册及两个 live PE 更新，VA、固定 ImagePath 和数据不变 | NOT RUN | B02；区分主安装 UAC 与常规 owner 服务更新，后者不新增 UAC/普通重启 |
| UPD-02 / P0 | B 升级而 A 留 v1；A 的服务版本/数据不变 | NOT RUN | B02；P-XU/P-AU |
| UPD-03 / P0 | A 随后升级；B 持续健康，双方集合正确 | NOT RUN | B02 |
| UPD-04 / P1 | 用 v1 入口 repair 已注册 v2；修复正确 release，不重播内嵌旧 seed，不重新初始化 | NOT RUN | B02；健康同版本 sync/普通 repair 不重复首次授权 |
| UPD-05 / P2 | candidate readiness 失败；已知 pre-commit 失败恢复旧代码，无混合注册/live-code 假成功 | NOT RUN | B02；隔离快照 |
| UPD-06 / P2 | commit ack 丢失；查询同一 operation，不回滚已确认 committed 的更新 | NOT RUN | B02 |
| UPD-07 / P1 | 真实 MSI 1618/服务 busy；有限等待、明确 Retry，无嵌套 MSI/无限轮询 | NOT RUN | B02；欢迎页停留不算 MSI 事务占用 |
| UPD-08 / P2 | Bootstrap 损坏；普通维护不假成功，显式授权恢复启动组件且保留数据 | NOT RUN | B02；不承诺修复任意坏业务记录 |
| UPD-09 / P0 | per-user 主更新后，在原 owner 的正常上下文协调 carrier | NOT RUN | B02；不让主安装结果代替 owner runtime 可用性 |
| UPD-10 / P0 | per-machine 交互主更新；共享主文件更新，carrier 仍属于原普通业务用户 | NOT RUN | B02 |
| UPD-11 / P2 | SYSTEM/企业无人值守主部署；不创建 SYSTEM-owned per-user carrier，owner 工作明确延期 | NOT RUN | B02；需要真实部署通道 |
| UPD-12 / P1 | 另一 owner 离线；无强制全用户更新期限，登录后按其上下文协调兼容性 | NOT RUN | B02；机器版主文件共享，不要求主程序路径按用户保留旧版 |
| UPD-13 / P1 | A 维护失败时其他模块/B 不受误伤；可诊断、可恢复，不要求重装整个 PowerToys | NOT RUN | B02 |
| UPD-14 / P2 | MSI/VA 提交前后进程终止、重启/断电；恢复或明确 recovery-required，无伪原子性 | NOT RUN | B02；专用 VM/故障注入，未知结果不盲目重放 |
| UPD-15 / P0 | 从**已含 machine-uninstall hook** 的旧版升级；旧 MSI 的升级卸载不得误清全部服务/数据 | NOT RUN | B02；仅 3000 -> 3001 不够，3000 无 hook；旧 3001 或等价基线 -> 后续版本才覆盖 |

## 11. 正式卸载、保留数据与清理

| ID / 优先级 | 操作与通过标准 | 当前状态 | 前置 / 注意事项 |
|---|---|---|---|
| REM-01 / P0 | 正常卸载 B；B 的注册/服务按 scope 移除，A 的服务/版本/数据不变 | NOT RUN | B02；两个真实交互 owner |
| REM-02 / P0 | KeepData 卸载重装；原集合、epoch/初始化状态恢复，不从旧明文覆盖或重置为空 | NOT RUN | B02；重新预配可需授权，不等于普通升级需 UAC |
| REM-03 / P2 | 显式 PurgeData 仅删授权 owner 的业务数据并记录 import suppression | NOT RUN | B02；破坏性，只用可丢弃数据；不删真实用户账号/profile |
| REM-04 / P0 | 正常机器版卸载实际触发全 owner 清理；新 report 覆盖 A/B，默认 KeepData | NOT RUN | B02；检查新 operation/time，不能拿旧 report 或退出 0 代表零残留 |
| REM-05 / P1 | owner 离线导致 carrier 注册无法清理时，准确报告 residual，不手删 MSI 内部注册、不冒充用户嵌套安装 | NOT RUN | B02；允许设计内 best-effort 残留，不等于全量清理 PASS |
| REM-06 / P1 | VA profile loaded 时报告 pending/原生码，解除占用后可重试；不强制 hive unload | NOT RUN | B02；service 删除不代表 profile 删除 |
| REM-07 / P1 | 仅清除已验证、当前 owner、terminal 的 helper stage；其他 stage 和未知结果证据保留 | NOT RUN | B02 |
| REM-08 / P1 | 授权取消、访问拒绝或未知移除结果不误报成功/不存在；保留准确残留及恢复入口 | NOT RUN | B02 |
| REM-09 / P1 | 已移除后重复清理幂等，不重建资源，不误删别的实例 | NOT RUN | B02 |
| REM-10 / P2 | remove/rollback 失败仍有可恢复 Code/Data，不使成功的 peer 安装失效 | NOT RUN | B02；快照环境 |
| REM-11 / P0 | per-user 正常外层 EXE/“已安装应用”卸载触发 owner 回调，或明确报告保留的服务/carrier | NOT RUN | B02；直接卸载内层主 MSI 绕过外层回调，不能替代此项 |

## 12. 环境矩阵、发布门槛和推荐顺序

| ID / 优先级 | 范围与通过标准 | 当前状态 |
|---|---|---|
| ENV-01 / P1 | 默认/自定义安装目录、空格/非 ASCII 路径；权限/固定映像/目录身份正确，不从下载目录执行特权代码 | NOT RUN |
| ENV-02 / P1 | GPO 禁用/启用、普通模块开关、无正常 Explorer/Session 0/不同用户 shell；按合同拒绝，无提升 fallback | NOT RUN |
| ENV-03 / P1 | 多显示器、不同 DPI、断接显示器、move-existing、最小化/最大化；记录实际硬件与排列 | NOT RUN |
| ENV-04 / P0 | 正式 x64/ARM64 × per-user/per-machine，以及支持的 Windows 10/11 组合分别留运行证据 | NOT RUN |
| REL-01 / P0 | Q02 的授权时机、fail-closed 范围和 UX 获 PM 确认 | BLOCKED |
| REL-02 / P0 | 完成正式签名、原始 MSI tables/ICE、所有适用 P0 及必要异常/恢复、安全评审；本地 WIX1105 表示 ICE 未执行，不是 PASS | NOT RUN |

| 正式矩阵 | 当前状态 | 版本/OS/账号证据 |
|---|---|---|
| P-XU | NOT RUN | 尚无本次集成签名包的已安装验收记录 |
| P-XM | NOT RUN | 同上 |
| P-AU | NOT RUN | 同上；必须 ARM64 实际执行 |
| P-AM | NOT RUN | 同上；必须 ARM64 实际执行 |

### 执行视图：工程门禁加六个工作包

第1节的七个统计类别用于回答“要求分布在哪里”；这里按实际操作组织工作，
替代原先重叠的全量/Debug两套执行顺序。工作包不是新增验收ID，也不是只有六个
独立case：每包包含若干场景和检查点，同一次操作可以为多条要求提供证据。
不能把“包已开始”或其中一个场景PASS当成整包完成。

**前置工程门禁：** 按受影响项目批量构建、跑单测/协议/authoring检查，并把同一份
结果映射到对应AUTO/UNIT/BUILD条件；不要为了每个ID分别重跑相同套件。
当前工程类20/27已整项通过，剩余生产配置/打包等缺口分开补。
任何实机包先核对实际客户端/runtime/catalog及程序集，不为切换case反复清VA或Reset。

| 工作包 | 一次准备后串起来执行的主要场景 | 主要对应要求 | 当前缺口和环境 |
|---|---|---|---|
| A 日常业务回归 | 创建/Capture→编辑保存→重开→GUI/CLI启动→复用/重捕获；串接Import/Export、坏输入、CAS、取消及结果/时间字段检查 | LOCAL-03；DATA-09～13、15～17；CLI-01/02/05/06；UX；PERF | E33/E36/E37已覆盖较多，不从头重跑；补R07修复回归、未保存/重捕获后Save、GUI取消/Ctrl+C等明确缺口。多数可用当前Debug；CmdPal须匹配本地host/catalog |
| B 权限边界与A/B隔离 | 普通/High前端×普通/提升目标的关键组合；同账号后补OTS；复用不匹配窗口、授权取消；同一批Data/Code/client身份探针再覆盖A/B隔离 | DATA-14；CLI-03/04/07；INST-07/08；ACL；SEC-01～05；ENV-02 | 下一阶段最高优先级。普通owner拒写/caller拒绝已有证据，缺真实High、管理员、第二账号/VA及身份分支；需相应授权和真实登录环境。Debug能测机制，但不关闭正式安装注册条件 |
| C 数据迁移与恢复 | 在可还原基线中做首次迁移、坏源、合法空集合、不再导入、清理失败、CAS/提交中断、操作结果查询、blob回收及服务/系统重启 | DATA-01～09、15、18；BLOB；CLEAN；LIFE-01及相关恢复子条件 | 主要剩余数据风险；需专用账号或隔离VM。空集合/旧文件和CAS已有部分证据；首次迁移及故障边界仍缺。不能在当前store手删index冒充首次初始化 |
| D 安装升级卸载旅程 | 按安装scope执行：首次安装/预配→第二用户→A先升级/B留旧版→B升级→repair→单用户卸载/KeepData重装→机器级清理；每阶段检查注册、VA、Code、数据和peer状态 | LOCAL-01/02/04；INST-01～06；UPD；REM；LIFE-01 | 条目最多，但不是每条做一次全新安装。32个正式安装/升级/卸载条件需当前正式包和配对旧版；本地helper只能补L64机制。特别保留UPD-15旧卸载hook检查 |
| E 生产信任与最终产物 | 围绕同一发布候选和受控签名fixture执行有效/非法catalog、身份、timestamp、吊销/离线、生产配置及原始MSI字节检查 | SEC-06～11；BUILD-02/04及相关生产构建；REL-02的证据组成部分 | 需要生产verifier路径和适用产物；Debug/local signer、mock或旧包不能代替 |
| F 环境兼容与性能收尾 | 选择明确的安装路径、架构/OS、屏幕/DPI/断接配置，重跑与该维度有关的核心路径；补冷暖/重复性能样本 | ENV-01/03/04；PERF及受影响业务项 | 要有真实设备/环境；不默认每个组合机械重跑126条，也不把单屏/一次4.982秒当全矩阵通过。正式发布组合仍须分别留证据 |

**当前Debug阶段的顺序：B的可执行部分→C的隔离数据场景→A剩余回归→F可用硬件子集。**
工程复测可与准备工作协调进行；不要因UAC/第二账号暂不可用而重复已通过的普通CRUD。
D/E的正式条件等对应包/环境准备好后执行；不要求Debug阶段先把它们全部关闭。
故障注入/断电安排在正常路径之后，且只用隔离或可丢弃实例。

### 怎样复用证据，而不减少必要覆盖

| 可以合并的执行 | 能复用的证据 | 不能省略的差异 |
|---|---|---|
| 一次相关自动化套件 | AUTO-03/04及其覆盖的UNIT条件共用同一结果文件 | 单测不代替真实服务、权限、安装或断电场景 |
| 一次真实启动并收集结果/进程/窗口/时间 | DATA-13/15、CLI-02、PERF-01等可引用同一case的不同观察 | GUI/CLI入口差异、普通/High差异仍按各自要求留证据；缺观察的条目不能自动PASS |
| 一套文件权限探针 | ACL-02、SEC-01相关子条件共用访问结果 | 普通owner不能代表管理员/另一用户/另一VA；Code不可读不是Data可读要求失败 |
| 一次真实CAS冲突及恢复 | DATA-09与UX-01相关子条件共用错误/草稿/保存结果 | CAS单测、服务停机及其他错误类别不是同一个场景 |
| 一套A/B环境及连续生命周期旅程 | 安装、分批升级、卸载、peer隔离等检查复用账号/fixture/快照 | 不同生命周期动作仍须真正执行，不能用安装成功推断升级/卸载安全 |
| 一次KeepData卸载重装旅程 | REM-02和DATA-07的对应数据/初始化条件复用前后快照 | 不代替OS重启、其他scope或其他owner的结果 |

减少的是重复搭环境、重复执行和重复采证，不是删除未验证的边界。原ID、状态和历史证据
保持可追溯；只有明确重复或不适用的要求经过范围确认后才归并/标N/A，不能靠删行提高通过率。
REL-01是PM决策，REL-02是汇总发布门槛，ENV-04是覆盖矩阵；它们不是额外的一次点击测试。
汇报时同时给出“条件状态”“工作包已覆盖/缺失场景”“前置环境”，不要只报26/126。

分类/重排本身没有执行新测试或改变状态；后续E39工程复测另记新证据和状态，
不改写E33/E36/E37封存报告。

三类 UAC 分开记录：主程序机器安装授权、storage 首次设置/特权恢复授权、工作区目标应用提权。
另有 Workspaces 目标 EXE 签名确认 UI，它也不是 Windows UAC。
不能通过“看见/没看见一个框”推断目标进程已提权；必须核对新进程的实际身份/IL。

当前承诺是 Q01 的受信任 writer 模型：阻止直接篡改和未授权写入，不把“已经获授权的
module 被完全控制后的行为”虚构成已被 ACL/UAC 解决。不同 scope 的风险接受需单独记录。
Q10 的“直接验证原始 MSI”仍是独立验证候选，未经另行立项不纳入本轮必须验收范围；
其他 VA/MSIX/alias 等旧原型的结论也不自动计入当前方案。
出现越权写入、跨 owner 混淆、legacy fallback 或无提示数据丢失时，立即停止该场景、保留
证据并记录 FAIL；不要先改 ACL、take ownership、清 registry 或反复重装来抹掉现场。

### 旧人工清单对照

| 旧人工 ID | 本台账对应项目 |
|---|---|
| H:PRE-01；H:SET-01～03 | BUILD-01～05、INST-01～03/06/08、LOCAL-01～03 |
| H:DATA-01～06；H:LIFE-01 | DATA-10/13/14/11/12/17/16、INST-07、LIFE-01 |
| H:PU-01～04 | INST-04、REM-01/02/08/11、SEC-02 |
| H:PM-01～06 | INST-02/04/08、REM-02/04/05/06、UPD-15 |
| H:MIG-01～04 | DATA-01/02/07/04/06 |
| H:UPD-01～04；H:REP-01～02 | UPD-01～04/08～12、INST-05 |
| H:SEC-01～02；H:FAIL-01～04 | SEC-01/02/10、ACL-01～04、DATA-09/10/18、UPD-07/13 |
| H:EXT-01～05 | UPD-11/14、SEC-07～11、REM-03、ENV-01～04 |

旧 3001 人工清单中“owner/admin 不能读 Data”的描述只适用于当时的旧版本；
当前要求以 ACL-01～04 为准。旧版本结果不得因此直接改写为新版本通过。

## 13. 证据索引、历史参考与执行记录

### E33：2026-10-30，指定远端WIP的6006正确性验证

输入为 `LegendaryBlair/PowerToys` 的 `wip/powertoys-verification-20260819`，
SHA **`15083b0ac8f66cd6f9d70ee9216a8f989d1e5eb3`**，与用户截图一致。
只提取 skill/profile，没有合并WIP产品代码。按 **Scenario B** 冻结3个变更相关父项、
14个子项，不冒充38项完整发布验收。运行ID：`1fb41cc83f774907b948f4935b20ca66`。

| 子项 | 结果 | 证据与范围 |
|---|---|---|
| CAPTURE/entry | PASS | 真实Settings入口恢复同一Editor，无重复实例；观察56 |
| CAPTURE/cancel | PASS | 取消返回原列表，index不变；观察73 |
| CAPTURE/identities | PASS | 三个自有Win32窗口和包Paint各一次，含晚创建fixture；观察423 |
| CAPTURE/geometry | PASS | 120 DPI坐标、普通/最小化/最大化匹配，预览已审阅；观察424 |
| CAPTURE/singleFlight | BLOCKED | 观察152证实Capture禁用及完成；833另见进度文字/bar，但6006完整冲突操作/主窗禁用组合未独立观察 |
| EDIT/saveReopen | FAIL | 保留应用/编辑值，但创建时间在编辑链中丢失；观察866及原始捕获/保存证据，R06 |
| EDIT/appProperties | PASS | Remove/Add back、自定义位置/参数、显式最大化/最小化Save/reopen；其他三行未变，观察481/905 |
| EDIT/discard | PASS | Cancel、breadcrumb两条路径均未改保存记录；观察518 |
| EDIT/renameShortcut | PASS | 新名称/搜索/快捷方式、旧名称清理及图标；观察553/964 |
| EDIT/preserveOthers | BLOCKED | 自有卡/快捷方式/图标已删，原两条摘要仍在；缺少完整基线且blob哈希不同，不宣称全字段保真，观察964 |
| LAUNCH/normal | PASS | 四个新PID及实际普通token，Paint确实打开自有PNG，窗口达到保存状态/矩形；观察754 |
| LAUNCH/reuse | PASS | 开启时同HWND移动且无重复；关闭时原窗口保持错误位置、新自有窗口定位；观察705 |
| LAUNCH/recapture | PASS | native完成后捕获新窗口，Revert回4行、Cancel回列表；观察833。快捷方式显示偏差另列R07 |
| LAUNCH/elevation | BLOCKED | 非管理员上下文按skill跳过，不触发UAC；不是High/OTS通过 |

**10 PASS / 1 FAIL / 3 BLOCKED；父项0 PASS / 1 FAIL / 2 BLOCKED；总体WITHHELD。**
**归因更正：** 初版E33把R06/R07均归为继承main，R06这一判断已撤回。
相同的复制构造函数不代表相同的保存行为：R06是分支直接保存编辑副本引起的回归，
R07的同样绑定顺序则自2024年存在。详见E34；原测试观察与10/1/3结果不变。
脚本选错控件、缺等待、无效CLI选项、Paint最小尺寸约束和额外Paint实例计数等，
均保留为驱动/fixture问题，不算产品FAIL。

清理：5个当前自有fixture进程均正常关闭，无剩余fixture进程；自有fixture目录、
两个快捷方式和图标均移除。借用Editor恢复空搜索/原列表/原最小化位置；
Settings恢复Dashboard、展开/滚动/位置；原前台和鼠标位置恢复。
canonical resource receipt仍有B07所列5个hold，没有删除/重写旧receipt来换取通过。
原业务数据未做原始文件回滚；B06明确保留。

归档目标（完整固定导出的 `report.md`、`details.md`、`results.json`、
`statistics.json` 及manifest/证据）：

```text
C:\Users\bozhang\OneDrive - Microsoft\PowerToys\Module-Signoff\Workspaces-6006-20261030-WIP15083b0\report.md
```

### E34：2026-10-30，R06/R07引入历史与main归因更正

用户追问是否由近期Workspaces PR引入后，重新fetch并检查main
`397c491b4ff60ba3263e3f4eedbe1783413c35e2`（10-30），追踪完整
`EditProject -> SaveProject -> SerializeWorkspaces` 调用链，而不只比较模型文件。

| 问题 | 可定位的引入历史 | main与分支的区别 |
|---|---|---|
| R06 创建时间清零 | 模型复制漏时间本身来自初始PR #34324；真正触发持久化清零的保存策略改动在protected-storage提交 `f1362af1cd1a179525b58c4af744237c79bb1bea`（09-24），由 `e04dbf97a2ceb65d51905e80bbb41acf03c1c997`（10-28）带入当前集成分支 | main保留原对象 `editedProject`，只回填编辑字段，最后序列化 `Workspaces.ToList()`，原时间仍在。分支 `SaveProjectAsync` 将 `projectToSave` 编辑副本替换进 `candidates` 并先序列化；该副本时间默认0，因而真实写入0 |
| R07 快捷方式勾选显示 | `editPage.DataContext = project` 在 `CheckShortcutPresence(project)` 之前，且属性无通知，均可追溯到 `579619952da5f6c56d9c1fac3f50fa6f8edce1cd`，初始Workspaces PR [#34324](https://github.com/microsoft/PowerToys/pull/34324)，2024-08-23合并 | 当前main和分支保留同样的绑定顺序/非通知属性；不是近期CLI/shared-session PR新引入的顺序 |

PR [#51114](https://github.com/microsoft/PowerToys/pull/51114) 于2026-10-27合并，
提交 `06e6ae43e2` 在 `MainViewModel.cs` 只调整SnapshotTool/Launcher的
`ModuleProcess.CreateStartInfo` 调用，未改上述保存策略或复选框绑定顺序。
R06不能归咎于该PR，也不是10-29的Capture快速发现提交 `b132ee3fbf` 首次引入。

定位：当前 `ViewModels\MainViewModel.cs:175-178` 的候选集合保存，
`Models\Project.cs:201-229` 的复制构造函数，
`ViewModels\MainViewModel.cs:287-288` 的绑定顺序。
本次仅作源码/历史核对和文档更正，没有修改产品代码或新增原版main的实机运行证据。
E33封存归档保持不变，其中“创建时间问题继承main”的归因应以本节更正为准；
创建时间清零的实测FAIL仍成立。

### E35：2026-10-30，修复编辑保存的创建时间清零

仅修改 `WorkspacesEditor\Models\Project.cs` 的复制构造函数：复制原有
`CreationTime` 和同一保存路径上的 `LastLaunchedTime`，不生成新时间、不修改
保存/CAS协议，也不修改快捷方式显示。已经写成0的历史数据不会自动推测恢复。

在现有 `WorkspacesCsharpLibrary.UnitTests\WorkspaceWriteTests.cs` 添加
`EditingCopyPreservesTimestampsInSavePayload`：构造有时间元数据的工作区、
创建并改名编辑副本，调用实际 `WorkspacesEditorIO.ToWrappers`，
检查JSON保存payload中的名称、创建时间和最近启动时间；覆盖从未启动的0哨兵及非零时间。

通过仓库 `tools\build\build.cmd -Platform x64 -Configuration Release` 构建
测试项目及Editor依赖，随后用VS `vstest.console.exe`：

| 阶段 | 结果 | 本地证据 |
|---|---|---|
| 修复前，仅新增回归 | 2例均FAIL，Expected 1793332734 / Actual 0 | `oct30-timestamp-results\timestamps-red.trx`、`red-build.log` |
| 修复后，完整WorkspaceWriteTests | 8 PASS / 0 FAIL，包含新增2例及原6例写入行为 | `oct30-timestamp-results\timestamps-green.trx`、`green-build.log` |

证据根目录同本节下方记录。本轮未更新运行中的Debug6006、服务或catalog，
未操作原业务工作区；没有重新执行E33 GUI场景，也没有改写其封存报告。
因此AUTO-14 PASS，DATA-10 RETEST，而不是宣称已部署修复或完整验收通过。

### E36：2026-10-30，创建时间修复的真实Editor验证

独立Scenario B运行 `90384354` 前缀，沿用指定WIP skill/profile。
本轮开始时没有Editor进程，先保存完整protected blob、index及原两条记录详情，
再只增量发布受影响的LocalE2E Debug Editor。
入口EXE仍为原catalog认可的同版本/同哈希，managed Editor DLL从
`D0AFE1CA...`变为`6207D44B809AD83AA1165CE1E869057AF7F1306460D2FE06447E8ABBF6651BA9`。
没有改catalog、维护服务、清VA、Reset或请求UAC；runtime身份保持不变。
不能据此泛化为所有增量构建都不需要更新服务/catalog，必须逐次核对。

| 实际阶段 | 创建时间 | 最近启动时间 | 结果 |
|---|---|---|---|
| Capture后创建自有工作区并Save，CLI读取完整保存记录 | 1793347374 | 0 | 正时间、9个捕获应用；未启动这些应用 |
| 第一次改名、Save、显式Reload、重开，CLI再次读回 | 1793347374 | 0 | 名称更新；其余全部字段与创建时一致 |
| 第二次改名、Save、显式Reload、重开，CLI再次读回 | 1793347374 | 0 | 同上；两次实际编辑页截图已审阅 |
| 通过自有卡More → Remove删除测试卡 | — | — | 原两条记录逐字段一致；完整业务payload同为2225字节且SHA256一致 |

原业务payload SHA256：
`582212232F6DFF41B693A89873D1F24701EBA83CDAFB1FE4A1C3B98D40C0BBB8`。
完整blob字节不同不是业务数据改变：按PTPS格式解码后，业务payload完全相同，
record信封的revision sequence从30到34，operationId/requestDigest随四次合法写入变化。
没有通过直接覆盖blob/index来获得相等结果。

清理：自有Editor正常退出，无Editor/SnapshotTool测试进程残留；三个测试名称的
快捷方式及自有图标均不存在；原前台与鼠标位置恢复；canonical resource检查17项通过。
保留修复后的开发程序集，用户下次从该Debug目录打开Editor即使用此修复。
本轮覆盖新建工作区的last-launched=0；非零最近启动时间另有E35保存payload单测，
不冒充本轮实际启动测试。R07、Capture完整互斥、High/OTS及完整DATA-10矩阵未重跑。

本轮封存目标：

```text
C:\Users\bozhang\OneDrive - Microsoft\PowerToys\Module-Signoff\Workspaces-TimestampFix-20261030\report.md
```

### E37：2026-11-02，本地Debug可执行权限与业务批次

用户明确允许本机Workspaces测试数据改动/删除。本轮仍先保存完整原始数据，
使用专用Win32空白窗口fixture，不启动/关闭用户文档应用；没有改产品代码、
服务/VA、catalog、账号、机器时钟或生产信任。样本为6006加E35时间修复，
匹配的Editor DLL仍为`6207D44B...`。初始前台不可用但UIA/窗口截图可用；
用户恢复可用窗口后另存有效桌面基线，没有伪造初始前台。

| 已执行组 | 实际结果与边界 |
|---|---|
| 文件访问 | 48次真实CreateFile权限探针：3次Data读取成功；40次Data/Code修改、追加、删除、WRITE_DAC/WRITE_OWNER均为Win325；5次Code读取被拒符合私有Code策略。无内容写入/ACL变更 |
| caller拒绝 | 未注册进程加载真实本地client，正常server身份验证后，读及安全的错误revision条件写均收到原始服务端Unauthorized/native5/retryNone；index不变。未隔离测试后续role/target规则 |
| Capture正常互斥 | 实测Capture/Cancel禁用、progress文字/bar可见、原生owner禁用；重复Invoke没有完成；同步WM_CLOSE被处理但pending窗保留，snapshot仅观察到1个，完成后回到可编辑页并取消 |
| Import/Export | 真正Export逐字段一致；修改那个实际导出文件，live保持不变；确认Import完整替换自有fixture；坏JSON/错误集合类型均无部分写入；修正同一文件后Retry成功 |
| CAS与历史 | 外部CLI真实启动写回导致旧Editor Save冲突，草稿保留、最新记录不被覆盖；Cancel/Reload后保存成功。实际非零last-launched在后续编辑后仍为1793592866，创建时间仍为1793592076 |
| CLI合同/启动 | text/JSON/id/name/details及错误输入实际执行；exit0/2/3与结果相符。新普通目标token、参数标题及144 DPI位置(180,240)-(1080,840)匹配；最终JSON为completed/arranged/updated |
| deadline与会话释放 | 2秒deadline约2826ms返回timeout/exit9；已启动的FIRST进程保留，SECOND未启动；后续独立正常启动exit0并arranged，未把未知结果盲目重放 |
| 合法空集合 | Retry导入空集合成功；放回不同ID的legacy文件，再关闭/重开Editor并Reload，仍为0条，没有再次导入；legacy文件随后恢复。不是首次迁移/OS重启/KeepData重装验收 |
| 收尾 | 经真实Import恢复原两条工作区：全部字段、完整JSON和payload字节均一致；settings及有效桌面前台/鼠标恢复，测试进程0、夹具目录已删除。最终额外6次CLI读取前后index/blob全部字节及文件名不变 |

选择的15个子项：**12 PASS / 2 BLOCKED / 1 FAIL(checklist)**，
**没有新增产品FAIL**。清单FAIL是本轮错误地把私有Code也列入owner可读要求；
不改ACL来迎合清单，也不改写冻结输入以隐藏定义错误。
两个BLOCKED是提升目标及High前端变体：实际token非管理员，按指定WIP默认规则跳过，
本轮未发起UAC。双账号/OTS、服务停机、首次迁移、正式安装等不能由本轮结果替代。

另保留记录完整性限制：第一次CLI实际上完成，但跟踪器漏掉短命宿主而超时，
连带3条activation receipt未解析。已按完整operation结果、专用路径、出生时间、
精确窗口身份独立确认并正常关闭那一个自有空白窗口；后续新的正常启动取得完整
frontend/target收据和exit0证据。旧receipt和未完成artifact注册记录不人为重写，
因此总体报告仍WITHHELD；这不等于当前仍有测试进程残留。

主台账在明确的L64范围关闭DATA-09、DATA-17、CLI-01，整项PASS由21变为24。
管理员/跨owner、全错误矩阵、正式安装及不同环境仍按原条件保留；Capture的实际失败路径
和R07快捷方式显示问题没有被顺带判通过。

归档目标：

```text
C:\Users\bozhang\OneDrive - Microsoft\PowerToys\Module-Signoff\Workspaces-L64-20261102\report.md
```

### E38：2026-11-02，推送集成分支并启动正式安装包构建

用户要求本地验证与主构建流水线并行推进。先提交已经通过E35/E36/E37验证的
时间保留修复和回归测试（`890cc68af1`），再提交验收记录与分类整理
（`6bd56af1472cd876abc6c2608fbf9888767d24d3`）。集成分支已推送到
`microsoft/PowerToys` 和 `LegendaryBlair/PowerToys` 的同名feature分支；
没有合入或直接推送main。

通过现有 `PowerToys Signed YAML Release Build`（definition `76541`，
`.pipelines/v2/release.yml`）启动
[ADO 162529587](https://dev.azure.com/microsoft/Dart/_build/results?buildId=162529587)：

| 字段 | 已核对值 |
|---|---|
| Source branch / commit | `refs/heads/feature/workspaces-protected-storage-main-20261028` / `6bd56af1472cd876abc6c2608fbf9888767d24d3` |
| 版本、配置、架构 | `0.101.6007.0`；Release；x64和ARM64 |
| 发布范围 | `private-validation` / `private`；不发布公开symbols，不触发preview发布 |
| 启动结果 | build API确认`inProgress`且branch/commit匹配；timeline已开始Build阶段 |
| 原始证据 | `nov02-installer-6007-queued.json`、`nov02-installer-6007-timeline.json` |

`az pipelines run`返回的是run对象，源码身份位于
`resources.repositories.self.refName/version`；不能把它的空
`sourceBranch/sourceVersion`误判为错误分支或重复排队。已另用build API核对。
本条只证明成功触发且构建身份正确，不证明签名/ICE/安装产物或实际安装通过。
B02及所有P环境验收仍保持原状态；最终产物必须另行核验。

**16:03（UTC+08:00）后续状态：** x64的`Build Protected Storage Carrier`
任务失败，日志229在ICE01/ICE02等报WIX0217，原文为
`The Windows Installer Service could not be accessed`。这是CI构建机的MSI验证执行错误，
不是E41本机CLI的owner-context错误；尚未确认为具体MSI authoring违规。
ARM64任务及整体run仍在执行，未宣称最终产物可用，未禁用ICE或自动重复排队。
原始证据为`nov02-installer-6007-x64-failure-timeline.json`、
`nov02-installer-6007-x64-carrier.log`。上述为当时观察；最终状态和产物核对见E42。

### E39：2026-11-02，构建期间补齐本地工程复测

在E38签名构建运行期间，针对当前产品源码 `6bd56af147` 执行独立x64 Release
工程门禁，不创建新的WIP GUI验收run，不重开或修改封存的E33/E36/E37。
使用仓库build wrapper；测试项目构建成功后用VS `vstest.console.exe`，
没有运行 `dotnet test`、本地部署helper或已安装产品维护。

| 工程门禁 | 实际结果 | 证据 |
|---|---|---|
| 默认原生Workspaces套件 | 显式 `WorkspacesCliLiveTests=false`；重新编译后193/193通过，0失败/跳过 | `nov02-engineering-results\native-build.log`、`native-default.trx`、`native-test.log` |
| Native生产helper编译/契约 | `Build-Carrier.ps1 -Platform x64 -Configuration Release -Version 0.101.6007.0 -ReleaseStage Compile -RunTests`；Setup/Lifecycle/ProvisionBroker/MsiAction编译和4,129契约检查通过 | `nov02-engineering-results\carrier-compile.log` |
| Carrier authoring和MSI表 | 编译非安装fixture MSI，核对RemoveFile/Media；本机WIX1105阻止ICE，不能记ICE PASS | `nov02-engineering-results\carrier-authoring-fixed.log` |
| 本地测试环境保留 | Debug6006 Editor EXE/DLL及CLI的前后SHA256相同；Authoring临时目录已清理；无服务、账号、证书或MSI注册变更 | `nov02-engineering-results\debug-bits-before.json`、`debug-bits-after.json`及契约检查输出 |

首次authoring执行已编译MSI并检查表，但读取旧的项目目录日志路径时报不存在。
当前wrapper将日志放在 `artifacts\logs\<project>`，因此修正
`Tests\Test-Authoring.ps1` 的单个路径（提交 `ead90ba490`）后重跑成功，
且准确输出“本机ICE未执行”。旧执行日志保留为 `carrier-authoring.log`。
这只是本地测试脚本修复，不改变产品或发布流水线；不因此取消/重复排队E38。

AUTO-02、AUTO-05由RETEST转PASS，整项PASS由24变为26，RETEST由2变为0。
正式签名/ICE/安装、High/OTS、跨owner和迁移恢复的缺口均保持；Compile输出
不是可发布的Setup，不能用于预配或作为E38的正式installer。

### E40：2026-11-02，用户配合同账号UAC的真实执行测试

用户明确同意处理UAC并要求开始。本轮另建Scenario B运行
`bc0df1507cde4e5098b74f12ab9f5573`，沿用指定WIP skill/profile；
只对这些明确授权的同账号提升场景覆盖默认缺少权限的skip，不扩展到OTS、
账号创建、服务维护、信任库或机器重启。先核对6006客户端/catalog/runtime及
时间修复DLL，保存完整原始数据，再通过Editor Import放入两个专用工作区。
fixture只创建空白窗口，报告自身token/坐标，并通过精确PID关闭标记自行退出；
不执行调用者指定的程序或修改业务数据。

| 场景 | 实际观察 | 判定和边界 |
|---|---|---|
| 普通入口混合权限 | Medium CLI20828；High Arranger24616；Medium目标8420及High目标18376，全部同owner/session2；exit0/completed，两应用arranged，history updated | PASS；144 DPI下两个目标物理矩形分别(180,240)-(1080,840)、(1170,240)-(2070,840)，匹配保存输入 |
| 错误权限窗口复用 | 提前启动同一`WsUacElevated.exe`的普通PID29704/HWND855584；启动后原身份/位置不变，另有High PID18376 | PASS；不是仅检查保存的`is-elevated`标志 |
| High入口的宿主创建 | 用户授权的真实High CLI24816创建Medium Launcher26380，二者同owner/session2 | 仅此子条件PASS，不能据此推断执行完成 |
| High入口后续执行 | CLI退出1；未观察到该次Arranger或fixture目标，无新的目标报告，history不变；现有日志没有足够内部错误细节 | 产品FAIL；CLI-03/R08。直接RunAs没有捕获stdout，不能臆测具体错误码/拒绝位置；未作产品修复 |
| 计划中的UAC取消 | 已提示用户对Arranger点否，但实际新CLI31868、High Arranger32388、普通目标12328/High目标31352完成启动，exit0 | 取消刺激未确认。用户澄清工具返回暂不可用，因此denial和“denial之后恢复”均BLOCKED；没有继续追加UAC，也不把这次成功当成取消被绕过 |

六个子项为 **3 PASS / 1 FAIL / 2 BLOCKED**；父项为
**1 PASS / 1 FAIL / 1 BLOCKED**，总体WITHHELD。High和取消项还保留对应正常路径
截图证据缺口。普通入口的原始产品确认截图经审阅后补挂到同一原始attempt的判断，
没有补拍、重写观察或冒充新执行。High失败后新普通入口成功的事实可证明会话未永久卡住，
但不能替代未发生的UAC取消恢复测试。

收尾首次Import因CLI已写回history、Editor持有旧revision而出现真实CAS冲突。
保留原失败/等待超时记录，观察确认具体Conflict和保存数据后，
按产品提示Cancel → Reload → 重新Import原始payload，恢复成功。
原两条工作区所有字段及完整2225字节业务payload均一致，
SHA256仍为`582212232F6DFF41B693A89873D1F24701EBA83CDAFB1FE4A1C3B98D40C0BBB8`。
Editor及全部自有fixture已退出，fixture目录删除，settings和原桌面恢复；
runtime ready及Debug客户端哈希未变。未直接覆盖protected blob/index。

主表DATA-14由BLOCKED转PARTIAL（同账号通过，OTS未测），CLI-03由NOT RUN转FAIL；
26条整项PASS不变。首次fixture编译的窄化警告修正、收尾CAS驱动恢复和截图引用补全
都按原始历史保留，不把它们算作产品缺陷，也不借此清除High执行失败。

本轮归档：

```text
C:\Users\bozhang\OneDrive - Microsoft\PowerToys\Module-Signoff\Workspaces-UAC-20261102\report.md
```

### E41：2026-11-02，管理员list失败的只读复现与根因定位

用户提供管理员终端执行实际Debug6006 CLI `list --json` 的截图：
`storageUnavailable`、泛化unexpected error文案、exit8。
本轮只作定向诊断，不启动工作区、不修改产品源码/二进制、服务、文件权限或数据，
不重开E40归档，也不将诊断脚本包装成新的完整WIP GUI验收。

| 证据 | 结果 |
|---|---|
| 同一CLI普通权限执行 | PID17880，exit0，原两条工作区可读 |
| 用户授权的同账号High执行 | PID32696，真实High、同owner/session2，约180ms返回与截图相同的exit8/JSON |
| 匹配Debug native库的只读探针 | 普通客户端构造成功；High探针PID30800直接调用真实`OpenOwnerConnectionToken`及`ProtectedStoreClient`构造，二者均返回`OwnerContextRequired`、native1346 |
| 实际High primary/linked token | primary为High/TokenPrimary；linked为同owner的Medium、非elevated，但`TokenType=2`（TokenImpersonation）、`ImpersonationLevel=1`（SecurityIdentification），不能作为SecurityImpersonation使用 |
| 无副作用核对 | 前后所有index/blob SHA256、ready内容和runtime-errors.log哈希一致；探针本身不发送service request、不调用ACL修改；诊断进程已退出 |

定位链路：

1. `WorkspacesCLI\main.cpp:121-124` 的list直接调用
   `CliCommands.cpp:238-240` 的`Workspaces::Repository().Load()`；
   `WorkspacesRepository.h` 的client成员先构造。list没有使用launch的Medium host边界。
2. `ProtectedStoreClient.cpp:92-97` 在构造时执行owner-context preflight。
   `Authentication.cpp:300-317` 获取并核对linked token；
   `TokenImpersonation/SecurityIdentification`在317行被明确拒绝为
   `OwnerContextRequired / ERROR_BAD_IMPERSONATION_LEVEL (1346)`。
   这是允许确认身份但不允许代表普通owner访问资源的token，不能简单移除检查。
   此时还没有进入`ConnectOwnerPipe`/`GetCapabilities`，不是服务端拒绝。
3. `WorkspacesCLI\main.cpp:159-163` 把所有ProtectedStorage异常转换成
   `storageUnavailable`/exit8，丢失typed error/native码；list使用初始化的null sink，
   该catch中的Logger调用没有给出有效落盘诊断。
4. `Resources.h:20-76` 没有`storageUnavailable`分支，
   `Localize()`又覆盖原message，最终回落`IDS_CLIINTERNALERROR`，
   所以用户看到的是unexpected error而不是owner-context说明。

匹配CLI SHA256：
`E57F3803F7B85466EA945D41A329D6D7B8B26F17E3322108371C926F416FD762`。
探针链接现有6006 `ProtectedStorage.Client.lib`和`ProtectedStorage.LocalCommon.lib`，
只读取自身token并执行构造preflight；不是注册新caller或绕开service authentication。
首次诊断编译因CRT类型不匹配失败，改用现有库的MTd后成功，未重编译/覆盖产品库。

这解释了**本次High list**，但不能直接解释E40的High launch：
`LaunchSession.cpp:301-305`已经对High请求选择`resolveInHost=true`，
由Medium host读取repository；E40仅有exit1，内部原始错误仍缺。
后续应分别验证完整launch边界，不能将两个失败强行合并。

修复方向：为管理员list提供已验证同owner/session的普通权限执行边界，
保留原JSON/stdout/exit-code合同；同时保真传递typed storage错误/native码及正确文案。
不能把识别级token提升为模拟级、把管理员token交给服务、放宽ACL或回退明文。
本轮尚未实施修复。CLI-07转FAIL，26个整项PASS不变；E40历史结论保留。

本地私有证据：会话files下 `nov02-high-list-results\` 的
`medium-diagnostic.json`、`high-diagnostic.json`、原始CLI stdout/stderr、
`before.json`/`after.json`及构建日志；探针源为`nov02-high-list-probe.cpp`，
驱动为`nov02-high-list-diagnostic.ps1`。账户SID仅留在本地证据，不写入公开台账。

### E42：2026-11-02，6007签名构建终态及x64 ICE失败诊断

[ADO 162529587](https://dev.azure.com/microsoft/Dart/_build/results?buildId=162529587)
在**17:00:23（UTC+08:00）**结束，整体`failed`。源码仍为
`6bd56af1472cd876abc6c2608fbf9888767d24d3`，版本`0.101.6007.0`，
没有自动重跑、重排build、关闭ICE或修改本机Debug6006。
监控schedule #4已停止；不存在等待中的构建资格结论。

| 任务 | 最终结果 | 实际范围 |
|---|---|---|
| x64 Release | FAILED | 唯一失败task为`Build Protected Storage Carrier`，log229；后续carrier签名、Broker/Setup发布及主installer stage跳过 |
| ARM64 Release | SUCCEEDED | carrier构建/ICE、各层签名、生产verifier、主安装器构建和发布均成功；是x64构建机交叉编译，不是ARM64实际安装/运行 |
| Build SDK / SDL sources | SUCCEEDED | 不能抵消x64安装包失败 |

**失败边界：** x64的WiX `build`先成功产生中间carrier MSI，随后
`wix.exe msi validate`运行ICE01、ICE02等时报`WIX0217`，
实际Windows Installer消息为错误1719：
`The Windows Installer Service could not be accessed`。
这不是C++/.NET编译失败、签名失败或已明确指出某张MSI表违规的ICE finding。
WIX0217中的external UI logger措辞也不能代替其内层1719错误。

下载并检查了x64的62,254,906字节failure artifact（27个日志文件），
包含真实`windows-installer-service.json`和`windows-installer-events.json`，
而不只查看控制台最后一行：

| 对照 | x64失败agent | ARM64成功agent |
|---|---|---|
| 环境 | `SHINE-INT-L 1` | `SHINE-INT-L 2`，另一台VM |
| 镜像/agent版本 | `SHINE-VS18-Latest 74.0.0` / `5.281.1` | 相同 |
| 校验前msiserver | Stopped / Manual | Stopped / Manual |
| 事后msiserver | Running / LocalSystem / exit0 | Stopped / Manual / exit0 |
| MsiInstaller事件 | 多条11719 / Error1719；同时有1033完成摘要 | 11707成功及1033摘要 |
| WiX执行工具 | `WixToolset.Sdk 5.0.2`的net472/x64工具 | 同一工具；ARM64 carrier实际执行ICE并以0 warning/0 error完成 |

因此**不能把“Stopped/Manual”直接当成根因，也不能保证Start-Service就能修好**：
Windows Installer本来可按需启动，成功job也从Stopped开始；失败job事后已Running。
事件1033的status0是本次校验会话的摘要，不能覆盖前面的1719/失败task，
更不能当作业务产品已安装验收成功。

目前最有力的判断是**失败agent上的Windows Installer/ICE执行环境问题**；
但具体触发原因（服务访问、执行上下文或该VM的Installer状态）尚未从现有日志证明，
也不能仅凭ARM64成功完全排除x64特定因素。下一步应在新agent上只重跑同提交的
x64失败job且保留ICE；若重现，再补详细MSI验证日志、SCM事件和实际token/执行环境。
本轮只诊断，没有实施这个重跑或修改authoring。

**实际已发布产物：** `build-arm64-Release`的服务端manifest列出：

| 文件 | 字节数 | 构建记录的SHA256 |
|---|---:|---|
| `PowerToysSetup-0.101.6007.0-arm64.exe` | 310090080 | `C87DF33ADF7FB5DE98AC970FB067AD4210FF4A6727B91F197EF546B49C1C1723` |
| `PowerToysUserSetup-0.101.6007.0-arm64.exe` | 311581984 | `3D5582784F6DA351150EF5FC0A0870879E011D8B87327E4C08251A2BC158A1B7` |
| `ProtectedStorage\PowerToys.ProtectedStorage.Carrier.msi` | 2494464 | release.json记录`73f26afdd9b2327a5cc77ac850b642dad84b0042da2ddc8f6e5de24524f4f70c` |

ARM64 `release-metadata.json`的buildId/sourceCommit/version和请求一致；
carrier `release.json`为ARM64、PE6007、`microsoft-production-v1`。
这些是发布manifest、构建hash日志及元数据核对，**没有下载运行两个EXE或进行本机独立
Authenticode/安装验证**。artifact内metadata的`result=succeeded`不改变整个run的FAILED。
没有`build-x64-Release`正常产物，只有`build-x64-Release-failure-1`日志。
正式126项状态保持不变，P-AU/P-AM及B02不自动通过。

原始证据均在会话files目录：
`nov02-installer-6007-terminal.json`、`nov02-installer-6007-terminal-timeline.json`、
`nov02-installer-6007-final-artifacts.json`、`nov02-installer-6007-arm64-manifest.json`、
两种架构的carrier/preflight/agent-init日志、
`nov02-ci-x64-failure\windows-installer-*.json`、
`nov02-installer-6007-arm64-windows-installer-*.json`及ARM64 release元数据。

### E43：2026-11-02，ICE 1719根因与重跑是否可能通过的进一步调查

用户质疑“换agent重跑”是否只是猜测。本轮继续读取历史ADO日志、实际Git对象和
WiX5.0.2源码，没有排队或重跑任何job，也没有调整ICE、权限或生产实现。
结论比E42更具体，但**仍未证明失败时Windows Installer内部为什么返回1719**。

| 对照 | 已核实的事实 | 可以／不能推出什么 |
|---|---|---|
| 3001，build158832978，09-28 | x64 Carrier task/log303真实执行`wix.exe msi validate`，0 warning/0 error | 同一Carrier设计曾通过x64 ICE；不是本次未验证的猜测 |
| 3002，build160176684，10-11 | x64 Carrier task/log303同样真实ICE通过；整体partiallySucceeded不改变该task通过的事实 | 第二个x64正向样本；不是只拿ARM64成功类推 |
| 三个版本源码比较 | 3001、3002、6007的Product.wxs、PowerToysProtectedStorage.wixproj、steps-build-protected-storage.yml三份Git blob逐一相同 | 这三个关键输入没有引入变化；不表示整个payload／工具镜像完全一致 |
| 镜像变化 | 3002为SHINE-VS18-Latest62.0.0，6007为74.0.0；前者也非交互、msiserver校验前Stopped/Manual | 旧成功并不能保证74镜像上的同一job一定成功 |
| 当天main，build162530035 | 同74.0.0镜像、agent5.281.1、非交互；x64主machine/user MSI的WiX5.0.2实际validate均0错误 | 不能把74镜像判断成“所有x64 MSI都会失败”；主MSI不是同一Carrier，仍不是严格复现对照 |

当天main的主MSI只按已有配置抑制ICE40；本次失败包含ICE01/02等97个不同action，
所以main成功不是因为把本次失败的所有ICE都关闭了。11-01的main对照
build162463271用的是73.0.0镜像，只作较弱历史参考；不能误写成74.0.0。

**97条错误的来源：** WiX5.0.2的
[WindowsInstallerValidator](https://github.com/wixtoolset/wix/blob/v5.0.2/src/wix/WixToolset.Core.Native/WindowsInstallerValidator.cs)
从`_ICESequence`取得action，逐个调用`session.DoAction`。
它的外部UI回调期望tab分隔的ICE结果；收到普通Windows Installer 1719消息时，
`ParseValidationMessage`按“不符合ICE消息格式”生成WIX0217。循环继续执行后续action，
故同一种底层失败被重复记录97次，**不是97个独立的MSI表authoring缺陷**。
[ValidateDatabaseCommand](https://github.com/wixtoolset/wix/blob/v5.0.2/src/wix/WixToolset.Core.WindowsInstaller/Validate/ValidateDatabaseCommand.cs)
使用临时数据库副本，finally删除验证副本。该校验不是执行本产品的常规安装旅程。

上游[WiX issue5473](https://github.com/wixtoolset/issues/issues/5473)记录过
同样1719/ICE错误与非交互低权限账号有关；
[issue4877](https://github.com/wixtoolset/issues/issues/4877)记录过服务上下文失败、
交互上下文成功。这些仅说明错误可由执行环境触发，**不是本次Windows Server2025
agent的根因证据**。两个job显示相同用户名类型，不能据此推断其实际token／授权状态相同。

当前证据缺口：failure artifact有wrapper log、binlog、事后service快照及最后30条
Application/MsiInstaller事件，但没有逐步MSI内部verbose trace、SCM/System相关事件、
校验进程的实际token/elevation记录，也没有保留失败的x64中间MSI作为可重复输入。
因此不能在现存材料上证明具体是权限、服务访问／启动交互，还是VM状态导致1719；
不能把“服务已Running”当作排除这些条件的证明。

**对重跑的判断：有可能通过，不是必然继续失败；没有依据承诺成功概率。**
历史同authoring的x64成功和当天同镜像的x64验证成功支持这一判断，
但本次同提交／同Carrier仍没有受控重跑。若重跑成功，只证明这个失败非必现，
不自动完成root-cause归因。

后续更有价值的是一次**带诊断的x64专项重跑**：保持ICE，保留失败中间MSI和验证trace，
记录实际caller token、service/SCM事件，再用同一MSI比较原执行环境与新agent；
区分单VM瞬态问题和可重复的Carrier／环境组合问题。不能用“反复重跑直到绿”
替代修复或作为验收证据。本轮未实施诊断增强或重跑，正式验收状态不变。

新增本地证据：
`nov02-ice-history-comparison.json`（三文件Git blob一致性、97个ICE计数）、
`nov02-history-3001-x64-carrier.log`、`nov02-history-3002-x64-carrier.log`、
`nov02-history-3002-x64-agent-init.log`/`preflight.log`、
`nov02-main-today-x64-agent-init.log`及当天machine/user MSI日志。

### E44：2026-11-03，High launch的原始异常与token对象权限定位

用户要求将High launch与High list一起分析后再决定架构。本轮保持产品源码、
6006 EXE、catalog、服务和业务数据不变；使用已确认不存在的随机workspace ID做
只读诊断。若流程正常，应在读取repository后返回workspaceNotFound，不应启动任何应用。
未重开或修改E40封存归档，也不把本轮诊断包装成完整WIP GUI验收。

| 实际诊断 | 结果 |
|---|---|
| 普通CLI + 不存在ID | exit3 / workspaceNotFound，证明普通读取链可用 |
| 用户授权的High CLI，捕获stdout/stderr | PID33800，exit1 / internalError，与E40的High失败表现一致 |
| 本地debugger观察原版High CLI及其Medium子进程 | High34908 -> Medium8036；首个异常为`PowerToys::ProtectedStorage::Error`，native5，what=`open own token DACL: access denied (win32=5)` |
| 第二次只读token对象诊断 | High28980 -> Medium35760；同owner/session2，重复得到同样原始错误；下面对象权限读数在创建时和异常时一致 |

原始异常的唯一对应源码位置为
`ProtectedStorage.Common\Common.cpp:1050-1051`：
`OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | READ_CONTROL | WRITE_DAC, ...)`。
调用链来自
`Repository.Load -> VerifyReady -> client.GetCapabilities -> ConnectOwnerPipe ->
AllowIdentityQuery -> GrantIdentityQuery`。该步骤在WaitNamedPipe/CreateFile之前，
目的是给对应VA增加只读身份查询ACE；尚未发送这次Runtime数据请求。
`IPCHelper::ModulePeer`没有调用storage client，本异常不是High/Medium交接管道的验证错误。

**直接观察到的两种不同“完整性”：**

| 属性/访问 | High CLI | High创建的Medium Launcher |
|---|---|---|
| 进程主token的内容IL / elevation | High / true | Medium / false |
| token这个内核对象的安全标签 | High，No-Write-Up | **仍为High，No-Write-Up** |
| token对象owner | BUILTIN\Administrators | BUILTIN\Administrators |
| DACL | Administrators/SYSTEM可管理；登录SID有限权限 | 同样的DACL形状，登录SID没有WRITE_DAC |
| 用该目标token作为观察线程身份申请TOKEN_QUERY、QUERY+DUPLICATE、READ_CONTROL | 成功 | 成功 |
| 用相同身份申请WRITE_DAC或QUERY+READ_CONTROL+WRITE_DAC | 成功 | **AccessDenied5** |

安全标签原文为`S:AI(ML;;NW;;;HI)`。上述访问探针在观察器线程模拟目标的实际主token后
执行OpenProcessToken，并立即RevertToSelf；没有向产品注入代码或写入对象ACL。
产品自己的原始OpenProcessToken失败与探针相互印证。
**Medium IL并不意味着它能修改自身token对象的DACL**；当前PPID/shell-parent创建机制
满足同owner/session/Medium检查，却没有满足SDK后续身份准备的额外要求。
这些是两项实际可见的权限限制；没有分别放宽DACL或mandatory label做绕过测试。

错误传递链也已确认：
`Common::Error(native5, open own token DACL)` ->
`ClientProtocol::TransportFailure`映射`StorageError(Unauthorized,5)` ->
`LaunchSessionHost::TryRunHost`的通用std::exception catch生成
`internalError / Workspace session failed / exit1` ->
High CLI收到并显示泛化错误。原始native码和阶段在host边界丢失。

与E41的区别：

| 路径 | 当前失败的前置条件 |
|---|---|
| High list | 取得的非提升linked token只有Identification级别，client构造preflight返回OwnerContextRequired/1346 |
| High launch | 真正Medium主token可用于普通身份；但client连接准备要求修改自身token DACL，对象权限不允许，native5最终变成exit1 |

两者都发生在Runtime实际数据请求处理之前，但不是同一个检查、也不是“无法区分IL”。
仅删除构造预检不能解决第二项；仅把list转交现有Medium Launcher也不能视为修复完成。
E40的缺失原始输出仍保持历史事实；本轮在同版本原始EXE、相同High入口上取得新的根因证据，
不能伪称当时已经记录到这些细节。

**对架构讨论的影响（未选定／未实施重构）：**

- VA Blob服务和Workspaces业务payload分离不必因此推翻；缺口在通用调用上下文与身份准备契约。
- 普通宿主不能只保证SID/session/Medium，还必须满足SDK实际连接所需能力；
  “已创建Medium进程”不能代表可用的storage consumer。
- 需要对比：普通执行载体的创建／身份初始化是否应保证最小VA查询权限，
  或服务端通用身份观察是否能避免要求每个客户端修改自身token DACL。
  后者属于认证机制设计，不是简单删掉客户端检查；仍须保留真实caller/image/role/target校验。
- 不在Workspaces业务代码中散布token补权限逻辑，不授予VA mutation/duplication能力，
  不为通过测试给普通进程广泛WRITE_DAC、不降低安全标签或回退明文。
- host/CLI必须保真携带typed error与native码，以免初始化失败被误判为存储损坏。

诊断限制：本地观察器成功读取首发异常类型、原生码和what字符串，但PDB符号栈没有正确展开，
保留原始地址输出、不将其当作成功的符号调用栈。源码定位由唯一错误文本、调用路径及原生
权限探针共同支持。没有将调试运行声称为无调试器完整业务验收；
另有不带debugger的High出口1和普通出口3对照。

收尾确认：原index/blob文件集合和SHA256、ready内容、CLI/Launcher EXE哈希均与本轮起点一致；
没有测试应用启动，所有自有诊断/CLI/Launcher进程均已退出，观察器EXE/OBJ已删除。
源码与日志在会话files的`nov03-high-launch-results\`，核心证据为
`baseline.json`、`medium-result.json`、`high-result.json`、`debug-events.log`、
`token-inspection\debug-events.log`和`diagnosis.json`。
真实SID/登录SID仅保留于本地证据，公开记录使用角色名称。
正式用例CLI-03/CLI-07仍FAIL，26条PASS不变；本轮没有实现产品修复。

### E45：2026-11-04，无caller-DACL替代机制试验准备及授权阻塞

用户要求验证：caller及其SDK不修改自身process/token DACL时，VA能否仍取得真实
身份和映像证据，保持owner/role/target约束完成受保护读写。
本轮只准备了独立诊断夹具，**实际VA测试0项，不能给出可行或不可行的技术结论**。

准备的非生产夹具使用独立服务名`PtStorageAuthProbe_6fcf12836ccb`和隔离stage，
不连接或修改6006服务。计划对比普通Medium、直接High、High派生Medium；
服务先测VA本身的进程/token/映像访问能力，再对非提升的可模拟caller测试安全的
普通owner模拟路径；High只请求Identification级连接，不向服务提供可执行High模拟权限。
同时准备公共／私有客户端路径、未注册映像、只读角色写入和错误target负向用例。
调用者在请求前后快照自身对象描述符，不执行补DACL操作。

测试服务仅在自身初始化时为原owner开放服务身份的只读查询；这不是修改caller的DACL。
固定fixture路径/哈希许可仅用于独立实验，不替代生产CMS/catalog信任，也不构成正式签名
或安装验收。该准备没有被加入产品源码或生产安装器。

| 阶段 | 事实 |
|---|---|
| 编译 | 独立原生fixture已编译；初次缺User32链接失败已修正并保留原日志；仅编译，不算认证机制PASS |
| 创建临时VA服务授权 | PowerShell RunAs返回`ERROR_CANCELLED (1223)`；管理员控制器未启动 |
| 后续授权 | 未自动重复提权；澄清工具返回用户暂不可用，因此暂停真实服务试验 |
| 独立收尾核对 | 测试service不存在、protected stage不存在、无测试进程；现有index/blob哈希和runtime ready均与起点一致 |
| 保留材料 | fixture源码、已编译测试EXE、带超时/finally清理的控制器及输入/日志保留；中间OBJ已删除 |

结果是**BLOCKED（需要一次获准的临时服务预配）**，不是身份认证测试失败，
也不是“无DACL方案已通过”。没有实施产品修复或架构重构，正式126项状态保持不变。

本地证据根为会话files下
`nov04-auth-6fcf12836ccb481a905ea9ed7b09ea4c\`，
`paused-result.json`记录0个真实VA测试及无副作用核对；
运行指针为`nov04-no-dacl-run.json`，驱动和源文件为`nov04-no-dacl-*.ps1/.cpp`。
测试EXE SHA256：
`967CD1F6DFA35040E961E91D1DDC5282EDA3E66D5ADCC0E830F8760BD828AC4B`。

### 本次证据

本地证据根目录：

```text
%USERPROFILE%\.copilot\session-state\c67b2458-a29d-41a3-a9fe-5f3ade11854a\files
```

这些是本机证据位置，不宣称其他机器或 CI 自动可访问。正式交付前应将脱敏材料归档到团队
批准的位置，并更新链接；不要将业务数据或凭据提交进仓库。

| 编号 | 原始证据 / 定位 | 能证明什么 |
|---|---|---|
| E01 | `oct28-integration-e2e-build-4.log` | 6003 全包构建；本地 pinned CMS 正常/篡改/其他 signer 检查 |
| E02 | `oct28-integration-results\oct28-integration-native.trx` | 188 项原生 Workspaces 默认单测，无失败/跳过；不含 opt-in live tests |
| E03 | `oct28-integration-results` 中 total=48 的托管 MSTest `.trx` | 48 项 managed 测试，UTC 2026-10-28 03:58 完成 |
| E04 | 本会话工具执行 `oct28-integration-storage-tests` 的输出；`oct28-integration-storage-build.log` **仅为构建日志** | 集成初期 native 3,019 assertions、20,000 mutations 的 PASS；最终 suite 复跑仍待补 |
| E05 | `oct28-integration-carrier-tests.log` | Compile 阶段 native helpers 和 4,129 installer contracts；未创建服务/profile/MSI 注册 |
| E06 | 本会话工具执行 `oct28-integration-carrier-tests` 中 `Test-ReleasePublication.ps1` 的控制台结果 | process-scoped mocked publication PASS；不是实际 MSI 安装或正式产物签名 |
| E07 | 本会话工具执行 `oct28-commit-integration` 的 CLI `--version`/`--help` 输出 | 新 CLI 6003 可启动；没有执行新服务的 list/launch |
| E08 | 备份提交 `c576e95f`、集成提交 `e04dbf97`；本会话源码/index/catalog 核对；`oct28-integration-data-before.json` 及取消后的逐文件哈希核对 | 源码保留、提交持久化、catalog 匹配和旧业务数据未改；不是新包已部署 |
| E09 | 本会话工具执行 `oct28-integration-live-repair` 的 `Start-Process -Verb RunAs` 结果 | `ERROR_CANCELLED (1223)`，真实更新未开始；不要引用并不存在的成功 repair report |
| E10 | `oct28-elevation-control.json`、`oct28-elevation-admin-detail.json`、`oct28-elevation-handoff.json` | 隔离实验中的 NoReadUp 和受限句柄候选机制；不是产品修复或新包 E2E PASS |
| E11 | `oct28-quickaccess-results\retry-red.trx`、`retry-green.trx` | 授权错误 Retry 回归修复前失败；修复后完整 managed suite 49 passed，0 failed/skipped |
| E12 | `oct28-quickaccess-auth-before.log`、`oct28-quickaccess-retry-ui.json` | 6003 客户端被旧 6002 catalog 在 authenticate 阶段拒绝；新 UI 的授权错误窗口只有 Cancel，无立即 Retry |
| E13 | `oct28-quickaccess-repair.log`；新 worktree `x64\Debug\ProtectedStorageLocalTest\Results\repair-20261028-144909-181.json` | 实际授权 repair 成功、Running/6003、旧 Code 保留、index/blob 前后哈希相同；非正式 MSI 升级 |
| E14 | `oct28-quickaccess-live-after.json`、`oct28-quickaccess-cli-after.json`、`oct28-quickaccess-cli-text.txt` | 真正 Quick Access 按钮启动 Editor，无授权错误；显示两条迁移记录；普通 CLI 查询及只读哈希核对 |
| E15 | `oct28-quickaccess-build.log`、`oct28-quickaccess-final-check.json`、`oct28-quickaccess-shortcut.json` | 本轮完整构建/pinned trust、所有客户端 catalog 哈希匹配、成功维护报告以及桌面快捷方式指向新包 |
| E16 | `oct28-launch-failure-6003.log`、`oct28-launch-failure-6003.json`、`oct28-launch-failure-dialog.png` | 用户两次真实启动的日志/截图；服务6003健康，Launcher/Arranger 文件匹配 catalog，工作区 elevated 标志、数据哈希和普通 owner 可读/执行 Arranger 文件的只读核对；没有重放启动或修改业务数据 |
| E17 | `oct29-snapshot-build.log` | 6004 全包构建及 pinned CMS 检查；服务尚未切换 |
| E18 | `oct29-snapshot-results\snapshot-red.trx`、`snapshot-green.trx`、`snapshot-query-limited.trx` | 旧实现拒绝无独立 plan 管道的真实快照交换；新实现 192 项组合及随后 3 项定向通过（193 个不同用例）。普通/错 operation/仅 limited-query 子进程均有实际进程回归，不模拟为 High |
| E19 | `oct29-storage-tests.log`、`oct29-snapshot-results\managed-storage.trx`、`oct29-main-installer-contract.log`、`oct29-publication-tests.log` | 3,018 native assertions + 20,000 mutations、49 managed、更新 inventory 的 authoring/mock 检查；服务端认证不变 |
| E20 | `oct29-snapshot-high.json`、`oct29-baseline-restored.json`、`oct29-snapshot-final-state.json`、`oct29-candidate-cli.json` | High 创建请求取消 1223；诊断 stage 已清理；6003 UI/服务恢复且数据不变；6004 catalog/CLI 文件检查，无真实 High/新服务运行声明 |
| E21 | `x64\Debug\ProtectedStorageLocalTest\Results\repair-20261029-160113-604.json`；`oct29-6005-before-repair.json` | 用户已完成 6004 保留数据更新；repair 前后 index/blob 哈希相同、旧 Code 保留；6005 构建前服务6004/无 recovery 的独立基线 |
| E22 | `oct29-6004-reported-behavior.json`、`oct29-current-elevation.json`；6004 Launcher/SnapshotTool 的 10-29 日志 | 两次 shared session 正常结束；请求 elevated 的 Notepad 仍只有已有普通 PID14436；两次 Capture 成功但等待约 42–45 秒、时间重叠。未捕获已退出 Arranger token，不声称 High/OTS 已验收 |
| E23 | `oct29-capture-ui-build.log`、`oct29-current-results\capture-ui.trx` | 独立 Release 构建及 10 个托管测试通过；含可控 pending Task 的真实 WPF Capture UI、完成/关闭和开始前取消回归 |
| E24 | `oct29-6005-build.log`、`oct29-elevation-build.log`、`oct29-current-results\elevation-matching.trx` | 6005 完整本地包构建/pinned trust 检查成功；原生 21 passed，其中 2 个新增 elevation 用例；XAML/whitespace 检查通过 |
| E25 | `oct29-6005-repair.log`；`x64\Debug\ProtectedStorageLocalTest\Results\repair-20261029-164427-290.json`；`oct29-6005-shortcut.json`、`oct29-6005-final-state.json` | 保留数据 repair 成功、ready6005；普通 Runner/Editor 恢复、CLI list 正常、桌面入口同步；最终 saved index/blob 哈希与更新前一致 |
| E26 | `oct29-6005-capture-pending.json`、`oct29-6005-capture-completed.json`、`oct29-6005-final-state.json` | 实际单 child、等待反馈、47.152 秒清单、可编辑页面和取消；首次 UIA 断言误把主窗 IsEnabled=true 当作未禁用，随后用原生 IsWindowEnabled=false 确认模态禁用；不是产品重复捕获失败 |
| E27 | `oct29-6005-launch.json`、`oct29-6005-launch-tokens.json`、`oct29-6005-final-state.json`；本会话 consent31072 进程观察 | 45 秒授权等待后 outcomeUnknown/exit9；普通 CLI/原 Notepad token 有实测，未获得新 High 目标证据；没有重复启动或绕过授权，后续无 pending 进程 |
| E28 | `capture-inventory-profile-stack-x64.log`、`capture-inventory-profile-workers-x64.log`、`capture-target-profile-appinfo.log`、`capture-target-profile-policy.log` | x64 首次 Next 约36.8秒；AppResolver 等待 Shell taskpool/RPC，后续属性读取很快。直接属性/STA/FASTITEMS 探测均未解决；具体 OS 内部 RPC 延迟原因未断言。WinRT AppInfo 约7–22ms；对应 Shell 解析约1.0–1.3秒；实际 token windowing policy 与被测包的 Shell elevation 能力相符 |
| E29 | `oct29-capture-perf-build.log`、`oct29-capture-perf-results\capture-discovery-final.trx` | 5 passed；同一测试进程新清单697ms、旧AppsFolder36958ms，11个可见应用比对通过。失败过的全目录替代原型未采用；当前实现仅给 Capture 增加快速发现，不改通用旧枚举/launcher fallback |
| E30 | `oct29-6006-build.log` | 完整6006本地包、同版本检查和pinned CMS正常/篡改/不同signer检查通过；不是部署报告 |
| E31 | `oct29-6006-data-before.json`、`oct29-6006-final-state.json`；本会话`oct29-6006-live`的Start-Process结果 | UAC取消1223，repair未开始；17:42服务6005/recovery0，候选SnapshotTool6006；业务index/blob哈希不变、无该输出目录的本地UI进程；没有生成虚假的成功repair报告 |
| E32 | `oct29-latest-main-capture-comparison.json`、`oct29-capture-perf-results\main-comparison-evening.trx` | 最新main `4150e73f9b12f5bfa16428e0904c84a4f1023c41`（10-29 23:10 +08:00）：AppUtils、SnapshotUtils、SnapshotTool项目、ModuleProcess与原集成main的blob一致；枚举函数与6005逐字一致。晚间同机Release库比较：旧37383ms/新2924ms，9个可见应用比对通过。不是原版main EXE对照；D:\PowerToys实际为旧stable，非今天main，非LocalTest日志最近为8月13日约6秒的历史记录 |

### 历史参考（不计入当前候选的 PASS）

| 编号 | 版本/时间 | 已有观察 | 不能据此宣称 |
|---|---|---|---|
| H01 | 3000/3001，2026-09 | ADO `158510076` / `158832978` 成功，签名/包证据；3001 修复 machine-uninstall hook 可达性 | 当前集成包签名/安装/ARM64/UAC 或机器卸载已通过 |
| H02 | 3000，2026-10-10 | 首次 UI 曾因未预配映射和 pipe EOF 失败；当时可用 Editor 验收 FAIL，下游业务 BLOCKED | 新版仍同样失败，或只修 pipe 就代表全部业务通过 |
| H03 | 6001/6002，2026-10 | 真实捕获/保存/重开、受控样例 launch/placement、重复点击与进度检查曾通过；定向查询避免两次约 35 秒枚举 | 6003 或所有应用/管理员启动/多显示器场景已通过；Capture 已优化 |
| H04 | 6002，2026-10-23～27 | 10-23 准备 legacy fixture；10-27 index 有相符 source receipt/cleanup acknowledgement，两条迁移记录可读 | 6003 的首次迁移及源清理/异常矩阵已验收；当前还是未初始化状态 |
| H05 | 6002，2026-10-23 | 重置/备份/迁移 fixture 工具重复执行，原数据备份哈希核对；profile loaded 被如实报告 | 新 helper 或所有历史 prototype/其他用户都已清完 |
| H06 | 6002 + 隔离实验，2026-10-27～28 | 管理员工作区在目标应用启动前发生 plan session failure；隔离 full-query 返回 5，受限句柄实验可查询实际映像 | 那次日志已记录精确失败行，或 6003 中已修复/已复现同一失败 |
| H07 | 6002，2026-10-28 | 取消新包更新后旧服务仍 Running/6002，原业务哈希相同，旧 Runner/Editor 列表已恢复 | 产品 Setup 的首次取消/恢复或正式服务升级已验收 |

### 初始执行记录

| 日期 | 用例 / 环境 | 实际结果 | 证据 / 后续 |
|---|---|---|---|
| 2026-10-28 | AUTO-01/07；AUTO | PASS：全包构建和本地 CMS 检查 | E01；无新包部署 |
| 2026-10-28 | AUTO-02、UNIT-02；AUTO | PASS：188 native tests | E02；opt-in live tests 未执行 |
| 2026-10-28 | AUTO-03、UNIT-02；AUTO | PASS：48 managed tests | E03 |
| 2026-10-28 | AUTO-04；AUTO | 集成初期运行 PASS；最终目标状态 RETEST | E04；不把构建日志伪称完整运行日志 |
| 2026-10-28 | AUTO-05/06；AUTO | PASS：4,129 contracts、mocked publication | E05/E06；不是实际 installed acceptance |
| 2026-10-28 | AUTO-08/09、LOCAL-01；AUTO/L64 文件检查 | PASS：CLI help/version、源码备份/哈希/版本检查 | E07/E08 |
| 2026-10-28 | LOCAL-02；L64 | BLOCKED：请求提升时被取消，实际服务更新未执行 | E09/B01；旧环境恢复，数据不变 |
| 2026-10-28 | AUTO-03、UX-01 授权错误子项；AUTO/L64 | PASS：新回归先失败后通过，49 managed tests；从 Quick Access 打开的授权错误 UI 不再显示无效 Retry | E11/E12；UX-01 其他分类不自动通过 |
| 2026-10-28 14:49 +08:00 | LOCAL-02；L64 | PASS：本次管理员授权后，6002 -> 6003 保留数据更新成功 | E13；关闭 B01；旧 Code 保留，index/blob 哈希不变，不是正式 servicing |
| 2026-10-28 | LOCAL-03/QuickAccess；L64 | PASS：Runner 15956 -> Quick Access 11880 的真实按钮启动 Editor 2540，进入列表，无授权错误 | E14；两条原工作区可见；主条目保持 PARTIAL，未代测 Settings/热键 |
| 2026-10-28 | CLI-01；L64 | PASS：普通 CLI text/JSON list、ID/details/name 查询均符合预期，无业务数据写入 | E14 |
| 2026-10-28 | ACL-01/owner-read、SEC-02/catalog 子项；L64 | PASS：普通 owner 可直接读；错 catalog 拒绝、匹配后正常授权；主条目均保持 PARTIAL | E12/E14；其他账号/VA/目标权限仍未测 |
| 2026-10-28 | LOCAL-01、AUTO-01/07/09；L64/AUTO | PASS：重建 bundle 的所有客户端哈希匹配；现有桌面本地快捷方式更新到集成目录 | E15；未执行管理员应用 launch，不关闭 DATA-14 或 R01 |
| 2026-10-28 16:19 / 16:30 +08:00 | DATA-14；L64 | FAIL：用户两次启动包含 elevated Notepad 的工作区，额外的 protected arranger plan 握手访问被拒绝；未启动工作区应用 | E16；最新 Launcher PID 28788、Arranger PID 11392，native=5、进程退出=7；当前唯一 Notepad PID 14436 创建于 10-21，并非此次启动 |
| 2026-10-28 | R01 / UX-01 诊断；L64 | 已定位产品失败阶段，不是旧服务版本或新 GetState 授权错误；NoReadUp 机制依据仍是 E10 的隔离实验与当前代码 | 服务 host17968/worker2072、6003、recovery=0；runtime 错误日志最后写入为 14:48，早于本次启动；index/blob 哈希不变。只读调研，未修改产品实现 |
| 2026-10-29 | AUTO-10；AUTO | PASS：回归先在 6003 失败（exit2，未请求快照），6004 的正常交换、错 operation 拒绝和 full-query 拒绝场景均通过 | E18；真实 Arranger、普通 token，仅限可丢弃 fixture；不启动目标应用、不修改工作区 |
| 2026-10-29 | AUTO-01～04/06～09；AUTO | PASS：6004 构建、193 个不同原生用例、49 managed、3,018 storage assertions 和 mutation/authoring/mock 检查 | E17～E20；非完整 installed/release/UAC 验收 |
| 2026-10-29 | DATA-14；High fixture | BLOCKED：UAC 返回 1223，High Arranger 未创建，通信未执行 | E20/B03；未重试提权。不能将此次记为新的 NoReadUp/IPC FAIL |
| 2026-10-29 | 环境恢复；6003 | PASS：桌面入口指向 `x64\Debug-6003-before-snapshot`，Runner19372/Editor30520 列表可用，原服务仍 6003，数据哈希不变 | E20；6004 候选位于正常 `x64\Debug`，未发起服务更新，不应手动混用两套客户端 |
| 2026-10-29 16:01 +08:00 | LOCAL-02；6004 L64 | PASS：用户完成 6003 -> 6004 repair；Running、dataPreserved=true、旧 Code 保留 | E21；解除 6004 未部署状态，不等于正式 MSI 验收 |
| 2026-10-29 16:09 +08:00 | DATA-14；6004 L64 | FAIL：两个 shared session 均完成，但普通既有 Notepad 被 elevated 条目复用，没有目标应用的新启动/UAC 路径 | E22/R05；不是再次出现 protected-plan 握手拒绝 |
| 2026-10-29 16:09–16:10 +08:00 | DATA-11、UX-05、PERF-02；6004 L64 | Capture 业务成功；UX FAIL：两个操作重叠，分别约 44.6/41.9 秒且无等待反馈 | E22；窗口迭代前长间隔与全量应用枚举位置一致，6005 添加精确计时再测 |
| 2026-10-29 | AUTO-11；Release AUTO | PASS：10 个托管测试，包括 2 个新增 WPF Capture 回归 | E23；不构造完整 MainViewModel，不读取/修改用户工作区；不是 native E2E |
| 2026-10-29 | 6005 构建准备 | 用户明确允许放弃测试草稿；关闭本地 Runner/Editor 等匹配路径客户端，保留已保存数据和用户既有 Notepad | E21 的更新前基线；不再以该草稿阻塞构建/更新 |
| 2026-10-29 | AUTO-01/07/12；AUTO | PASS：完整6005构建、本地pinned CMS、21个原生AppUtils用例，含2个新增elevation回归 | E24；不是High/OTS实机证明 |
| 2026-10-29 16:44 +08:00 | LOCAL-01/02；6005 L64 | PASS：匹配服务/catalog已更新、旧Code保留、业务哈希不变，恢复普通Runner/Editor | E25；未重建VA、未Reset/Purge |
| 2026-10-29 16:44–16:45 +08:00 | DATA-14；6005 L64 | BLOCKED：Arranger的UAC授权未完成，CLI45秒deadline后返回outcomeUnknown/9；不自动重放未知操作 | E27；后续确认无pending consent/Arranger，也无新Notepad；不宣称管理员启动已通过 |
| 2026-10-29 16:46–16:48 +08:00 | UX-05/正常路径、DATA-11/Capture取消、PERF-02；6005 L64 | PASS：等待有文字/进度、主窗原生禁用、一个SnapshotTool，47.293秒后进入编辑页，取消返回列表 | E26；清单47.152秒，未优化枚举；最后两个saved workspaces与index/blob哈希不变 |
| 2026-10-29 | PERF-02/R03；隔离 AUTO 调研 | 已定位等待发生在首个 AppsFolder Next，不是保护服务请求；该枚举循环及 COM 模式与集成 main 相同 | E28；诊断仅观察自身进程，未改系统目录/包/服务来加速 |
| 2026-10-29 | AUTO-13；Release AUTO | PASS：新清单697ms vs旧36958ms，11个可见应用关键元数据一致；共5项定向回归通过 | E29；6006完整包构建/实机验收单独记录，不把内部函数耗时当UI端到端耗时 |
| 2026-10-29 | AUTO-01/07；6006 AUTO | PASS：完整6006包和本地签名绑定检查 | E30；未部署 |
| 2026-10-29 17:40–17:42 +08:00 | LOCAL-02、PERF-03；6006 L64 | BLOCKED：服务更新的UAC取消1223；未开始repair，未执行后续两个GUI Capture计时 | E31/B04；未重试提权、未改服务/data ACL，当前业务哈希不变；诊断二进制/obj已清理，源码和日志保留 |
| 2026-10-29 23:12–23:18 +08:00 | R03/PERF-03；最新main只读比较 | 已抓取并核对当天23:10的main；没有发现可解释“main更快”的Capture代码差异；Release库晚间比较旧37383ms、新2924ms通过 | E32；未merge/rebase、未修改产品代码、未部署。明确区分“定位热点/绕开热点”与“证明原版main和原型快慢差异原因”；后者仍需实际二进制同环境对照 |

### 后续逐项填写模板

```text
时间（含时区）：
用例 ID / 环境 ID：
代码提交 / 客户端、Bootstrap、Runtime、MSI 版本：
OS build / 架构 / 安装 scope / 实际安装路径：
业务用户 A/B / caller 与目标进程 token、IL、会话：
前置状态、受控测试数据、实际步骤：
期望结果：
实际结果 / exit code / native error / operation ID：
状态：PASS | FAIL | BLOCKED | NOT RUN | PARTIAL | RETEST | N/A
证据编号及文件/日志/截图位置：
数据、服务、进程和测试 fixture 的恢复/残留情况：
更新了哪些主表条目；受影响的旧 PASS 是否需要 RETEST：
```
