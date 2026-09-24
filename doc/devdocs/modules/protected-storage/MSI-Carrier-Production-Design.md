# PowerToys Protected Storage：MSI Carrier 生产设计

日期：2026-09-23；签名策略更新：2026-09-24
状态：**生产实现设计草案，基于已通过核心 E2E 的独立原型；不是已落地产品的代码说明。**

## 0. 阅读约定与适用范围

- **[现有]**：在 `D:\PowerToys` 主源码中可定位的项目或函数。
- **[原型]**：在 `D:\PowerToys-Workspaces-MsiCarrier\src\modules\Workspaces\WorkspacesMsiCarrierPrototype` 中已有的机制。
- **[拟新增] / [拟改造]**：本设计提出的生产项目、类、函数或行为，尚未声称实现。
- 文中的 C++ / C# 签名是接口设计；类型名、命名空间与项目名在实现时统一定稿，不是当前可调用 API。
- 首期只接入 **Workspaces**；底层为可复用的 per-owner protected storage，不包含 Workspaces 业务 schema。
- 本文取代旧文档中与“当前 MSI-carrier 方案”冲突的架构描述，旧方案保留为历史记录。
- **2026-09-24 已批准的签名策略调整**：取消跨产物单一叶子证书 pin，不维护 A/B 证书列表；
  保留已提取文件集合路径，采用机器上下文的完整代码签名链、经验证的 Microsoft 发布者和
  Windows Microsoft 正式应用根策略。CMS 必须验证内容及绑定签名值的可信 RFC3161 时间戳；
  不以证书名称、任意有效签名或自报 signingTime 代替这些检查。Q10 原始 MSI 路径仍仅为独立验证候选。

相关资料：

- [设计决策与评审状态](MSI-Carrier-Production-Open-Questions.md)
- [英文两页拓扑 slides](MSI-Carrier-Prototype-Topology.html)
- 原型说明：`D:\PowerToys-Workspaces-MsiCarrier\src\modules\Workspaces\WorkspacesMsiCarrierPrototype\README.md`
- 原型精确协议：同目录 `Runtime\RuntimeContract.txt`。

建议阅读顺序：

| 目的 | 章节 |
|---|---|
| 确认产品范围、拓扑、权限边界 | §0–3 |
| 实现项目、类、函数与 IPC | §2、§4–8 |
| 接入 Workspaces / PowerToys 安装更新 | §9–11、文末源码映射 |
| 迁移、重试、卸载和数据保留 | §10、§12–13 |
| 安排实现与验收 | §14；文末原型复用及差距表 |

### 0.1 已确认的产品决策

| 决策 | 设计约束 |
|---|---|
| Q01 | 校验合法 caller 身份与目标权限；不另建“认证后进程是否被攻陷”的识别或业务审批架构 |
| Q03 | 每用户实例独立更新；普通/安全更新均不设全用户强制完成期限 |
| Q04 | 不影响其他正常模块；失败操作具有可见的 Retry 入口，按真实状态重试而不是盲目重放 |
| Q05 | 不允许错误后的部分迁移；module 决定业务内容是否可迁移；初始化后不自动重导旧副本 |
| Q06 | 全机卸载尽量清所有产品实例；离线用户或其他实际原因产生的残留必须可见，但不成为方案 blocker |
| Q07 | 迁移提交成功后删除旧源文件；普通卸载保留业务数据及初始化状态，显式选择可彻底删除；支持 module 导入/导出；不加密设置数据 |
| Q08 | 正常代码更新不新增 UAC/Windows 重启；异常修复允许授权，最终 profile 清理可明确要求重启 |
| Q09 | 首期仅 Workspaces；其他模块不要求同步接入 |

**唯一待 PM 的产品问题是 Q02。** 本文暂按 fail-closed 设计：不能完成必要预配时，
阻断依赖 protected store 的 Workspaces 保存/启动，不阻断其他正常 PowerToys 功能；
首次授权放在用户显式启用功能时。最终触发时机、阻断范围和文案待 PM 确认。
这不是要求每次使用都提权：已正确预配的普通用户可以正常读写和升级。

### 0.2 明确不承诺的目标

- 不保证已经被控制的合法 writer 所提交的数据仍代表用户意图，不新增敏感计划审批协议。
- 保留现有必要的 Windows 提权确认；UAC 不是“配置与原计划一致”的自动证明。
- 不引入 MSIX、MST、常驻 SYSTEM updater、独立 runtime bundle 更新通道或数据加密。
- 不要求 SYSTEM 在没有真实用户上下文时完成所有离线用户的 context-2 MSI 更新/卸载。
- 不把原型的提交前回滚 PASS 等同于生产级断电原子性、任意损坏自动修复或全机零残留。

## 1. 总体拓扑和角色

```text
PowerToys release（per-user / per-machine 外层分发）
├─ PowerToys 主程序、Workspaces 客户端及业务 validation
└─ ProtectedStorageSetup.exe + 内嵌 Carrier.msi（对应 release）
                                     │
               用户 A 的普通上下文运行 Setup / MSI
                                     │
                         MSI 注册 A：USERUNMANAGED (2)
                                     │
                    MSI action → 更新控制 IPC（操作 ID）
                                     ▼
SCM（机器级登记） ── VA_A ── Bootstrap.exe
                             ├─ Runtime.exe：通用 Blob 读写服务
                             └─ 临时 UpdateCoordinator：同 VA，替换两个 PE
                                    │
                          Code_A / Data_A / update journal

用户 B：同一个 release MSI / ProductCode，独立注册、VA_B、服务及目录

仅首次预配 / 维护边界变更 / 授权修复 / 最终卸载：
原 owner Setup → UAC 授权 helper → 临时 SYSTEM broker
                                      └─ 从已验证 MSI 提取固定 Lifecycle 逻辑
```

### 1.1 必须区分的三种“bootstrap”

| 名称 | 含义 |
|---|---|
| PowerToys 安装器 bootstrapper | 现有 WiX Burn 外层安装 EXE，决定主产品安装 scope |
| ProtectedStorageSetup | 拟新增的用户侧 carrier 安装/维护入口，不常驻 |
| 服务 `Bootstrap.exe` | 每 owner 的固定 SCM 入口，以该 owner 的 VA 身份运行，并管理 Runtime |

**MSI 携带 Bootstrap 和 Runtime，不“host”运行进程。** SCM 启动 Bootstrap；
Bootstrap 启动 Runtime。VA 不执行 MSI。普通 owner 发起 MSI，但不直接写私有 Code 目录。

### 1.2 生命周期与业务数据分离

- Bootstrap：服务控制、Runtime 生命周期、版本/就绪验证、更新与恢复控制。
- Runtime：通用目标授权、读取、条件写入、事务性初始化；不解析 Workspaces JSON。
- Workspaces：业务 validation、schema 转换、迁移、导入/导出、UI 与启动行为。
- Setup/Lifecycle：机器资源创建/修改/删除；不执行 module 的迁移解析器。
- SYSTEM 永远不执行 VA 可写的 live Bootstrap/Runtime 来完成特权维护；通过 SCM 启动时仍使用配置好的 VA。

## 2. 生产项目布局与依赖

以下为**拟新增生产布局**。不把带有 `Demo` 名称、开发私钥或故障注入入口的项目直接加入正式安装包。

```text
src\common\ProtectedStorage\
  ProtectedStorage.Common\           C++ static library
  ProtectedStorage.Client\           C++ static library
  ProtectedStorage.Client.Managed\   C# library
  ProtectedStorage.Bootstrap\        C++ EXE
  ProtectedStorage.Runtime\          C++ EXE
  ProtectedStorage.Setup\            C++ EXE
  ProtectedStorage.ProvisionBroker\  C++ EXE
  ProtectedStorage.Lifecycle\        C++ EXE（仅嵌入 MSI）
  ProtectedStorage.MsiAction\        C++ EXE（仅嵌入 MSI）
  tests\                            native / managed / integration tests

installer\PowerToysProtectedStorage\
  PowerToysProtectedStorage.wixproj  per-user carrier MSI
  Product.wxs
  PayloadManifest.*
  ClientCatalog.*
```

| 项目 / 输出 | 主要职责 | 依赖 / 部署 |
|---|---|---|
| `ProtectedStorage.Common.vcxproj` | 协议、身份、路径、ACL、签名、错误、持久化基础设施 | 静态链接；不单独作为后台进程 |
| `ProtectedStorage.Client.vcxproj` | native 数据/控制客户端，验证服务端身份 | Workspaces native 项目、Setup、MsiAction 引用；不带服务实现 |
| `ProtectedStorage.Client.Managed.csproj` | managed 数据/维护客户端及 DTO | WorkspacesCsharpLibrary/Editor 引用；框架版本跟随该分支现有 managed 工程 |
| `ProtectedStorage.Bootstrap.vcxproj` → `Bootstrap.exe` | SCM host、worker supervisor、更新协调器入口 | VA Code；MSI payload |
| `ProtectedStorage.Runtime.vcxproj` → `Runtime.exe` | 通用 protected store server | 同 VA 的 worker；MSI payload |
| `ProtectedStorage.Setup.vcxproj` → `PowerToys.ProtectedStorageSetup.exe` | owner 捕获、预配/更新/repair/remove/retry 编排 | 主 PowerToys 安装目录；内嵌本 release 的 carrier MSI 和 broker |
| `ProtectedStorage.ProvisionBroker.vcxproj` → broker resource | 临时 SYSTEM 服务，固定维护操作 | 内嵌 Setup，授权后写入 SYSTEM staging；不安装永久机器 updater |
| `ProtectedStorage.Lifecycle.vcxproj` → MSI Binary resource | 创建/验证目录、服务 ACL、预配、修复、卸载 | broker 从其已验证的 MSI 提取；不能接受任意可执行路径 |
| `ProtectedStorage.MsiAction.vcxproj` → MSI Binary resource | owner MSI prepare/commit/rollback/remove guard | 普通 owner 上下文，不设置不模拟的 SYSTEM CA |
| `PowerToysProtectedStorage.wixproj` | carrier 注册、major upgrade、嵌入 payload、事务 action | 同版本所有 owner 使用同一 ProductCode，owner 独立注册 |

依赖方向：

```text
Workspaces业务层 → Client.Managed / Client → versioned protocol
Runtime / Bootstrap → Common
Setup / MsiAction → Client + Common
ProvisionBroker / Lifecycle → Common 中的安全维护工具
Common、Runtime、Bootstrap 不引用 Workspaces model/editor/launcher
```

不增加一个“业务审批服务”来解决 Q01 已排除的运行态攻陷问题。
新增的是保护边界和安装/维护组件，不是多个常驻管理员进程。
一个 owner 正常常驻两个 VA 进程：Bootstrap + Runtime；更新协调器、Setup 和 broker 都是临时角色。

## 3. 身份、目录、ACL 与授权

### 3.1 生产命名（拟定）

