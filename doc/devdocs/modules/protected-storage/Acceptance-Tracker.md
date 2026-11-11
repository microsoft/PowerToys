# Workspaces Protected Storage 验收台账

最后更新：2026-11-11（UTC+08:00；E72先做隔离SCM/profile实验，再实现生产profile删除、身份受限的残留目录清理及受保护的重启/延时重试。x64构建、6,254条contract checks及真实SYSTEM任务小范围测试通过；签名安装包的外层卸载/重启验收仍待补，REM-06保持PARTIAL）

**这是验收状态的唯一维护入口。每执行完一项，就更新本文件；不能只在聊天里报告通过。**

本台账合并了 [原生产验收矩阵](../workspaces-protected-storage-acceptance.md)、
`HUMAN-TESTS-Workspaces-ProtectedStorage-0.101.3001.0.md`，以及后续本地 E2E、
只读 Data、迁移/重置、性能和 PR #51114 集成的验证要求。保留原生产矩阵的用例 ID；
旧人工清单的同名 ID 以 `H:` 为前缀引用，避免把两个不同的 `DATA-01` 混为一项。

## 1. 当前基线与结论

### 2026-11-11统计快照（E68关闭普通x64外层卸载回调，保留重装与profile缺口）

保留126个唯一历史ID，其中125项为有效要求，DATA-17因用户未提出显式文件
Import/Export需求而撤回为N/A。N/A不算通过或未完成；不重复计入子项、风险、
历史记录和环境矩阵。该比例不是独立测试数、剩余工作量或风险消除比例。

| 状态 | 条数 |
|---|---:|
| PASS | 27 |
| PARTIAL | 27 |
| FAIL | 0 |
| RETEST | 2 |
| BLOCKED | 1 |
| NOT RUN | 68 |
| N/A（已撤回） | 1 |
| 合计 | 126 |

27条PASS由AUTO 12条、UNIT 6条、LOCAL-01、PERF-02/03、E37关闭的
CLI-01、E55关闭的DATA-01、CLI-03/07、E59关闭的INST-01及E68关闭的REM-11组成；
仍不代表核心架构和正式安装验收已完成。
显式标P0的45条中，整项PASS 8条、PARTIAL 19条、FAIL 0条、RETEST 0条、BLOCKED 1条、
NOT RUN 17条。当前正式BLOCKED仅为REL-01（PM决策）；REM-02已完成卸载保留一半，
转PARTIAL，尚待重装恢复验证；
E40将DATA-14补到PARTIAL，
E40/E41分别记录CLI-03/CLI-07的历史FAIL，E55补记修复后的人工PASS；
不要与E33的三个BLOCKED子项混淆。
R06已有E36定向实机PASS，但DATA-10包含更多条件，仍只计PARTIAL。
E50的Capture重复ID失败历史保留；E55用户确认修复后的捕获/保存/重开通过，
DATA-12转PARTIAL，尚未由本次确认覆盖取消分支。E53迁移/源清理证据加E55重开确认，
DATA-01转PASS。AUTO-02/13的Photos名称一致性失败仍记RETEST；
人工业务回归通过不代表这两个单测缺口消失，也不覆盖旧失败历史。
E56将DATA-17从有效范围撤回；DATA-09过去把仅启动历史变化报冲突当作成功，
这一预期不符合原Workspaces自动保留启动时间的行为。现改为区分历史变化与真正
配置冲突，置RETEST；新模块单测通过不冒充完整实机竞争验收。
E61用户确认6012测试1/3通过，历史自动合并获得P-XU人工结果，
DATA-09转PARTIAL，真正配置冲突/未知结果的完整UI路径仍另计。
E59仅关闭P-XU/6010的当前owner首次预配：实际carrier注册、VA进程身份、
正常UI与空集合读取已确认，不外推升级、重启、OTS或全新VA profile创建。
E60确认实际服务和carrier均升级到6012，UPD-01/09转PARTIAL：
不是服务升级失败，而是完整6010业务数据前后比对、升级期间授权/调用上下文
及后续重启等证据不能由“升级后Ready”替代。
E61普通外层卸载实际执行主MSI成功，但owner carrier清理回调未执行，
REM-11记FAIL；仍在运行的store不能冒充KeepData卸载完成，REM-02暂受阻。
E62已修正回调接口并以实际BA DLL验证WiX事件分派，原生安装器套件4,136项通过；
E63确认6013整体FAILED：x64产物已发布，ARM64在MSI验证环境中失败。
E64随后只读确认用户部署后的x64正式实例已由6012切到6013，真实进程和
提交记录相符；代理没有执行sync/repair来促成升级，也没有新的卸载/重装结果，
因此不关闭REM-11或REM-02。
E65随后实际观察到6013外层卸载仍未清理保护实例：E62的回调签名修复生效，
但另一个动作判定时序缺陷使owner removal未启用。仍是同一REM-11要求失败，
不重复增加FAIL计数；Data仍在活动store中，不作为成功KeepData卸载证据。
E66将意图判断移到成功的OnPlanComplete，并补实际生产回调类的完整状态
序列回归；最小验证先红后绿，不调用系统清理。6014已排队但未部署，
因此仍不关闭REM-11/REM-02。另查6013已在同提交ARM64 attempt2后整体转绿，
E63的初次CI失败作为历史保留，不与已确认的6013卸载逻辑缺陷混淆。
E68首次取得6014真实外层卸载的正向结果：主MSI成功后owner清理被启用，
经用户授权完成carrier、SCM及运行Code移除，Data进入retained/keep状态。
REM-11在P-XU普通owner范围转PASS，REM-02转PARTIAL待重装；
REM-06从NOT RUN转PARTIAL，仅确认profile pending的实际记录，未验证删除/重试。
E61/E65的失败历史保留，不把这些变化扩充为所有scope、零残留或完整生命周期通过。

### 分类总览：125项有效要求，另保留1个N/A历史项

目前有98条尚未整项关闭，但其中只有68条记为NOT RUN；另有27条已部分覆盖、
2条RETEST、1条受阻，不能全部当作“从零开始”。历史失败记录保留，
DATA-17不再计入有效范围；27/125不是工作量完成百分比。

下面按**主要验证目标**将有效ID归入一个类别；E56明确撤回DATA-17，不以此增加通过数。
“未关闭”包含PARTIAL/FAIL/RETEST/BLOCKED/NOT RUN；“未跑”仅指NOT RUN。

| 类别 | 总条数 | 整项PASS | 未关闭 | 其中未跑 |
|---|---:|---:|---:|---:|
| 工程基线与自动化 | 27 | 18 | 9 | 3 |
| 日常功能、交互和性能 | 18 | 3 | 15 | 6 |
| 跨权限执行与运行上下文 | 7 | 2 | 5 | 4 |
| 写保护、授权和用户隔离 | 9 | 0 | 9 | 5 |
| 数据迁移、一致性与恢复 | 17 | 1 | 16 | 13 |
| 安装、升级、卸载与生命周期 | 36 | 3 | 33 | 28 |
| 生产信任、环境与发布门槛 | 11 | 0 | 11 | 9 |
| 合计（有效项） | 125 | 27 | 98 | 68 |

| 类别 | 唯一归属的正式ID |
|---|---|
| 工程基线与自动化 | AUTO-01～14；BUILD-01～05；UNIT-01～08 |
| 日常功能、交互和性能 | LOCAL-03；DATA-10～13、16；CLI-01～02、05～06；UX-01～05；PERF-01～03 |
| 跨权限执行与运行上下文 | DATA-14；CLI-03～04、07；INST-07～08；ENV-02 |
| 写保护、授权和用户隔离 | ACL-01～04；SEC-01～05 |
| 数据迁移、一致性与恢复 | DATA-01～09、15、18；BLOB-01～03；CLEAN-01～03 |
| 安装、升级、卸载与生命周期 | LOCAL-01～02、04；INST-01～06；LIFE-01；UPD-01～15；REM-01～11 |
| 生产信任、环境与发布门槛 | SEC-06～11；ENV-01、03～04；REL-01～02 |

DATA-17仅作为已撤回功能的历史记录保留，不归入上述有效类别。

剩余最多的是安装生命周期34条、数据生命周期16条、日常功能15条。安装类中
INST-01～06、UPD-01～15、REM-01～11共32条属于正式安装/升级/卸载条件，
不能要求当前Debug样本先把它们全部关闭。

已通过的26条分布在：工程/自动化18条；日常功能3条（CLI-01、
PERF-02/03）；跨权限2条（CLI-03/07）；数据一致性1条（DATA-01）；
部署核对1条（LOCAL-01）；正式首次预配1条（INST-01/P-XU）。
“整项0 PASS”不等于完全没测：写保护类已有4条PARTIAL，普通owner拒写和
未注册caller拒绝已实测；日常功能另有9条PARTIAL。跨权限、跨owner及生产生命周期
仍然是真实缺口，不能仅靠调整分类消除。

后续执行按第12节的工作包组织，原ID只作为每个阶段的验收检查点。

| 项目 | 当前记录 |
|---|---|
| 待验收代码 | `feature/workspaces-protected-storage-main-20261028`；E72 profile清理提交`daf418c10611c8ba10fad30e055081ba714176ac`已push至origin/fork，6,254条contract checks及SYSTEM小范围任务测试通过。保留E62/E66卸载回调修复、KeepData及Setup/Broker分工 |
| 正式签名构建 | E72已queue 6015 [163595010](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163595010)，源码`daf418c106`、版本0.101.6015.0、Release x64/ARM64已独立核对，当前inProgress，未宣称绿色或产物可用。6014 [163468842](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163468842)绿色作为历史基线；构建绿色不等于安装/卸载验收 |
| 主线基线 | `8056ba09721708bceee6176291b24af7ebabb0b3`；包含 PR #51114，合并提交 `06e6ae43e225cac51c2f282b0eebc42c6f995c97` |
| 集成 worktree | `D:\PowerToys-Workspaces-Integration` |
| 待测本地包 | 完整x64 Debug **`0.101.6011.0`**文件及E57历史验证保留。E69按用户指定手动删除LocalTest服务，Code/Data未删，当前不再是已运行的LocalTest实例；后续需显式重新预配，不能拿历史状态冒充当前可用 |
| 正式首装前基线 | E58 11-08 23:54：当前owner正式主程序/ARP bundle/carrier/service不存在，owner Code/Data/Policy/Installer及legacy源已清空或移出；自动下载更新关闭，普通启动配置保留。LocalTest与两个demo保留。旧正式VA profile loaded/native32，不能称全新机器 |
| 正式安装现状 | E68：6014主程序、主MSI/用户bundle、owner carrier、正式SCM服务及Code目录已移除；SCM返回1060、carrier6014/6013均返回1605。Data/Policy/Installer保留，状态为retained/keep |
| 原代码备份 | `backup/workspaces-protected-storage-20261028`，提交 `c576e95f70d4247efeb68afe1c50bc5f7e57f4e2`；含原 worktree 全部已跟踪/未跟踪源码改动 |
| 原 worktree | `D:\PowerToys-Workspaces-E2E`；原 HEAD `146518e3a3c3120257799b89e061b304f81fdab7`、索引和源码保留 |
| 最近确认的运行环境 | E71正式及5个旧实验服务/profile已移除，正式Data index/blob hash在E72仍未变；旧profile-review标记和历史事务未手动清理。E72另有一个独立SCM probe profile仍Loaded，已安排仅限该probe的测试清理任务；不是正式产品服务或数据 |
| 新包部署状态 | 用户已完成6014普通外层卸载，E68当时仅只读观察；E71另经授权人工删除profile。6015尚在构建，未安装新包、未运行新产品卸载流程，E72仅运行隔离fixture |
| 数据保护结果 | E68在正式服务已移除后验证2条工作区可读、sequence15，frame/hash/revision/receipt一致，epoch/init ID未重置，owner/Admins仍FR。未做卸载前最后一刻逐字段对照，重装恢复仍待验证 |
| 当前验收结论 | REM-11/P-XU普通owner外层清理PASS；REM-02/REM-06 PARTIAL。保留profile-review/3010及历史事务备份，不宣称零残留；27 PASS、27 PARTIAL、0当前FAIL、1 BLOCKED、1 N/A |

`6003`、`6004`、`6005`、`6006`、`6008`、`6009` 都是合入 main 后的**本地 E2E 测试构建**，使用
`ProtectedStorage.LocalLifecycle.exe` 和本地测试 bundle，不是正式 carrier MSI 部署。
本地保留数据的 `Repair` 不能替代生产常规 MSI 升级、无新增 UAC、卸载或 servicing 验收。

两个 worktree 当前使用同一个本地测试服务身份和设置目录；源码隔离不代表运行环境隔离。
不能同时运行新旧客户端并假定它们都匹配同一份服务 catalog。6008/6009 已观察到匹配运行；
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
| `L64` | 当前x64 Debug6011客户端/runtime匹配已由E57确认，B08解除；具体UI/业务测试仍按各自版本和证据记录，不能继承6009全部PASS |
| `P-XU` / `P-XM` | 包含本次集成代码的正式签名 x64 per-user / per-machine 安装 |
| `P-AU` / `P-AM` | 包含本次集成代码的正式签名 ARM64 per-user / per-machine 安装及 ARM64 运行 |

下方业务、CLI、ACL、UX、性能表的当前实机状态针对 L64；正式验收时必须按相同 ID
另记适用的 P 环境结果，**不得继承 L64 的 PASS**。安装、MSI 升级和卸载表属于 P 环境。
优先级 P0 表示首轮主路径，P1 表示常见异常，P2 表示专项/故障注入；P2 并不自动免于发布要求。

## 3. 阻塞和风险

| 编号 | 事实 | 影响与解除条件 |
|---|---|---|
| B01（已解除） | 早前提权取消后服务留在 6002；14:39 发现新 6003 客户端反复被旧 catalog 拒绝 | 2026-10-28 14:49 已授权保留数据更新，核对新 ready/catalog 和不变的数据哈希（E13/E15）。未执行的用例转为 NOT RUN，不自动变为 PASS |
| B02 | E59/E60正式首装及升级后置证据保留。E63的6013 x64构建成功，ARM64 Carrier ICE因1719失败，完整servicing/原始包/实机矩阵仍待补 | 不以单架构成功关闭整轮构建或生产资格；不跳过ICE换取绿色。6007/6010/6012历史保留 |
| B03（历史探测取消） | 6004 隔离 High Arranger 探测曾返回 1223；之后用户已完成 6004 服务更新（E21） | High/OTS 完整资格仍需独立证据；不再把部署列为 6004 阻塞。6005 更新单独记录 |
| B04（已解除） | E31 的6006更新曾返回1223；E33 开始时已观察到匹配的6006客户端/runtime | 本轮实际业务验证已执行；不把历史授权取消继续当作当前部署阻塞 |
| B05（E33限制非当前High入口阻塞） | E33会话与Runner非管理员，历史验证跳过管理员变体；E55用户另行确认同owner普通/High入口回归通过 | 不扩大为OTS或另一owner通过；本轮代理未另行提权/采集High进程证据 |
| B06（E33缺口保留；E36新一轮已补足） | E33未保留完整事前字节，旧结果不能追溯改PASS；E36在任何变更前保存完整blob和逐记录详情 | E36清理后原记录全字段及2225字节业务payload一致；blob整体变化来自revision/operationId/requestDigest信封元数据。此证据仅证明E36相对其自身起点的保真 |
| B07 | E33 canonical resource receipt保留4个未解析launch记录及旧PID31508查询拒绝 | 已实测当前5个自有fixture退出、无剩余fixture进程、借用UI恢复；不手改旧receipt消除hold，不终止无法确认身份的PID |
| B08（E57已解除） | E56的维护UAC取消曾使磁盘6011/service6009不匹配；E57已只读确认运行service6011及真实CLI可用 | 取消历史不删除；不再将匹配部署列为当前阻塞，也不据此关闭尚未执行的业务case |
| B09（E68已解除） | 6014真实外层卸载已启用owner cleanup，carrier/正式服务/运行Code实际移除，E65时序缺陷的修复取得P-XU实机证据 | 不扩充为重装恢复、其他scope或profile完全清理；REM-02/REM-06仍PARTIAL |
| R01（6003 失败；6004 已完成实机会话） | 6003 的额外握手失败保留 E16；6004 两次 shared session 正常完成（E22），没有再卡在 protected-plan | 不把未采集 token 的已退出 Arranger 当作 High 证明；目标应用的 elevation 复用缺陷见 R05 |
| R02 | 通用 “protected storage unavailable” 曾掩盖启动会话错误；授权不匹配还曾提供无效 Retry | 后一问题已修复并实测（E11/E12）；UX-01 的其他错误分类仍需逐项验证，不能根据弹窗直接诊断服务坏了 |
| R03（已绕开慢路径，快慢差异尚未闭环） | 6005清单47.152秒；独立x64诊断定位到AppsFolder首次Next等待。10-29 23:10最新main仍使用相同枚举循环（E32），不是已找到main的另一套快速实现 | E29/E32：新路径分别697/2924ms，旧路径36958/37383ms；没有改服务timeout。未对原版main二进制做同机同权限A/B，不能据源码相同断言原版必慢，也不能把6006优化说成已证明的分支回归根因修复 |
| R04 | 服务删除不等于VA profile已卸载；E58清理正式owner残留时DeleteProfile返回32，profile仍loaded | 业务store/初始化状态已不存在，但不是零OS残留或新机器。没有强制卸载hive；若要求profile也重新创建，需释放占用后另核对/清理。REM-06/CLEAN-02不因本次环境准备自动通过 |
| R05 | 6004 `move-existing-windows=true` 时只匹配名称/路径/PWA，没有比较实际 token elevation；普通 Notepad14436 可满足 elevated 条目，绕过新启动路径 | 6005 在既有窗口选择和新窗口认领处均要求 elevation 匹配；仅用 limited process query，查询失败不当作普通窗口，不引入身份握手或 DACL 修改 |
| R06（L64定向实机PASS） | E35修复复制构造函数；E36实际新建自有工作区，再连续两次改名、Save、Reload、重开 | 创建时间三次均 `1793347374`，last-launched三次均0，除改名外全部字段一致；不是只读内存或单测结论。Debug6006 Editor已包含修复；既有零值不自动恢复 |
| R08（E55同owner L64回归通过） | E40/E44的6006 High启动失败及token对象WRITE_DAC/native5根因保留；E49已改为data client仅授予process查询权限，运行6009包含修复 | E55用户确认普通/High GUI及CLI入口回归通过，CLI-03 PASS；不再把旧故障标作当前未修复。异常错误传递仍按UX-01单独覆盖 |
| R09（E55同owner L64回归通过） | E41的6006 High list因Identification linked token返回OwnerContextRequired/1346，历史失败保留；E49已使用验证过的同owner/session普通Explorer连接token | E55用户确认普通/High list回归通过，CLI-07 PASS；不等于失效桌面、错误owner或所有异常错误映射已测，不扩大到OTS |
| R07 | E33 重新捕获后快捷方式复选框显示未勾选，现有快捷方式仍在；Revert后恢复勾选 | `DataContext` 赋值后才调用 `CheckShortcutPresence`，对应属性无变更通知；该顺序可追溯到2024-08-23初始PR #34324（`579619952d`），当前main仍保留。本轮未证明快捷方式被删除，也未修产品代码 |
| Q02 | 首次管理员授权时机、fail-closed 范围和 UX 仍需 PM 结论 | 当前不得启用不受保护 fallback；技术检查通过不等于 PM 已批准 |

