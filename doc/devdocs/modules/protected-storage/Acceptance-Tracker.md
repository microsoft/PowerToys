# Workspaces Protected Storage 验收台账

最后更新：2026-10-29（UTC+08:00）

**这是验收状态的唯一维护入口。每执行完一项，就更新本文件；不能只在聊天里报告通过。**

本台账合并了 [原生产验收矩阵](../workspaces-protected-storage-acceptance.md)、
`HUMAN-TESTS-Workspaces-ProtectedStorage-0.101.3001.0.md`，以及后续本地 E2E、
只读 Data、迁移/重置、性能和 PR #51114 集成的验证要求。保留原生产矩阵的用例 ID；
旧人工清单的同名 ID 以 `H:` 为前缀引用，避免把两个不同的 `DATA-01` 混为一项。

## 1. 当前基线与结论

| 项目 | 当前记录 |
|---|---|
| 待验收代码 | `feature/workspaces-protected-storage-main-20261028`；`b0e90065f1` 基线上增加 Capture 快速应用发现；不改通用旧枚举、launcher fallback 或服务授权 |
| 主线基线 | `8056ba09721708bceee6176291b24af7ebabb0b3`；包含 PR #51114，合并提交 `06e6ae43e225cac51c2f282b0eebc42c6f995c97` |
| 集成 worktree | `D:\PowerToys-Workspaces-Integration` |
| 待测本地包 | x64 Debug **`0.101.6006.0`，构建成功但未部署**；入口 `D:\PowerToys-Workspaces-Integration\x64\Debug\PowerToys.exe`；必须先匹配更新服务/catalog，不能混用当前6005服务 |
| 原代码备份 | `backup/workspaces-protected-storage-20261028`，提交 `c576e95f70d4247efeb68afe1c50bc5f7e57f4e2`；含原 worktree 全部已跟踪/未跟踪源码改动 |
| 原 worktree | `D:\PowerToys-Workspaces-E2E`；原 HEAD `146518e3a3c3120257799b89e061b304f81fdab7`、索引和源码保留 |
| 最近确认的运行环境 | 服务仍 **6005**：host18428/worker30608、recovery=0（17:42读数）；构建前已关闭本地客户端，当前没有该输出目录的 UI 进程 |
| 新包部署状态 | 6006 更新的 PowerShell UAC 返回1223，实际 repair 未开始（E31）；没有重试或绕过授权。6005历史部署成功记录保留E25 |
| 数据保护结果 | 相对本轮6006构建前基线，当前saved index/blob哈希完全相同；没有Reset/Purge、重建VA或迁移。用户在两轮之间的业务修改不与E25旧哈希混比 |
| 当前验收结论 | 快速发现回归5项通过；新清单 **0.697秒** vs 同机旧 **36.958秒**，11个当前可见应用关键元数据相符。6006完整构建通过，但 **编辑器端到端耗时仍BLOCKED于服务更新授权**；不能把内部函数耗时当成完整Capture时间 |

`6003`、`6004`、`6005`、`6006` 都是合入 main 后的**本地 E2E 测试构建**，使用
`ProtectedStorage.LocalLifecycle.exe` 和本地测试 bundle，不是正式 carrier MSI 部署。
本地保留数据的 `Repair` 不能替代生产常规 MSI 升级、无新增 UAC、卸载或 servicing 验收。

两个 worktree 当前使用同一个本地测试服务身份和设置目录；源码隔离不代表运行环境隔离。
不能同时运行新旧客户端并假定它们都匹配同一份服务 catalog。6006 必须完成匹配的
服务/catalog 保留数据更新后再恢复客户端；旧 `Debug-6003-before-snapshot` 仅作历史备份。
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
| `L64` | 当前候选为 x64 Debug 6006；真实本地 E2E 必须先部署匹配的服务/catalog，旧6003/6004/6005结果单独保留 |
| `P-XU` / `P-XM` | 包含本次集成代码的正式签名 x64 per-user / per-machine 安装 |
| `P-AU` / `P-AM` | 包含本次集成代码的正式签名 ARM64 per-user / per-machine 安装及 ARM64 运行 |

下方业务、CLI、ACL、UX、性能表的当前实机状态针对 L64；正式验收时必须按相同 ID
另记适用的 P 环境结果，**不得继承 L64 的 PASS**。安装、MSI 升级和卸载表属于 P 环境。
优先级 P0 表示首轮主路径，P1 表示常见异常，P2 表示专项/故障注入；P2 并不自动免于发布要求。

## 3. 阻塞和风险