| 项目 | 路径 / 名称 |
|---|---|
| SCM service | `PowerToysProtectedStorage_<ownerSID>` |
| VA account | `NT SERVICE\PowerToysProtectedStorage_<ownerSID>` |
| Code | `%ProgramFiles%\PowerToysProtectedStorage\Owners\<SID>\Code\{Bootstrap.exe,Runtime.exe}` |
| release 身份目录 | 同一 Code 下的已签名 `client-catalog.json` / `.p7s` 与 release manifest；随两个 PE 一起验证/发布/回滚 |
| 私有数据 | `%ProgramData%\PowerToysProtectedStorage\Data\<SID>\` |
| 初始策略 | `%ProgramData%\PowerToysProtectedStorage\Policy\<SID>\` |
| 机器维护记录 | `%ProgramData%\PowerToysProtectedStorage\Installer\<SID>\` |
| 业务数据 pipe | `\\.\pipe\PowerToysProtectedStorage.Data.<SID>` |
| 更新控制 pipe | `\\.\pipe\PowerToysProtectedStorage.Control.<SID>` |
| owner 可写源缓存 | `%LocalAppData%\Microsoft\PowerToys\ProtectedStorage\Sources\` |
| 授权 staging | `%ProgramData%\PowerToysProtectedStorageSetup_<nonce>\` |

原型前缀 `PtMsiCarrierDemo_` 仅为证据，不复用于产品。路径通过 Windows known-folder API 解析，
不硬编码 `C:\Users` 或假设 VA profile 位于用户目录；profile 清理用实际 VA SID 的 ProfileList 记录。

### 3.2 权限矩阵

| 对象 | 普通 owner / module | 对应 VA | SYSTEM 维护 |
|---|---|---|---|
| 私有 Code/Data | 不直接写；业务读取经 IPC | 可按设计读写 | 显式维护 |
| 其他 owner 的 Code/Data | 无数据权限 | 无数据权限 | 仅已授权、正确归属的全机维护 |
| 共享父目录、Policy/Installer | 不可修改 | 只读所需策略/记录，不可更改维护授权 | 创建/维护 |
| 自己的 SCM 服务 | 不赋予配置变更或任意启停权限 | Query / Start / Stop，不授予 ChangeConfig / WriteDac / WriteOwner | 创建、变更与删除 |
| 数据 IPC | 通过角色/目标授权后使用 | 作为 server | 不作为日常业务客户端 |
| 更新 IPC | 仅维护角色，且 payload 独立验证 | 实际更新执行方 | 不借此执行用户提供的程序 |

root 由 SYSTEM 创建并显式指定 owner；不能假定 LocalSystem 创建文件就必然得到 SYSTEM owner。
Code/Data 的 VA 写权限意味着 VA 失陷可以影响自己的代码和数据，这不是 immutable Bootstrap 设计。
私有文件不许 reparse/hardlink 绕过；目录祖先、文件身份和共享模式必须按 handle 验证。
“访问被拒绝”与“对象不存在”是不同错误，不能成功化。

### 3.3 `CallerAuthenticator` 与 `TargetAuthorizer`（拟新增）

```cpp
Result<CallerIdentity> CallerAuthenticator::Authenticate(HANDLE pipe);
Result<void> TargetAuthorizer::Authorize(
    const CallerIdentity&, TargetId, StorageOperation);
Result<void> ServerIdentityVerifier::Verify(
    HANDLE pipe, const OwnerIdentity&, ServerRole);
```

`CallerIdentity` 至少包含实际 TokenUser SID、进程 handle/PID/创建时间、image identity、
签名/catalog 检查结果和授权角色。步骤：

1. pipe 只接受本机连接，设置受保护 DACL；模拟客户端取得真实 SID，及时恢复模拟状态。
2. 绑定实际 pipe caller 进程及创建时间，不接受请求正文中的 `ownerSid` 自我声明。
3. 比较已配置 owner SID，检查受支持用户身份；OTS 管理员不能替换业务 owner。
4. 根据可信 release catalog 验证预期 PE 身份、产品/角色与精确文件哈希，并验证 catalog 的发布签名。
5. 用角色到 target/operation 的静态授权表做最小权限检查；读、改、初始化和维护权限不能混同。
6. 客户端同样验证实际 pipe server 为对应 VA、正确 Bootstrap/Runtime image 和本次 live process。

**这不是运行态失陷识别。** 合法 module 被控制后提交的业务内容在 Q01 受信任 writer 假设之外。
路径或“Microsoft 签名”本身不是完整 caller 授权，更不能把同一 SID 下全部进程当作合法客户端。
目标角色映射、调用者绑定与本地 image 检查需要正式安全评审；原型的 owner-SID-only IPC 不满足该生产要求。

### 3.4 授权策略与静默更新不冲突

SYSTEM policy 固定授权根、公钥/签名信任约束、允许的产品角色与 target 范围。
release 内签名的 `ClientCatalog` 列出兼容客户端哈希、protocol 范围和既有角色绑定；
它随 carrier payload 验证与提交，不能由普通用户提供未经签名的 allowlist。

常规客户端换版本不应要求在 SYSTEM policy 中逐一手改 PE hash。
catalog 只能在既有授权范围内更新，不能借更新扩大到任意模块、任意文件路径或任意 SYSTEM 操作。
变更信任根、服务身份、固定 ImagePath 或扩展受保护授权范围，走显式维护边界，不能冒充普通代码更新。
catalog 的轮换/兼容性及签名证书撤销策略由发布基础设施确定并验证，不承诺离线撤销实时生效。
生产 manifest 必须覆盖 catalog 的 hash、格式版本和角色绑定；catalog 的读取验证也必须固定句柄与祖先。
更新/回滚的单位因此是“两个 PE + 关联签名元数据”，不只是调用原型的 `PublishPair()` 后单独修改 allowlist。
不能让旧 PE、新 catalog 或相反的混合状态通过 readiness 检查。

## 4. 类设计与主要函数

### 4.1 通用类型与 native client

| 类 / 所在项目 | 主要函数 | 输入 / 输出与职责 |
|---|---|---|
| `OwnerIdentity` / Common | `FromCurrentToken()`、`FromBoundRequester(pid,birth)`、`ServiceSid()` | 只从真实 token 得到 owner，生成确定 VA/service identity |
| `StoragePaths` / Common | `ForOwner(owner)`、`ResolveTarget(target)`、`ValidateLayout()` | 从固定 roots/target catalog 映射路径；不把客户端文件路径作为写入目的地 |
| `SafeFile` / Common | `OpenPinnedRead()`、`WriteAndFlush()`、`PublishAtomic()` | 验证 ancestor/reparse/link/ACL，持有源句柄直到消费完成 |
| `ReleaseVerifier` / Common | `VerifyPackage()`、`VerifyPayload()`、`VerifyClientCatalog()` | 签名、产品、架构、hash、版本与上下限，错误保留原生代码 |
| `ProtectedStoreClient` / Client | `Connect()`、`GetTargetState()`、`GetBlob()`、`PutBlob()`、`QueryWrite()`、`AcknowledgeSourceCleanup()` | typed data IPC，timeout ≠ 操作失败，保留 request/operation ID |
| `MaintenanceClient` / Client | `GetStatus()`、`PrepareUpdate()`、`QueryUpdate()`、`CommitUpdate()`、`RollbackUpdate()` | 与 Bootstrap 控制面交互，不提供任意 service/path 参数 |
| `ProtectedStoreClient` / Client.Managed | 对应 `*Async(..., CancellationToken)` | C# DTO/异步 pipe；与 native 共用协议规范和 golden test vectors |

不额外部署一个只为 C# 转调而常驻的 native bridge。native/managed 客户端分别实现同一 framing，
用共同协议向量测试一致性；协议定义不依赖 C++ 内存结构 padding。

### 4.2 Bootstrap 与 Runtime

| 类 / 项目 | 主要函数 | 职责 |
|---|---|---|
| `BootstrapService` / Bootstrap | `ServiceMain()`、`OnStop()`、`StartControlServer()`、`GetStatus()` | SCM 状态/控制；先验证自身 VA/路径/策略，再启动 worker |
| `RuntimeSupervisor` / Bootstrap | `Start()`、`WaitReady()`、`DrainAndStop()`、`VerifyChild()` | job、明确继承 handle、PID+birth+image 校验；worker 就绪不只等于进程存在 |
| `UpdateTransactionManager` / Bootstrap | `Prepare()`、`Query()`、`Commit()`、`Rollback()`、`RecoverPending()` | 更新 journal 状态机、操作去重、提交决定和维护锁 |
| `UpdateCoordinator` / Bootstrap 临时模式 | `Run()`、`StopOldPair()`、`PublishRelease()`、`StartAndVerify()`、`RestoreRelease()` | 同 VA、在旧 host job 外运行，跨 host 重启持有 lease；发布/恢复两个 PE 及配套 catalog/manifest |
| `RuntimeService` / Runtime | `RunWorker()`、`StartDataServer()`、`Drain()`、`Stop()` | 通用业务数据面、启动恢复与新请求阻断 |
| `RequestDispatcher` / Runtime | `ValidateEnvelope()`、`Dispatch()` | 认证、限额、命令路由，不解析业务 JSON |
| `BlobStore` / Runtime | `Inspect()`、`Read()`、`ConditionalWrite()`、`QueryWrite()` | 初始化/Replace CAS、单 target 原子数据提交 |
| `TransientBlobStore` / Runtime | `Create()`、`Read()`、`Remove()`、`Reclaim()` | 经角色授权的 preview/snapshot 暂存，有界生命周期，不成为永久数据的备用来源 |
| `StoreRecovery` / Runtime | `RecoverTarget()`、`ValidateRecord()`、`FindCommittedOperation()` | 区分未初始化、已初始化、异常；不静默创建空数据覆盖损坏 |
| `MaintenanceGate` / Common | `Acquire()`、`BeginDrain()`、`WaitForWriters()` | 更新/repair/remove 与写入互斥，持久化提交点明确 |

### 4.3 Setup、MSI action 与特权维护

| 类 / 项目 | 主要函数 | 职责 |
|---|---|---|
| `OwnerSetupCoordinator` / Setup | `Inspect()`、`EnsureProvisioned()`、`Upgrade()`、`Repair()`、`Remove(mode)` | 普通 owner 生命周期总入口；不猜测“当前登录用户” |
| `AuthorizationRendezvous` / Setup + Broker | `CreateRequest()`、`Authorize()`、`WaitForResult()` | 绑定 caller PID/birth、同一已验证 Setup image、nonce、一次操作；取消/超时明确 |
| `MsiInventory` / Setup | `EnumerateOwnerProducts()`、`FindInstalledRelease()` | MSI API context 2 查询，拒绝歧义；不使用 Win32_Product 触发 repair |
| `MsiExecutor` / Setup | `InstallRelease()`、`RepairInstalled()`、`UninstallInstalled()` | 在 owner 非提权 token 下执行，取得实际 native MSI result |
| `MsiTransactionAction` / MsiAction | `Begin()`、`Prepare()`、`Commit()`、`Rollback()`、`RequireRemovalAuthorization()` | 每次 MSI 新操作 ID；CA 与 VA 事务绑定 |
| `ProvisionBroker` / Broker | `RunPhase()`、`ExtractVerifiedLifecycle()`、`PublishResult()` | 临时 SYSTEM host；只调用固定 helper 和 verb |
| `InstanceProvisioner` / Lifecycle | `Create()`、`CommitInitial()`、`UndoInitial()` | 创建身份、ACL、policy、maintenance lock、SCM；保留初始化状态而非无条件 reseed |
| `InstanceMaintenance` / Lifecycle | `PrepareRemove()`、`CommitRemove(mode)`、`UndoRemove()`、`RepairBootstrap()` | 拿维护锁；SYSTEM 不信任 VA journal 来获取额外授权 |
| `CleanupCoordinator` / Lifecycle | `EnumerateOwnedInstances()`、`TryCleanupOwner()`、`CleanupProfile()`、`BuildReport()` | 按 Q06 best-effort 收口，报告每项残留、owner 与重试要求 |
| `OperationRetryCoordinator` / Setup / 客户端编排 | `GetFailure()`、`ClassifyRetry()`、`RetryFailedOperation()` | 查询真实结果，只重试失败单元；UI journal 只是提示，不是授权来源 |

`RepairInstalled()` 与 `RepairBootstrap()` 不相同：
前者通常复用已注册 release 的 MSI、服务可用时验证同版本 payload；
后者适用于 fixed Bootstrap 不可运行，必须通过授权的固定维护程序修复，不能假装普通 repair 总能工作。

## 5. 通用数据 API

### 5.1 Endpoint 与 framing（拟定 v1）

生产数据/控制 endpoint 分离，避免 Runtime 排空时导致更新决策 pipe 一同失效。
控制 endpoint 由 Bootstrap 承载，数据 endpoint 由 Runtime 承载。

```text
Header: magic:u32, protocolMajor:u16, protocolMinor:u16,
        command:u32, headerLength:u32, bodyLength:u32,
        requestId:16-byte UUID
