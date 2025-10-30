选 Runner 作为唯一的 Server/Broker，ptcli 只是瘦客户端。


模块通过各自的 ModuleInterface 向 Runner 注册“可被调用的命令/参数模式”。


ptcli → Runner 用 统一的 IPC（建议 NamedPipe + JSON-RPC/自定义轻量 JSON 协议）。


Runner 再把请求转发到对应模块（可以是直接调用模块公开的接口，或转译为该模块现有的触发机制，如 Event/NamedPipe）。


对“历史遗留的 event handle/pipe 触发点”，短期由 Runner 做兼容层；长期逐步统一为“命令接口”。


这样你能得到：能力发现、参数校验、权限/提权、错误码一致、可观察性一致、向后兼容。



组件与职责


ptcli（瘦客户端）




解析命令行：ptcli -m awake set --duration 1h / ptcli -m workspace list


将其映射为通用消息（JSON）发给 Runner。


处理同步/异步返回、展示统一错误码与人类可读信息。


最多内置“列出模块/命令的帮助”这类“离线功能”，但真正的能力发现来自 Runner。




Runner（统一 Server/Broker）




启动时建立 NamedPipe 服务端：\\.\pipe\PowerToys.Runner.CLI（示例）。


维护 Command Registry：每个模块在加载/初始化时注册自己的命令（名称、参数 schema、是否需要提权、是否长任务、超时时间建议、描述文案等）。


收到请求后：


校验模块是否存在、命令是否存在、参数是否通过 schema 验证。


如需提权且当前 Runner 权限不足：按策略返回“需要提权”的标准错误，或通过你们现有的提权助手启动“Elevated Runner”做代办。


转发给目标模块（优先调用模块公开的“命令接口方法”；若模块尚未改造，由 Runner 适配为该模块现有触发（Event/NamedPipe））。


汇总返回值，统一封装标准响应（状态、数据、错误码、诊断信息）。






Module（实现者）




实现 IModuleCommandProvider（示例命名）：


IEnumerable<CommandDescriptor> DescribeCommands() 暴露命令元数据；


Task<CommandResult> ExecuteAsync(CommandInvocation ctx) 执行命令；


可标注“需要前台 UI”、“需要管理员”、“可能长时间运行（支持取消/进度）”等。




现有“事件/NamedPipe 触发路径”的模块：短期由 Runner 适配；长期建议模块直接实现上面的 ExecuteAsync，统一语义与可观测性。



协议与数据结构（建议）
请求（ptcli→Runner）
{
  "v": 1,
  "correlationId": "uuid",
  "command": {
    "module": "awake",
    "action": "set",               // 例如 set/start/stop/list 等
    "args": { "duration": "1h" }   // 按模块定义的 schema
  },
  "options": {
    "timeoutMs": 20000,
    "wantProgress": false
  }
}

响应（Runner→ptcli）
{
  "v": 1,
  "correlationId": "uuid",
  "status": "ok",                   // ok | error | accepted (异步)
  "result": { /* 模块返回的结构化数据 */ },
  "error": {                        // 仅当 status=error
    "code": "E_NEEDS_ELEVATION",    // 标准化错误码
    "message": "Awake requires elevation",
    "details": { "hint": "rerun with --elevated" }
  }
}

进度/异步（可选）


长任务时，status="accepted" 并返回 jobId；ptcli 可 ptcli job status <jobId> 轮询，或 Runner 通过同管道 增量推送 progress（JSON lines）。


取消：ptcli 发送 { action: "cancel", jobId: "..." }，Runner 调用模块 CancellationToken。



命令发现与帮助


ptcli -m list：列出模块（Runner 直接返回 registry）。


ptcli -m awake -h：DescribeCommands() 中的 Awake 条目返回所有 action、参数与示例。


参数 schema：用简化 JSON Schema（或手写约束）即可，让 ptcli 能本地提示，也让 Runner 能服务器端校验。



示例映射
Awake


ptcli -m awake set --duration 1h
→ Awake.Set(duration=1h)
→ Runner 调用 AwakeModule.ExecuteAsync("set", args)
→ 结果：{ "effectiveUntil": "2025-10-30T18:00:00+08:00" }


ptcli -m awake stop
→ Awake.Stop()（幂等）


Workspaces


ptcli -m workspace list
→ Workspaces.List() 返回 { "items": [{ "id": "...", "name": "...", "monitors": 2, "windows": 14 }] }


ptcli -m workspace apply --id 123 --strict
→ Workspaces.Apply(id=123, strict=true) 支持进度与失败报告（缺失进程、权限不足等）。



Runner vs 直接敲模块事件/NamedPipe
直接敲模块（如你举的 EventWaitHandle）优点


省一跳，模块自己掌控。


对少数“拍一下就够”的快捷触发点，写起来快。


缺点（关键）


入口分散：每个模块各有各的触发名、参数约定、错误语义。


能力发现困难：ptcli 无法统一列出“模块能干啥、参数是什么”。


权限与多实例问题：有的模块需要管理员/前台，有的在用户会话，有的在服务；直接对模块打洞容易踩坑。


审计/可观察性差：难以统一日志/遥测/超时/取消。


演进成本高：接口一旦铺散，很难回收。


走 Runner Proxy（推荐）


统一注册：模块只跟 Runner 说“我能做哪些命令、参数是什么”。