| 编号 | 事实 | 影响与解除条件 |
|---|---|---|
| B01（已解除） | 早前提权取消后服务留在 6002；14:39 发现新 6003 客户端反复被旧 catalog 拒绝 | 2026-10-28 14:49 已授权保留数据更新，核对新 ready/catalog 和不变的数据哈希（E13/E15）。未执行的用例转为 NOT RUN，不自动变为 PASS |
| B02 | 尚未在本台账提供并核验包含本次集成代码的正式签名安装包及升级配对版本 | P 环境安装/升级/卸载不能用本地 helper 或历史 3000/3001 CI 结果替代 |
| B03（历史探测取消） | 6004 隔离 High Arranger 探测曾返回 1223；之后用户已完成 6004 服务更新（E21） | High/OTS 完整资格仍需独立证据；不再把部署列为 6004 阻塞。6005 更新单独记录 |
| B04 | 6006服务更新的UAC返回1223；repair进程未启动，服务仍6005 | E31；匹配更新前不要启动6006客户端，不能重试提权或把新客户端的授权失败当作Capture算法回归 |
| R01（6003 失败；6004 已完成实机会话） | 6003 的额外握手失败保留 E16；6004 两次 shared session 正常完成（E22），没有再卡在 protected-plan | 不把未采集 token 的已退出 Arranger 当作 High 证明；目标应用的 elevation 复用缺陷见 R05 |
| R02 | 通用 “protected storage unavailable” 曾掩盖启动会话错误；授权不匹配还曾提供无效 Retry | 后一问题已修复并实测（E11/E12）；UX-01 的其他错误分类仍需逐项验证，不能根据弹窗直接诊断服务坏了 |
| R03（已绕开慢路径，快慢差异尚未闭环） | 6005清单47.152秒；独立x64诊断定位到AppsFolder首次Next等待。10-29 23:10最新main仍使用相同枚举循环（E32），不是已找到main的另一套快速实现 | E29/E32：新路径分别697/2924ms，旧路径36958/37383ms；没有改服务timeout。未对原版main二进制做同机同权限A/B，不能据源码相同断言原版必慢，也不能把6006优化说成已证明的分支回归根因修复 |
| R04 | 服务删除不等于 VA profile 已卸载；旧测试曾报告 profile 仍 loaded | 清理应报告准确残留，不强制卸载 hive；REM-06/CLEAN-02 单独判断 |
| R05 | 6004 `move-existing-windows=true` 时只匹配名称/路径/PWA，没有比较实际 token elevation；普通 Notepad14436 可满足 elevated 条目，绕过新启动路径 | 6005 在既有窗口选择和新窗口认领处均要求 elevation 匹配；仅用 limited process query，查询失败不当作普通窗口，不引入身份握手或 DACL 修改 |
| Q02 | 首次管理员授权时机、fail-closed 范围和 UX 仍需 PM 结论 | 当前不得启用不受保护 fallback；技术检查通过不等于 PM 已批准 |

## 4. 已取得的自动化与环境保留证据

这些条目只关闭其明确写出的检查范围，不关闭后续的已安装场景。