Body:   UTF-8 metadata + length-delimited opaque bytes
```

拟定 wire v1 的具体编码：

| offset | 字段 | 编码 |
|---|---|---|
| 0 | magic | 4 字节 ASCII `PTPS` |
| 4 / 6 | major / minor | little-endian uint16，初值 1 / 0 |
| 8 | command | little-endian uint32，来自版本化 enum |
| 12 / 16 | headerLength / bodyLength | little-endian uint32；v1 headerLength=36 |
| 20 | requestId | 16 字节 UUID，固定 RFC/network byte 顺序；不得 memcpy Windows GUID 结构而不转换 |
| 36 | body | uint32 metadataLength + UTF-8 JSON metadata + 剩余 opaque bytes |

`requestId` 关联一次传输请求/响应；`operationId` 是 metadata 中的持久化业务操作 ID，
重试可以使用新 requestId，但必须保留原 operationId 和请求摘要。
JSON 数字不承载高精度 revision/PID birth 等不适合某些客户端表示的整数；
这些字段使用规定的十进制字符串或固定二进制类型，native/managed golden tests 必须一致。
`metadataLength <= bodyLength - 4`，总长度先检查再分配，禁止溢出、trailing bytes 和畸形 UTF-8。
未知 major 拒绝；minor 可协商的扩展有显式 capability，不靠忽略必需字段“向前兼容”。

`GetCapabilities()` 在通过基础身份校验后返回协议范围、功能位、运行 release 和容量上限；
签名的 release compatibility metadata 决定客户端是否可继续，而不是只比较 EXE 文件版本完全相同。
初始工程限额建议：单 Blob 16 MiB、metadata 64 KiB、每 owner 有界并发队列；
这些是可测量的起始限额，不是已通过产品数据规模测量的结论。
超限返回 `QuotaExceeded`，不截断，不靠无界缓冲解决。

### 5.2 类型与调用示例

```cpp
struct TargetId { std::string value; };           // catalog 内的逻辑键，不是路径
struct Revision { std::string epoch; uint64_t sequence; };
enum class WriteConditionKind { IfUninitialized, IfRevision };
struct WriteCondition {
    WriteConditionKind kind;
    std::optional<Revision> expected;
};
struct BlobValue {
    TargetId target;
    Revision revision;
    std::string contentSchema;                  // opaque module metadata
    std::vector<std::byte> bytes;
};
struct WriteRequest {
    Guid operationId;
    TargetId target;
    WriteCondition condition;
    std::string contentSchema;
    std::vector<std::byte> bytes;
    std::optional<SourceReceipt> migrationSource;
};
```

| API | 返回 | 关键语义 |
|---|---|---|
| `GetTargetState(target)` | `Uninitialized / Initialized / RecoveryRequired` + revision | 基于受保护状态，不以文件是否存在简单代替 |
| `GetBlob(target)` | `BlobValue` | 一致读取已提交版本；未初始化不返回“空成功” |
| `PutBlob(request)` | `Committed(revision, operationId)` 或 typed error | 验证权限/条件，持久化后才回成功 |
| `QueryWrite(target, operationId)` | `Committed / NotCommitted / Unknown` | 超时后核对同一操作；不凭断连推定结果 |
| `GetServiceStatus()` | 实际协议版本、release、维护/就绪状态 | 不将 MSI 注册版本当作当前进程版本 |

preview 流程复用同一个数据服务的通用短期对象能力：
`CreateTransientBlob(targetClass, operationId, bytes)` 返回服务生成的对象 ID；
`GetTransientBlob(targetClass, id)` / `DeleteTransientBlob(targetClass, id)` 仍校验 owner 与角色。
不能用任意 targetClass 或对象 ID 越过 target 授权，也不从旧的用户可写 preview 文件提供隐式 fallback。

`IfUninitialized` 对应之前的 `IfAbsent` 产品语义，但明确“逻辑未初始化”而非“磁盘文件不存在”。
普通替换使用 `IfRevision`，防止两个 Editor 或 Snapshot/Editor 互相覆盖。
若客户端尚无 revision，先读取；不要为了方便使用无条件 Replace 跳过并发检查。

数据 API 不暴露 `MigrateWorkspaces()`、任意 `ReadFile(path)`、任意 `WriteFile(path)`、
启动任意进程或删除任意目录。显式彻底删除是生命周期操作，不能把 Blob 写入升级成删除服务的权限。

### 5.3 错误与重试

统一结果携带 `operationId`、`errorCode`、`nativeCode`、`retryClass` 和安全的 message key：
其中 `ModuleValidationFailed` 由 module 本地层产生，不意味着通用 Runtime 执行了业务 validation。

| 错误 | 处理 |
|---|---|
| `Unauthorized / TargetDenied` | 不重试绕过；报告客户端/权限问题 |
| `NotProvisioned / AuthorizationRequired` | 显式 setup 入口；Q02 暂定阻断受影响功能 |
| `AlreadyInitialized` | 读取现有目标，不再导入旧文件；并发初始化赢家必须可核对 |
| `RevisionConflict` | 重新读取并让 module 合并/提示，不能盲目覆盖 |
| `BusyMaintenance` | 有界等待或用户重试；原数据不丢弃 |
| `InvalidPayload / ModuleValidationFailed` | 修复输入后再试，不自动跳过坏条目 |
| `Timeout / OutcomeUnknown` | 先 Query；不生成新 ID 重复提交 |
| `RecoveryRequired / IncompatibleVersion` | 暂停相关操作，诊断/修复；不能明文 fallback |
| `CleanupPending / RebootRequired` | 单独显示尚未完成的清理，不回滚已经成功迁移的数据 |

不把 `NotCommitted` 当成“不曾发生过任何写入”。有限 journal 无法证明历史操作时应返回 `Unknown`，
必须读取 revision/完整记录并由 module 处理，不把过期去重记录当成重放许可。

## 6. 存储提交、初始化与并发

### 6.1 持久化模型（拟定）

每个目标一个受保护的 record，包含：

```text
recordFormatVersion / targetId / storeEpoch / revision
initialized=true / contentSchema / contentLength / contentHash
lastCommittedOperation / requestDigest
optional migration source receipt + source-cleanup state
opaque payload bytes
```

hash 用于一致性/损坏检测，不提供防同 VA 攻击的密码学授权。
设置数据不加密，不依赖 VA profile 解密密钥。
首次提交前，owner/target 的已建立状态由受保护的 target catalog/index 标识；
历史已初始化但 record 丢失时进入恢复，不能再次成为 `Uninitialized`。

`BlobStore::ConditionalWrite()`：

1. 验证 caller role、target、schema label framing、大小及 operation ID。
2. 获取该 target 的序列化写锁；maintenance gate 允许新写才继续。
3. 核对初始化状态或 CAS revision，并检查同 operation ID 的请求摘要。
4. 将 payload 与 metadata 写入同一新 generation record，flush；写入有界 journal 的提交意图。
5. 用 Windows 文件发布原语切换目标并维护已初始化索引；恢复代码能够区分 intent、commit 和损坏。
6. 持久化最终结果后返回；响应丢失时可凭同 operation ID 查询。

不能把“写数据文件 + 另写一个布尔标记”当作已解决原子性。
journal、generation、索引具体格式属于 Common/Runtime 实现；
必须验证 flush/rename 各边界的故障恢复，不将多个独立文件发布称为一个 OS 原子事务。

### 6.2 维护窗口中的数据规则

- 更新前停止接收新写，等待已接受写达到明确终态，持久化后再退出 worker。
- 新 Runtime 在 `prepared` 期间只允许维护就绪/查询，不开放普通写入。
- MSI 决定 rollback 时恢复旧代码，不覆盖维护前已经确认成功的业务保存。
- 普通数据写入恢复开放在 committed/unchanged 且 runtime 就绪之后。
- 原型只验证 PE 和 worker readiness，没有实现这里的业务写排空；这是生产必补项。
- Workspaces 的业务 schema 转换由 module 在代码更新完成后执行，作为独立 CAS 事务。
  默认不让 Runtime 更新隐式重写业务 schema，避免旧代码回滚后无法读取被提前升级的数据。

## 7. 版本化发布与 MSI 所有权

### 7.1 Payload chain

```text
PowerToys release
  → Setup（本 release）
      → 内嵌完全相同的 Carrier.msi
      → ProvisionBroker resource → 同一份 Carrier.msi

Carrier.msi Binary:
  Lifecycle → 初始 Bootstrap / Runtime / policy resources
  MsiAction → Bootstrap / Runtime / manifest / detached CMS
              + 生产新增 signed ClientCatalog