统一协议：ptcli 只会说一种“通用 JSON 命令”。


统一安全/提权/会话：Runner 最懂自己所在的权限/桌面会话，可决定是否需要跳 Elevation/切用户会话。


兼容旧触发：Runner 内部去“Set 事件/写管道”，外部对 ptcli 完全透明。


可测试/可监控：所有调用都经由同一 Broker，便于打点、限流、诊断。



结论：把直接事件/管道触发视为“模块侧 private API”，只由 Runner 调用。ptcli 与普通用户两边都只看得到 Runner 的“公共命令接口”。


Runner 怎么“轻量 Server”


进程：沿用现有 Runner，不另起新守护；新增一个 CommandRouter 子系统即可。


IPC：NamedPipeServerStream + StreamJsonRpc（或你们已有的 JSON 框架）；单管道多请求（长度前缀 + correlationId）。


并发：每请求一个 Task，模块执行受自身并发控制。


安全：给管道设定 DACL，仅允许同一交互式用户（或受信 SID）连接；参数白名单与长度限制防注入。


错误码：统一枚举（像 HTTP 状态一样）：


E_MODULE_NOT_FOUND / E_COMMAND_NOT_FOUND / E_ARGS_INVALID


E_NEEDS_ELEVATION / E_ACCESS_DENIED


E_BUSY_RETRY / E_TIMEOUT / E_INTERNAL





最小可行落地（增量实施顺序）


在 Runner 加一个 Pipe + CommandRouter，硬编码两个演示命令：


Awake.Set(duration)（直接调用 Awake 的现有 API）


Workspaces.List()（调用 Workspace 管理器）




写 ptcli：只做 JSON 打包、发管道、打印结果。


给两个模块各加 IModuleCommandProvider，从 Runner 注册。


把 1~2 个“历史事件触发点”接入 Router（Runner 内部去 Set Event），对外暴露为 Module.Action。


扩展：help/describe、Job/进度、取消、提权路径、返回码规范化。



简短示例（C#，仅示意；注释英文）
Runner – 接口定义
public record CommandDescriptor(
    string Module, string Action, string Description,
    IReadOnlyDictionary<string, ParamSpec> Params,
    bool RequiresElevation = false, bool LongRunning = false);

public interface IModuleCommandProvider
{
    IEnumerable<CommandDescriptor> DescribeCommands();
    Task<CommandResult> ExecuteAsync(CommandInvocation ctx, CancellationToken ct);
}

public record CommandInvocation(string Action, IReadOnlyDictionary<string, object?> Args);
public record CommandResult(bool Ok, object? Data = null, string? ErrorCode = null, string? ErrorMessage = null);

Runner – 注册与路由（伪码）
// On module load:
registry.Register(provider.DescribeCommands(), provider);

// On request:
var cmd = request.Command; // module, action, args
var provider = registry.Resolve(cmd.Module, cmd.Action);
ValidateArgs(cmd.Args, provider.Schema);
if (provider.RequiresElevation && !IsElevated())
    return Error("E_NEEDS_ELEVATION", "Elevation required.");
return await provider.ExecuteAsync(new CommandInvocation(cmd.Action, cmd.Args), ct);

ptcli – 调用（伪码）
// Build request from CLI args
var req = new { v = 1, correlationId = Guid.NewGuid(), command = new { module, action, args } };
using var client = new NamedPipeClientStream(".", "PowerToys.Runner.CLI", PipeDirection.InOut);
await client.ConnectAsync(timeout);
await WriteJsonAsync(client, req);
var resp = await ReadJsonAsync(client);
Render(resp);
return resp.status == "ok" ? 0 : 1;


兼容性与演进策略


不强行改造所有模块：Runner 先用“适配器”把旧事件/管道触发包起来，CLI 立即可用；后续模块逐步实现 IModuleCommandProvider。


版本化：请求/响应带 v，便于以后扩展字段。


可观测性：Runner 统一日志：调用者、命令、耗时、结果码；必要时加 ETW/Trace。


权限：Runner 决策提权路径；ptcli 接到 E_NEEDS_ELEVATION 时，可提示 --elevated 或弹系统 UAC（按你们策略）。


并行/队列：对长任务（如 workspace apply）可按模块内队列执行，避免竞态；CLI 侧提供 job status/cancel。



你关心的具体问题直答


“由 moduleinterface 做统一入口管理是个好主意吗？”
好，但入口管理应驻留在 Runner。模块通过 ModuleInterface 报告命令与执行入口，Runner 统一对外暴露。


“ptcli 直接找模块执行，还是让 Runner 派发？”
让 Runner 派发。这样获得统一发现、权限、错误、日志与演进空间。


“现在很多模块用 named pipe/event 触发，这算绕过 Runner 吗？”
目前你看到的例子确实是“模块直连触发”。建议以后仅 Runner 使用这些内部触发（做兼容/过渡），对外统一成命令。


“Runner 起一个 light-weight server 吗？”
对。就在 Runner 里加一个轻量 NamedPipe JSON-RPC 服务，不必单独再起一个服务进程。



要的话，我可以按上面的协议给你一版 ptcli（C#）与 Runner 侧 CommandRouter 的“最小可用代码骨架”，再加上 Awake/Workspaces 的两个真实命令适配示例。