## 4. 已取得的自动化与环境保留证据

这些条目只关闭其明确写出的检查范围，不关闭后续的已安装场景。

| ID | 检查与通过标准 | 当前状态 | 最近结果 / 证据 |
|---|---|---|---|
| AUTO-01 | 新 worktree 完整构建同版本 Runner、Settings、Quick Access、Editor、Launcher、LauncherUI、SnapshotTool、Arranger、CLI 和本地服务 bundle | PASS | E56完整Debug6011构建通过，所有入口PE版本检查及build-pinned CMS正/负检查通过；不等于已更新运行service。E30/E51旧构建记录保留 |
| AUTO-02 | 本次集成的默认原生 Workspaces 单测全部通过，含 repository、CLI 合同、元数据及缺少 protected preview 的拒绝行为 | RETEST | E50：194项中193 PASS/1 FAIL，失败为Photos与照片查看器的名称一致性，单独复跑同样失败。重复Capture ID回归及RepositoryTests10项通过；E39的193/193历史保留 |
| AUTO-03 | 托管存储/协议/迁移/repository 测试通过 | PASS | E56：59 passed，新增历史自动合并/真正冲突/新epoch拒绝/有限重试/结果未知原意图查询、重开后仅重试cleanup等回归；撤掉无需求的显式Import分支。E50的51项历史保留 |
| AUTO-04 | 最终目标代码的 native storage 套件及 20,000 次 framing mutation 全部通过 | PASS | 2026-10-29；3,018 assertions + 20,000 mutations，E19。少的一条是已删除 peer helper 的参数断言；服务端映像和权限检查没有删除 |
| AUTO-05 | 原生 Setup/Lifecycle/Broker/MsiAction 编译及安装器契约检查通过 | PASS | E66：Release x64构建0错误/警告，4,230 checks通过；11个Plan→Execute场景使用生产回调类和WiX分派器，仅替换清理副作用，先红后绿。E62真实DLL签名回归保留；不替代签名包卸载、ICE或REL-02 |
| AUTO-06 | 发布流程的 mocked gates、最终文件字节/哈希、固定 policy/version、篡改拒绝检查通过 | PASS | 2026-10-29；E19，含新 inventory；仍是 process-scoped mock，不是实际发布/servicing |
| AUTO-07 | 本地测试 CMS 正常签名接受，内容改变或不同 signer 拒绝 | PASS | 2026-10-29；6006 E30，`expiredFixture=False`；不代替生产时间戳/吊销检查 |
| AUTO-08 | 新 CLI 的 `--version`、`--help` 正常退出且报告当前候选版本 | PASS | 2026-10-29；6004，E20；未连接 6004 服务执行 list/launch |
| AUTO-09 | 原源码完整保存、新集成提交持久化；新客户端文件哈希匹配新 catalog；维护不改变原业务数据 | PASS | E08备份保留；E30/E31证明6006构建匹配bundle及构建/取消更新未改业务哈希；这些检查本身不是运行证据，实际6006客户端/服务业务路径另见E33/E36 |
| AUTO-10 | 真实 Arranger 用既有认证 IPC 获取快照；拒绝错误 operation；无需完整进程查询 | PASS | E18：旧 6003 因要求额外 plan 参数而失败，6004 通过；限权案例仅修改可丢弃 Medium 子进程 DACL，并实际确认 full-query 拒绝、limited-query 成功，不是 High/MIC 验收 |
| AUTO-11 | Capture 等待可见、禁用重复操作/关闭、完成后恢复；开始前取消不启动 native capture | PASS | E23：Release WorkspacesCsharpLibrary.UnitTests 10 passed，其中 2 个新增真实 WPF/STA 窗口用例；模拟 pending Task，不代表已部署 native Capture E2E |
| AUTO-12 | 既有/新窗口匹配要求实际 elevation 一致；查询失败不按普通权限处理 | PASS | E24：Release AppUtils 21 passed，包含当前真实 token 的匹配/相反 elevation 拒绝、无效 PID 两种请求均拒绝；不是实际 High/OTS 新应用启动 |
| AUTO-13 | Capture 快路径保留当前可见应用的名称、路径、包身份与提升能力；快捷方式/Steam/PWA 不丢失必要启动信息 | RETEST | E50当前Photos样本：旧AppsFolder名称Photos，快路径为照片查看器，一致性测试失败；与重复ID修复不同，AppUtils实现/用例本轮未修改，不把此项写成PASS。E29的5项/11应用匹配历史保留，需确认语言策略后复测 |
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
| LOCAL-01 / P0 | 核对新目录全部 PE 版本和 catalog；不得夹带旧 worktree 客户端 | PASS | E56的6011包内全部PE版本/catalog及pinned trust检查通过；E57补充service6011、recovery=0及真实CLI读取成功。仅匹配/读取证据，不扩大为全UI/升级验收 |
| LOCAL-02 / P0 | 退出旧 UI；保留数据更新服务；记录 Code 前后版本、ready、index/blob 哈希及回退证据 | PARTIAL | E33已确认6006部署后状态，B04解除；本轮没有执行或完整见证该次更新的前后数据/回退流程，不能据此把整项判PASS。6004 -> 6005 的PASS保留E25 |
| LOCAL-03 / P0 | 从新 Runner 的 Settings、Quick Access、热键打开 Editor；实际进程全部来自新包，读到正确集合 | PARTIAL | E33真实Settings入口恢复同一6006 Editor并读取原集合；当前版本的Quick Access/热键入口未全部重测，不再归因于部署阻塞 |
| LOCAL-04 / P1 | 开发构建的 settings/log/mutex/event 与正式安装隔离；不改正式配置、不自动更新/卸载正式版本 | NOT RUN | 部署前置已满足；新旧 LocalE2E 本身并非不同命名空间 |
| INST-01 / P0 | A 的真实 per-user 首次预配；carrier 为 A 的 USERUNMANAGED/context 2，服务为对应 VA，不是 SYSTEM/管理员 owner | PASS | E59/P-XU/6010，Windows11 Enterprise26300/x64。用户报告完成首次Setup；原生MSI API独立确认carrier ProductCode及A/context2，实际Bootstrap/Runtime primary SID均为对应VA且Session0，正式CLI可读，signed Setup inspect Ready/native0。沿用E58残留profile，不宣称新profile首次创建或OTS |
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
| DATA-01 / P0 | 真正未初始化的 store + 两个旧工作区；完整校验、提交并重开；持久化后才删除匹配旧源 | PASS | L64/6009：E52真实Reset，E53独立CLI全字段一致、初始sequence=1、source hash/operation ID匹配、cleanupAcknowledged=true且legacy已删除；E55用户确认测试1重开/Reload后数据不变、不重复Setup/迁移。重开部分为人工报告，不冒充代理采证 |
| DATA-02 / P1 | 旧 JSON 损坏、版本/条目不支持；整体拒绝，无部分发布/成功标记，源保留且可修复重试 | NOT RUN | 部署前置已满足；需要独立迁移 fixture |
| DATA-03 / P1 | 旧数据含 elevated/参数等敏感字段；按 module 规则完整验证/迁移，service 不解释业务 JSON | NOT RUN | 部署前置已满足；不能为迁移成功静默丢弃字段 |
| DATA-04 / P2 | 读取/迁移/清理之间替换源文件；提交验证过的同一快照，不能删除后来替换的源 | NOT RUN | 部署前置已满足；独立 fixture，记录文件 identity/hash |
| DATA-05 / P2 | 提交后响应丢失；查询原 operation ID，不能重复导入覆盖成功迁移 | NOT RUN | 部署前置已满足；需确定性故障注入 |
| DATA-06 / P1 | 阻止旧源清理；target 仍权威，显示 pending；Retry cleanup 只删除匹配源，不重复迁移 | NOT RUN | 部署前置已满足；不是 blob GC/服务修复 |
| DATA-07 / P0 | 已初始化后重启/更新/KeepData 重装，或放回旧文件；不得再次自动导入旧副本 | PARTIAL | E37：真实Import合法空集合，放回不同ID的legacy marker，关闭并重新启动Editor、Reload后仍为0条，无再导入；marker恢复。更新/KeepData重装及OS重启组合未测 |
| DATA-08 / P2 | 初始化记录存在但 record 缺失/损坏；明确 recovery-required，不回退明文、不初始化为空 | NOT RUN | 部署前置已满足；仅 disposable VM/数据 |
| DATA-09 / P1 | 并发仅改变启动时间时自动保留最新历史并有限CAS重试；真正配置冲突拒绝覆盖且保留草稿；结果未知先查询原操作 | PARTIAL | E61用户确认6012测试1通过，含编辑期间CLI仅写启动时间后自动合并、不弹冲突；按P-XU人工结果记录。真实配置差异/epoch/未知结果有E56单测，完整UI边界未补；E37旧冲突预期不再算当前PASS |
| DATA-10 / P0 | 创建、改名/参数/布局、保存、重开、删除；完整持久化；保存失败保留可恢复草稿 | PARTIAL | E61用户确认6012测试1的改名/布局保存、重开、CLI读回及启动通过；离线数据确认新名称Workspace 23已持久化。未扩展为删除/保存失败全部组合通过；E36/R06及E33旧记录保留 |
| DATA-11 / P0 | Capture/取消；preview 归属正确、有大小/生命周期限制、可回收，不写可篡改临时计划 | PARTIAL | E33：6006实际Capture/取消/重新捕获；四个自有应用身份、DPI、最小化/最大化状态匹配，包含晚创建窗口；无LocalE2E legacy live文件。跨owner/大小/过期仍未测 |
| DATA-12 / P0 | 已保存/未保存工作区 Launch & Edit；使用准确 preview，重新捕获/保存/取消符合预期 | PARTIAL | E50重复应用ID失败及修复红绿回归保留；E55用户确认测试2：已保存/未保存工作区Launch & Edit、Capture、Save、重开通过，不报错、不丢窗口、ID唯一。原Capture问题的L64修复回归已通过；取消属于未确认的测试4，不因测试2通过将其补PASS。R07独立保留 |
| DATA-13 / P0 | Editor/快捷方式/CLI 启动普通工作区；目标、参数、布局、move-existing、最小化/最大化正确 | PARTIAL | E33：自有快捷方式启动3个Win32 fixture及包Paint，实际普通token、PNG参数、DPI布局/最小化正确；move-existing开关的同一原窗口行为正确。全入口/全部状态组合未覆盖 |
| DATA-14 / P0 | Medium 执行核心 -> High Arranger；同账号提升和 OTS 分别测试；原 owner/会话/精确计划不变，目标应用 elevation 正确 | PARTIAL | E40：用户授权后，普通CLI20828 -> High Arranger24616，普通目标8420/High目标18376均同owner/session2并按保存布局排列，exit0；已有普通同映像29704保持原窗/位置，没有冒充High目标。OTS及其他入口未测；E27/E22历史保留 |
| DATA-15 / P0 | 安装元数据与 last-launched 经可观察 CAS 写回；不覆盖新编辑，不在 destructor 静默 I/O，不因重试重复启动 | PARTIAL | E37：实际CLI报告persistenceStatus=updated并产生非零last-launched；随后编辑Save/Reload保留创建时间和该非零时间，且真实CAS冲突未覆盖native写回。安装元数据变更、写回失败/未知结果等组合未完整覆盖 |
| DATA-16 / P1 | CmdPal/ModuleServices 枚举与启动使用同一 repository/owner；读取不预配，无 legacy escape | NOT RUN | 部署前置已满足；相关正式 host 还需新 catalog/构建证据 |
| DATA-17 / P1 | 已撤回：用户选择文件的显式Import/Export产品功能 | N/A | E56：用户只要求固定旧文件自动迁移，未要求通用导入/导出。删除相应产品入口和专用API；E37测试仅保留历史，不再计PASS或发布门槛。本地测试备份工具不等于新增产品需求 |
| DATA-18 / P2 | 已接受 save 时进入维护/停止/重启；已接受写入结果可确定，未接受请求可重试，不假成功 | NOT RUN | 部署前置已满足；保留 operation ID，未知结果先查询 |

## 7. PR #51114 的共享执行与 CLI

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| CLI-01 / P0 | 普通终端 list/filter/details/JSON 与 Editor 同一集合；不修改 index/blob，不读旧明文 | PASS | L64 E37：text/JSON及按ID/名称details匹配；最终6次读取前后全部index/blob文件名与字节完全相同。错误选项/缺失名称另验证exit2/3；合法空集合放回legacy后CLI仍读protected空集合 |
| CLI-02 / P0 | GUI 和 CLI 的 Medium 入口使用共享执行核心；CLI 同步等最终 arrangement 结果，失败/部分成功不报全成功 | PARTIAL | E37：CLI普通启动exit0/completed/arranged/updated，真实新目标为普通token、参数及144 DPI位置匹配；deadline为exit9/timeout，未伪报成功。全部GUI入口和部分应用失败矩阵仍未覆盖 |
| CLI-03 / P0 | High GUI/CLI launch 先交给同用户/会话的 Medium host；按保存的 elevation 标志启动，不因高权限终端把所有目标提权 | PASS | E55的L64人工结果保留；E61用户另确认P-XU/6012测试3普通/High CLI及GUI可用、普通目标不随入口整体提权。人工报告不冒充代理新PID/token采样；E40/E44失败历史保留，OTS/跨owner另测 |
| CLI-04 / P1 | 拒绝错误 owner/会话、失效进程/映像、非法 private host 输入；普通调用不能注入任意 workspace snapshot | NOT RUN | 部署前置已满足；保护存储和 plan 的身份要求不得因合并而悄悄移除 |
| CLI-05 / P1 | GUI Cancel/Ctrl+C/deadline/owner exit/第二次启动；停止后续应用，保留已启动应用，释放锁，结果未知不自动重放 | PARTIAL | E37的deadline/已有进程保留/新会话证据保留。E40计划取消UAC，但实际exit0并启动High目标，用户操作无法确认，未发生可证实的denial；不能改为取消PASS。该新启动仅证明此前High失败后会话可再次使用 |
| CLI-06 / P1 | 控制台应用独立控制台；CLI stdin/stdout/JSON、退出码、code page、隐私遥测合同保持主线行为 | NOT RUN | 部署前置已满足；错误/警告不得污染 JSON 流，参数/工作区内容不进遥测 |
| CLI-07 / P0 | 普通与提升终端分别验证 list 的 owner 上下文和错误反馈 | PASS | E55 L64与E61用户确认的P-XU/6012普通/High list均通过；按人工结果记录，不新增代理High调用证据。E41失败历史保留，错误owner/失效桌面等负向上下文不外推通过 |

## 8. 权限、隔离与信任