```

沿用原型 exact-byte 核对：Setup 和 Broker 内嵌 MSI 必须完全一致，
Lifecycle seed 与 MsiAction 的两个 PE 必须匹配。
业务客户端/catalog 可随主 PowerToys 分发，但不能提供一条绕过 carrier MSI 的 live PE 更新入口。

### 7.2 Carrier MSI 与主 MSI 是两种安装归属

- carrier MSI 固定为普通 owner 的 per-user context 2。
- PowerToys 主安装继续独立支持 per-user/per-machine，不通过切换 `ALLUSERS` 把 carrier 注册偷偷变成 SYSTEM 或管理员所有。
- 同 release 的 A/B 共用 ProductCode，不使用 MST；不同 release major upgrade 更换 ProductCode，保持产品 UpgradeCode。
- 生产 GUID、命名和版本由正式构建生成，不复用原型 GUID；PE 四段版本与 MSI 三段版本建立明确 release 映射。
- 不为不同字节复用同一 release 身份：same-version repair 只接受该 release 的精确哈希。
- MSI File table 不把 VA 自行替换的 live Code 当作静态 MSI-owned file 维护。
  MSI 负责 release 注册/资源与事务 action；Code 发布由已授权 VA 完成。

### 7.3 MSI action 时序

| Action | 执行上下文 / 时机 | 作用 |
|---|---|---|
| `Begin` | 普通 owner，InstallInitialize 后 | 创建本次 UUID，绑定 release 和 owner |
| `Rollback` | 模拟 owner 的 rollback CA，prepare 前登记 | 只撤销匹配的未提交操作 |
| `Prepare` | 模拟 owner 的 deferred CA | 从 MSI 资源取 payload，向 VA 请求 prepare 并等待就绪 |
| `Commit` | 模拟 owner 的 commit CA | 将精确操作的 commit 决定提交给 VA，查询终态 |
| `RequireRemovalAuthorization` | 模拟 owner 的 remove CA | 独立卸载须有受保护维护授权；major upgrade 的旧产品移除不删除服务 |

外层协调器在主 MSI 执行完成后串行运行 carrier MSI，
不在主 MSI CA 中启动嵌套 MSI；busy 1618 进入正常重试状态。
维护统一识别 0/3010/1641 的含义，但“需要重启”不等于“所有清理都完成”。

## 8. 更新控制 API 与状态机

```cpp
PrepareResult PrepareUpdate(UpdateOperationId, VerifiedSourceDescriptor);
UpdateStatus QueryUpdate(UpdateOperationId);
UpdateResult CommitUpdate(UpdateOperationId);
UpdateResult RollbackUpdate(UpdateOperationId);
ServiceStatus GetStatus();
```

source descriptor 仅选择数据输入；目的 Code、服务账户、ImagePath 由已配置路径决定。
源目录可在 owner 可写 staging，但每个源文件及祖先在检查/复制期间保持不可替换的 handle，
数据不作为 SYSTEM 可执行源使用。变更 operation/release/hash 的重复请求拒绝。

```text
Absent → Preparing → Prepared → Committed
                  ↘         ↘
                   RollingBack → RolledBack
任意不可确认/不可恢复状态 → RecoveryRequired
同版本 + 精确匹配 + 就绪 → Unchanged
```

| 状态 | 允许的操作 |
|---|---|
| Preparing | query、匹配 rollback 请求；拒绝其他新更新 |
| Prepared | 匹配 commit/rollback；业务写入仍被 drain |
| Committed | query / 幂等 commit；不接受旧操作 rollback |
| RolledBack | query / 幂等 rollback；不接受旧操作 commit |
| Unchanged | 确认同版本验证成功，不回退到之前的 generation |
| RecoveryRequired | 明确修复路径；不把未知结果转换为 absent/success |

原型采用 180 秒 prepared 决策超时后回滚、约 60 秒控制调用超时；生产值需结合性能和 Windows Installer 实测。
操作超时不是产品强制更新期限（Q03），也不是无界自动重试。

### 8.1 提交之后的故障恢复

MSI 和 VA 不是同一个事务引擎。生产必须显式处理：

| 观察 | 处理原则 |
|---|---|
| prepare 前验证失败 | Code 不变；MSI 按原生失败退出 |
| prepare 后、commit 前失败 | 尝试恢复旧 PE，核对 readiness，记录终态 |
| commit 已成功但响应丢失 | 同 ID 查询/幂等确认，不能启动新的 rollback |
| MSI 注册和 live PE 不一致 | 标记需要协调，取得真实注册、签名 release 和 VA 决策后恢复；不静默显示成功 |
| Bootstrap/更新协调器丢失 | 可运行的可信 VA host 先恢复；否则显式授权 repair（Q08） |
| 元数据损坏或不能确定旧/新状态 | 暂停相关功能并保留诊断；不猜一个版本，也不导入旧业务数据兜底 |

拟新增 `ReleaseReconciler::Inspect()` / `PlanRecovery()` / `ExecuteRecovery()`，由 owner Setup 编排，
能在正常 owner token 下恢复正确的 MSI 状态时执行；需要改变保护边界时明确授权。
SYSTEM 不读取 VA 可写 journal 然后任意执行其中指定的路径。
是否能在无额外授权条件下覆盖全部 crash 点必须经故障注入确认，
不能为了满足“retry”而跳过反降级或强行修改 Windows Installer 数据库。

## 9. 首次预配与 owner 绑定

### 9.1 用户流程（Q02 暂定）

```text
用户显式启用 Workspaces
→ WorkspacesInitializationCoordinator.EnsureReadyAsync()
→ Inspect：实例不存在
→ 展示首次安全设置说明
→ 普通 owner Setup 建立一次性 rendezvous
→ UAC 授权同一已验证 Setup / 临时 broker
→ SYSTEM 创建对应 VA、固定服务配置和私有目录
→ 初始两个 PE、policy、maintenance.lock 从 MSI 资源落盘
→ 验证 VA 服务及 worker 就绪，提交初始预配
→ 回到原 owner 执行 context-2 carrier MSI
→ 成功后执行该 module 的首次数据初始化
```

如果管理员提供的是另一账户凭据，业务 owner 仍来自提权前原始进程；
不能用管理员 helper 的 `%USERPROFILE%`、HKCU 或 TokenUser 决定要创建谁的实例。
初次安装失败的回收只处理本次新创建且由本次 receipt 授权的资源，不能删除以前已经建立的数据。

原型的全局 ready/succeeded/failed events 是可复用的同步机制，不是完整授权本身。
生产应同时验证原 requester 的 PID、birth、进程存活、同一 Setup image、签名/实际字节、
nonce 和固定操作。不能允许 UI 输入任意 owner SID、MSI、DLL、执行命令或目的路径给 SYSTEM。

### 9.2 用户取消或不能授权

返回 `AuthorizationRequired` / `UserCancelled`，保持旧源文件和未初始化状态。
Workspaces 提供“Retry setup”，不在每次后台轮询中反复弹 UAC。
按 Q02 暂定方案，受影响的保存/启动不能回退旧明文数据源；其他正常 PowerToys 功能继续使用。
授权完成不自动意味着业务迁移成功，两步具有不同错误状态和重试单元。

### 9.3 安装入口与维护 CLI（拟定）

```text
PowerToys.ProtectedStorageSetup.exe inspect --json
PowerToys.ProtectedStorageSetup.exe ensure
PowerToys.ProtectedStorageSetup.exe upgrade
PowerToys.ProtectedStorageSetup.exe repair
PowerToys.ProtectedStorageSetup.exe remove --keep-data
PowerToys.ProtectedStorageSetup.exe remove --purge-data
PowerToys.ProtectedStorageSetup.exe retry <opaque-operation-id>
```

公开命令默认只作用于当前真实 owner，普通维护不接受 `--owner arbitrarySID`。
全机卸载具有独立的已授权入口，从受保护 inventory 枚举 owner，而不是允许任意调用者指定系统路径。
内部 elevated/broker 模式只接受完整的 owner-bound request，不作为公开通用管理工具。

## 10. Workspaces 数据迁移与日常使用

本节的业务类全部为**拟新增或拟改造**，与第 4 节的通用 storage 类不同。

### 10.1 业务接口

```csharp
interface IWorkspacesRepository
{
    Task<WorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken);
    Task<SaveResult> SaveAsync(
        WorkspacesDocument document, Revision expectedRevision,
        Guid operationId, CancellationToken cancellationToken);
}

interface IWorkspacesMigrationValidator
{
    ValidationResult ValidateAndConvert(ReadOnlyMemory<byte> legacyBytes);
}

sealed class WorkspacesInitializationCoordinator
{
    Task<InitializationResult> EnsureReadyAsync(CancellationToken cancellationToken);
    Task<InitializationResult> RetryAsync(Guid failedOperation, CancellationToken cancellationToken);
}

sealed class WorkspacesMigrationCoordinator
{
    Task<MigrationResult> InitializeIfNeededAsync(CancellationToken cancellationToken);
    Task<SourceCleanupResult> RetrySourceCleanupAsync(CancellationToken cancellationToken);
}
```

| 类 | 主要函数 | 责任 |
|---|---|---|
| `ServiceWorkspacesRepository` | `LoadAsync()`、`SaveAsync()` | module model ↔ 经过业务 validation 的 UTF-8 内容；使用 CAS revision |
| `WorkspacesMigrationValidator` | `ValidateAndConvert()`、`ValidateLaunchDescriptors()` | 文件/对象整体检查、旧 schema 转换、敏感字段取舍，不在 Runtime 中实现 |
| `LegacyWorkspaceSource` | `OpenStableSnapshot()`、`DescribeSource()`、`DeleteIfUnchanged()` | 只处理已知旧文件；源 identity、长度/hash 和句柄绑定，避免删掉后来新写的数据 |
| `WorkspacesMigrationCoordinator` | `InitializeIfNeededAsync()`、`QueryOutcomeAsync()`、`RetrySourceCleanupAsync()` | 无部分成功的首次迁移、确认提交再删源 |
| `WorkspacesImportExport` | `ImportAsync()`、`ExportAsync()` | 用户明确选路径；module 校验后整体写入；外部文件不是自动可信的 live store |
| `WorkspaceLaunchSnapshotProvider` | `GetCommittedProject()`、`CreatePreview()`、`ReadPreview()` | 保存的 workspace 与临时预览使用明确来源，不留旧 temp-file 绕过通道 |
| `WorkspacesFailureViewModel` | `ShowFailure()`、`RetryCommand`、`RetryFailedOperationAsync()` | 本地化状态和重试入口，保留用户未提交编辑而不静默丢弃 |

### 10.2 Target 与角色授权（首期拟定）

| 逻辑目标 | 生命周期 | 典型角色与权限 |
|---|---|---|
| `workspaces.repository` | 持久化；一个 owner 的完整 workspace 集合 | Editor/实际保存者初始化与 CAS 写；Launcher 读取并按现有职责更新应用元数据及 lastLaunchedTime |
| `workspaces.preview` + server-issued preview ID | 暂存；一次 capture/edit/launch preview | 生产者创建，受授权消费者读；仅该 owner，不能当作 repository 初始化来源 |
| 数据提交回执/初始化元数据 | 与 target record 关联 | service 维护；客户端不可通过普通 JSON 字段伪造 |

preview ID 只是引用，不是代替 caller 身份验证的 bearer secret。
临时数据可保持在 service 内存/有界私有暂存中；由流程结束、进程结束或容量界限回收。
这不是旧源文件的 retention period，也不要求给用户建立新的备份保留策略。

对 renderer、arranger 等仅需数据的角色采用只读权限；不因它们同属 Workspaces 就开放更新或任意写入。
需要哪些进程持有 preview 写权限，按实际 capture/editor 交互接线，而不是把 `*.exe` 全部放入 writer。
敏感行为由 module validation 负责；不新增 Q01 排除的独立计划审批层。
**Launcher 不能误设为只读：现有代码实际更新应用信息和最后启动时间。**
其 module repository 应执行读取最新 revision、按业务键更新对应字段、完整 validation、CAS 提交；
并发冲突可以对可证明不冲突的业务字段重新合并，不能把旧的整个 workspace 集合覆盖回去。
通用 service 不检查“只能改 lastLaunchedTime”之类业务字段；这些约束属于可信 module 代码。

### 10.3 完整迁移时序

1. 确认服务已预配、协议兼容且非维护状态；查询 `workspaces.repository` 的初始化状态。
2. 若已初始化：读取 protected record，不读取旧文件替换它；必要时继续仅针对旧源的清理重试。
3. 若从未初始化：module 以原用户权限打开已知 legacy 文件并取得稳定快照。
4. module 对整份迁移单元验证和转换；任何错误都失败，保留源，不提交部分 workspace。
5. 生成操作 UUID，提交 `PutBlob(IfUninitialized, completeBytes, SourceReceipt)`。
6. 若另一个正常客户端已抢先初始化，读取赢家结果；不能因为收到 `AlreadyInitialized` 就删除自己未被接受的源。
7. 只有确认本次提交成功，或查询证明确为本次已接受的迁移，才删除对应旧源对象。
8. 删除失败/源被替换：显示“迁移完成，旧文件清理待处理”，保留初始化状态，仅重试清理，不重导数据。
9. 源不存在且逻辑从未初始化：module 可创建合法默认数据；空 workspace 列表也是有效初始化。
10. 已初始化但 record 缺失/损坏：恢复路径，不按“文件没了”重新初始化。

删除发生在 module 原用户上下文，VA 不需要遍历真实用户 profile。
不递归删除整个原设置目录，因为其中可能有不属于本次迁移的日志、快捷方式或其他配置。

### 10.4 源清理回执（拟新增通用元数据能力）

为覆盖“数据提交成功后 module 崩溃，尚未删源”，record 保存 source receipt：
origin tag、源 file identity、源内容 hash 和 migration ID。
service 只保存这些有界 opaque 字段，不解释 Workspaces 路径、不代表它为客户端提供文件删除权限。
后续普通 Save 不得丢弃未完成的 source-cleanup receipt。

补充通用 API：

```text
GetTargetState(target)
  → 可返回已提交 migration receipt 与 cleanupPending