| ID | 检查与通过标准 | 当前状态 | 最近结果 / 证据 |
|---|---|---|---|
| AUTO-01 | 新 worktree 完整构建同版本 Runner、Settings、Quick Access、Editor、Launcher、LauncherUI、SnapshotTool、Arranger、CLI 和本地服务 bundle | PASS | 2026-10-29；6006，E30；不宣称全包零警告/完整符号，仍有既有XAML警告 |
| AUTO-02 | 本次集成的默认原生 Workspaces 单测全部通过，含 repository、CLI 合同、元数据及缺少 protected preview 的拒绝行为 | RETEST | 6004 的 193 个不同用例通过记录保留 E18；6005 运行受影响 AppUtils 21 项（AUTO-12），不是重跑全部默认/opt-in 套件 |
| AUTO-03 | 托管存储/协议/迁移/repository 测试通过 | PASS | 2026-10-29；49 passed，0 failed/skipped，E19；旧 E03/E11 记录保留 |
| AUTO-04 | 最终目标代码的 native storage 套件及 20,000 次 framing mutation 全部通过 | PASS | 2026-10-29；3,018 assertions + 20,000 mutations，E19。少的一条是已删除 peer helper 的参数断言；服务端映像和权限检查没有删除 |
| AUTO-05 | 原生 Setup/Lifecycle/Broker/MsiAction 编译及安装器契约检查通过 | RETEST | 旧 E05 的 4,129 检查和 helper 编译结果保留；本轮仅重跑 storage native、更新 inventory 的 authoring/mock，未重新编译整个生产 helper 图 |
| AUTO-06 | 发布流程的 mocked gates、最终文件字节/哈希、固定 policy/version、篡改拒绝检查通过 | PASS | 2026-10-29；E19，含新 inventory；仍是 process-scoped mock，不是实际发布/servicing |
| AUTO-07 | 本地测试 CMS 正常签名接受，内容改变或不同 signer 拒绝 | PASS | 2026-10-29；6006 E30，`expiredFixture=False`；不代替生产时间戳/吊销检查 |
| AUTO-08 | 新 CLI 的 `--version`、`--help` 正常退出且报告当前候选版本 | PASS | 2026-10-29；6004，E20；未连接 6004 服务执行 list/launch |
| AUTO-09 | 原源码完整保存、新集成提交持久化；新客户端文件哈希匹配新 catalog；维护不改变原业务数据 | PASS | E08备份保留；6006构建匹配bundle，构建/取消更新后当前业务哈希不变（E30/E31）。不代表6006客户端已连接新服务 |
| AUTO-10 | 真实 Arranger 用既有认证 IPC 获取快照；拒绝错误 operation；无需完整进程查询 | PASS | E18：旧 6003 因要求额外 plan 参数而失败，6004 通过；限权案例仅修改可丢弃 Medium 子进程 DACL，并实际确认 full-query 拒绝、limited-query 成功，不是 High/MIC 验收 |
| AUTO-11 | Capture 等待可见、禁用重复操作/关闭、完成后恢复；开始前取消不启动 native capture | PASS | E23：Release WorkspacesCsharpLibrary.UnitTests 10 passed，其中 2 个新增真实 WPF/STA 窗口用例；模拟 pending Task，不代表已部署 native Capture E2E |
| AUTO-12 | 既有/新窗口匹配要求实际 elevation 一致；查询失败不按普通权限处理 | PASS | E24：Release AppUtils 21 passed，包含当前真实 token 的匹配/相反 elevation 拒绝、无效 PID 两种请求均拒绝；不是实际 High/OTS 新应用启动 |
| AUTO-13 | Capture 快路径保留当前可见应用的名称、路径、包身份与提升能力；快捷方式/Steam/PWA 不丢失必要启动信息 | PASS | E29：5 项定向 Release 回归通过，11 个可见应用元数据与旧枚举一致；快捷方式覆盖环境变量路径、显式 AUMID、Steam URL、不可读链接；PWA/Steam 既有计划回归通过。不是所有安装应用/所有机器的完整目录等价证明 |

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
| LOCAL-01 / P0 | 核对新目录全部 PE 版本和 catalog；不得夹带旧 worktree 客户端 | PASS | 6006构建版本检查和匹配bundle，E30；服务仍6005，未将文件检查冒充部署 |
| LOCAL-02 / P0 | 退出旧 UI；保留数据更新服务；记录 Code 前后版本、ready、index/blob 哈希及回退证据 | BLOCKED | 6006：B04/E31；6004 -> 6005 的PASS保留E25 |
| LOCAL-03 / P0 | 从新 Runner 的 Settings、Quick Access、热键打开 Editor；实际进程全部来自新包，读到正确集合 | BLOCKED | 6006：B04；旧6005直接启动/列表记录保留E25，非当前候选通过 |
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
| DATA-07 / P0 | 已初始化后重启/更新/KeepData 重装，或放回旧文件；不得再次自动导入旧副本 | NOT RUN | 部署前置已满足；合法空集合也算初始化 |
| DATA-08 / P2 | 初始化记录存在但 record 缺失/损坏；明确 recovery-required，不回退明文、不初始化为空 | NOT RUN | 部署前置已满足；仅 disposable VM/数据 |
| DATA-09 / P1 | 编辑器和受支持 writer 产生真实 CAS 竞争；冲突可见、草稿保留、不覆盖无关修改 | NOT RUN | 部署前置已满足；开两个窗口本身不证明发生竞争 |
| DATA-10 / P0 | 创建、改名/参数/布局、保存、重开、删除；完整持久化；保存失败保留可恢复草稿 | NOT RUN | 部署前置已满足；覆盖中文/空格，删除记录不能复活 |
| DATA-11 / P0 | Capture/取消；preview 归属正确、有大小/生命周期限制、可回收，不写可篡改临时计划 | PARTIAL | E26：6005 native Capture -> protected preview -> 可编辑页面 -> 取消成功，saved index/blob 不变；跨 owner、大小/过期完整矩阵仍需测 |
| DATA-12 / P0 | 已保存/未保存工作区 Launch & Edit；使用准确 preview，重新捕获/保存/取消符合预期 | NOT RUN | 部署前置已满足；缺失/过期 preview 不接受任意 temp JSON，单测只覆盖其中拒绝分支 |
| DATA-13 / P0 | Editor/快捷方式/CLI 启动普通工作区；目标、参数、布局、move-existing、最小化/最大化正确 | NOT RUN | 部署前置已满足；使用自有 fixture 的新 PID/启动时间，不把既有 Notepad 当新启动 |
| DATA-14 / P0 | Medium 执行核心 -> High Arranger；同账号提升和 OTS 分别测试；原 owner/会话/精确计划不变，目标应用 elevation 正确 | BLOCKED | 6005 E27：Medium CLI21516 启动后 consent31072 仍待授权，45 秒 deadline 后 exit9/outcomeUnknown；未观察到新 Arranger/Notepad token，未重试。后续无残留 consent/Arranger、新 Notepad。6004 复用 FAIL 保留 E22；OTS 未测 |
| DATA-15 / P0 | 安装元数据与 last-launched 经可观察 CAS 写回；不覆盖新编辑，不在 destructor 静默 I/O，不因重试重复启动 | NOT RUN | 部署前置已满足；成功启动与历史写回失败要分别反馈 |
| DATA-16 / P1 | CmdPal/ModuleServices 枚举与启动使用同一 repository/owner；读取不预配，无 legacy escape | NOT RUN | 部署前置已满足；相关正式 host 还需新 catalog/构建证据 |
| DATA-17 / P1 | Export 得到一致显式副本；Import 确认后完整替换，坏 JSON 整体拒绝；副本不是 live source | NOT RUN | 部署前置已满足；先备份可丢弃测试集合，不擅自替换真实业务数据 |
| DATA-18 / P2 | 已接受 save 时进入维护/停止/重启；已接受写入结果可确定，未接受请求可重试，不假成功 | NOT RUN | 部署前置已满足；保留 operation ID，未知结果先查询 |