| ID / 优先级 | 操作与通过标准 | 当前状态 | 证据 / 注意事项 |
|---|---|---|---|
| ACL-01 / P0 | 所属普通用户和管理员直接读取/list Data 的 index、blob、日志；其他普通用户/其他 VA 不获得读取权限 | PARTIAL | E37普通owner读取Data目录/index/blob成功；管理员与跨owner/VA矩阵未测。Code不属于此可读承诺，实际Code读取被拒；本轮额外要求Code可读的清单定义被判错，不放宽ACL |
| ACL-02 / P0 | owner/admin 正常文件访问不能写、追加、删除、替换、更改 DACL/owner；SYSTEM/对应 VA 仍可写 | PARTIAL | E61用户确认6012测试3直接修改Data被拒、合法Editor保存成功；代理只读SDDL确认owner/Admins为FR、SYSTEM/VA为FA，未另做写入探针。E37旧探针保留，完整管理员删除/替换/DACL/owner矩阵不由概括确认自动关闭 |
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
| UX-03 / P1 | 进度UI冷启动能收到状态、关闭/取消无挂死；正常界面无额外工具栏，待清理时仅显示旧源清理提示/重试 | NOT RUN | E56移除Import/Export和常驻Reload；pending由持久化迁移回执推导，下次打开先仅重试cleanup。清理不等于重新迁移、blob GC或服务/profile修复；UI失败/恢复完整路径仍需实机验证 |
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
| UPD-01 / P0 | A v1 -> v2 真实 MSI major upgrade；注册及两个 live PE 更新，VA、固定 ImagePath 和数据不变 | PARTIAL | E60的6010→6012及数据证据保留；E64另确认6012→6013事务committed、新carrier、两实际VA进程/固定路径/出生时间与6013 ready相符。未补6013业务数据逐字段比较、主程序升级前完整基线、授权过程及重启，不能整项PASS |
| UPD-02 / P0 | B 升级而 A 留 v1；A 的服务版本/数据不变 | NOT RUN | B02；P-XU/P-AU |
| UPD-03 / P0 | A 随后升级；B 持续健康，双方集合正确 | NOT RUN | B02 |
| UPD-04 / P1 | 用 v1 入口 repair 已注册 v2；修复正确 release，不重播内嵌旧 seed，不重新初始化 | NOT RUN | B02；健康同版本 sync/普通 repair 不重复首次授权 |
| UPD-05 / P2 | candidate readiness 失败；已知 pre-commit 失败恢复旧代码，无混合注册/live-code 假成功 | NOT RUN | B02；隔离快照 |
| UPD-06 / P2 | commit ack 丢失；查询同一 operation，不回滚已确认 committed 的更新 | NOT RUN | B02 |
| UPD-07 / P1 | 真实 MSI 1618/服务 busy；有限等待、明确 Retry，无嵌套 MSI/无限轮询 | NOT RUN | B02；欢迎页停留不算 MSI 事务占用 |
| UPD-08 / P2 | Bootstrap 损坏；普通维护不假成功，显式授权恢复启动组件且保留数据 | NOT RUN | B02；不承诺修复任意坏业务记录 |
| UPD-09 / P0 | per-user 主更新后，在原 owner 的正常上下文协调 carrier | PARTIAL | E60原owner/context2证据保留；E64签名Setup inspect确认6013 carrier及实时Ready，受保护事务表明6012→6013已提交，非只换主程序。未采集实际升级期间协调进程token/授权过程，不将后置检查扩充成完整过程验证 |
| UPD-10 / P0 | per-machine 交互主更新；共享主文件更新，carrier 仍属于原普通业务用户 | NOT RUN | B02 |
| UPD-11 / P2 | SYSTEM/企业无人值守主部署；不创建 SYSTEM-owned per-user carrier，owner 工作明确延期 | NOT RUN | B02；需要真实部署通道 |
| UPD-12 / P1 | 另一 owner 离线；无强制全用户更新期限，登录后按其上下文协调兼容性 | NOT RUN | B02；机器版主文件共享，不要求主程序路径按用户保留旧版 |
| UPD-13 / P1 | A 维护失败时其他模块/B 不受误伤；可诊断、可恢复，不要求重装整个 PowerToys | NOT RUN | B02 |
| UPD-14 / P2 | MSI/VA 提交前后进程终止、重启/断电；恢复或明确 recovery-required，无伪原子性 | NOT RUN | B02；专用 VM/故障注入，未知结果不盲目重放 |
| UPD-15 / P0 | 从**已含 machine-uninstall hook** 的旧版升级；旧 MSI 的升级卸载不得误清全部服务/数据 | NOT RUN | B02；仅 3000 -> 3001 不够，3000 无 hook；旧 3001 或等价基线 -> 后续版本才覆盖 |

## 11. 正式卸载、保留数据与清理

完整卸载是方案要求，不能把写入pending标记或返回3010当作清理闭环。
KeepData仅允许保留约定的业务数据及必要的保留/安全元数据，不默认豁免VA profile、
服务/运行Code、临时helper或已确认不再需要恢复的历史事务/旧可执行副本。
受占用项可以准确报告延期，但应有实际删除重试、结果复核及完成后清理自身的路径；
未解除的占用或未知事务仍按pending/residual报告，不能伪称零残留。
E71手工删除证明当前profile可删，不代表这些产品流程已实现。

| ID / 优先级 | 操作与通过标准 | 当前状态 | 前置 / 注意事项 |
|---|---|---|---|
| REM-01 / P0 | 正常卸载 B；B 的注册/服务按 scope 移除，A 的服务/版本/数据不变 | NOT RUN | B02；两个真实交互 owner |
| REM-02 / P0 | KeepData 卸载重装；原集合、epoch/初始化状态恢复，不从旧明文覆盖或重置为空 | PARTIAL | E68/P-XU：6014正常移除后retained/keep、Data可读且epoch/init ID保留；尚未重装验证恢复，不整项PASS。minimum-reinstall-release为0.101.6014，后续用6014或更新版本，重新预配可需授权 |
| REM-03 / P2 | 显式 PurgeData 仅删授权 owner 的业务数据并记录 import suppression | NOT RUN | B02；破坏性，只用可丢弃数据；不删真实用户账号/profile |
| REM-04 / P0 | 正常机器版卸载实际触发全 owner 清理；新 report 覆盖 A/B，默认 KeepData | NOT RUN | B02；检查新 operation/time，不能拿旧 report 或退出 0 代表零残留 |
| REM-05 / P1 | owner 离线导致 carrier 注册无法清理时，准确报告 residual，不手删 MSI 内部注册、不冒充用户嵌套安装 | NOT RUN | B02；允许设计内 best-effort 残留，不等于全量清理 PASS |
| REM-06 / P1 | VA profile loaded 时报告 pending/原生码，解除占用后实际删除并复核目录/注册记录消失，清除已完成标记；不强制 hive unload | PARTIAL | E72已实现直接删除、受保护任务重试、重装取消及自清理；SYSTEM隔离任务实际覆盖占用失败保留worker、释放后重试成功、过期请求不删profile和外部链接安全。真实签名安装包的卸载/重启链路未验证，不因共享代码fixture通过而整项PASS |
| REM-07 / P1 | 仅清除已验证、当前 owner、terminal 的 helper stage；其他 stage 和未知结果证据保留 | NOT RUN | B02 |
| REM-08 / P1 | 授权取消、访问拒绝或未知移除结果不误报成功/不存在；保留准确残留及恢复入口 | NOT RUN | B02 |
| REM-09 / P1 | 已移除后重复清理幂等，不重建资源，不误删别的实例 | NOT RUN | B02 |
| REM-10 / P2 | remove/rollback 失败仍有可恢复 Code/Data，不使成功的 peer 安装失效 | NOT RUN | B02；快照环境 |
| REM-11 / P0 | per-user 正常外层 EXE/“已安装应用”卸载触发 owner 回调，或明确报告保留的服务/carrier | PASS | E68/P-XU/6014：普通外层/uninstall，perUser1/action4/related0/armed1，主MSI成功后ownerCleanup1，SYSTEM prepare/commit-remove执行；carrier/服务/运行Code实际移除。profile-review/3010另计REM-06，重装另计REM-02；E61/E65失败保留 |

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
| P-XU | PARTIAL | E59首装/E60升级及人工业务结果保留；E68的6014普通外层卸载关闭REM-11。KeepData重装、profile清理以及其他数据/授权/重启/异常组合仍待补，不关闭整环境矩阵 |
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
当前工程类18/27已整项通过，剩余生产配置/打包及Photos一致性复测等缺口分开补。
任何实机包先核对实际客户端/runtime/catalog及程序集，不为切换case反复清VA或Reset。

| 工作包 | 一次准备后串起来执行的主要场景 | 主要对应要求 | 当前缺口和环境 |
|---|---|---|---|
| A 日常业务回归 | 创建/Capture→编辑保存→重开→GUI/CLI启动→复用/重捕获；区分历史自动合并与真正CAS冲突，再测取消/结果字段 | LOCAL-03；DATA-09～13、15～16；CLI-01/02/05/06；UX；PERF | E56撤回显式Import/Export，不再用其搭建产品验收场景。补历史合并、真实冲突、R07、未保存/重捕获后Save及取消等缺口；不要重复已通过案例 |
| B 权限边界与A/B隔离 | 普通/High前端×普通/提升目标的关键组合；同账号后补OTS；复用不匹配窗口、授权取消；同一批Data/Code/client身份探针再覆盖A/B隔离 | DATA-14；CLI-03/04/07；INST-07/08；ACL；SEC-01～05；ENV-02 | E55已有同owner普通/High入口人工通过，普通owner拒写/caller拒绝另有证据；仍缺管理员文件权限、OTS、第二账号/VA及负向身份分支，需相应授权和真实登录环境。Debug能测机制，但不关闭正式安装注册条件 |
| C 数据迁移与恢复 | 在可还原基线中做首次迁移、坏源、合法空集合、不再导入、清理失败、CAS/提交中断、操作结果查询、blob回收及服务/系统重启 | DATA-01～09、15、18；BLOB；CLEAN；LIFE-01及相关恢复子条件 | 主要剩余数据风险；需专用账号或隔离VM。E53首次迁移/匹配源清理及E55人工重开确认已闭合DATA-01；空集合/旧文件和CAS已有部分证据，故障边界仍待覆盖。不能在当前store手删index冒充首次初始化 |
| D 安装升级卸载旅程 | 按安装scope执行：首次安装/预配→第二用户→A先升级/B留旧版→B升级→repair→单用户卸载/KeepData重装→机器级清理；每阶段检查注册、VA、Code、数据和peer状态 | LOCAL-01/02/04；INST-01～06；UPD；REM；LIFE-01 | 条目最多，但不是每条做一次全新安装。32个正式安装/升级/卸载条件需当前正式包和配对旧版；本地helper只能补L64机制。特别保留UPD-15旧卸载hook检查 |
| E 生产信任与最终产物 | 围绕同一发布候选和受控签名fixture执行有效/非法catalog、身份、timestamp、吊销/离线、生产配置及原始MSI字节检查 | SEC-06～11；BUILD-02/04及相关生产构建；REL-02的证据组成部分 | 需要生产verifier路径和适用产物；Debug/local signer、mock或旧包不能代替 |
| F 环境兼容与性能收尾 | 选择明确的安装路径、架构/OS、屏幕/DPI/断接配置，重跑与该维度有关的核心路径；补冷暖/重复性能样本 | ENV-01/03/04；PERF及受影响业务项 | 要有真实设备/环境；不默认每个组合机械重跑全部有效条目，也不把单屏/一次4.982秒当全矩阵通过。正式发布组合仍须分别留证据 |

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

### E42：2026-11-02，6007签名构建首轮终态及x64 ICE失败诊断