AcknowledgeSourceCleanup(target, receiptId, operationId)
  → 幂等更新匹配 receipt 的状态，不修改业务 bytes/revision
```

客户端重新执行 cleanup 时必须读取该已提交 receipt，验证目前的源仍为那一份；
不能信任用户可改的 retry-state 文件来决定要删除什么。
无需定时 retention job，使用已有 Retry 或下次正常初始化流程处理。

### 10.5 日常保存、导入与导出

```text
Load → {完整 WorkspacesDocument, revision}
Edit → module validation
Save → PutBlob(IfRevision=loadedRevision, completeBytes)
Conflict → 重新读取 / 显示冲突，不无条件覆盖
```

导入是显式操作，按 module 的整体校验规则生成完整目标集合，
合并/替换策略由 Workspaces UI 明确表达，最终一个 CAS 提交；不允许错误时导入半份。
导出是用户显式选择输出文件，将当前有效配置按模块格式写出；
导出的副本没有 protected-store 权威身份，再导入时必须重新 validation。
service 不接受用户指定的导入/导出文件路径，避免变成任意文件代理。

## 11. PowerToys 两种安装 scope 的更新接线

### 11.1 原则

```text
外层主 PowerToys transaction 完成
→ 原 owner 的非提权协调器 / 新版 runner 启动 gate
→ 检查 carrier 与客户端兼容性
→ 有新版且本 owner 实例已存在：静默升级 owner carrier
→ 实际版本就绪后开放相关功能
```

正常更新不延迟到第一次 launch workspace 才做；但新 module 从未启用且没有实例时，
后台更新不能擅自弹首次预配 UAC。
单次失败由 Q04 的状态/Retry 处理，不回滚其他成功模块。

### 11.2 主程序 per-user

主安装和 owner carrier 分别是串行事务。更新入口在原用户普通上下文中执行。
若 PowerToys 正以高权限运行，需要显式取得该真实 owner 对应的非提权会话/启动器；
不能把一个任意 Explorer 或其他登录用户当作目标。无法建立正确上下文就报告 deferred/retry。

### 11.3 主程序 per-machine

机器级主安装可以由授权管理员或 SYSTEM 完成，carrier 仍属于每个真实 owner。
有交互发起人时，在提权前绑定 owner，主安装完成后回到该 owner 的普通协调器；
授权管理员只提供维护许可，不拥有原用户的业务实例。

无人交互的企业/SYSTEM 安装只部署新的已签名代码/载体；
用户下次正常启动 PowerToys 时检查和协调自己的实例，不声称 SYSTEM 安装已更新所有用户。
主程序文件全机共享，A/B 的实例可暂时不同版本，因此兼容性判定必不可少。
用户离线不是强制更新期限的开始，不新增逾期停用机制（Q03）。

### 11.4 `ProtectedStorageUpdateCoordinator`（拟新增集成类）

```cpp
UpdatePlan PlanForOwner(const OwnerContext&, const InstalledRelease&, const ReleaseCatalog&);
OperationResult UpdateOwnerInstance(const UpdatePlan&);
OperationResult RetryFailedOperation(const FailureReference&);
CompatibilityResult CheckBeforeModuleUse(ModuleId);
```

`PlanForOwner()` 只规划当前 owner，不遍历全机偷偷更新其他人。
`FailureReference` 为 opaque operation ID；必须重新验证实际版本和受保护状态，不能执行用户改写的命令。
操作被 Windows Installer 1618、服务 busy 或取消打断时保留明确记录。
用户点击 Retry 只重试失败的事务单元；模块 validation 错误需要先修复输入。

## 12. 卸载、保留数据与重新安装

### 12.1 每用户 remove

```text
owner Setup 选择 KeepData / PurgeData
→ 明确授权并绑定 owner + mode
→ SYSTEM 获取 maintenance lease / 设置 drain
→ 停服务，写入本次 pending-remove receipt
→ 原 owner 正常执行 MSI uninstall
→ 确认实际 MSI 结果
→ 授权 helper 完成该 owner 的服务/Code 清理
→ 按数据选择处理 store
→ 清理对应 helper receipts、缓存和 VA profile，报告结果
```

MSI uninstall 未知或失败时不能直接删除剩余资源假装成功；
按已确认结果恢复或记录待处理，不越过原 MSI 生命周期。

### 12.2 普通卸载默认 KeepData

**这与原型 `Lifecycle::erase_owner()` 的行为不同，是必改项。**
原型清理会删除 Data；生产不得把测试 teardown 当作卸载策略照搬。

KeepData 保留：

- 原 owner 的业务 record、store epoch、revision、初始化状态及未完成迁移清理回执。
- 能验证 retained store 归属、版本/格式的最小 SYSTEM inventory/tombstone。
- 为读取 retained data 所必要的授权/格式信息；不因卸载重置为首次 migration。

去除服务/代码后，retained data 可封存为 SYSTEM-only 或保持经验证的 owner VA ACL；
重新预配时由授权 helper 恢复正确 VA 访问，不让普通用户直接改数据。
不依赖 VA profile 的解密密钥，因此 profile 可单独清理；这并不允许删除初始化元数据。
retained-data 标记不包含允许 SYSTEM 执行任意路径的用户内容。

### 12.3 显式 PurgeData

UI 明确确认后，把 purge mode 与同一次授权 request 绑定。
删除该 owner 的产品业务数据及对应初始化内容，不删除真实用户 profile/账户。
全机卸载也不默认 purge 所有用户数据。删除失败记录实际残留，不能报告全量清理成功。
正常卸载/显式数据重置必须保持可区分；重装时不能从留下的 legacy 副本自动恢复已显式 purge 的数据。
在同次确认范围内能删除旧源就删除；若仍有源残留，保留不含业务内容的最小 purge/禁止自动导入状态，
并显示待清理项，而不是声称“彻底删除”后又重新导入。
若后续确需“恢复出厂后允许重新导入”，作为显式 Import，而不是隐式 migration。

### 12.4 全机 remove：best-effort

使用 SYSTEM 维护 inventory 枚举本产品实例，逐项验证 service identity、fixed path 和状态。
能正常停止、移除的服务就清理；离线不自动意味着 SCM 不可删除。
不能合法完成离线用户 context-2 MSI uninstall 的，记为 residual registration，不手工改 MSI 数据库。

```text
MachineRemovalReport
  mainProductRemoved
  perOwner[]:
    ownerSid / serviceStopped / serviceRemoved / codeRemoved
    msiRegistrationRemoved / dataRetained / profileRemoved
    pendingItems[] / nativeErrors[] / retryClass
  completeCleanup: false（只要还有残留）
```

有残留不阻塞方案或自动回滚所有已成功清理项（Q06），但应明确区分仍运行的实例与纯文件/注册残留。
后续修复、重试或重装可读取已验证的 cleanup intent；不能保证离线用户一定会再次登录。

### 12.5 VA profile、helper staging 与缓存

仅根据从 service name 计算的 VA SID、实际 ProfileList 和限定路径处理 profile；
支持系统 `ServiceProfiles` 和配置的 profile 根目录，不误删 LocalService/NetworkService。
profile busy 可返回待重启清理，不能无限重启循环或强制卸载 hive。

临时 Setup 目录需要检查可信 owner receipt、服务停止/删除状态、原 requester PID+birth、
子操作终态和成功结果，再删除本 owner 的已完成目录；peer 目录不属于失败。
owner 可写 cache/legacy source 清理由原用户处理，拒绝 reparse/hardlink 或替换对象的危险遍历。
Windows Installer 系统 cache 不由产品按文件名手动清理。

生产日志默认不记录工作区完整命令行、配置内容或秘密；仅安全的错误分类、
operation ID、release 和必要的 owner 内部标识。最终用户诊断导出应提示其可能包含路径/账户信息。

## 13. Retry、状态展示与用户反馈

### 13.1 业务状态模型（拟新增）

```text
NotProvisioned
  → Provisioning → ReadyForMigration
  → AuthorizationRequired / ProvisionFailed

ReadyForMigration
  → Migrating → Ready
  → MigrationFailed

Ready
  → Updating → Ready
  → UpdateFailed / RecoveryRequired

迁移提交成功但源删除失败：
  Ready + SourceCleanupPending（不是 MigrationFailed）

卸载服务成功但 profile 尚占用：
  Removed + CleanupPending（不是 Installed，也不是 FullyClean）