## 7. PR #51114 的共享执行与 CLI

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| CLI-01 / P0 | 普通终端 list/filter/details/JSON 与 Editor 同一集合；不修改 index/blob，不读旧明文 | PARTIAL | 6005 JSON list 正确返回原有两个工作区且数据哈希不变（E25）；text/filter/details 全组合未重跑 |
| CLI-02 / P0 | GUI 和 CLI 的 Medium 入口使用共享执行核心；CLI 同步等最终 arrangement 结果，失败/部分成功不报全成功 | NOT RUN | 部署前置已满足；核对实际 PID 与结果，不只看 exit 0 |
| CLI-03 / P0 | High GUI/CLI launch 先交给同用户/会话的 Medium host；按保存的 elevation 标志启动，不因高权限终端把所有目标提权 | NOT RUN | 部署前置已满足；High -> Medium 与 Medium -> High Arranger 是两条独立链路 |
| CLI-04 / P1 | 拒绝错误 owner/会话、失效进程/映像、非法 private host 输入；普通调用不能注入任意 workspace snapshot | NOT RUN | 部署前置已满足；保护存储和 plan 的身份要求不得因合并而悄悄移除 |
| CLI-05 / P1 | GUI Cancel/Ctrl+C/deadline/owner exit/第二次启动；停止后续应用，保留已启动应用，释放锁，结果未知不自动重放 | NOT RUN | 部署前置已满足；同时验证 callback teardown、冷启动 readiness 和 final-result ack |
| CLI-06 / P1 | 控制台应用独立控制台；CLI stdin/stdout/JSON、退出码、code page、隐私遥测合同保持主线行为 | NOT RUN | 部署前置已满足；错误/警告不得污染 JSON 流，参数/工作区内容不进遥测 |
| CLI-07 / P0 | 普通与提升终端分别验证 list 的 owner 上下文和错误反馈 | NOT RUN | 部署前置已满足；不能只用 launch 的降权结果推断 list |