[ADO 162529587](https://dev.azure.com/microsoft/Dart/_build/results?buildId=162529587)
首轮在**17:00:23（UTC+08:00）**结束，整体`failed`；后续人工重试终态见E54。源码仍为
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

### E46：2026-11-04，获准执行无caller-DACL认证矩阵

用户重新明确授权后，在独立服务`PtStorageAuthProbe_36ab620b7259`上执行真实VA实验。
E45取消的输入及结果没有覆盖。新运行ID为`36ab620b72594fcbbcd52dffd86f2e4e`；
三轮复用同一个临时服务名/VA身份，每轮均停止、删除服务并移除隔离stage。
实际使用3次同owner/session UAC，而非最初预计的1次；没有继续重复请求提权。

本实验使用独立客户端、现有common helpers及协议，不是修改后的生产SDK或Workspaces
二进制。caller不修改自身process/token DACL，前后读取完整owner/DACL/label作比较。
服务仅为自身对象增加owner只读观察权限；SCM required privileges配置为
`SeChangeNotifyPrivilege/SeImpersonatePrivilege`，未配置SeDebug。
High连接使用`SecurityIdentification`；仅对非提权、可模拟的普通pipe caller临时模拟，
完成校验后恢复VA进行数据I/O。没有借用可执行的High调用者模拟上下文。

#### 夹具纠正及证据范围

| 轮次 | 实际结果 / 纠正 |
|---|---|
| 首轮 | 11份报告。2个High请求到达服务但认证失败；其余9项先被夹具的反向服务验证阻塞，不能算目标机制FAIL。夹具比正式SDK多申请了服务进程的QUERY_INFORMATION并额外要求服务映像映射校验 |
| 服务验证对齐 | 改为正式SDK同类的QUERY_LIMITED_INFORMATION/SYNCHRONIZE、真实VA身份、映像路径、SCM配置/PID和pipe/lifetime绑定；未放松服务端caller认证。普通Medium及3个负向控制通过，High/派生Medium仍失败 |
| 最小权限补测 | 普通caller模拟分支也分别尝试`0x1000`、`0x101000`、`0x101400`进程访问掩码，排除只申请过大权限的干扰；结果和具体阻塞点见下表 |

三轮共33份客户端报告，**不是33个独立验收用例**。最终矩阵11项：
7个控制项PASS，4个应成功的候选机制用例FAIL；最终11项caller描述符均未改变。
两个修正版本的独立原生夹具均以x64、`/W4 /WX`编译通过；未重建产品。

| 最终场景 | 数量 / 结果 | 证据 |
|---|---|---|
| 普通Medium：公共路径读/写/读回、用户私有路径读 | 4 PASS | VA直接查询caller被拒绝；模拟普通pipe caller后可核验token、实际映像映射及hash，再恢复VA读写。数据`initial -> updated`，revision为1 |
| 普通Medium：未注册映像、只读角色写入、错误target | 3 PASS | 分别返回`CallerImageDenied`、`TargetDenied`、`TargetDenied`，确实到达对应授权检查，不是前置认证失败冒充负向PASS |
| 纯High：公共/私有路径读 | 2 FAIL | High身份以Identification级到达服务，避开了原linked-token前置失败；但VA以三种掩码打开真实caller进程均返回5，无法取得完整认证证据 |
| High经PPID方式创建Medium：公共/私有路径读 | 2 FAIL | 普通pipe模拟后，limited-query句柄可读取owner/session/token及路径文件hash；但`ProcessImageFileMapping`类44查询返回`0xC0000022`。请求完整query权限则OpenProcess返回5；不能把路径/hash当作实际加载映像证明 |

最后一行实测caller主token内容为Medium，而process/token对象仍为Administrators owner及
High label；process带NW/NR，token带NW。本轮没有更改这些对象，也没有省略实际映像
绑定来制造成功。所有成功数据操作都记录了真实VA SID；正式签名/CMS catalog、跨owner、
生产安装及完整Workspaces启动未因此获得PASS。

**技术结论：这条“不改caller DACL + High仅身份识别 + VA/普通caller模拟”的候选路径，
在本机没有解决这两个已知问题中的任何一个：纯High、High派生Medium均仍不能完成认证。**
普通Medium成功只是基线对照，不代表修复了其中一项。去掉客户端失败点后，阻塞转移到了
服务端进程/实际映像取证。它不是所有无DACL方案均不可能的证明，更不是整个VA隔离存储架构NO-GO。
`CreateProcessWithTokenW`与PPID的创建方式A/B、替代owner连接token均未在本轮验证。

#### 清理及保留阻塞

最终独立核对：临时service和protected stage不存在，无本轮测试进程；三个本轮诊断EXE及
中间OBJ、private-client目录已清理，源码/编译日志/输入/原始结果保留。
原6006的index/blob哈希和ready内容与首次运行前一致，未改产品代码、服务配置或业务数据。

**仍有一个已停止/删除服务对应的临时VA profile被Windows保持加载：**
`C:\Windows\ServiceProfiles\PtStorageAuthProbe_36ab620b7259`。
`DeleteProfileW`返回87；针对该临时SID hive的`reg unload`返回Access denied。
没有强删已加载profile、停止系统Profile服务或重启机器；该清理项BLOCKED，待释放占用后
定点清理，不能称为全部无残留。

证据根为会话files下`nov04-auth-36ab620b72594fcbbcd52dffd86f2e4e\`：
根目录保留首轮；`server-verification-recheck\`保留第二轮；
`query-limited-recheck\`保留最终轮。
`final-result.json`核对11项结果、全部caller描述符不变、原数据不变和残余profile；
每轮`input.json`、源码、client JSON、`service-events.jsonl`、`cleanup.json`及
`execution-summary.json`均保留。首轮driver未将cleanup.errors计入退出码，后续已修正；
因此首轮退出0不表示profile清理成功。后两轮退出1来自已记录的profile清理阻塞，
不是没有运行客户端矩阵。

最终夹具EXE清理前SHA256：
`9BC1CDAD8EE30E495BABC237612CE197FBD7F1B97D108A0E032912C2DEC528B3`。
正式126项状态仍为26 PASS / 22 PARTIAL / 2 FAIL / 1 BLOCKED / 75 NOT RUN。

### E47：2026-11-04，独立验证process-only查询授权和High普通连接token

用户要求先验证候选1、2；本轮只验证这两项，未执行`CreateProcessWithTokenW`与PPID
创建方式A/B，也没有将两项组合为统一SDK实现。获1次同owner/session UAC，
复用E46临时服务身份`PtStorageAuthProbe_36ab620b7259`，未新增第二个VA profile。
新证据运行ID为`1ed65c5636b34b0bb15b858ff6e087c1`。

独立原生夹具x64 `/W4 /WX`编译、脚本语法和普通owner Explorer-token只读预检通过。
复用E46已对齐的SCM/pipe服务验证和有界传输，服务端始终校验真实pipe PID、primary
token owner/session、实际映像映射、hash、角色、固定target和进程存活。
成功数据操作前明确检查线程没有遗留模拟token，再以精确VA身份执行数据I/O。
固定fixture路径/hash仅为实验许可，不是生产CMS/catalog或正式签名资格验证。

#### 两项分别验证的机制

| 候选 | 实际改动 | 观察 |
|---|---|---|
| 1：process-only查询授权 + 分离认证 | caller仅给自身process增加查询/SYNCHRONIZE ACE，不改自身token DACL。服务先以VA取得完整查询进程句柄，再模拟普通pipe caller查询primary token、打开映像并校验映像映射，恢复VA后读写 | 普通Medium及当前PPID创建的Medium均通过；派生进程的process/token owner仍为Administrators、对象label仍High，未通过改label或换创建API规避问题 |
| 2：Explorer普通token连接 + 原有查询授权 | High客户端保持High，取得并核验同owner/session、真实Windows Explorer的普通primary token，仅在建立连接时使用。caller仍保留原有process/token两项查询ACE；服务仍以VA查询primary token，再用普通pipe上下文检查映像 | 公共及私有路径High客户端均完成读/写/读回；服务认证的是实际High客户端PID及High primary token，不是Explorer。pipe token为普通Medium/SecurityImpersonation，不提供可执行High模拟上下文 |

候选1实测process上的VA ACE为`0x101400`，全部A类客户端的token描述符逐字未变；
process owner和mandatory label也未变。VA直接查询这些token仍返回5，但在普通pipe
模拟上下文查询成功；VA预先取得的完整process句柄支持映像类44查询，返回0。
这证明不能只删除token授权代码而保留原认证顺序。

候选2为隔离连接token来源这一变量，**明确保留原有token DACL查询授权**；
新增VA token权限仅`TOKEN_QUERY (0x8)`，并非TOKEN_DUPLICATE或写权限。
因此候选2成功不能表述为“High也已无需修改token DACL”。

#### 最终18项与故障对照

| 分类 | 数量 / 实际结果 |
|---|---|
| 正向读写 | 6 PASS：普通Medium公共写/读回及私有读；PPID派生Medium公共/私有写及读回；纯High公共/私有写及读回 |
| 授权负向 | 9 PASS：普通Medium、派生Medium、纯High各验证未注册映像、只读角色写入、错误target；均先完成真实映像证明，再分别拒绝为`CallerImageDenied`或`TargetDenied` |
| 不授权process的对照 | 1项按预期失败：VA OpenProcess返回5，证明完整进程查询权限仍有必要 |
| 仅授权process但仍由VA查询token的对照 | 1项按预期失败：process打开成功，token查询返回5，证明需要服务端分离取证上下文 |
| High仍取linked token的对照 | 1项按预期失败：实际同owner非提权linked token为TokenImpersonation/SecurityIdentification，真实`OpenOwnerConnectionToken`返回1346 |

共6个正向、9个授权负向、3个故障对照，不能把18项都描述为业务操作成功。
5次写入均有真实读前revision、写后读回，revision依次从0增长到5。
所有已到达映像校验的授权负向都记录映像映射状态0，未用前置认证失败冒充角色/target
校验PASS。

具体证据示例：候选1派生Medium PID24148在token DACL不变时读写成功，
VA token query仍为5、普通owner query成功、mapped-image status为0；
候选2 High PID37304通过Explorer PID10432的普通token连接，服务记录caller PID37304、
High primary token及普通pipe token，并完成真实映像绑定和数据I/O。
这些PID仅为历史观察，不作为当前存活或身份依据。

**结论：候选1和候选2分别在本机所测上下文中可行。**
未验证两者组合后High也仅授权process的路径；未修改原生/托管产品SDK、Workspaces
创建流程、生产runtime或安装器；未验证无Explorer、OTS、跨owner及其他OS环境。
所以CLI-03/CLI-07仍FAIL，正式126项和26条PASS均不改变。

#### 收尾与证据

临时service、protected stage、private-client目录、诊断EXE/OBJ及本轮进程均已清理；
原6006 index/blob哈希和ready与本轮起点一致。E46归档166个文件逐一复核哈希未变。
复用的旧profile仍为
`C:\Windows\ServiceProfiles\PtStorageAuthProbe_36ab620b7259`、Loaded=true；
它是E46既有清理阻塞，不是新增profile。本轮没有再次强行卸载hive或重启系统。

会话files下`nov04-owner-query-1ed65c5636b34b0bb15b858ff6e087c1\`
保留`input.json`、两个C++源文件、脚本、编译日志、18份最终case JSON及RPC子记录、
service事件、清理记录和`final-result.json`。
独立收尾脚本逐项验证真实caller PID、owner/session、High保持High、普通pipe token、
query-only ACE、token/label变化边界、映像hash/映射、角色/target、读回/revision及原数据不变。
诊断EXE清理前SHA256：
`3440B73C8FDC476D80A2C5BA851479C493AE695DE9FABC4423BD6B84CDFF748F`。

### E48：2026-11-04，统一“不改caller token DACL”的组合验证

首先澄清E47的方向：Medium候选1不改token DACL；High候选2仍保留token查询授权。
用户认可统一实现值得验证，本轮将High的普通owner连接token与候选1的process-only
授权/分离认证组合，未修改产品SDK、Workspaces创建方式或生产runtime。
运行ID为`fc285f32acb247219be495c110371e79`，获1次同owner/session UAC，
继续复用E46临时VA身份，没有新增profile。

三类正常调用统一为：

1. caller仅给自身process增加VA查询/SYNCHRONIZE权限，不修改自身token DACL。
2. Medium使用普通调用上下文；High保持High，仅建立pipe时使用已验证的同owner/session
   Explorer普通token。
3. 同一个服务认证分支先以VA打开真实caller process，再在普通pipe模拟上下文查询其
   primary token、打开映像、完成实际映像绑定和hash/角色/target校验。
4. 清除线程模拟后，以VA身份进行受保护数据I/O。

这不是完全不修改caller ACL：process上的query-only授权仍保留。测试服务也仍在自身
初始化时提供owner观察服务身份所需的查询权限；“token不改”限定为caller自身token。

| 场景 | 正向结果 | 授权负向 |
|---|---|---|
| 普通Medium | 公共路径写/读回、私有路径读通过 | 未注册映像、只读角色写入、错误target均正确拒绝 |
| 当前PPID方式创建的Medium | 公共/私有路径写及读回通过；process/token对象仍保留原High label和Administrators owner | 同上三项正确拒绝 |
| 纯High | 公共/私有路径写及读回通过；调用进程前后仍High，服务认证实际High primary token及caller PID | 同上三项正确拒绝 |

最终18项由6个正向PASS、9个授权负向PASS和3个预期失败对照组成：
不授予process查询权限仍在VA OpenProcess返回5；原linked-token路径仍返回1346；
High不采用普通owner连接token、仅发送Identification上下文时，
返回`OrdinaryImpersonationRequired`。没有把这些对照当成业务成功。

**18个caller token描述符均逐字未变**；process owner和mandatory label也未变。
所有正常认证及授权负向使用同一个`split`分支；VA直接查询caller token仍返回5，
但普通owner模拟上下文查询成功，实际映像类44绑定返回0。
5次写入均有读前revision及写后读回，revision由0依次增长到5。
成功I/O前检查线程不存在模拟token，并记录精确VA身份。

例如High PID15804借助Explorer PID10432的普通token连接；服务记录的caller PID仍为15804、
primary token仍High、pipe token为Medium/SecurityImpersonation，token描述符不变，
实际映像绑定和写后读回均成功。这些PID仅为历史证据。

**结论：统一“不改caller token DACL”的组合在本机所测三类调用上下文中可行，
已不再仅是两项独立候选通过。** 下一步是接入真实原生/托管SDK和runtime，并用真实
High list/launch及普通/负向路径回归；本轮没有完成产品修复，不关闭CLI-03/CLI-07。
Explorer不可用、OTS、跨owner、其他OS及生产CMS/catalog资格仍未验证。

收尾核对：原6006 index/blob哈希和ready不变；本轮service、stage、private-client、
诊断EXE/OBJ及测试进程已清理。E46的166个、E47的109个归档文件哈希均未改变。
E46已有的`C:\Windows\ServiceProfiles\PtStorageAuthProbe_36ab620b7259`
仍Loaded=true并保留，未新增profile，也未强制卸载hive或重启系统。

会话files下`nov04-owner-query-combined-fc285f32acb247219be495c110371e79\`
保留输入、两个C++源文件、脚本、编译/预检日志、18份case结果及RPC子记录、service事件、
清理记录和`final-result.json`。x64 `/W4 /WX`编译及独立收尾断言通过。
诊断EXE清理前SHA256：
`B89B7A68EF180D7D8C562B4F75139D561E46C1E3A7E9280DDC6ACC4CC08928D9`。

### E49：2026-11-04，产品SDK/runtime整合、构建与部署阻塞

用户要求继续代码整合。产品代码已按E48验证的路径修改，而不是继续停留在独立Probe：

- 原生数据客户端使用`AllowProcessIdentityQuery`，仅为自身process增加查询权限。
  `AllowIdentityQuery`继续用于服务自身的process/token观察授权，避免破坏客户端对服务
  身份的验证。
- 公共`AuthenticateOwner`以VA取得真实caller的完整查询进程句柄，再在普通pipe模拟
  上下文读取primary token并核对owner/session。映像映射、catalog角色、target和
  进程存活检查保留。维护调用者的非提权检查移到普通模拟上下文内，仍检查真实primary token。
- 原生High连接从同owner/session的真实Windows Explorer取得非提权Medium primary token，
  不再依赖不可模拟的linked token；不存在合适桌面时仍以OwnerContextRequired失败。
- 托管数据SDK同样只修改process DACL，并验证High连接token的owner、session、类型、
  IL和Explorer进程存活。Workspaces的PPID创建和业务启动流程没有修改。

**有意保留的兼容例外：Bootstrap/MSI控制通道仍准备原有process/token查询授权。**
旧Bootstrap在更新前需要直接查询维护调用者token；删除该授权会阻断对旧服务的状态/
升级控制访问。这个例外位于原生`Common::Call`控制通道，不在普通数据SDK请求路径。
实际用新编译的MaintenanceClient查询尚未更新的6006 Bootstrap成功，返回healthy、
workerReady及6006版本；没有执行MSI更新或修改原数据。

#### 已完成的工程验证

| 项目 | 结果 |
|---|---|
| 原生产品存储回归 | Release x64构建及3031断言通过，另含20000次确定性frame mutations；包括实际process query-only授权、caller token owner/DACL/label不变 |
| 托管产品回归 | 50项通过；新增真实进程授权/token DACL不变、普通连接token owner/session不匹配拒绝。首次新测试using排序触发SA1210，修正后重新构建及测试通过 |
| 维护关联项目 | Setup、MsiAction及InstallerTests构建通过；4129条installer contract检查和main-installer静态集成检查通过，未执行真实MSI |
| 完整本地包 | x64 Debug `0.101.6008.0`构建通过，包含原生/托管Workspaces客户端、Bootstrap/Runtime、Runner及相关UI；build-pinned CMS正确内容通过、篡改/不同signer拒绝 |
| 旧服务兼容性 | 新MaintenanceClient真实查询原6006 Bootstrap成功；证明旧控制通道可达，不代表完整旧版本MSI升级已验收 |
| 原生格式 | 仅对修改行运行仓库clang-format配置，未全文件重排旧代码 |

使用现有构建包装器，未添加依赖；未进行新的ADO排队、正式签名或发布。
完整包的QuickAccess WMC1500弃用告警是既有告警，不作为错误或新功能PASS。
代码修改局限于ProtectedStorage公共库、托管客户端及相应测试；SDK用法文档同步更新。

#### 实际部署未发生

已备份原index/blob并记录ready；构建前普通6006 CLI list通过。
构建关闭了本地调试版Runner PID13580和QuickAccess PID24664，未发现打开的Editor。
随后一次维护UAC返回`ERROR_CANCELLED (1223)`，提升控制器未启动；
询问是否允许重试时用户不可用，因此没有重复提权。

结果不是“6008 High入口测试失败”，而是**尚未运行**：
真实High list、High缺失ID诊断、High Editor读写、受控工作区启动及恢复均没有新实机结果。
测试控制器、观察/空白窗口夹具仅已准备和编译，不能把其计划行为写成执行成功。
未导入或启动测试工作区，未重置数据，未创建维护服务或新profile。

**当前环境需要明确交接：磁盘Debug客户端已是6008，运行服务仍是6006，
尚未匹配；Runner暂未恢复启动。** 需获准完成保留数据的本地服务更新后，再验证和使用
该调试包。原服务host11888/worker13852及ready6006保留，原index/blob哈希未变。
E46遗留临时VA profile仍是既有阻塞，未在本轮操作。

正式CLI-03/CLI-07保留旧6006的FAIL及E41/E44根因；6008待实机复测，不新增PASS。
正式126项仍为26 PASS / 22 PARTIAL / 2 FAIL / 1 BLOCKED / 75 NOT RUN。

证据：会话files下`nov04-sdk-product-results\`保存原文件备份、baseline、完整包构建日志、
旧Bootstrap状态、关联项目/installer检查日志和准备的live脚本输入。
`nov04-sdk-native-tests.log`及`nov04-sdk-managed-results\managed-storage.trx`分别记录
3031断言和50项测试；取消记录来自`nov04-sdk-live`工具执行结果。
后续必须先核对该目录和实际版本状态，不能盲目重跑会覆盖证据的准备脚本。

### E50：2026-11-05，Launch & Edit后Capture重复应用ID

用户报告管理员窗口CLI list/launch已通过，但编辑工作区、Launch & Edit后Capture出现
“check the input and retry”对话框。已读到实际服务6008、recovery=0；
Editor PID37664为同owner/session2的Medium进程，Editor/Snapshot EXE哈希匹配6008 bundle。
用户的管理员CLI成功报告保留，不把Medium Editor观察伪装成High CLI token证据。

#### 根因证据

- 16:10:54至16:10:59 Launcher PID9168完成shared session，说明失败不在本次launch。
- Snapshot PID31016、6132分别在16:11:04、16:11:14记录
  `Could not create a complete protected Workspaces capture`；原catch-all没有保留异常细节。
- 托管栈停在`RunSnapshotToolAsync`的exit/reference检查，未进入后续预览读取。
  InvalidOperationException被统一映射成InvalidPayload，形成截图中的误导性提示。
- 只读检查发现Explorer HWND20711900、2557468携带相同
  `PowerToys_WorkspacesAppId`：`{E6301185-2051-4BF7-8DE2-1AD014E379E3}`；
  这两个窗口也出现在失败Snapshot的枚举日志中。
- 用原6008 Snapshot枚举对象和库做不发布的重放，采集12个应用：
  每个应用单独校验都通过，整份预览因重复Explorer ID返回InvalidPayload。
  仅为重复项重新生成ID，整份序列化即可通过。没有构造Repository实例或提交preview。

窗口上的旧ID标签可以跨launch保留；原Capture只为缺失ID补值，不处理重复标签。
新预览要求应用ID唯一，所以这个具体问题发生在Workspaces捕获数据整理/校验层，
不是E41/E44的VA token访问失败，也不能靠重跑同一Capture必然恢复。

#### 修复与验证

- Snapshot保留第一个已有ID，为重复（大小写不敏感）和缺失ID生成新GUID，保留所有窗口
  及其他应用属性。生成失败立即报错，不进入无限重试；未放松保存/导入校验。
- 原生日志保留StorageError native code和异常原因；Editor将子进程失败/无效返回引用
  标记为CaptureFailed，使用现有本地化Capture提示，不再误标为输入文档无效。
- 新回归用原“只补空ID”行为运行：RepositoryTests 9 PASS/1 FAIL，失败恰为重复ID案例；
  修复后10/10 PASS。覆盖重复ID大小写、空ID、保留窗口/位置、序列化和再次整理的稳定性。
- 托管存储/呈现套件51/51 PASS；Editor与Snapshot的Release构建通过。
  新include最初暴露ShellExecute宏污染，已将GuidUtils不需要的Shell依赖缩为ObjBase，
  并补全自身标准头；随后Snapshot构建通过。
- 因共享GUID头变化，复跑默认原生套件：194项中193 PASS/1 FAIL。
  唯一失败是未修改的`CaptureDiscoveryPreservesVisibleApplicationMetadata`：
  当前Photos的旧枚举名称为Photos、快路径为照片查看器，单独运行也失败。
  未为本次修复修改该逻辑/断言或宣称整套通过；AUTO-02/13记RETEST。

诊断及测试驱动的失败也保留：首次诊断缺ShellAPI声明导致编译失败，随后补齐；
首次诊断输出了完整校验数据，但自身COM退出期间AV，离线map定位到VirtualDesktop析构。
修正的是一次性诊断进程的COM生命周期，未据此宣称用户产品发生了该崩溃；
修正后只读重放exit0，仍得到相同重复ID结论。初次红测指向错误DLL目录，未执行测试，
不计为红测；另有旧Debug DLL不含Photos新用例的NoTests结果，不计为基线通过。

#### 运行环境与用户窗口

本轮只构建Release，没有覆盖live Debug6008或更新catalog/service；修复还未部署到当前UI。
询问是否允许处理草稿后部署时用户暂不可用，因此未自动停止Editor或触发UAC。
随后用户明确要求关闭工作区：已在精确Editor37664中选择错误对话框Cancel，
待Capture正常返回后调用正常窗口关闭，进程已退出；没有强杀、自动保存或启动应用。
Explorer、Notepad等工作区目标应用没有被关闭。

在替换匹配本地包并完成真实Capture复测前，DATA-12保持FAIL。
部署修复还需新的匹配构建/维护授权；不能将当前Release验证称为6008已热修复。
17:26用户再次报告相同对话框。再次核对：Snapshot EXE仍为11-04构建的6008、
SHA256 `CA74D13F96421A3B49F899CDAEED93294ABDE4B650E9CA8A92057AF0CB8D0ED1`，
与旧6008 bundle一致；17:26:24仍输出旧catch-all文本。
新打开的Editor为PID14460；服务仍6008（host32220/worker9128、recovery=0）。
这不是已部署修复后的失败，而是仍未更新实际使用的Debug包。
询问是否允许处理当前草稿、关闭本地Editor/Settings/Runner并部署6009时用户不可用；
本次没有再次关闭窗口或触发UAC。已完成的Release源码验证不能替代这一步部署。
证据根为会话files下`nov05-capture-results\`：
`lifetime-corrected\validation.json`及捕获副本、红/绿TRX、托管51项TRX、
原生全套/Photos单项结果、产品构建日志与Editor身份观察均保留。
原截图和此次6008日志作为本地证据保存，不提交包含用户窗口标题的捕获副本。

### E51：2026-11-05，完整构建6009但维护UAC取消

用户明确要求代理关闭本地Debug测试程序、直接build/deploy，不再反复询问普通操作。
本轮已按此授权关闭该工作树下Runner32980、QuickAccess31388、Settings19152和
Editor14460，未关闭Explorer/Notepad等目标应用。

完成现有`Build-PowerToysLocalE2E.ps1 -Mode Build -Version 0.101.6009.0`：
全套同版本客户端、Bootstrap/Runtime、catalog/bundle构建及pinned CMS正/负检查通过。
因此6009确实已经build，不再只是Release/source修复。构建前原index/blob已备份，
工作区清单与每个ID的details已保存。首次基线命令误用无selector的`--details`，
CLI拒绝invalidArguments；改为逐ID读取后继续，未将该命令错误算成产品回归。

随后一次保留数据维护UAC返回`ERROR_CANCELLED (1223)`。提升控制器没有启动，
未产生`repair-result.json`；询问能否再接收一次UAC时用户不可用，因此未自动重复提权，
也没有通过已有管理员窗口或其他机制绕过确认。

**E51结束时磁盘Debug为6009，服务仍6008，尚未匹配；后续状态见E52。**
构建/关闭已完成；剩余的是获准执行保留数据repair及真实注册Snapshot/Editor验证。
准备的`nov05-capture-6009-native.ps1`会直接运行真实Snapshot的LaunchAndEdit模式，
检查重复窗口标签、exit/reference及原数据，但本轮未运行，不伪造Capture已通过。
部署后还须完成实际Editor预览读取及完整UI路径；DATA-12保持FAIL。

会话files下`nov05-capture-6009-results\`保留baseline、原文件备份、6008原日志、
逐ID清单、完整6009构建日志。驱动为`nov05-capture-6009-build.ps1`、
`nov05-capture-6009-repair.ps1`及`nov05-capture-6009-native.ps1`。
Source/Release验证与完整包构建都不能替代这次被取消的实际服务更新。

### E52：2026-11-05，首次Setup和迁移的清理/准备

用户要求实际执行清理和准备，而非只提供步骤。重置前确认客户端及
LocalLifecycle均6009，运行服务ready为6009、host32532/worker33616、recovery=0。
这取代E51结束时的版本不匹配状态，但不补记High CLI或Capture通过。

先通过现有Stop模式关闭同owner、同Debug路径的Runner33236及QuickAccess29212；
随后用普通owner CLI逐ID读取完整数据，将两条工作区保存为清理范围之外的
portable JSON：`Migration - Notes`（1个应用）、`Migration - Two Windows`（2个应用）。
未关闭Explorer/Notepad等目标应用。

接受一次同owner/session UAC后执行：

```powershell
& 'D:\PowerToys-Workspaces-Integration\tools\build\Build-PowerToysLocalE2E.ps1' -Mode Reset -SkipBuild -Version 0.101.6009.0 -BackupBeforeReset
```

原生`clean-backup`回执为`ok/serviceRemoved/testDataRemoved/backupVerified=true`，
且`exportedWorkspaces=true`。保护数据备份位于本地测试Backups的该owner目录下，
本次ID为`F8518FDE-A1D2-4D74-81F0-CA3999995DFA`。
服务VA profile仍被Windows占用：`profileCleanupPending=true`、native code32；
没有强制卸载hive或删除无关实验profile，Installer仅保留`local-test.json`身份记录。

重置后以普通owner将样例复制到
`%LOCALAPPDATA%\Microsoft\PowerToysLocalE2E\Workspaces\workspaces.json`。
普通文件及SHA256与外部样例一致：
`40CD7A466B978CCEC8F27A0F5AB2797A186B690A9602EDD62B18484387846FA3`。
再次确认测试服务、该owner Code/Data/Policy均不存在，无purge/维护状态，
本地Debug进程为0。6009构建/bundle及桌面Local E2E快捷方式保留。

**仅完成清理和迁移准备，没有打开Editor、执行Setup或消费迁移输入。**
用户下一步从普通桌面快捷方式进入Workspaces，点击Set up并接受UAC，
再观察两条工作区是否迁入及legacy文件是否按receipt清理。
当前不记迁移或live Capture PASS，正式状态计数不变。
本地证据在会话files的`nov05-migration-preparation`，
包含外部样例、input/ready、reset日志和原生清理回执；不提交用户JSON。

### E53：2026-11-05，首次Setup后的迁移与源清理成功

19:43用户提供首次打开后的Editor截图，显示E52准备的两条工作区，
`Migration - Notes`有1个应用、`Migration - Two Windows`有2个应用。
19:44只读核对确认6009服务Running，host36448/worker23468，recovery=0。

新保护index/blob于19:42:31写入。repository sequence为1，
initialization operation为`15889ba5-b465-4322-9098-89a9b440cd82`，
与migration receipt的MigrationId一致；source hash准确匹配E52外部样例：
`40CD7A466B978CCEC8F27A0F5AB2797A186B690A9602EDD62B18484387846FA3`。
`cleanupAcknowledged=true`，固定legacy路径的`workspaces.json`已不存在。
另启普通owner CLI逐ID读取details，两条工作区及三个应用的全部JSON字段
与样例完全相同；核对前后index未变，不只是UI缓存命中。

截图中的Retry cleanup按钮在MainPage.xaml中无条件显示，不是pending状态标志；
本次实际源清理已经完成，无需点击。此处source cleanup与E52服务profile占用残留
不是同一项清理。

**本次Local E2E首次Setup、迁移提交、完整数据读取和匹配源清理成功。**
未关闭重开Editor、重启服务/系统或复测Capture；DATA-01含重开条件，记PARTIAL，
不将单个成功场景扩展为完整生产安装/迁移矩阵PASS。当前为24 PASS、22 PARTIAL、
3 FAIL、2 RETEST、1 BLOCKED、74 NOT RUN。只读结果在会话files的
`nov05-migration-verification`，包含result/index/ready及受保护数据的本地副本；
不提交用户JSON。

### E54：2026-11-05，修复已push并启动6010私有构建，记录6007升级基线

用户要求更新台账、push当前代码、启动同一private branch的新构建，
并利用上次绿色构建与新构建做后续升级测试。本轮不执行安装或升级。
E53首次迁移及源清理证据已入账，DATA-01仍保留未关闭重开Editor的剩余条件。

**旧版基线已重新确认，而非沿用E42的失败状态：**
[build162529587](https://dev.azure.com/microsoft/Dart/_build/results?buildId=162529587)
在人工重试后于11-02 19:38:37（UTC+08:00）整体`completed/succeeded`；
source commit仍为`6bd56af1472cd876abc6c2608fbf9888767d24d3`，
version为`0.101.6007.0`。Build及x64 job为attempt2成功，
ARM64 job为attempt1成功；两架构的carrier构建/签名、两种scope的主MSI、
Bootstrapper及MSI签名均有成功task。
API返回正常`build-x64-Release`和`build-arm64-Release`产物，
且`retainedByRelease=true`。旧`build-x64-Release-failure-1`只是首轮失败日志，
不能选作升级安装包。没有据此声称已下载并独立验证全部安装包字节/签名。

新候选版本为`0.101.6010.0`，高于旧6007及本地Debug6009；
使用原pipeline76541、同一
`refs/heads/feature/workspaces-protected-storage-main-20261028`、
Release/x64+ARM64、`releaseIntent=auto`、不发布public symbols。
本地执行pipeline原metadata resolver已确认结果为`private-validation/private`，
不发布preview、也不允许public symbols。

实现提交`0a59eeafe9c338886db8b33d445e7966fca16741`已push至origin和fork，
两端远端分支tip均核对一致。有效新构建为
[162959015](https://dev.azure.com/microsoft/Dart/_build/results?buildId=162959015)，
11-05 20:00:59（UTC+08:00）排队，已进入`inProgress`。
Runs回执与独立Build API查询均确认精确branch、source commit、6010版本和构建矩阵；
当时x64/ARM64/SDK job尚在等待agent，不称构建绿色或安装包已就绪。
本节的后续构建链接补记为文档更新，不改变该run固定的产品代码。

**排队操作异常如实保留：** 两次通用Runs API提交未应用请求中的分支/版本覆盖，
实际回执成为main/auto，产生`162958562`和`162958629`。
均在发现后立即取消，并确认`completed/canceled`，未当作6010或产品失败。
改用`az pipelines run --branch ... --commit-id ... --parameters versionNumber=0.101.6010.0`
后得到上述正确run；该命令返回Runs结构，最终核对读取
`resources.repositories.self`并用Build API交叉确认，而非将缺少顶层source字段误判为错误分支。

本次代码已有E49/E50定向回归及E51完整6009包构建证据；没有把原生
Photos名称差异失败或尚未进行的live Capture复测改成PASS。
当前发布不修改运行中的Local E2E服务、迁移数据或Editor。

后续升级候选为**6007 -> 6010**；必须等新构建成功并核对最终产物后，
使用相同架构/相同安装scope的真实外层安装器进行测试，而非LocalTest Repair。
应核对工作区及初始化状态保留、VA/固定服务路径不变、实际PE版本更新、
不重复迁移/首次设置以及正常owner维护授权行为；本轮不关闭UPD类验收。
构建API、artifact和installer阶段证据保存在会话files的
`nov05-private-build-6010`，包括旧版绿色build/timeline/artifacts、两个误排的取消回执、
正确`queued-build.json`、`verified-build.json`和初始timeline。

### E55：2026-11-05，用户确认本地测试1/2/3通过

20:57用户明确回复“1/2/3 均通过 请更新台账”。对应上一轮建议中的
L64/x64 Debug6009测试清单，作为**用户人工确认**记录，不冒充代理新一轮UI自动化
或进程/token/退出码采集。所附图片展示四个操作按钮，不单独作为这些回归的截图证据。

| 用户确认的测试 | 本次结果 | 正式条目更新 |
|---|---|---|
| 1：迁移后关闭重开Editor、Reload，记录不变且不重复Setup/迁移 | PASS（人工） | 与E52/E53首次迁移及源清理证据合并，DATA-01转PASS |
| 2：已保存/未保存工作区Launch & Edit、Capture、Save、重开；重复ID场景不报错、不丢窗口、ID唯一 | PASS（人工） | 原Capture故障的修复回归通过；DATA-12转PARTIAL，保留其尚未由本次确认覆盖的取消条件 |
| 3：普通/High CLI list/launch及High GUI，核对owner/session/IL，普通目标不随入口整体提权 | PASS（人工） | 同owner L64的CLI-03、CLI-07转PASS；不继承为OTS、另一用户或P环境通过 |

测试4的取消/重复操作、测试5权限回归、测试6异常迁移没有收到通过确认，
本轮不改为PASS。Photos名称差异仍在AUTO-02/13的RETEST中，生产安装/升级/
ARM64运行也不由这次人工回归覆盖。正式统计为27 PASS、22 PARTIAL、0 FAIL、
2 RETEST、1 BLOCKED、74 NOT RUN；旧E40/E41/E44/E50失败记录保留。
本轮只更新台账并讨论四按钮的用途/呈现，没有修改按钮或产品逻辑。

### E56：2026-11-06，收回额外功能并恢复原保存语义

用户明确仅要求固定旧文件data migration，未要求任意文件Import或Export，
并同意正常时隐藏清理入口、重开后只重试未完成cleanup。
本轮移除四按钮工具栏中的Import/Export/Reload，删除相应专用产品方法和资源；
没有把它们搬进另一个菜单继续作为需求。自动migration和本地测试备份工具保留。
DATA-17保留历史ID但改N/A；E37实际测试不被删除，也不再贡献当前通过数。

核对main基线`8056ba`：原IOUtils已有文件锁、原子替换/读回校验和较新
last-launched-time合并，Editor启动完成后也会自动重读。
因此“CLI只更新启动时间就要求用户Reload”不是合理的保护存储验收预期，
DATA-09改为区分历史自动合并与真正配置冲突，置RETEST。

实现位于Workspaces模块，通用service协议/JSON不改变：
- Editor保存保留已读和并发更新的较新启动时间；仅同epoch且除此字段外完全相同
  的集合可自动重基，最多3次条件写；新增/删除和用户编辑不被历史合并覆盖。
- 真正配置差异或新epoch仍报Conflict，草稿保留；不提供原revision的无效Retry。
  用户明确取消编辑后返回列表会自动读取最新状态；删除冲突后也刷新列表。
- 结果未知时保存原始编辑意图与实际合并后的请求，先查询原operation；
  重试同意图不重复提交，不同意图不能冒充已提交的原请求。
- Cleanup banner从服务端migration receipt/ack推导；普通读取也反映pending，
  重开自动只尝试旧源清理。成功后隐藏；失败时保留入口、禁止同按钮并发重试。
  不再在正常主界面放维护工具栏，也不在启动时为cleanup弹阻塞警告。

验证：托管59/59通过；实际Editor Release构建及变更XAML Styler检查通过。
初次构建暴露的字段排序、属性通知参数和StyleCop格式错误均已修正后再通过。
新增回归覆盖历史合并/原草稿不变、增删保留、真正配置冲突、新epoch、
有界重试、合并后未知结果的同/不同意图，以及重开/普通保存后仍能仅清理原迁移源。
未改Photos枚举实现，不将旧名称差异失败说成已解决。

本地构建前已关闭同owner/本Debug目录程序，备份两条当前工作区全部字段、
index/blob原字节和哈希；没有Reset。完整Debug6011构建、包内PE版本检查及
pinned CMS正/负检查已通过。

随后一次保留数据Repair提权返回“operation canceled by the user”，没有取得
提升控制器进程或repair-result；未重复请求或借用既有管理员窗口绕过确认。
收尾ready仍为6009、host36448/worker23468、recovery=0，两份保护文件名字/哈希
与构建前完全一致，本地Debug进程0。**源码与完整Debug构建完成，不代表新UI已可运行
或6011实机验收通过。** B08记录这一实际部署阻塞。

获准后可在同账号管理员PowerShell执行保留数据更新（不要Reset）：

```powershell
& 'D:\PowerToys-Workspaces-Integration\tools\build\Build-PowerToysLocalE2E.ps1' -Mode Repair -SkipBuild -Version 0.101.6011.0
```

证据位于会话files的`nov06-ui-scope-results`，包含59项TRX、Release/Debug构建、
XAML检查结果、基线副本和`final-environment.json`；用户数据副本不提交。
本轮未commit/push，也未修改6010 CI源码或安装正式MSI。

### E57：2026-11-06，发布范围纠正，说明真实配置冲突的测试方法

用户询问如何验证真正配置冲突、能否启动多个Editor，同时要求commit/push并
在ADO启动新的private build。本轮不执行冲突写入、不修改单实例规则、不Reset。

Editor使用`Local\PowerToys_Workspaces_Editor_InstanceMutex`，LocalTest另加后缀；
同一登录会话的第二个同类实例会退出。正式版与LocalTest、或不同业务owner的
实例也不共享同一目标数据，不能用它们冒充同一记录的并发配置冲突。
完整实机case需另行准备匹配LocalTest catalog的受控writer：
先让Editor保留revision R的草稿，再由writer经真实SDK/CAS对同一owner、
同一workspace提交实质配置变化为R+1；随后Editor保存必须拒绝覆盖，
错误关闭后仍保留草稿，明确取消编辑再返回列表时自动读到R+1。
不使用未授权脚本直写保护文件，不把CLI只更新启动时间当作这个case。
现有59项单测已有真实配置变化拒绝/草稿输入保留逻辑覆盖，但这个writer/UI
组合尚未准备或执行，DATA-09仍RETEST。

本地状态已变化：13:46及本轮均确认client/runtime6011匹配；
本轮ready为host20940/worker6992、recovery=0，实际CLI list读取两条记录成功。
部署由用户独立完成，不伪造代理Repair执行、UAC或数据前后不变证据。
B08解除；E56当时取消的事实和最后数据哈希仍保留。本轮不打断正在测试的UI。

CI查询确认6010/build162959015于11-05 23:19:26（UTC+08:00）
整体`completed/succeeded`，source为`0a59eeafe9`，x64/ARM64/SDK/发布均成功，
`build-x64-Release`与`build-arm64-Release`正常产物存在。
新候选使用6012、同一private branch、固定本次实现commit、Release/x64+ARM64，
不发布public symbols；使用已验证的`az pipelines run --branch --commit-id`方式，
不重复E54通用Runs API误排路径。
实现提交`264346e7771e5fe75e824b78e61289b4e9dca9f4`已push至origin和fork，
并分别读取远端tip确认一致。有效新run为
[163076849](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163076849)，
11-06 15:06:16（UTC+08:00）排队，当前`inProgress`。
Runs回执及独立Build API均确认完整commit、branch、6012版本和Release/x64+ARM64；
初始timeline中Build/SDK/SDL阶段已开始，架构job等待agent，没有宣称构建绿色。
此后仅补记构建链接的文档提交，不改变该run固定的产品代码。
升级可选6010 -> 6012，旧6007仍保留；本轮不执行安装或升级测试。
证据保存于会话files的`nov06-private-build-6012`。

### E58：2026-11-08，6010首次安装前的正式owner基线

用户要求准备“阶段0”，本轮目标是当前owner的正式
`PowerToysProtectedStorage`命名空间，不是LocalTest reset。
签名包CI已重新查询：6010/162959015及6012/163076849均成功，
各有正常x64/ARM64产物；这不等于已安装/升级验收通过。

盘点发现实际主程序为0.101.3000，虽然其ARP项出现在HKLM，
**原生MSI API确认实际为当前owner的USERUNMANAGED/context2**，
没有把注册表位置误判为全机安装。正式carrier/service已经不存在，
签名Setup的`inspect --json`返回`DataRetained`；旧Data、Policy、Installer
保留状态及legacy JSON仍在。另有2222/2293两个缓存丢失的旧Burn条目。
两个独立demo产品及LocalTest服务不在本轮清理范围。

先备份普通Workspaces/config文件并核对hash。第一次UAC取消后没有执行
保护数据删除或卸载；用户明确回复“Do it again”后，第二次授权才启动工作。
受保护stage中的临时SYSTEM任务重新确认无正式carrier/service、无owner runtime，
拒绝reparse路径，先备份全部目标并核对源/副本hash，再删除准确owner的
Code/Data/Policy/Installer。原DACL元数据同时保留，没有修改MSI内部注册。
临时任务已注销、worker脚本已移除；正常owner再次核对18个保护备份文件hash一致。
备份位置：

```text
C:\Program Files\PowerToysBaseline-d22d3116e431495ca38f124c6e594025\Backup
```

随后通过原Microsoft签名外层EXE卸载3000，exit0；
从现存Downloads原签名EXE移除2222的旧bundle，exit0。
2293缺少原卸载缓存，且实际MSI盘点无该产品；在Windows“Programs and Features”
中按**名称和2293版本**确认目标，接受系统提供的“已卸载，是否移除无效列表项”
操作。这里仅注销孤立ARP项，不冒充执行了2293 MSI卸载。
未使用Win32_Product，也没有手删Windows Installer内部注册。

Legacy `workspaces.json`已移至会话备份区，并验证hash不变；
新6010首次打开将没有旧迁移输入。普通配置只将
`download_updates_automatically`改为false，其余JSON值与备份相同，
`run_elevated=false`保留，其他模块偏好没有清空。

23:54收尾核对：
- 本owner正式主程序目录、ARP bundle、carrier/service及owner四类保护根均不存在；
  MSI API只剩两个原demo产品，context及ProductCode与前置盘点一致。
- 正式legacy源不存在；没有PowerToys应用进程、当前用户PowerToys AppX包，
  没有指向旧安装目录的pending file rename操作。
- LocalTest service保持Running；未删除它的Data、未删除demo或其他owner数据。
- 正式VA profile的DeleteProfile返回32且仍loaded，没有强卸hive；
  该OS残留不含已清除的repository初始化状态，因此可测试owner首次预配，
  但不能将本机称为全新OS/新账号/零profile残留环境。

**当前owner的业务首装基线已准备；没有安装6010、启动新的Setup或执行升级。**
若还要验证全新VA profile创建，应在释放占用并确认profile清除后或干净VM中另测，
不能仅因业务状态已清空便声称该分支已覆盖。INST/UPD/REM等产品验收状态不因
本次环境准备改PASS。
会话files的`nov08-production-baseline`保留普通备份、quarantined源文件、
两次授权记录、保护清理回执、MSI前后枚举、官方卸载日志、Windows孤立项处理记录
及`ready-baseline.json`。早期失败的只读probe（原生null参数/advertised产品属性）
已纠正后重新枚举，不将probe错误解释为产品安装状态。

### E59：2026-11-09，正式6010首次Setup后为空集合，状态正常

用户报告已安装6010、打开Workspaces完成初步Setup，目前显示空白工作区页面。
代理未创建或修改记录，只读核对至00:17（UTC+08:00）：

| 观察 | 实际结果 |
|---|---|
| 安装入口 | 用户安装目录的PowerToys/Editor/CLI/ProtectedStorageSetup均为`0.101.6010.0`；Setup Authenticode为Valid、Microsoft Corporation |
| 正式检查 | `inspect --json`为Ready/native0、无maintenance cleanup pending；installedVersion为MSI三段`0.101.6010` |
| Carrier | `{6DF456B9-641B-5F61-A352-0D608BC410EF}`；原生MsiEnumProductsEx查询确认USERUNMANAGED/context2，业务owner等于当前普通用户A |
| 实际服务进程 | Bootstrap25236、Runtime37048；以只读process/token句柄独立核对primary SID均等于该服务的VA SID，Session0，不是LocalSystem |
| 固定映像 | `%ProgramFiles%\PowerToysProtectedStorage\Owners\<A>\Code\Bootstrap.exe`及同目录Runtime.exe |
| 健康与policy | ready version6010、data_recovery_required=0；policy app为PowerToysProtectedStorage、signer_policy=microsoft-production-v1、minimum_version6010 |
| 业务存储 | CLI成功返回`workspaces: []`；index sequence=1，epoch=`07b24595-56ba-46ce-a25e-dfc699c8b6bb`，initializationOperationId=`cfa046e2-a345-406f-8dd2-dd1de47354fe` |
| UI | 安装版Editor30420标题Workspaces Editor，无LocalTest标记；显示“There are no saved Workspaces.”，Create Workspace可用，不是白屏或错误对话框 |

E58有意移走旧legacy源，所以这是成功初始化的空库，不是旧数据丢失或迁移失败。
index没有migrationSource；此时cleanupAcknowledged=false并不代表待清理或失败。
6010仍有四个常驻按钮是该版本的旧UI，不能据Retry cleanup可见判定pending；
移除这些按钮的是6012。只读复核前后index未变。

INST-01在P-XU/6010、本账号范围记PASS；用户执行安装/Setup，代理核对后置状态，
不伪造UAC次数或全新VA profile创建证据。E58旧profile残留说明仍有效。
没有据此关闭后台不隐式预配、OTS、多owner、重启、数据迁移或6010 -> 6012升级条件。
下一步应先在6010创建/保存两条测试工作区并建立升级前完整数据基线。
本地证据位于会话files的`nov09-production-6010-setup`，
包含inspect、MSI/实际token身份、ready/index、UI树、OS和最终状态；账号SID只保留本地。

### E60：2026-11-09，正式服务实际升级到6012

用户报告已在6010创建并保存两条测试工作区、启动并重开，随后升级6012仍能打开，
要求确认服务是否真的升级。代理本轮只读观察，没有补跑sync/ensure/repair，
没有重启服务、启动目标应用或更改工作区来使检查通过。

| 证据 | 6010基线E59 | 当前6012 |
|---|---|---|
| 安装文件 | 主程序/Editor/CLI/Setup均6010 | 四者均6012，Setup Authenticode Valid/Microsoft |
| Carrier ProductCode | `{6DF456B9-641B-5F61-A352-0D608BC410EF}` | `{501E0380-57DB-5AEB-AC36-D2A8FB2E14E1}`；MSI API确认新项context2/同owner，旧项已不再注册 |
| Bootstrap实际PID | 25236 | 38060；01:14:29.338创建 |
| Runtime实际PID | 37048 | 20804；01:14:29.633创建 |
| 进程身份与路径 | 原VA，Session0，固定Code目录 | 两个实际primary token仍为相同VA/Session0；路径与E59逐一对应；creation FILETIME均匹配ready birth，非只看磁盘文件 |
| 健康 | Ready/6010/recovery0 | signed Setup inspect为Ready/native0/installedVersion6012，无maintenance cleanup pending；ready为6012/recovery0 |
| 初始化身份 | epoch `07b24595-56ba-46ce-a25e-dfc699c8b6bb`，init operation `cfa046e2-a345-406f-8dd2-dd1de47354fe` | 两者均不变；sequence=6；当前两条工作区分别3/2个应用，CLI及逐ID details读取成功 |

`inspect`的Ready并非只判断主程序版本：它核对注册版本对应的实际控制端健康、
worker ready、没有未决事务/maintenance/data recovery。01:14:33更新诊断
`update-state.json`为completed/native0，作为时间线旁证，不作为单独授权证据。
policy的`minimum_version`仍为6010是最低允许版本，不是当前运行版本；
bundle验证采用不低于当前版本和policy floor的规则，不能据此误判升级未完成。

**服务/载体升级子结果成功，已经不是6010服务继续支撑6012界面。**
VA、固定映像路径和初始化身份均保留。用户升级前创建的两条完整数据基线尚未
提供给代理；E59代理保存的是创建前空库，所以本轮不虚构业务字段前后完全一致。
UPD-01/09记PARTIAL，等待其剩余数据/授权流程证据；也不把per-user路径外推为
machine-uninstall hook、其他owner、重启或ARM64通过。
本地证据为会话files的`nov09-production-6012-upgrade`，
包含inspect、版本、ready/index、新旧MSI注册和实际进程身份对照、
当前完整workspace details、policy及安装/更新诊断；账号SID只保留本地。

### E61：2026-11-09，6012普通卸载漏掉保护实例清理

用户确认上一轮建议中的测试1（升级后写入/重开/启动及历史合并）
和测试3（正式包普通/High入口及直接Data拒写）均通过，作为P-XU/6012
人工结果记录；代理未重跑这些操作。随后用户已卸载，要求检查artifacts与Data。

**实际结果不是正常KeepData卸载完成：**

| 项目 | 只读观察 |
|---|---|
| 主程序 | 用户安装目录不存在，主MSI及用户Burn条目消失，当前用户PowerToys/CommandPalette相关AppX为0 |
| 主卸载日志 | 外层6012 EXE的`/uninstall`；ProtectedStoragePerUserBundle=1、WixBundleAction=4、WixBundleElevated=0；主MSI及外层均exit0 |
| Carrier | `{501E0380-57DB-5AEB-AC36-D2A8FB2E14E1}`仍为当前owner/context2、版本6012 |
| 服务/进程 | 正式service仍Running；Bootstrap38060和Runtime20804均实际存活，创建时间仍为01:14:29，不是只根据旧ready文件判断 |
| Code/Installer | Code目录仍存在，普通owner不可枚举；active/service-created仍在，没有retained/purged/pending-remove/drain或新的commit-remove日志 |
| Data | 当前index指向的live blob可读；PTPS framing、contentHash、target/schema、revision、operation/digest与index全部一致 |
| 内容 | Workspace 1（2应用）、Workspace 23（3应用），ID与E60两条相同；sequence=11，epoch和初始化operation与E60相同 |
| 权限/明文 | index的protected DACL为SYSTEM/对应VA FA、owner/Admins FR；固定legacy workspaces.json不存在。代理只读取，没有写权限探针或数据修改 |

业务Data目前完整，但仍属于**未被卸载的活动store**，不能用“数据还在”
替代KeepData移除/重装验收。用户期间已做写入测试，sequence及名称变化不
解释为卸载丢失；没有卸载前最后一刻的完整快照，不伪造逐字节前后比较。

**确认的代码缺陷：** `SilentFilesInUseBAFunctions.cpp`声明
`OnExecuteComplete(HRESULT, BOOL*)`，而项目实际使用的WiX5.0.2
`IBootstrapperApplication.h`及`BalBaseBAFunctions.h`接口为
`OnExecuteComplete(HRESULT)`。额外参数使其成为另一个函数，
未覆盖真正回调；又没有`override`令编译拒绝该错误。
因此引擎沿基类单参数空实现返回S_OK，owner清理代码不会经此回调执行。
实机日志有BA加载、Detect/Plan/ExecuteBegin，却没有本类ExecuteComplete
和owner removal日志，符合该缺陷；不能归咎于用户使用了管理员入口或取消
storage清理授权。最新maintenance receipt仍是11:21的sync，
update-state诊断仍是01:14升级结果，没有新的remove记录。

REM-11记FAIL；REM-02因正确移除尚未发生而BLOCKED。这不是仅剩Data/Policy
或OS profile的正常保留场景。没有用remove/repair/重装或阶段0脚本替用户
清理来掩盖原结果；本轮没有修改产品代码。
后续需修正回调签名、以override约束并做实际外层卸载回归，不能仅凭再次编译
成功关闭此项。直接调用signed Setup remove不能替代外层回调验证。

证据在会话files的`nov09-production-6012-uninstall`：
原始外层/MSI卸载日志、MSI枚举、实际存活进程、artifact/marker、
当前index/live blob及离线校验结果。诊断副本不作为live源，不提交用户数据。
普通owner读取Code被拒绝没有被转换为“目录不存在”；初始WMI路径过滤未返回
进程的结果也没有被当作“进程已退出”，随后按已知PID和出生时间独立确认。

### E62：2026-11-09，修复卸载回调分派并排队private6013

用户决定维持现有架构，先修复E61卸载缺陷，再排队private build。本轮不实施
HTML讨论中的Setup/Broker合并，也不清理原机器上的失败现场。

**源码修复：** 提交`b64732533a88d74c98428d1c1a0ac550ad024e9c`：

- `SilentFilesInUseBAFunctions.cpp`改为WiX5要求的`OnExecuteComplete(HRESULT)`，
  删除不存在的取消输出参数及日志解引用；本类六个SDK回调均加`override`，
  后续接口不匹配将由编译器拒绝，不再静默成为重载。
- 保持原ordinary-owner、explicit per-user uninstall、主MSI成功、一次性执行等
  门控以及默认KeepData语义；没有改变授权或卸载的整体分工。
- 在既有`ProtectedStorage.InstallerTests`中加载真正构建的BA DLL，
  经`BAFunctionsCreate`返回的WiX `pfnBAFunctionsProc`派发
  `BA_FUNCTIONS_MESSAGE_ONEXECUTECOMPLETE`，覆盖`S_OK`和`E_FAIL`。
  engine日志接收端为测试替身，但产品回调和WiX分派器没有被替换。

**实际执行：** Windows `10.0.26300.0`、x64 Release、当前普通owner
（effectiveAdministrator=false），17:17 +08:00记录环境及DLL哈希：

| 阶段 | 结果 |
|---|---|
| 修复前 | 构建成功，但回归在第5个check失败：`WiX ExecuteComplete was not dispatched to the product callback`，exit1 |
| 修复后 | 同一测试路径通过，完整安装器套件`PASS 4136 installer contract checks`，exit0 |
| 构建/产物 | 使用仓库build.ps1，Release x64零错误/警告；实际BA产物与测试目录副本SHA256一致 |
| 安全范围 | 不派发PlanBegin，不arm owner removal；不调用Setup remove，不修改服务、profile、账号或MSI注册。测试用临时文件自行清理 |

这证明错误签名导致的**真实SDK分派缺陷已修正**，不是完整Burn外层卸载、
owner-cleanup UAC、carrier移除、VA进程退出或KeepData重装已通过。REM-11
保留E61的FAIL，REM-02仍BLOCKED；不把源码修复或单测改写为实机PASS。

**Private build：** 修复已推送origin及fork。
2026-11-09 17:20:08 +08:00以`az pipelines run`排队
**0.101.6013.0 / [163342432](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163342432)**，
definition76541，Release x64/ARM64；`releaseIntent=auto`、
`publishSymbolsToPublic=false`、`useVSPreview=false`。
随后通过独立build查询确认分支
`refs/heads/feature/workspaces-protected-storage-main-20261028`、
完整源码SHA与版本参数均正确；状态inProgress，尚无绿色或签名产物结论。
后续台账提交不改变该build固定的源码SHA。

证据在会话files的`nov09-uninstall-fix`：`build-before.log`、
`before.txt`、`build-after.log`、`after.txt`、`validation.json`、
`queued-6013.json`、`verified-6013.json`。
初次构建遇到WiX头文件缺失，按已修改的测试依赖执行显式`/t:Restore`后解决；
一次测试DLL定位错误也已改为构建后复制实际目标路径。上述前置错误不混作
回调缺陷的红灯证据，最终红/绿结果只引用对应的before/after记录。

### E63：2026-11-10，6013 ARM64 Carrier ICE失败排查

用户报告6013失败并要求分析。本轮只查询ADO、下载已有日志并核对源码，
没有重跑job、排队新版本、修改产品/流水线或操作本机正式实例。

[build163342432](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163342432)
在11-09 19:46:11（UTC+08:00）整体`completed/failed`；
源码`b64732533a88d74c98428d1c1a0ac550ad024e9c`、6013版本及分支均正确。
唯一失败task是ARM64 job的`Build Protected Storage Carrier`，log301，
19:42:32–19:42:38执行；不是本轮卸载回调的C++编译错误。

| 阶段 | x64 | ARM64 |
|---|---|---|
| PowerToys主项目 | 成功，log108 | 成功，log113 |
| 安装器共享DLL | 成功；后续打包完成 | log289包含实际SilentFilesInUseBAFunctions.cpp编译，0 errors；5个非致命warning不是本次失败 |
| Carrier输入验证 | log192通过生产trust verifier | log300通过生产trust verifier |
| Carrier MSI | log193真实执行ICE后0 warning/0 error | WiX build产生中间MSI，随后WindowsInstallerValidation失败 |
| 后续发布 | 主MSI/Bootstrapper完成，`build-x64-Release`正常产物存在 | Carrier签名及后续Broker/Setup、主MSI/Bootstrapper跳过，只有`build-arm64-Release-failure-1` |

**直接失败原因已确认：** `wix.exe msi validate`运行ICE时，Windows Installer
返回1719：`The Windows Installer Service could not be accessed`。
WiX以WIX0217的external UI message logger错误包装它；97个ICE action重复
出现该失败，外加MSBuild exit1形成task的98条error记录，不能解释成97处独立的MSI表缺陷。

下载的failure artifact不仅有binlog，还包含原始服务和事件快照：

- ARM64校验前是`Stopped / Manual`，成功的x64 job也同样如此；按需启动状态本身不是根因。
- ARM64失败后`msiserver`已为`Running / LocalSystem / exit0`。
  因此不能简单归因为服务没启动，也不能保证加一条Start-Service就能修复。
- 捕获的最后30条MsiInstaller事件中，29条为11719，明确写出Error1719；
  另一条1033含status0，只是会话摘要，不能覆盖验证错误或当成产品安装验收成功。
- 两架构分别使用不同VM，均为非交互Windows Server2025、SHINE-VS18-Latest77.0.0、
  agent5.281.1；成功一侧不是通过关闭ICE或使用另一套WiX版本绕过问题。

与绿色6012的源码`264346e777`比较，
`Product.wxs`、`PowerToysProtectedStorage.wixproj`和pipeline均未改变。
本轮修复集中在BA回调接口与回归测试，且ARM64 BA已实际编译成功。
E42/E43曾记录6007在x64侧出现同类1719；E54确认那一轮在同提交人工重试后转绿。
这些证据支持**agent上的Windows Installer/ICE执行环境问题**，不支持回退回调修复。

**尚未证明的部分：** 当前材料没有Windows Installer内部verbose trace、
SCM/System相关事件或校验进程的实际token/elevation记录；
不能进一步确定是服务访问权限、执行上下文、启动交互还是VM状态导致1719。
也没有通过本轮重试证明它一定是一次性故障，不能承诺“重跑必过”。

建议保持同一6013版本与源码，先只重跑失败的ARM64 job，保留完整ICE校验。
若再现，再补上述诊断交给CI环境维护者；不通过SuppressValidation/SuppressIces
或改产品代码来隐藏这类执行环境错误。本轮没有执行该重跑。
正式验收计数不变，尤其不把x64包存在或回调编译成功改记为REM-11 PASS。

证据位于会话files的`nov10-private6013-failure`：
`build.json`、`timeline-raw.json`、`artifacts.json`、两架构agent/preflight/carrier日志、
`arm64-shared-dlls-289.log`及下载的`arm64-diagnostics\windows-installer-*.json`。
该failure artifact仅作诊断，不作为ARM64安装包或发布成功证据；
x64安装器文件本轮未下载运行，未新增独立Authenticode或安装验证结论。

### E64：2026-11-10，只读确认正式Bootstrap/Runtime已升级6013

用户要求检查当前服务版本。16:14–16:20（UTC+08:00）只读核对正式
`PowerToysProtectedStorage_<owner>`，不检查或混用LocalTest namespace，
没有执行sync、repair、remove或数据写入来促成结果。

| 项目 | 实际观察 |
|---|---|
| 签名Setup | per-user安装位置下的PowerToys.ProtectedStorageSetup.exe，文件版本0.101.6013.0，Authenticode Valid / Microsoft |
| 实时inspect | `inspect --json`返回Ready/native0/exit0；productCode=`{6C2D8CEA-1996-58E7-B9FD-4F235686E9B3}`、installedVersion=0.101.6013 |
| Bootstrap | PID43528，16:03:34创建，实际映像为固定Code\Bootstrap.exe，原owner对应VA，Session0，仍存活 |
| Runtime | PID37908，父PID43528，16:03:35创建，实际映像为固定Code\Runtime.exe，同一VA、Session0，仍存活 |
| 服务配置 | 注册表ImagePath为固定Bootstrap路径及owner参数，ObjectName为该owner的NT SERVICE虚拟账户，Start=2；SCM状态Running且PID匹配 |
| 运行版本 | ready报告0.101.6013.0、data_recovery_required=0；原生GetProcessTimes取得的两个精确出生时间均与ready一致，不是复用陈旧PID或只看ready文件 |
| 更新事务 | `04E41B0F-6808-4D61-B460-B6BBCAF0E226`明确old_version=0.101.6012.0、new_version=0.101.6013.0，包含两PE的新旧hash；journal已到committed，记录两实际VA进程/readiness再次验证 |

运行release版本由实时签名inspect、匹配进程代际的ready和已提交双PE更新
事务交叉确认；没有把主程序目录里的6013文件版本当作服务更新的唯一证据。
最初WMI的StartName/PathName/ExecutablePath为空属于查询限制，没有被解释为
缺失或错误配置；随后以最小只读进程权限、主token查询及服务注册表读取补齐。

结论限于**当前x64保护实例已完成6012→6013代码切换并健康运行**。
本轮未重新读取工作区JSON、未做逐字段数据保留比对，未还原用户操作的完整
主程序升级前状态；不能据此区分完整主程序升级与重装主程序后的保护实例升级。
6013外层卸载尚待结果，REM-11历史FAIL与REM-02 BLOCKED保留；
本机版本确认也不改变E63的ARM64 CI失败记录。

证据在会话files的`nov10-production-6013-check`：
`service-processes.json`、`setup-inspect.json`、
`actual-process-identities.json`、`update-transaction.json`。
这些只读诊断不作为live数据源，不提交其中的本机身份或业务数据。

### E65：2026-11-10，6013卸载后保护实例仍存活，定位第二处门控时序缺陷

用户已卸载6013，要求静态文件和服务状态检查。本轮从16:44到17:01
（UTC+08:00）只读取文件、注册表、MSI枚举、进程及日志；未使用
Win32_Product，未执行remove/repair、服务停止、目录清理或再安装。

| 项目 | 实际结果 |
|---|---|
| 外层入口 | 缓存的PowerToysUserSetup-0.101.6013.0-x64.exe，命令行`/uninstall`，perUser=1、WixBundleElevated=0 |
| 主程序 | 用户安装目录不存在，native2/3；主MSI `{76F7CEB9-0881-4439-86C6-3BCC23EF3BF8}`及Burn `{D9B6C9CD-1E9C-45D2-A0B6-848FABA1D55D}`已移除，日志显示主MSI与外层exit0 |
| Carrier | `{6C2D8CEA-1996-58E7-B9FD-4F235686E9B3}`仍注册于原owner/context2，版本0.101.6013、State5；ARP也仍有该条目 |
| 正式服务 | Running，注册表Start=2、ObjectName仍为该owner的VA；Bootstrap43528、Runtime37908为E64同一代进程，出生FILETIME完全一致且仍存活 |
| Code | 目录确认存在；两个实际映像仍指向该Code路径。普通owner读具体PE文件返回native5，记录为不可直接验证，不将其误报为文件不存在 |
| Installer / Policy | 目录保留，active/service-created仍在；retained/purged/pending-remove/drain均无。lifecycle.log仍只有11-09初次seed/commit-install，没有本次remove |
| Data | 当前blob与index、receipt及contentHash一致，可解析出2条工作区；sequence13，epoch/init ID与E61相同，owner/Admins为FR、SYSTEM/VA为FA |
| VA profile | 仍存在且Loaded；正式服务仍运行，不能将这次失败解释成仅剩待重启删除的profile |
| 范围隔离 | LocalTest服务和既有demo条目仍在且未动；本次失败判断仅针对正式owner实例 |

Data在当前时点内部一致可读，但仍是**未移除的活动store**，不是已完成
KeepData卸载后保留的数据。没有卸载前最后一刻完整快照，不伪造逐字节对照。
sequence13的写入时间在卸载前，不能把相对E61的变化归因于卸载。

**6013签名修复生效，但整体卸载修复不完整：**
外层日志在16:38:13明确出现
`Running execute complete BA function. hrStatus=0x0`，
证明E62修正的WiX回调已被实际安装器调用。随后没有owner清理、token拒绝、
payload失败或cleanup deferred日志；bundle-removal日志目录也没有产生。
最新maintenance仍是E64的16:17 inspect，MsiAction日志最后更新16:03，
没有新的remove receipt。不能归咎于用户改用管理员入口或取消清理授权。

**另一处实现问题在动作判定时机：**
本地`SilentFilesInUseBAFunctions.cpp`的`OnPlanBegin`立即读取
`WixBundleAction`并保存`m_removeOwner`。核对实际使用的WiX5.0.2源码：

- [variable.cpp](https://github.com/wixtoolset/wix/blob/v5.0.2/src/burn/engine/variable.cpp#L295)
  将该内置动作变量初始化为0。
- [core.cpp](https://github.com/wixtoolset/wix/blob/v5.0.2/src/burn/engine/core.cpp#L439)
  先调用`BACallbackOnPlanBegin`，返回后才调用`PlanSetVariables`。
- [plan.cpp](https://github.com/wixtoolset/wix/blob/v5.0.2/src/burn/engine/plan.cpp#L340)
  才把本次action写入`BURN_BUNDLE_ACTION`。

因此首次PlanBegin时还没有本次Uninstall动作4，清理开关提前计算为false；
之后没有重新计算。日志末尾的`WixBundleAction=4`是后续状态，不能倒推
回调早期读取值也是4。变量读取时机来自同版本源码对照，不冒充已采集
当时内存中的门控值；现场“完成回调到了、owner removal没到”与其一致。

E62的回归明确没有派发PlanBegin，以避免触发真实卸载；它成功覆盖了接口
分派，却没有覆盖真实Plan变量发布时序。这是上一轮验证的缺口。
后续需在动作已发布的时点判定清理意图，并覆盖完整Plan→Execute状态顺序，
再做正式外层卸载；本轮仅检查和定位，没有修改产品实现或发起新构建。
REM-11继续FAIL、REM-02继续BLOCKED，保留E61/E62/E64的历史与范围区分。

证据在会话files的`nov10-production-6013-uninstall`：
外层/主MSI卸载日志、`msi-inventory.json`、`arp.json`、`artifacts.json`、
`service-processes.json`、`markers.json`、`latest-maintenance.json`、
`storage.index`、`live-blob.bin`、`data-integrity.json`、`lifecycle.log`，
以及只读获取的WiX5.0.2上游源码副本。本机身份及业务数据副本不提交仓库。

### E66：2026-11-10，修复计划动作时序，最小隔离验证后排队6014

用户要求先修复E65问题、做本地最小验证，再queue ADO private build。
产品提交`5fc28726f48b909094d3a6ec7aec6bba31b88a9a`已推送origin/fork；
不实施此前讨论中的Setup/Broker合并，也不修改其授权、数据或服务模型。

**实际改动：**

- `OnPlanBegin`只重置旧的清理意图；成功的`OnPlanComplete`才读取WiX已经
  发布的动作和per-user标志，失败/取消或变量读取失败保持不启用。
- `OnExecuteComplete`仍要求主MSI成功、非关联bundle、正常per-user卸载、
  每个实例仅执行一次。新增计划变量读取结果、related/armed/attempted日志，
  不再只能从“回调进入”猜测是否实际启用清理。
- 将现有回调类共享到头文件，把原系统清理代码保留为生产factory固定绑定
  的执行函数；测试仅替换这一副作用边界。没有运行时测试开关、新DLL导出、
  签名豁免或普通owner权限扩大。原token检查、helper路径、KeepData调用和
  错误处理不变。

**本地最小验证（x64 Release，非安装运行）：**

| 项目 | 实际结果 |
|---|---|
| 修复前负向控制 | 保留旧的提前读取逻辑，生产回调类经WiX SDK分派，在第3个check失败：`PlanBegin read a stale action before WiX published the new plan` |
| 修复后正常卸载 | 变量初始0，PlanBegin后才发布Uninstall，再经PlanComplete/ExecuteComplete；清理计数为1，重复完成回调不再次清理 |
| 不应清理的情形 | machine scope、关联upgrade bundle、Install/Modify/Repair、计划失败/取消、主执行失败、变量读取失败均为0 |
| 重新规划 | Uninstall→Install及Uninstall→失败计划不会沿用旧的清理意图 |
| 总结果 | 11个状态序列场景及完整套件`PASS 4230 installer contract checks`，exit0；Release x64构建零错误/警告，E62实际BA DLL分派回归也继续运行 |
| 真实现场保护 | 正式服务仍为PID43528/Running，6013 carrier marker未变，storage.index和lifecycle.log与E65副本hash一致，未产生生产bundle-removal日志 |

这里使用**同一份生产回调类及真实WiX消息分派器**，engine变量发布时间由
fixture驱动，系统清理函数替换为计数器；不是仅检查布尔helper，也不是
真实Burn外层安装/卸载、UAC或carrier/service/Code删除成功。
新签名包仍必须做完整外层卸载验收，REM-11/REM-02不关闭。

额外只读提取用户的6013 UserSetup BA容器，确认实际
`SilentFilesInUseBAFunction.dll`与`PowerToys.ProtectedStorageSetup.exe`
位于同一目录且签名均Valid，Setup文件版本为0.101.6013.0。
未运行这些提取文件；该检查只排查既有cleanup payload缺失/位置问题，
不作为6014产物或卸载成功证据。临时提取目录已删除，原Downloads EXE未动。

早期尝试的进程内HKCU/KnownFolder隔离未能重定向LocalAppData，未用于测试；
探针临时目录与键已清理，最终采用上述副作用边界替换，未重定向用户真实设置。
测试直接链接SDK分派器所需的Windows `version.lib`依赖已补齐；
红灯记录是在成功构建后取得，不将初次链接错误混作时序缺陷的复现。

**构建状态：** 本轮重新查询发现6013/163342432已于17:04:53（UTC+08:00）
整体succeeded，ARM64为attempt2、x64为attempt1，源码仍b64732533a；
本代理没有发起该重试。E63的首次1719失败保留，绿色构建并未消除E65逻辑缺陷。

2026-11-10 18:13:34（UTC+08:00）排队
**0.101.6014.0 / [163468842](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163468842)**，
definition76541，Release x64/ARM64；沿用private-validation参数。
独立Build API查询确认分支、版本和完整源码SHA均匹配5fc28726f4；
状态inProgress，尚无该候选的绿色、签名产物或实际卸载结论。
台账后续提交不改变该run固定的产品源码。

证据在会话files的`nov10-plan-gate-fix`：before/after日志、构建日志、
`production-preserved.json`、`bundle-payload-check.json`、
`6013-now-green.json`、`6013-green-timeline.json`、
`queued-6014.json`、`verified-6014.json`。

**后续状态确认（2026-11-11）：** Build API返回6014/163468842已于
11-10 20:25:47（UTC+08:00）整体succeeded，source仍为完整5fc28726f4提交；
artifact API列出正常`build-x64-Release`和`build-arm64-Release`。
这足以选择对应架构候选做人工测试，不表示回调回归已在private pipeline执行，
也不关闭REM-11。针对当前卸载修复可直接安装6014再验证功能及正常外层卸载；
仅当另测完整主程序6013→6014升级时，才需要先恢复6013主程序基线。
原服务残留场景不能标作干净首装或完整主程序升级。本轮未执行安装操作。

### E67：2026-11-11，核实之前CI与本地验证为何漏掉外层卸载缺陷

用户询问private build及本地安装/运行/卸载为何没有提前发现问题。
本轮按实际源码核对覆盖范围，不把“构建绿色”“底层helper清理成功”
解释成正式外层卸载已验收：

| 层次 | 实际覆盖 | 漏掉的路径 |
|---|---|---|
| Private release | `.pipelines\v2\release.yml:110–111`明确为runTests=false、buildTests=false；主任务为Build而非Build;Test，并执行签名/打包/ICE等检查 | 不执行产品安装后的生命周期验收；E66新增回调序列也只有本地执行证据，没有自动成为private pipeline门禁 |
| 编译后的卸载hook检查 | `steps-build-installer-vnext.yml`调用Test-ProtectedStorageMsiTables.ps1读取MSI表/嵌入payload。per-user分支检查不存在SYSTEM all-owner清理CA；machine分支检查其类型/条件/顺序及字节 | 不执行per-user Burn的Plan/Execute回调，不验证owner carrier/service/Code实际移除 |
| Local E2E清理 | Build-PowerToysLocalE2E.ps1的Reset/Clean直接调用LocalLifecycle clean/clean-backup；E52有此类真实服务清理记录，local-e2e.json明确productionInstaller=false | 绕过正式UserSetup EXE的BA回调和原用户carrier卸载衔接；不能把这条路径的成功继承给正式外层卸载 |
| E62第一次回归 | 真实BA DLL和SDK分派器验证ExecuteComplete入口，明确不派发PlanBegin以避免清理真实实例 | 没有覆盖WixBundleAction由0到本次动作的发布时间，也没有验证何时启用owner清理 |
| UI/主MSI结果 | E61/E65的主MSI及外层均exit0，主程序消失 | owner清理未被启用时没有失败返回，单看UI/exit0不能发现仍运行的服务和注册中的carrier |

因此两个缺陷都能逃过旧检查：错误重载在没有override时是合法C++；
提前读取动作变量则是运行时序逻辑错误。安装、正常运行以及升级的非卸载
路径也不要求触发owner移除，不能拿这些正常路径代替卸载正向场景。
最近真正走正式外层卸载并检查系统后置状态的E61/E65都记录为FAIL，
没有完整通过的该路径证据。

**本轮发现并修正的检查维护遗漏：** E66把回调类放到头文件后，
Test-ProtectedStorageMainInstaller.ps1仍只扫描旧cpp，原样运行在第63行失败。
现同步读取头文件中的门控和cpp中的实际移除调用，再运行非Compile模式
通过。只修正这处直接引入的静态检查路径；没有改产品逻辑、CI开关或
重新排队构建。该静态PASS仍不是已安装产品的卸载PASS。

验证缺口仍需明确保留：定向回调序列应成为自动执行的门禁，完整生命周期
需要隔离环境中的真实UserSetup安装/卸载，并核对MSI注册、SCM、进程、
Code及KeepData后置状态。仅添加测试源文件或看到private build绿色，
不意味着这些检查已经在CI执行。本轮未实施上述CI/E2E扩展，
正式验收计数以及REM-11/REM-02状态不变。

### E68：2026-11-11，6014外层卸载完成核心清理，Data保留，profile仍待处理

用户报告刚卸载6014并询问UAC是否预期。13:48–13:55（UTC+08:00）仅
查询SCM、MSI、进程、注册表与文件，读取原始卸载日志并保存证据；
没有执行remove/repair、停止进程、删除目录、强制卸载hive或代替用户重装。

**本次确实走到了之前缺失的清理链：**

- 实际外层为PowerToysUserSetup-0.101.6014.0-x64.exe `/uninstall`，
  perUser=1、WixBundleElevated=0。
- PlanComplete记录perUser=1、action=4、related=0、armed=1；
  主MSI成功后ExecuteComplete记录ownerCleanup=1、attempted=0。
- 13:43:58记录owner移除返回restartRequired/native3010；maintenance operation
  为`CF0F51D676634BD2AA21124F0BE608B4`。生命周期日志新增SYSTEM的
  prepare-remove、commit-remove，不是仅凭bundle exit0判断成功。

| 对象 | 只读结果 |
|---|---|
| 主程序/主MSI/bundle | 用户安装目录native2，主MSI `{45B83A9B-BE34-482B-B537-721326ABCD6D}`卸载exit0，bundle `{76F931F1-2E5F-4331-B334-5CB8A49A34DC}`ARP已移除 |
| Carrier | 本次`{F1A89873-6967-5842-B15C-8848DDD2D7CA}`及旧6013均查询返回1605；原owner的carrier marker已移除 |
| SCM/正式进程 | OpenService返回1060，服务注册键不存在；原6014 Bootstrap1264/Runtime25232均不存在，未发现运行中的临时ProvisionBroker/Setup |
| 运行Code | Code目录及固定Bootstrap.exe/Runtime.exe路径均为native3，不是AccessDenied伪装的不存在 |
| Installer | active/service-created/pending-remove/drain消失，retained内容为mode=keep；最低重装版本0.101.6014 |
| Data | 2条工作区、sequence15，PTPS frame、contentHash、revision及receipt一致；epoch/init ID与历史相同，owner/Admins仍只读 |
| 残留 | Data/Policy/Installer按KeepData保留；原VA profile存在且Loaded，cleanup-pending=va-profile-review；Data内还保留4个历史事务目录、15份EXE备份，共约5.49MiB事务文件 |

这里确认的是**正式服务、carrier和运行Code已移除，业务Data已进入卸载保留状态**，
不是零残留。Data中的ready.txt是历史就绪记录，不能据此说服务仍在运行；
transactions中的Bootstrap/Runtime/Updater副本也是历史备份，不是仍在运行的Code。
本轮没有即时卸载前的完整业务快照，不声称全部字段逐字节一致；后续重装恢复仍待验证。

**UAC属于现有per-user设计的预期行为：** 普通用户可以卸载自己的主程序，
但SCM服务和受保护Code的移除仍需特权。外层以原owner执行主MSI和carrier操作，
通过签名Setup `--authorize remove`的一次授权启动临时SYSTEM清理组件；
默认KeepData不取消这项权限需求。初次预配和卸载各自需要授权，不等于每次
正常版本升级都重新预配。此前未触发owner清理的失败不能作为“卸载应无UAC”的基线。

**3010/profile边界：** 当前Lifecycle::erase_owner只检查ProfileList并写
va-profile-review标记，没有执行DeleteProfile或安排自动重启删除。
因此3010不能被解释成“重启后一定零残留”；仍需单独处理profile清理/重试。
正式进程已退出但profile仍Loaded，没有为追求干净状态强卸hive。

REM-11在P-XU普通owner路径由FAIL转PASS，保留E61/E65失败历史；
REM-02由BLOCKED转PARTIAL（卸载保留已验证，重装未验证）；
REM-06由NOT RUN转PARTIAL（pending报告已观察，删除/重试缺口仍在）。
正式统计更新为27 PASS、27 PARTIAL、0 FAIL、2 RETEST、1 BLOCKED、
68 NOT RUN、1 N/A。不外推ARM64、per-machine、OTS或完整生命周期矩阵。

证据在会话files的`nov11-production-6014-uninstall`：外层/主MSI/
bundle-removal日志、remove receipt、SCM/MSI注册查询、原PID查询、
路径native错误码、retained/cleanup-pending/最低版本及lifecycle日志、
Data离线校验、profile和历史事务文件清单。业务数据与本机身份副本不提交仓库。

### E69：2026-11-11，指定5个旧实验服务/profile清理及授权重试

用户明确要求删除Services截图第一行和最后四行，以及各自运行账户的profile：
PowerToysProtectedStorageLocalTest_<owner>、PtAliasProtoSvc_5356246d、
PtAliasProtoSvc_d2864683、PtMsiCarrierDemo_<owner>、PtPerUserVaDemo_<owner>。
先核对了服务精确名称、运行账户SID和ProfileList路径；没有把两个Alias的
服务账号profile误认成原交互用户profile，也没有包含正式6014卸载后保留的数据/
VA profile。清理前四个目标profile为Loaded，PerUserVaDemo的profile未Loaded。

准备的清理仅停止/删除上述服务，并通过支持的DeleteProfile API删除对应
profile；不删除SAM本地账户、MSI/AppX注册或Code/Data，不强制卸载hive。
提升入口拟以短生命周期SYSTEM任务执行，结果写入临时受保护目录；这只是
管理员手动维护工具，不是产品卸载行为或REM-06通过证据。

首次ShellExecute提升返回ERROR_CANCELLED（1223），授权入口没有启动。
14:41再次只读确认五个服务仍为原状态/PID，五个profile仍存在，正式Data
index hash未改变；没有创建临时SYSTEM任务或Program Files结果目录。
没有利用其他管理员窗口绕过取消，也没有自动重试；该次尝试处于授权受阻，
不把“脚本已准备”记作服务或profile已删除。

**用户明确要求“再试试”后，重新取得UAC授权：**
14:47（UTC+08:00）由一次性SYSTEM任务执行精确白名单清理，先验证运行账户SID
和ProfileList路径，再停止并删除服务，最后调用DeleteProfile。没有强杀未知进程
或强卸hive。原生删除及14:51独立复核结果：

| 目标 | Service | 对应profile |
|---|---|---|
| PowerToysProtectedStorageLocalTest_<owner> | 已删除，SCM1060、服务注册键无 | 仍Loaded，DeleteProfile返回32 |
| PtAliasProtoSvc_5356246d | 已删除，SCM1060、服务注册键无 | PtAliasProto5356246d.000仍Loaded，native32 |
| PtAliasProtoSvc_d2864683 | 已删除，SCM1060、服务注册键无 | PtAliasProtod2864683仍Loaded，native32 |
| PtMsiCarrierDemo_<owner> | 已删除，SCM1060、服务注册键无 | 仍Loaded，native32 |
| PtPerUserVaDemo_<owner> | 已删除，SCM1060、服务注册键无 | 删除成功，ProfileList记录不存在、目录native2 |

原Launcher/Worker/Bootstrap/Runtime的6个PID均不再存在。其余4个profile因
共享占用仍无法删除，通常需重启释放后再尝试；没有承诺重启本身会自动删除，
也没有注册延迟删除任务或擅自重启。
两个Alias本地账号仍保留；MSI/AppX注册和Code/Data不在这次删除范围。
正式Workspaces的index hash与操作前一致，未选中的正式VA profile也保留。
临时SYSTEM任务、受保护结果目录及提升进程已结束/清理，只保留会话诊断文件。
这次手工维护不作为产品卸载、profile自动清理或REM-06整项通过证据。

证据在会话files的`nov11-selected-service-cleanup`：
`before.json`、`preflight.json`、清理脚本、`cancelled-verification.json`、
SYSTEM执行的`result.json`及独立只读复核`after.json`。
正式验收状态与E68统计保持不变。

### E70：2026-11-11，重启后profile残留、占用及权限只读诊断

用户确认已重启，要求判断profile是否残留、是否仍被占用或权限不足。
本机LastBootUpTime为15:00:50，15:21查询确认：

- 正式PowerToysProtectedStorage VA profile及E69删除受阻的4个旧实验profile
  仍有目录和ProfileList记录，但全部Loaded=false、Status=0、State=0。
- 上述5个SID及各自SID_Classes均不在HKEY_USERS，主hive和Classes hive已卸载。
  正式及5个旧实验服务均未恢复；E69已删除的PerUserVaDemo profile仍不存在。
- 当前诊断进程是已提升管理员。5个profile根目录ACL均授予SYSTEM/
  Administrators FullControl；只打开DELETE权限再关闭，根目录和ProfileList
  注册键均成功，未调用删除API或修改ACL。
- 每个profile的NTUSER.DAT、UsrClass.dat及各自LOG1/LOG2共30个文件，
  使用OPEN_EXISTING、GENERIC_READ、零共享打开后立即关闭，全部native0；
  随后仅请求DELETE权限并关闭也全部native0。未使用DELETE_ON_CLOSE。
  这些探测证明采样时核心hive无阻止独占读取的占用，所检对象删除访问权正常，
  不等于已递归检查全部子项或实际DeleteProfile一定成功。

E69在SYSTEM下失败的原生码是32（共享占用），而非5（拒绝访问）。
重启前Loaded状态现已解除，但不能从重启后快照反推之前持有句柄的具体进程。
限定14:35之后的User Profiles Service Application/Operational日志未找到
这些SID的匹配事件；未将ProfSvc、AppXSvc或其他系统组件臆断为已证实的占用者。

正式卸载残留的确定代码原因仍是Lifecycle.cpp的erase_owner仅检查ProfileList
并写入va-profile-review，没有调用DeleteProfile或安排释放占用后的自动重试。
commit-remove只因cleanup-pending存在就返回3010，并非Windows确认已安排
重启删除。因此重启释放hive不等于profile目录和注册记录会自动消失。
未再次删除任何profile、卸载hive、变更服务/账户或修改权限；
正式Data index hash与E68/E69一致，保留现场供后续明确授权的删除验证。

证据位于会话files的`nov11-selected-service-cleanup`：
`post-reboot-inventory.json`、`post-reboot-access-probes.json`、
`post-reboot-profile-events.json`。本次不提升REM-06或其他正式验收状态。

### E71：2026-11-11，授权删除5个剩余profile及产品清理边界

用户要求先尝试删除，并提出完整卸载artifacts应为方案要求。16:31在现有已提升
管理员上下文下，精确核对正式VA及4个剩余旧实验profile的SID、ProfileList路径、
Loaded=false/Special=false、服务不存在、hive不存在及目录祖先非reparse后，
使用DeleteProfileW删除这5个profile。没有改Loaded/State/RefCount，
没有强卸hive、接管权限或手工递归删目录；没有删除任何SAM账户。

5个调用全部成功（native0）。即时原生路径查询均为2，ProfileList/CIM记录不存在；
16:32独立进程再次核对目录、ProfileList、Win32_UserProfile、HKEY_USERS/
SID_Classes和对应服务均不存在。与E69先前删除的PerUserVaDemo profile合计，
本轮范围内正式VA及5个旧实验服务profile已全部清除。
正式Data的storage.index和blob内容hash前后相同，真实交互用户profile仍Loaded，
两个Alias SAM账号仍保留。未创建新的后台任务/清理stage；
Installer里的旧va-profile-review标记及Data历史事务仍保留，没有声称整机零残留。

这次结果结合E69的SYSTEM/native32与E70的重启后Unloaded，支持此前共享占用
已释放、当前权限足够且支持的删除API能够成功；不能确定重启前具体占用进程，
也不能推广成任何Loaded profile都需要重启才能删除。

**产品讨论结论（未实施架构改造）：**
Win32_UserProfile.Loaded是只读观测值，不是“禁用profile”的开关。
正确顺序是阻止实例再次启动、让本实例进程退出并释放句柄/加载引用，
再由授权维护流程调用DeleteProfile并复核。Bootstrap的Worker析构已有
stop事件/等待及专属job兜底，ServiceMain在Worker析构后才继续退出；
这不保证系统中其他组件持有的profile引用也释放。
若产品自己调用LoadUserProfile，应由相应所有者持有token/hProfile并配对
UnloadUserProfile；该API要求先关闭已打开的hive键，不是对任意SID的强制卸载接口。
不能只写注册表State/RefCount、临时再Load/Unload一次或强卸hive来假装清理完成。

未解除的占用应有限等待、保留准确原生码，并提供真正可恢复的特权重试路径；
如果采用重启后重试，其可信执行组件必须能在主产品移除后继续完成操作，
重试时校验实例/代次、防止误删随后重装的新profile，完成后清除pending及自身。
KeepData和必要安全元数据的保留应与可删除运行artifact明确分开。
本次只完成手工profile删除及要求澄清，未实现上述重试机制、未queue新build，
REM-06及统计不因手工成功而提升为PASS。

API依据：
[Win32_UserProfile.Loaded/refCount只读](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/userprofileprov/win32-userprofile)、
[UnloadUserProfile](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-unloaduserprofile)、
[DeleteProfileW](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-deleteprofilew)。
本机证据在会话files的`nov11-selected-service-cleanup`：
`post-reboot-delete.json`、`post-reboot-delete-verification.json`。

### E72：2026-11-11，profile清理小范围测试及生产实现

用户要求先做小范围测试，再更新产品代码、commit并queue private build。
此前E70/E71确认：删除profile不改变同名服务SID，保留Data的ACL不需要在同owner
普通重装时做跨SID迁移；`keepdata-identity-check.json`还核对了服务/profile删除后
`sc showsid`与原VA SID、Data/index/blob的写入ACE完全一致。KeepData完整重装仍待验收。

**先做实际SCM实验：**
创建独立`PowerToysProfileCleanupProbe_*` VA服务，停止/删除后profile仍Loaded。
SYSTEM持有并释放自己配对的LoadUserProfile/UnloadUserProfile引用后，
DeleteProfile仍返回32，说明其他加载引用不会被这一次配对卸载清掉。
未写Loaded/RefCount、强卸hive、停止全局ProfSvc或重启机器。
测试使用独立服务名、SID和profile，不在正式PowerToys命名空间中创建服务。

**产品实现：**

- `Lifecycle.cpp`在移除服务/Code后实际调用DeleteProfile，短时共享占用有5秒上限；
  检查精确VA SID、ServiceProfiles路径及原目录的volume/file identity。
- 实验还复现：DeleteProfile遇到锁定文件时可能已删除ProfileList但留下目录，
  下一次调用返回2。仅在注册记录及两个hive均不存在、目录身份仍匹配时，
  使用原先的句柄式树删除机制清理该已授权的孤立目录；不跟随reparse子项。
- 未完成时保存SYSTEM写入的operation/SID/目录身份及原生码，复制并再次验证
  当前签名Lifecycle作为独立worker，登记SYSTEM任务。30秒后及重启后尝试，
  注册触发重复间隔5分钟/持续1天，失败还有有限的1分钟重试；
  不创建永久SYSTEM服务，不执行用户配置提供的路径或命令。
- 任务的收尾使用由签名Lifecycle生成、放在SYSTEM写入目录中的固定ASCII批处理，
  只等待签名worker、检查完成回执、删除自身操作的worker/request/script和任务。
  动态内容仅为经过校验的owner SID/operation；元数据内容不作为命令执行。
  使用相对自身的路径及显式等待，覆盖Unicode根路径和Windows-subsystem worker。
- 重新安装与profile worker共用SYSTEM lifecycle lease。同SID重建服务时，
  在新实例加载profile前取消旧cleanup operation；旧任务只能退出并回收自己的stage。
  目录被替换、目标SID不符或新实例仍活动时不能删除profile。
- 保留Data、初始化状态、现有Setup/Broker/Bootstrap分工和签名要求；
  没有增加生产测试开关或签名豁免。旧历史transactions/非profile清理不在本轮擅自删除。

**本地证据及其边界：**

Release x64 Lifecycle及InstallerTests构建无error/warning；6,254条installer checks通过，
包括新增scope/目录身份/有限重试/命令参数检查和profile名称fuzz。
主安装器静态检查、C++新文件格式及diff检查通过。

额外的显式opt-in live测试运行于SYSTEM，使用独立虚拟SID及临时profile。
CreateProfile生成的未加载fixture被移至ServiceProfiles测试布局；它不是实际外层
安装器创建的正式实例。worker使用同一份产品清理代码，测试副本调整为Windows
子系统以匹配Lifecycle；生产签名入口没有被绕过或加测试分支。
两个真实Task Scheduler case通过：

- 文件保持占用期间实际任务失败（LastTaskResult=1），保留worker/request且无完成回执；
  释放占用后通过同一个任务重试，profile/注册记录、task、stage及批处理消失。
- 换成新的cleanup operation后，旧任务不删profile，只回收自己的临时材料；
  指向profile外的链接不导致外部sentinel被删除。

中间的fixture/命令编排失败均在发布前修复。特别是单行cmd条件链可能错误报告成功，
不能用控制台worker或只测成功分支代替验证；最终改成受保护的固定批处理并实际
验证GUI worker的失败保留和成功自清理。相关原始失败日志保留，不冒充产品卸载结果。
失败fixture和其测试任务已逐项清理；实际SCM probe的Loaded状态尚未解除，
仅该probe保留待重启清理的测试任务。正式Data的index/blob hash与E71一致。

这些证据不等于签名6015安装包已完成外层卸载或重启验收。REM-06/P-XU保持PARTIAL，
KeepData重装的REM-02也不因此通过；仍需新包实机安装/卸载及重启后的资源复核。
private release pipeline仍关闭buildTests/runTests，本地通过不能表述成ADO执行了这些测试。

原始证据在会话files：
`nov11-profile-cleanup-probe-system.log`、`nov11-profile-live-fixed-batch.log`、
`nov11-profile-final-unit.log`、`nov11-profile-final-lifecycle-build.log`、
`nov11-profile-batch-fixture-cleanup.log`及本轮机器状态快照。

19:03最终复核仅剩一个隔离probe清理任务：
`PowerToys-ProtectedStorage-ProfileCleanup-4BFFFCE8436F4AF9ABDFE5137E436C51`。
它以SYSTEM运行，有boot/registration触发；实际失败返回1、pending原生码32，
worker/request/script完整保留且没有done回执。其他试验任务/失败fixture和
Program Files里的临时探针stage已清理；该probe不会触及正式Data。
实际重启后的完成状态仍需复核，不把任务登记成功当成重启删除已通过。

产品提交`daf418c10611c8ba10fad30e055081ba714176ac`已push至origin/fork。
随后通过definition76541按该commit明确queue版本`0.101.6015.0`：
[build163595010](https://dev.azure.com/microsoft/Dart/_build/results?buildId=163595010)。
独立build show确认sourceVersion、feature branch、Release x64/ARM64及版本参数；
状态inProgress，未等待或声称构建成功。队列与核对原始JSON：
`nov11-profile-private6015-queue.json`、`nov11-profile-private6015-verified.json`。
本台账随后单独提交；ADO固定使用上述产品commit，不因台账后续提交漂移。

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