```

`WorkspacesFailureViewModel` 至少输出：

```csharp
sealed record OperationFailure(
    Guid OperationId,
    FailureStage Stage,
    string MessageResourceKey,
    RetryKind Retry,
    int? NativeCode);
```

`RetryKind` 区分 `RetryNow`、`RetryAfterInputFix`、`RetryAfterAuthorization`、
`RetryAfterRestart`、`InspectUnknownOutcome`、`NotRetryable`。
本地 UI 可以保留用户未保存草稿，但不能把它写回旧权威文件作为 fallback。

### 13.2 重试单元

| 失败阶段 | 用户动作 | 实际执行 |
|---|---|---|
| 初始预配拒绝/取消 | Retry setup | 新的显式授权；核对是否已有部分实例，不盲目重复创建 |
| legacy validation | 修复输入后 Retry migration | 重新验证整份源，重新判断逻辑初始化状态 |
| Blob write 断连 | Retry save | 先 query 同操作；确认未提交才按规则再写，冲突时返回 module |
| carrier MSI 1618 / transient busy | Retry update | 等待当前 installer 结束，再运行该 owner 的合法维护事务 |
| VA prepared/commit 结果未知 | Inspect / Retry update | 对同 ID 查询并协调，不生成新事务覆盖 pending |
| Bootstrap 不可运行 | Repair | 明确管理员修复边界；不通过不可信现有 Code 自举 SYSTEM |
| 源文件删除失败 | Retry cleanup | 仅处理匹配源 receipt，不重做成功 migration |
| 卸载后 profile 未清理 | Restart / Retry cleanup | 核对 native 错误与资源状态，仅清理匹配 VA |

不以一个模块失败关闭整个 PowerToys；若共享依赖确实不可用，应诚实标明所有实际受影响模块。
后台自动重试必须有预算/退避，不能无限循环；本期必须提供用户可见的 Retry，不把自动化当作唯一入口。

## 14. 实现阶段与验收

### 14.1 分阶段交付

| 阶段 | 产物 | 退出条件 |
|---|---|---|
| P1：生产基础设施抽取 | Common / Client / Bootstrap / Runtime / Setup / carrier MSI；正式命名与签名 | 原型行为在正式项目中保持；无 Demo key、故障 CLI 或宽泛 owner-only writer |
| P2：通用存储 | 认证/目标授权、Blob 条件写、原子初始化、journal、data drain | 双客户端/故障注入证明无部分提交、无重复初始化、无跨目标/owner 写入 |
| P3：Workspaces 接入 | 统一 repository、业务 validation、整份迁移、temp/preview 改造、导入导出 | 所有执行与保存入口都不再绕回 legacy 文件；首次成功删源、失败可 retry |
| P4：更新/卸载集成 | 主产品两 scope 的 owner 更新协调、keep/purge、best-effort 全机清理 | OTS owner 不变；A/B 升级独立；正常卸载不丢用户保留数据 |
| P5：生产资格 | 故障恢复、平台/策略、性能、用户体验、安全评审 | 关键门禁通过；Q02 经 PM 确认；任何未覆盖限制写入发布说明而不是隐藏 |

生产工程默认沿用该分支 native C++ 工具链、现有 C# 框架和测试体系；
不要把原型 `/MT`、路径、timeout、测试机 VS 版本直接提升为新的全仓库要求。
新增 `.vcxproj` / `.csproj` / `.wixproj` 需要加入对应 solution、产品打包和 CI 矩阵。

构建顺序：

```text
Common / Client / Bootstrap / Runtime / MsiAction（仅版本与固定信任策略）
→ 正式签名 PE + 按最终 PE hash 生成 payload manifest/catalog
→ 签名 manifest/catalog + 添加并验证 RFC3161 时间戳
→ Lifecycle（嵌入精确 release 资源，正式签名）
→ carrier MSI（正式签名）
→ ProvisionBroker（嵌入相同 MSI，正式签名）
→ Setup（嵌入 broker 与同一 MSI，正式签名）
→ 主 PowerToys 两种安装包携带 Setup / release 描述
```

签名嵌套资源后再编译其宿主，避免最后一轮签名改变 MSI 内外对比的 PE 字节。
release key 不下发到客户端，测试开发 key/测试根不能成为正式信任来源。
生产策略标识为 `microsoft-production-v1`，不是证书 hash；受保护 policy 使用 format 2。
每份签名独立满足相同发布信任策略，允许不同有效 Microsoft 叶子证书，不比较它们彼此是否相同。
CI 使用与服务/安装辅助程序相同的 native TrustVerifier，避免“只放宽构建，运行时仍拒绝”。
已验证 TSA 时间用于评估代码签名证书有效期；缺失/错误时间戳拒绝，不能因已安装证书自然过期而
直接使正确时间戳保护的发布失效。吊销检查区分明确吊销与离线未知：前者拒绝，后者按 consumer
离线策略明确诊断；未知不能掩盖其他签名、发布者、用途或根信任错误。

### 14.2 必需测试

| 测试组 | 必须覆盖 |
|---|---|
| 身份与授权 | A/B；普通/高权限/OTS；错误 role/target；同 SID 非法 image；假 pipe server；PID 重用 |
| 存储 | 空合法数据、首次初始化、重复调用、CAS 冲突、超限、异常 Unicode/framing、丢失 record、读写并发 |
| 迁移 | 完整有效源、任意坏条目整体失败、module 拒绝敏感行为、源在校验后变化、commit ACK 丢失、删除失败、重装不重导 |
| Temp/preview | 捕获、Editor 编辑后预览、Launcher/arranger 使用同一快照、跨 owner 访问拒绝、进程异常时有界回收 |
| 更新 | v1→v2、同版 repair、坏 candidate 回滚、签名/hash/catalog 不匹配、旧 client 兼容、busy 1618、取消、维护期间保存 |
| 发布信任 | 不同有效 Microsoft 叶子证书通过；自建/用户根、假 Microsoft 名称、错误 EKU/根、丢失/移植时间戳拒绝；证书到期与离线吊销语义 |
| 恢复 | 在 MSI action、PE 发布、record/journal 更新、确认响应等边界终止进程/重启；不把 unknown 当成功 |
| 卸载 | A/B 独立；KeepData 重装；PurgeData；全机 best-effort；离线注册残留；profile busy；helper 残留归属 |
| 平台与 UX | Windows 支持矩阵、x64/ARM64、Windows Installer/WDAC/AppLocker 策略、无管理员场景、Q02 文案、Retry 与取消 |
| 完整性回归 | 普通用户不能直接修改 Code/Data/control；其他 owner VA 不可写；SY 维护不执行 VA 可写 PE |

不通过在 Session 0 中伪造用户桌面来替代真实 A/B 会话验收。
实际桌面和同机多账户均需验证，截图/人工确认与机器日志证据分别标注。

### 14.3 原型 PASS 的准确边界

- 原型已证明 MSI-carried 双 PE 更新、真实 owner context-2 注册、各 owner 独立 VA、提交前回滚及正确卸载归属。
- 本轮 A/B 用户按时序完成安装、独立升级、B repair、bad-v3 回滚、B 卸载后 A 仍正常；B 有日志，A 最后一项依据用户确认。
- 普通数据 API、生产 caller catalog、完整 Workspaces migration、main installer 集成和生产故障恢复不在该 PASS 中。
- 新项目/类表是实现方案，不把原型的全局函数表述成已经存在的同名生产类。
- 全机卸载留有可见残留可以符合 Q06；静默吞掉残留不符合 Q06。

## 15. 原型函数到生产类的映射

以下路径均相对：
`D:\PowerToys-Workspaces-MsiCarrier\src\modules\Workspaces\WorkspacesMsiCarrierPrototype`。
行号为本次阅读版本的定位提示，后续变更以符号名为准。

| 原型源位置 / 实际符号 | 可抽取到生产类 | 不可直接当作已经完成的部分 |
|---|---|---|
| `Runtime\Common.h`：`Paths`、`Policy`、`Bundle`、`MsiCall`、`Transaction` | StoragePaths、ReleaseVerifier、MaintenanceClient、UpdateTransactionManager | 数据 protocol、caller roles、生产 catalog 尚无 |
| `Runtime\Common.cpp:476`：`Paths::Paths`；`:530`：`ValidateBundle` | StoragePaths、ReleaseVerifier | 正式名称/manifest 需版本化；原型开发 pin 必须替换为正式发布者信任策略 |
| `Runtime\Common.cpp:645`：`PipeCaller`；`:661`：`Call` | CallerAuthenticator、ServerIdentityVerifier | 原型 owner 比较不是完整 production caller identity 验证 |
| `Runtime\Common.cpp:953`：`DecideOperation`；`:1016`：`RunUpdate` | UpdateTransactionManager | 不能声称解决 post-commit MSI/VA 分歧和任意断电恢复 |
| `Runtime\Bootstrap.cpp:130`：`Worker`；`:181`：`VerifyReady` | RuntimeSupervisor | 真实 BlobStore 恢复/数据 readiness 与 drain 待新增 |
| `Runtime\Bootstrap.cpp:301`：`Prepare`；`:392`：`Serve`；`:481`：`ServiceMain` | BootstrapService、UpdateTransactionManager | 原型单 control pipe；生产 data endpoint 从 Runtime 提供 |
| `Runtime\Bootstrap.cpp:534`：`Updater` | UpdateCoordinator | 保持同 VA 和跨 host 生命周期；不能另建 SYSTEM updater |
| `Runtime\Runtime.cpp:6`：`wmain` | RuntimeService | 当前主要是 version/ready/stop worker；并非已实现业务存储服务 |
| `Installer\SetupSupport.h`：`requester_owner`、`event_name`、`require_normal`、`verify_package` | OwnerIdentity、AuthorizationRendezvous、MsiInventory | 生产 exact-byte/OTS/context 与异常取消必须资格验证 |
| `Installer\Setup.cpp:74`：`configure`；`:261`：`authorized_msi` | OwnerSetupCoordinator、MsiExecutor | 尚未接入主 PowerToys 的 updater/installer |
| `Installer\MsiAction.cpp:120`：`begin`；`:139`：`prepare`；`:165`：`decision` | MsiTransactionAction | 控制状态必须与生产兼容/恢复方案一致 |
| `Installer\ProvisionBroker.cpp`：`extract_provisioner`、`provisioner`、`phase`、`run` | ProvisionBroker | 成功 helper staging 也要归属正确地清理，不应永远保留 |
| `Installer\Lifecycle.cpp:313`：`install`；`:358`：`lifecycle` | InstanceProvisioner、InstanceMaintenance | 生产保留数据/重装路径必须改造 |
| `Installer\Lifecycle.cpp:257`：`erase_owner` | CleanupCoordinator 的测试参照 | 它会删 Data，不能直接作为生产默认卸载 |
| `Installer\Product.wxs:2–37` | 新 carrier MSI authoring | 生产 GUID、正式签名、CI 和主安装分发都需接线 |

原型测试/脚本可迁入正式测试工程，但 `SelfTest` 的故障分支、v3-bad、开发证书、
诊断权限调整以及 `Clean-Demo` 的手工脚本不作为正式产品运行路径发布。
生产正式清理逻辑应归入经过测试的 Lifecycle/Setup 代码，输出结构化可追踪结果。

## 16. 现有 Workspaces 项目的具体接入点

本节基于 `D:\PowerToys\src\modules\Workspaces` 的当前代码阅读。
下面的“现有符号”确有对应源码；右列描述**拟改造**，不是现有实现。

### 16.1 项目引用调整

| 现有项目 | 引用 / 改造方向 |
|---|---|
| `WorkspacesCsharpLibrary\WorkspacesCsharpLibrary.csproj` | 引入 `ProtectedStorage.Client.Managed`；放置 managed repository、migration validator/coordinator、import/export 与 DTO 适配 |
| `WorkspacesEditor\WorkspacesEditor.csproj` | 通过 WorkspacesCsharpLibrary 使用 repository 和初始化状态；UI/ViewModel 不直接选择权威文件路径 |
| `Workspaces.ModuleServices\Workspaces.ModuleServices.csproj` | `GetWorkspacesAsync()` 接 repository；在明确用户动作上做 setup/retry，不让只读枚举偷偷弹 UAC |
| `WorkspacesLauncher\WorkspacesLauncher.vcxproj` | 引用 native Client；native module repository 负责读取、更新应用信息和 lastLaunchedTime |
| `WorkspacesSnapshotTool\WorkspacesSnapshotTool.vcxproj` | capture 结果写入受控 preview/snapshot 对象，替代 temp JSON |
| `WorkspacesWindowArranger\WorkspacesWindowArranger.vcxproj` | 使用 owner-bound 只读 launch snapshot/IPC，不获得通用保存/维护权限 |
| `WorkspacesModuleInterface\WorkspacesModuleInterface.vcxproj` | 在启动 Editor 的用户流程接入初始化/失败反馈；不作为所有 runtime 调用的 SYSTEM 代理 |
| `WorkspacesLauncherUI` managed 项目 | 增加服务就绪/失败/重试状态展示，不将 UI 进程当作提权授权根 |
| `WorkspacesLib` native shared code / 当前编译项 | 新增 native `WorkspacesRepository` 与业务转换/validation；保留 JSON codec，剥离默认直接文件读写入口 |

客户端 DLL/库不是进程身份：ModuleServices 可能由别的 PowerToys host 承载，
生产 caller catalog 必须列出实际获准的宿主 EXE 及角色，不能验证“引用了哪个 DLL”就给予写权限。
通过现有模块接口枚举 Workspace 的 host 默认仅具有所需的读取/请求启动能力。

### 16.2 源码与主要函数的变更表

路径均相对 `D:\PowerToys\src\modules\Workspaces`：

| 现有源位置 / 符号 | 当前行为 | 拟改造 |
|---|---|---|
| `WorkspacesCsharpLibrary\Data\WorkspacesStorage.cs:17–62`：`WorkspacesStorage.Load()`、`GetDefaultFilePath()` | 从 LocalAppData 读取 `workspaces.json` | 正常 Load 改为 service repository；默认文件路径仅供 `LegacyWorkspaceSource` 首次迁移读取 |
| `WorkspacesCsharpLibrary\Data\WorkspacesStorageJsonContext.cs` | managed schema 序列化上下文 | 继续作为 module codec，不放进 service |
| `WorkspacesCsharpLibrary\Data\TempProjectData.cs:10–18`：`File`、`DeleteTempFile()` | 单例 temp 文件路径及删除 | 改为 preview ID + 生命周期 API；不能仅换权威路径却遗留 temp 执行绕过 |
| `WorkspacesLib\WorkspacesData.cpp:14–23`：`WorkspacesFile()`、`TempWorkspacesFile()` | native 默认文件路径 | 从 live 调用路径移除；遗留函数只允许在 migration/兼容工具中使用 |
| `WorkspacesLib\JsonUtils.cpp:8–95`：`ReadSingleWorkspace()`、`ReadWorkspaces()`、`Write()` 重载 | 同时承担 codec 与文件 I/O | 提取 `Parse/Serialize` 纯业务 codec，live I/O 走 `WorkspacesRepository`；Import/Export 显式文件 I/O 仍允许 |
| `WorkspacesEditor\Utils\WorkspacesEditorIO.cs:23–183`：`ParseWorkspaces`、`ParseTempProject`、`SerializeWorkspaces`、`SerializeTempProject` | 权威/临时文件直接读写 | 迁到异步 repository/preview adapter；调用失败必须反馈，不能只 log 后继续成功流程 |
| `WorkspacesEditor\ViewModels\MainViewModel.cs:192,264,347–357` | 编辑、添加、删除后保存，读取 temp；保存后删 temp | 保存 await CAS 成功后更新 UI/释放 preview；失败保留编辑态并显示 Retry |
| `WorkspacesEditor\WorkspacesEditorPage.xaml.cs:52` | cancel 删除 temp 文件 | cancel 对应当前 preview；不能误清其他并发编辑实例 |
| `WorkspacesEditor\App.xaml.cs` | 启动时解析 Workspace | 接 `EnsureReadyAsync()`；区分未预配、未迁移、恢复中和正常数据 |
| `Workspaces.ModuleServices\IWorkspaceService.cs:13–21`、`WorkspaceService.cs:18–87` | `LaunchWorkspaceAsync()`、`LaunchEditorAsync()`、`GetWorkspacesAsync()`；`SnapshotAsync()` 尚是失败 stub | 保持接口兼容并暴露明确的 ready/error；不声称现有 Snapshot API 已支持服务迁移 |
| `WorkspacesLauncher\main.cpp:101–104,129–130,204` | LaunchAndEdit 读 temp；正常读权威集合；更新应用/path/id 后写回 | 输入只允许 workspace ID 或预览引用，repository Load/Update 做 validation/CAS；所有分支同样接线 |
| `WorkspacesLauncher\Launcher.cpp:62–71` | 析构中更新 lastLaunchedTime 并直接 `json::to_file` | 移到可观察的显式 `PersistLaunchMetadata()` / 完成回调；保存失败可 Retry，不能依赖析构完成重要 I/O |
| `WorkspacesLauncher\AppLauncher.cpp:30–45`：`LaunchApp()`；`Launch()`、`LaunchPackagedApp()` | 多种 EXE/URI/AUMID/package 路径，按 elevation 标志 open/runas | 保留业务 launch 分支；保证输入来自当前一致 snapshot；不在 generic service 实现 launch 规则 |
| `WorkspacesSnapshotTool\main.cpp:59–83`、`SnapshotUtils.cpp::GetApps()` | 捕获应用并写 temp snapshot | 整个 capture 结果经 module 校验后存为 preview；不把部分捕获/解析失败包装成成功迁移 |
| `WorkspacesWindowArranger\main.cpp:17–67`：`WinMain()` | 先尝试 temp JSON（49–52），再读权威 JSON（66–67） | 必须移除两条直接读路径，改为来自已绑定 launcher session 的只读布局 snapshot |
| `WorkspacesLauncher\WindowArrangerHelper.cpp:38–46,73`：`Launch(projectId,elevated,keepWaitingCallback)` | 仅把 projectId 传给 arranger，通过 AppLauncher 转发 elevation | 增加 launcher PID/birth/launch-session 绑定与 plan handoff，不通过 OTS 管理员 HKCU/profile 猜原 owner |
| `WorkspacesWindowArranger\WindowArranger.cpp:129–159,279,403–538` | 窗口定位与 launcher IPC | `TryMoveWindow/GetNearestWindow/processWindows/processWindow/moveWindow/receiveIpcMessage/sendUpdatedState` 保留布局职责；不获得任意存储写权限 |
| `WorkspacesModuleInterface\dllmain.cpp:322–339` 附近 | host 通过事件/`ShellExecuteExW` 启动 Editor | 显式用户入口连接 ready/retry UX；校验原 owner，上报初始化失败而不是 fallback |

路径行号随主分支变更可能移动；这些接入点需要在实现 PR 中按符号再次定位。
`WorkspacesEditor.csproj` 已排除部分旧本地 Data/Utils 编译项，实际变更应进入真正被编译的共享实现，
不能只改一份未编译的同名旧文件。

### 16.3 Launcher 写入与异步完成

新增 native module 类（**拟新增**，不进入通用 Runtime）：

```cpp
class WorkspacesRepository {
public:
    Result<WorkspaceSnapshot> Load();
    Result<SaveResult> Save(
        const WorkspacesDocument&, const Revision&, Guid operationId);
    Result<SaveResult> UpdateApplicationMetadata(
        WorkspaceId, const ApplicationMetadataChanges&, Guid operationId);
    Result<SaveResult> RecordLaunchCompleted(
        WorkspaceId, LaunchTimestamp, Guid operationId);
};
```

最后两项在 module 内完成业务级 merge + validation + CAS，
传到 service 仍然是通用 `PutBlob`，不会变成服务端 `SetLastLaunchedTime` 业务接口。
只更新仍存在且 identity 匹配的 workspace/app；用户同时删除或重新编辑时必须明确冲突处理。
Launcher 生命周期结束时，要么保存已确认成功，要么发布可恢复失败记录，不在析构中吞错误。

### 16.4 临时工作流不能遗漏

```text
SnapshotTool.GetApps()
→ module 完整校验 capture
→ CreateTransientBlob(workspaces.preview)
→ preview ID 经受控启动/IPC 交给 Editor
→ Editor 编辑 → 更新预览 / 显式 CAS 保存 repository
→ Launcher LaunchAndEdit 使用该预览 snapshot
→ arranger 使用同一 owner/launch session 的只读布局快照
→ 完成或取消释放 preview
```

workspace ID、preview ID 和 operation ID 是三种不同标识，不能互换。
旧 `temp-workspaces.json` 不能继续作为可提权启动流程的隐含回退入口。
传参/IPC 只传引用或已验证当前 session 的数据，不让高权限消费者凭调用者任意路径重新读取配置。

**Arranger 的 owner 交接是必须接线的现有缺口，不只是未来优化：**

- 当前 `WindowArrangerHelper::Launch()` 只传 projectId，arranger 自己重新读 temp/权威文件。
- 在 OTS 提权使用另一管理员凭据时，arranger 当前 token/profile 可能不再是原 owner；
  所以不能直接以其当前身份连接另一个用户的 data pipe，再关闭 owner 校验来“修好”。
- 拟新增 Workspaces 内部 `WorkspaceLaunchSession::Create()` / `SendPlacementPlan()` /
  `ReceivePlacementPlan()`。正常 launcher 从 repository/preview 读取一致数据，
  并将**本次窗口匹配与排列所需的只读 snapshot**交给本次启动的 arranger。
- IPC 必须绑定 launcher/arranger 的真实进程 handle、PID+birth、预期 image、owner 和 session nonce；
  命令行 projectId/nonce 本身不是授权。绑定失败即拒绝，不退回文件读取。
- 这是已有 launcher→arranger 协作的身份与数据来源修正，不是新增 Q01 排除的业务审批组件。
  Arranger 不因收到 plan 获得 repository 写权限，也不成为任意高权限程序启动代理。

### 16.5 复用现有测试工程

- `WorkspacesLib.UnitTests\WorkspacesDataTests.cpp:10–34`：旧路径测试需区分 legacy-source 与 live repository，不继续断言 live 必须使用原文件。
- `WorkspacesLib.UnitTests\JsonUtilsTests.cpp:33–179`：保留 codec 错误/成功测试，新增 repository fake/协议错误，分离 Import/Export 文件测试。
- `WorkspacesEditorUITest\WorkspacesEditingPageTests.cs`：capture、编辑、管理员启动偏好、LaunchAndEdit 及保存 Retry。
- `WorkspacesEditorUITest\WorkspacesLauncherTest.cs`：启动、取消、dismiss 与无服务/维护状态。
- `WorkspacesEditorUITest\WorkspacesSettingsTests.cs`：设置启动 Editor、快捷入口和禁用模块行为。
- 新增 shared vectors 让 native/managed validation、codec、revision/operation 处理一致；并不假设 C# 与 C++ 现有 JSON 行为天然完全相同。

## 17. 现有主安装器、Updater 与发布接入点

路径相对 `D:\PowerToys`。现有 PackageIdentity MSIX/其他模块的安装代码继续存在，
不意味着本方案采用 MSIX 承载 runtime；本设计不顺手改造无关模块。

### 17.1 精确源码映射

| 现有源位置 / 符号 | 当前职责 | 拟改造 |
|---|---|---|
| `installer\PowerToysSetupVNext\Common.wxi:37–54` | 根据 PerUser 切换主 MSI scope、权限、名称与 UpgradeCode | 保留主产品两 scope；额外携带同一个 per-owner carrier，不让 carrier scope 随主 MSI 改变 |
| `installer\PowerToysSetupVNext\Product.wxs:19–26` | 主 `<Package Scope="$(var.InstallScope)">` | carrier 保持独立产品/注册；不复用主 MSI 的 UpgradeCode 或文件 ownership |
| `installer\PowerToysSetupVNext\PowerToys.wxs:1–64` | Burn bundle、安装路径、已有产品搜索与 package chain | 在两类主安装中携带已签名 Setup/release 文件；owner 更新协调必须在主事务结束后 |
| `src\runner\UpdateUtils.cpp:114–121`：`LaunchPowerToysUpdate`；`src\runner\main.cpp:425` 调用点 | 启动 PowerToys.Update | 绑定原 owner/update operation；不因后续 UAC 或退出 runner 丢失业务 owner |
| `src\Update\PowerToys.Update.cpp:185–237`：`InstallNewVersionStage1` | 复制 updater 到 temp、关闭主程序、准备 stage2 参数 | 在关闭前保留所需身份/进程证据；不能只保留裸 PID 或可改的 owner 字符串 |
| `src\Update\PowerToys.Update.cpp:238–334`：`InstallNewVersionStage2` | 校验并执行主安装器，等待结束，更新 UpdateState | 主事务成功后编排该 owner 的 carrier；明确“主产品已更新 / module 实例失败”两种状态 |
| `src\Update\PowerToys.Update.cpp:334–396`：`WinMain` 分支 | update action dispatch、成功后重启 PowerToys | post-install runner 与 updater 不能同时开始同 owner 更新；使用 per-owner operation gate |
| `src\common\updating\updateLifecycle.h:19–74`：`BuildStage2Arguments`、`BuildPowerToysExePath`、`CanRelaunchAfterUpdate`、`IsSafeDownloadedInstallerFilename` | 更新参数和路径安全/重启工具 | 保留现有约束；新参数是验证过的 release/operation 关联，不能放任意 helper 命令 |
| `src\common\updating\updating.cpp:146–191` | `get_pending_updates_path`、下载/清理更新缓存 | 与 carrier 缓存区分；不能删掉仍被 MSI 使用的 source 或未完成恢复的 rollback 材料 |
| `src\common\updating\installer.cpp:165–345` | `exe_version_info_is_powertoys`、`msi_upgrade_code_is_powertoys`、`is_expected_powertoys_installer`、`verify_installer_trust` | 复用可信文件验证基础；carrier 必须具有自己的明确产品身份，不把它宽泛当成任意 Microsoft MSI |
| `src\common\utils\elevation.h:156–417` | elevated 检查、drop privileges、run elevated/non-elevated 等 helper | 可复用启动基础，但必须验证原 owner/会话；不能用“任意当前桌面用户”代替原发起人 |
| `src\runner\main.cpp:573–620` | runner elevated setting 与重启调度 | 已以高权限运行 PowerToys 时，业务/维护 owner 仍明确；不依赖继承 high token 执行 per-user carrier |
| `installer\PowerToysSetupVNext\Product.wxs:111–225` | 主 MSI install/remove/custom action 排序 | 不在正在执行的 MSI action 中再次启动 carrier MSI；区分 upgrade 旧包移除与最终 uninstall |
| `installer\PowerToysSetupCustomActionsVNext\CustomAction.cpp:211–246`：`LaunchPowerToysCA` | 安装后的主程序启动 | 已有 launch 不等于主 MSI 已退出；runner gate 必须等待主事务结束/忙状态解除后才维护 carrier |
| 同文件 `:991–995`：`UninstallServicesCA` | 现有 service teardown 入口 | 新 cleanup coordinator 的独立接入点；现有函数不是已经完成 per-owner best-effort inventory 清理 |
| 同文件 `:599–726`：`InstallPackageIdentityMSIXCA`、`UninstallPackageIdentityMSIXCA` | 当前主程序 package identity 生命周期 | 不作为 VA runtime 的安装/身份来源；保持现有模块行为 |

### 17.2 生产集成类的位置与接口

| 拟新增/改造类 | 建议放置 | 关键函数 |
|---|---|---|
| `ProtectedStorageUpdateCoordinator` | `src\common\updating\`，供 Update/runner 调用 | `PlanForOwner()`、`UpdateOwnerInstance()`、`CheckBeforeModuleUse()`、`RetryFailedOperation()` |
| `OriginalOwnerContext` | 通用维护代码，启动前捕获 | `Capture()`、`ValidateLiveRequester()`、`StartOwnerWorker()`；持有真正 token/process/session，不能只序列化用户名 |
| `ModuleMaintenanceState` | protected storage client 编排层 + module UI DTO | `LoadPending()`、`RecordFailure()`、`MarkResolved()`；仅描述状态，不携带可执行命令授权 |
| `MachineRemovalCoordinator` | Lifecycle/主卸载集成边界 | `EnumerateOwnedInstances()`、`TryRemoveEach()`、`PublishResidualReport()` |

原 owner 上下文需要贯穿“runner 已退出但安装/更新未结束”的阶段，
不能要求 `OriginalOwnerContext` 重新打开已经退出且 PID 可重用的原 runner。
优先让非提权 owner coordinator 在外层安装期间存活、持有 kernel handles；
涉及 OTS 的特权 child 仅回传限定结果。
无法继续维持正确上下文时，报告该 owner 的待处理更新，留待其正常进程恢复，不能换成管理员身份继续。

### 17.3 事务与卸载接线限制

主 MSI 的某些 launch/remove custom action 位于 execute sequence 中。
不能直接把 `MsiInstallProductW(carrier)` 或另一个 `msiexec` 插到里面，导致 nested MSI / 1618 /
调用父事务等待子事务、子事务又等待父事务锁的死锁。

推荐外层 Setup/Burn/Update coordinator 串行编排：

```text
主安装完成并释放 MSI transaction
→ owner coordinator 确认 release 可用
→ carrier MSI transaction
→ module 状态发布 / Retry
```

对直接运行主 `.msi` 而无外层 updater 的入口，正常 runner 启动检查是补偿入口，
但也必须处理主事务尚未结束的 busy 状态。
机器级卸载只能在已合法授权的范围清 service/code；
离线用户 MSI 注册维护不能在 SYSTEM 下伪装成该 owner，也不能嵌套用户 MSI 来阻塞主卸载。
确实无法做的项目按 Q06 形成残留报告与重试路径。

### 17.4 构建与 CI 接线

| 现有项目 | 集成动作 |
|---|---|
| `src\Update\PowerToys.Update.vcxproj` | 引用更新协调器、native client/公共维护代码，保持现有日志/依赖方式 |
| `installer\PowerToysSetupVNext\PowerToysInstallerVNext.wixproj:117,159–160` | 主 MSI 打包正式 Setup/release；保留现有 CustomAction 项目引用 |
| `installer\PowerToysSetupVNext\PowerToysBootstrapperVNext.wixproj:36` | 主 Burn 的两 scope 发布均携带正确架构/版本的 carrier 入口 |
| `installer\PowerToysSetupCustomActionsVNext\PowerToysSetupCustomActionsVNext.vcxproj:138` | 如果接入机器卸载 hook，引用固定 native 生命周期接口，不运行脚本拼接任意命令 |
| `installer\PowerToysSetup.slnx` 及主产品构建 solution | 加入正式工程与依赖顺序，避免只在独立原型 Build.ps1 中能编译 |

具体流水线 YAML/job 名称随主分支结构接线，本设计不虚构已存在的 job。
验收必须包含两种主安装 scope、两种目标架构、普通 token/OTS 和 user-driven update；
不能只验证独立 carrier MSI 的 build 成功。

## 18. 尚需工程验证的设计风险（不是重新增加产品 open questions）

| 风险 | 本设计的处理与验收点 |
|---|---|
| 生产 caller 身份 | SID-only 原型必须升级为受保护 policy + signed catalog + 真实进程绑定；不声称能识别被攻陷的合法 writer |
| Launcher/arranger/temp 遗漏 | 正式 JSON、临时 JSON、析构保存、外部 ModuleServices 读取与 OTS arranger 都纳入映射，不只改 Editor 保存 |
| 原始 owner 跨退出/提权丢失 | 非提权 coordinator 生命周期、PID/birth/handles 与 session 绑定；失败则显式延期 |
| MSI / VA 两事务提交分歧 | journal/reconciliation 与 fault injection；仅提交前回滚 PASS 不足以上线 |
| 更新后旧客户端仍运行 | signed compatibility catalog + endpoint negotiation；新 release 与旧实例短暂共存必须可控 |
| KeepData 与 Demo 删除行为不同 | 新 Lifecycle 明确区分 keep/purge、封存/重装，保留初始化状态；不能复制 erase_owner |
| 全机清理不足 | 尽量清服务，失败逐 owner 报告，不手改 MSI；符合 Q06 best-effort 而不是伪造全量成功 |
| 数据恢复和排空未实现 | worker 只有 ready/stop 不足以支撑业务 store；BlobStore/StoreRecovery/drain 必须另行实现测试 |

本设计可以作为实现任务拆分和 API 评审基线。
只有 Q02 仍需 PM 决策；其余工程问题按上述约束实现并验证，
不能因为产品决策已关闭就将未实现/未验证的机制标为完成。