## 8. 权限、隔离与信任

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| ACL-01 / P0 | 所属普通用户和管理员直接读取/list Data 的 index、blob、日志；其他普通用户/其他 VA 不获得读取权限 | PARTIAL | 6004 普通 owner 读取 index/blob/ready 和哈希核对成功（E21/E22）；管理员及跨 owner/VA 实机矩阵仍未完成 |
| ACL-02 / P0 | owner/admin 正常文件访问不能写、追加、删除、替换、更改 DACL/owner；SYSTEM/对应 VA 仍可写 | NOT RUN | 部署前置已满足；不实际写内容的 open-write 探针；SharingViolation/不存在不算 AccessDenied 通过 |
| ACL-03 / P1 | 既有 Data 启动迁移、嵌套文件、新 blob、原子 index 替换均保持只读策略；内容哈希/对象 owner 不变 | NOT RUN | 部署前置已满足；AUTO fixture 有覆盖但不等于完整服务实测 |
| ACL-04 / P1 | Code/Policy/SCM 权限不因 Data 可读而放宽；排障以共享 read/write/delete 打开，不能阻塞正常提交 | NOT RUN | 部署前置已满足；不测试管理员主动 take ownership/backup privilege 并宣称 ACL 可阻止机器管理员 |
| SEC-01 / P0 | 普通用户不能修改 Code/Data/control；另一 VA 不能改其他 owner 的树 | NOT RUN | 部署前置已满足；正式多用户矩阵另记 |
| SEC-02 / P0 | 错 SID、未授权映像/角色/目标、伪造 owner 请求被拒绝；CLI 身份列入正确 catalog | RETEST | 6003 部分实机证据 E12～E15 保留；6004 已部署且服务回归 E19 通过，但不等于全部实机拒绝矩阵通过。6005 未改服务授权策略 |
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
| UX-01 / P0 | 区分未设置、服务停机、存储恢复、arranger 身份/通信、UAC 取消、应用启动失败；日志保留阶段/native error | RETEST | 旧 6003 的授权错误 Retry 和 LaunchFailed 证据保留 E11/E12/E16；6004 GUI 已运行，6005 的实际失败 UX 仍需覆盖 |
| UX-02 / P0 | Launch & Edit 连点不产生第二次启动；页面 busy 可恢复，失败保留草稿，busy/failed 非“坏数据” | NOT RUN | 部署前置已满足；历史 H03 不直接沿用 |
| UX-03 / P1 | 进度 UI 冷启动能收到状态；关闭/取消无挂死；Import/Export/Reload/Retry cleanup 作用明确 | NOT RUN | 部署前置已满足；Reload 不重新预配，Retry cleanup 不做 blob GC |
| UX-04 / P1 | 中英文、长路径/错误文本等可读且本地化；无 console/JSON 内容泄漏或无响应错误循环 | NOT RUN | 部署前置已满足；真实 UI 观察另留证据 |
| PERF-01 / P1 | 正常 launch/arranger 不重复全量枚举；记录 metadata/首应用/arrangement 分段耗时，legacy fallback 有说明 | NOT RUN | 部署前置已满足；另见 R03；用同机同数据比较，不凭印象认定更快 |
| PERF-02 / P1 | Capture 独立量测全量枚举和 UI 响应；不得因 launch 优化宣称 Capture 已改善 | PASS | 量测范围：E26，清单 47.152 秒/整体约 47.293 秒，进度可见。**不表示性能达标或已加速**；R03 性能问题保留 |
| PERF-03 / P0 | Capture恢复秒级可用，不靠只显示进度或丢失必要应用元数据；记录真实点击Capture到可编辑界面耗时 | PARTIAL | AUTO：下午新697ms/旧36958ms、11个可见应用；晚间新2924ms/旧37383ms、9个可见应用，比对均通过（E29/E32）。不能把697ms视为固定延迟；完整GUI计时受B04阻塞 |
| UX-05 / P0 | Create/Capture 等待期间显示进度和说明，不允许第二次 Capture；完成/失败后恢复可操作状态，进度窗不遮挡错误对话框 | PARTIAL | E26：6005 正常捕获/等待/完成/取消实机 PASS；仅一个 native child，Capture/Cancel 禁用，主窗 IsWindowEnabled=false，完成后 Save/Cancel 可用。失败/Retry 实机待测；6004 FAIL 保留 E22 |
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

推荐按以下顺序进行；不要为了重测第一项就在已有 store 中手删 index/policy/初始化记录：

1. 先为 6004 执行经授权的保留数据部署（LOCAL-02），再切换入口/复测 LOCAL-03；旧 B01 已解除不代表 6004 已部署。B03 的 High 探测需单独取得授权。
2. DATA-10、CLI-01、DATA-13：先做普通权限 CRUD/重开/普通启动，留可恢复的受控测试数据。
3. DATA-14、CLI-03/07、UX-01：同账号提升，再单独测 OTS；记录实际 PID/token/IL 和拒绝阶段。
4. DATA-11/12、UX-02/03、DATA-15/17：预览、取消、进度、写回和 Import/Export。
5. 用新账号或备份恢复后的专用 fixture 做 DATA-01/02/06/07，验证真实首次迁移。
6. ACL-01～04、SEC-01～04、LIFE-01、PERF/BLOB：先非破坏性检查，再隔离环境故障注入。
7. 取得正式包后按 P 矩阵执行安装、A/B 隔离、常规升级、KeepData 卸载重装；最后做 purge/断电等 P2。

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
