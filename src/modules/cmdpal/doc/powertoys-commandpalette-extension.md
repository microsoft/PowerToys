Build a packaged Command Palette extension that exposes com.microsoft.commandpalette, while re-using the existing PowerToys sparse MSIX identity for discovery, activation, COM hosting, and dispatching RPCs to PowerToys Runner which fans out to module interfaces and functions.

0) Goals / Non-Goals

Goals

Let Command Palette (host) discover a PowerToys extension via the Windows AppExtension contract while code runs out-of-package (sparse).

Provide an RPC hop from the host → Runner (named pipe/COM) → Module Interface (DLL boundary) → Module function.

Define protocol, capabilities model, error handling, versioning, packaging & identity, lifetime, and telemetry.

Non-Goals

Not replacing existing module UIs/workflows.

Not a general inter-process broker for arbitrary apps.

Not prescribing a specific serialization library; JSON is assumed, but binary is allowed behind the protocol façade.

1) Terms

Host: Command Palette process (WinUI3 app) loading extensions.

Extension: sparse packaged project that declares uap3:AppExtension Name="com.microsoft.commandpalette".

Sparse Identity: Microsoft.PowerToys.SparseApp MSIX providing identity & cataloging while binaries live outside the package.

Runner: PowerToys core process that exposes a named-pipe RPC service (and optional COM server) for module dispatch.

Module Interface: A stable C ABI / COM ABI surface implemented by each module’s DLL to expose callable methods.

Command: A unit of invocation (e.g., workspace.list, awake.enable, fancyzones.applyLayout).

2) High-Level Architecture

1. Command Palette gathers user intent → issues an RPC call to Runner:

```json
{ "module":"workspace", "method":"list_workspaces", "params":{} }
```

2. Runner RPC Server (named pipe) validates schema, and forwards to the Module Interface Dispatcher (DLL call).

3. Module Interface resolves module+method to a function pointer and marshals parameters.

4. Module executes function and returns a structured result or error.

5. Response flows back to Host; Host renders result as list/content/toast in Command Palette.

3) Prerequisites

PowerToys sparse package installed:

Build/Install from src/PackageIdentity/readme.md to ensure Microsoft.PowerToys.SparseApp is registered.

Command Palette SDK restored:

src/modules/cmdpal/extensionsdk projects (Microsoft.CommandPalette.Extensions, .Toolkit).

Developer cert of the sparse package trusted locally.

4) Packaging & Identity
4.1 Add your executable to the sparse package

* Edit src/PackageIdentity/AppxManifest.xml:
```xml
<Applications>
  <Application Id="CmdPalExt.YourExtension"
               Executable="External\Win32\<yourExe>.exe"
               EntryPoint="Windows.FullTrustApplication">
    <uap:VisualElements DisplayName="Your CmdPal Extension" Square44x44Logo="Assets\Square44x44Logo.png" Description="..." />
  </Application>
  <!-- Existing PowerToys entries remain -->
</Applications>
```

* Embed sparse identity in your Win32 binary (RT_MANIFEST):
```xml
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <msix xmlns="urn:schemas-microsoft-com:msix.v1"
        packageName="Microsoft.PowerToys.SparseApp"
        applicationId="CmdPalExt.YourExtension"
        publisher="CN=PowerToys Dev Cert, O=Microsoft Corporation, L=..., C=US"/>
</assembly>
```

4.2 Register the App Extension & COM server
In your packaged project’s Package.appxmanifest (follow SamplePagesExtension pattern):
```xml
<Extensions>
  <!-- COM host out-of-proc for the extension -->
  <com:Extension Category="windows.comServer">
    <com:ComServer>
      <com:ExeServer Executable="External\Win32\<yourExe>.exe"
                      Arguments="-RegisterProcessAsComServer"
                      DisplayName="CmdPal Extension COM Host">
        <com:Class Id="{YOUR-CLS-GUID}" DisplayName="YourExtension" />
      </com:ExeServer>
    </com:ComServer>
  </com:Extension>

  <!-- Command Palette AppExtension -->
  <uap3:Extension Category="windows.appExtension">
    <uap3:AppExtension Name="com.microsoft.commandpalette"
                       Id="YourExtension"
                       PublicFolder="Public">
      <uap3:Properties>
        <CmdPalProvider xmlns="http://schemas.microsoft.com/commandpalette/2024/extension">
          <Metadata DisplayName="Your Extension" Description="..." />
          <Activation>
            <CreateInstance ClassId="{YOUR-CLS-GUID}" />
          </Activation>
        </CmdPalProvider>
      </uap3:Properties>
    </uap3:AppExtension>
  </uap3:Extension>
</Extensions>
```

4.3 Project configuration
```xml
<PropertyGroup>
  <GenerateAppxPackageOnBuild>true</GenerateAppxPackageOnBuild>
  <AppxBundle>Never</AppxBundle>
  <ApplicationManifest>..\..\..\src\PackageIdentity\AppxManifest.xml</ApplicationManifest>
  <OutDir>$(SolutionDir)$(Platform)\$(Configuration)\WinUI3Apps\CmdPalExtensions\$(MSBuildProjectName)\</OutDir>
</PropertyGroup>
```

5) Extension Entry Point (COM) & Lifetime
5.1 COM entry
```csharp
[ComVisible(true)]
[Guid("YOUR-CLS-GUID")]
public sealed class SampleExtension : IExtension
{
    private readonly ManualResetEvent _lifetime = new(false);

    public void Initialize(IHostContext ctx)
    {
        // optional host features / events subscription
    }

    public IEnumerable<ICommandProvider> GetProviders() =>
        new ICommandProvider[] { new PowerToysCommandProvider() };

    public void Dispose()
    {
        _lifetime.Set(); // signal process shutdown
    }

    // Program.cs
    public static int Main(string[] args)
    {
        if (args.Contains("-RegisterProcessAsComServer"))
        {
            using var server = ExtensionServer.RegisterExtension<SampleExtension>();
            WaitHandle.WaitAny(new[] { server.LifetimeHandle /* or your own */ });
            return 0;
        }
        return 0;
    }
}
```
6) Providers, Pages, and Items
Derive from Microsoft.CommandPalette.Extensions.Toolkit.CommandProvider.

Provide a consistent capabilities set:
```csharp
public sealed class PowerToysCommandProvider : CommandProvider
{
    public PowerToysCommandProvider()
    {
        Id = "PowerToys";
        DisplayName = "PowerToys";
        Icon = new Uri("ms-appx:///Public/Icons/powertoys.png");
    }

    public override IEnumerable<CommandItem> TopLevelCommands()
    {
        yield return CommandItem.Run("Workspaces", "List or launch a workspace")
             .WithId("workspace.list")
             .WithInvoke(async ctx => await RpcInvoke(ctx, "workspace", "list_workspaces", new {}));
        // …more commands
    }
}
```
Use toolkit pages (ListPage, MarkdownContent, FormContent, etc.) to render results.

7) RPC Contract (Host ↔ Runner)
7.1 Transport

Named pipe (default): \\.\pipe\PowerToys.CmdPal.Rpc

Framing: length-prefixed (uint32 LE) followed by UTF-8 JSON payload.

Alt: COM interface IPowerToysRpc for in-box activation; keep both for flexibility.

7.2 Message schema (JSON)
Request
```json
{
  "version": "1.0",
  "id": "guid-or-ulid",
  "module": "workspace",
  "method": "list_workspaces",
  "params": { "filter": "" },
  "context": {
    "caller": "CmdPalExtension",
    "session": "host-session-id",
    "uiCulture": "en-US"
  },
  "timeoutMs": 8000
}
```
success
```json
{
  "id": "guid-or-ulid",
  "ok": true,
  "result": {
    "items": [
      { "id": "daily", "title": "Daily Dev", "monitors": 2, "tags": ["dev"] }
    ]
  },
  "elapsedMs": 23
}
```
error
```json
{
  "id": "guid-or-ulid",
  "ok": false,
  "error": {
    "code": "Module.NotFound",
    "message": "Module 'workspace' is not available",
    "details": { "installed": false, "suggest": "install-module workspace" },
    "retryable": false
  }
}
```

7.3 Error codes (minimum set)
```
  Bad.Request (schema/validation)
  Module.NotFound
  Method.NotFound
  Module.Failure ()
  Busy (queue full)
  NotEnabled(disabled module/functionality possibly due to gpo)
```

7.4 Versioning

version in request selects contract version; Runner must accept N & N-1 at minimum.
New optional fields must be ignored by older servers.
Breaking changes require a new version and capability negotiation during handshake method:

```json
{ "module":"core","method":"handshake","params":{"want":["rpc/1.0","rpc/1.1"]} }
```

8) Runner RPC Server
8.1 Responsibilities

Accept connection, authenticate (see Security), parse & validate schema.

Maintain a dispatcher registry: {module → IModuleDispatcher}.

Apply per-module budgets (CPU time, wall time, memory).

Normalize results and write framed responses.

8.2 Dispatcher interface (C# example)
```csharp
public interface IModuleDispatcher
{
    string Name { get; } // "workspace"
    RpcResult Invoke(string method, JsonElement @params, RpcContext ctx);
    IEnumerable<ModuleMethodDescriptor> Describe(); // discovery
}
```

8.3 Discover/Introspection
Method core.listModules returns:
```json
{
  "modules": [
    { "name":"workspace", "version":"1.2.0", "methods":[
      { "name":"list_workspaces", "params":{"filter":"string?"}, "returns":"WorkspaceList" }
    ]}
  ]
}
```

9) Module Interface (DLL boundary)
9.1 ABI
Flat C exports
```c++
// returns newly allocated UTF-8 JSON strings; host frees via provided freeFn
typedef const char* (__stdcall *invoke_fn)(const char* method, const char* jsonParams, void* ctx);
typedef const char* (__stdcall *describe_fn)(void);

__declspec(dllexport) int __stdcall PT_GetModule(IPTModule** out);
```

PowerToys ships a helper implementation in `PowertoyModuleIface`. Each module inherits default `describe` / `invoke` implementations so that, at a minimum, a `navigateToSettings` verb is exposed. Modules can override those members to add richer metadata or behaviors, but no module can opt out of providing at least the settings deep link.

9.2 Method routing

* Module receives method and params JSON; validate and route to a function:
* e.g., workspace.list_workspaces → ListWorkspaces().
* Return JSON only across the boundary; internal domain types are module private.

9.3 Timeouts & cancellation

Runner injects a deadline in ctx. Modules must honor it (e.g., CancellationToken).

10) Security
* Named pipe DACL: grant READ/WRITE to S-1-15-2-<PFN SID> and LocalSystem (for service scenarios).
* No elevation over RPC. If a module requires elevation, respond with Unauthorized and let host prompt the user to re-try elevated via existing PowerToys flow.
* Validate method white-list per module; deny unknown calls with Method.NotFound.

11) Performance & Reliability (Ignore for now)
* Budgets:
RPC parse+dispatch ≤ 2 ms typical.
Module method wall-time ≤ 200 ms default (configurable by method).
Response size ≤ 256 KB default (soft limit; paginate long lists).
* Queuing: bounded per-client queue; overflow → Busy.
* Crash isolation: module exceptions → Module.Failure with truncated stack info; Runner continues.
* Back-pressure: Host should not issue more than N in-flight calls (default 4).
* Telemetry IDs must not contain user content (hash identifiers).

12) Telemetry & Logging

* Event names:
CmdPal.Rpc.Request, CmdPal.Rpc.Success, CmdPal.Rpc.Error, CmdPal.Rpc.Timeout

* Fields:
module, method, elapsedMs, resultSize, error.code, version

* PII: none. Truncate strings and never log params.

13) Reference: Minimal Runner RPC (sketch)
 Minimal ABI between Runner and Modules
```cpp
// English-only comments as requested.

struct RpcContext
{
    std::string caller;        // optional: PFN, cert thumbprint, etc.
    std::string uiCulture;     // optional
    uint32_t    deadlineMs;    // per-call budget
};

struct RpcResult
{
    bool ok = false;
    std::string json;          // if ok=true => result JSON; else => error JSON
};

struct IModuleDispatcher
{
    virtual ~IModuleDispatcher() = default;
    virtual const char* Name() const = 0; // e.g., "workspace"

    // paramsJson is raw JSON text. Must return JSON text.
    // Implementations must be exception-safe and never throw across the ABI.
    virtual RpcResult Invoke(std::string_view method,
                             std::string_view paramsJson,
                             const RpcContext& ctx) noexcept = 0;

    // Optional: discovery for core.listModules
    virtual std::string Describe() const = 0; // JSON
};
```
> Existing DLL interfaces can wrap/adapt to this small surface without changing each module’s internal types.

* Request/Response model
```cpp
// Request (UTF-8 JSON)
struct RpcRequest {
    std::string id;        // guid/ulid
    std::string version;   // "1.0"
    std::string module;    // "workspace"
    std::string method;    // "list_workspaces"
    std::string params;    // raw JSON object as string
    uint32_t    timeoutMs; // 0 => default (e.g., 8000)
    // optional context omitted for brevity
};

struct RpcResponse {
    std::string id;
    bool ok = false;
    std::string payload;   // if ok => {"result": {...}}, else => {"error": {...}}
};
```

* Named pipe server:
```cpp
static bool ReadExact(HANDLE h, void* buf, DWORD cb, DWORD* outRead, DWORD timeoutMs);
static bool WriteExact(HANDLE h, const void* buf, DWORD cb, DWORD timeoutMs);

static bool ReadFrame(HANDLE h, std::string& out, DWORD timeoutMs)
{
    uint32_t len = 0;
    DWORD got = 0;
    if (!ReadExact(h, &len, sizeof(len), &got, timeoutMs) || got != sizeof(len)) return false;
    if (len > (64u * 1024u * 1024u)) return false; // hard cap

    out.resize(len);
    return ReadExact(h, out.data(), len, &got, timeoutMs) && got == len;
}

static bool WriteFrame(HANDLE h, std::string_view payload, DWORD timeoutMs)
{
    const uint32_t len = static_cast<uint32_t>(payload.size());
    DWORD wrote = 0;
    if (!WriteExact(h, &len, sizeof(len), timeoutMs)) return false;
    return WriteExact(h, payload.data(), len, timeoutMs);
}
```

* Json parsing
Use nlohmann/json. Example:
```cpp
#include <nlohmann/json.hpp>
using json = nlohmann::json;

static bool ParseRequest(std::string_view j, RpcRequest& out, std::string& err)
{
    try {
        auto d = json::parse(j);
        out.id       = d.value("id", "");
        out.version  = d.value("version", "1.0");
        out.module   = d.at("module").get<std::string>();
        out.method   = d.at("method").get<std::string>();
        out.timeoutMs= d.value("timeoutMs", 8000);
        if (out.timeoutMs == 0 || out.timeoutMs > 60000) out.timeoutMs = 8000;

        // Keep original params text to avoid re-serialization differences.
        if (d.contains("params")) {
            out.params = d["params"].dump();
        } else {
            out.params = "{}";
        }
        return true;
    } catch (const std::exception& e) {
        err = e.what();
        return false;
    }
}
```

* Module registry and dispatch
```cpp
class RpcServer
{
public:
    // Inject your concrete module dispatchers here.
    void Register(std::unique_ptr<IModuleDispatcher> m)
    {
        registry_[m->Name()] = std::move(m);
    }

    // Main accept loop on a background thread.
    void Run(std::atomic_bool& stopToken)
    {
        for (;;)
        {
            if (stopToken.load()) break;

            SECURITY_ATTRIBUTES sa{ sizeof(sa) };
            // Optional: build a DACL that allows only the PowerToys PFN SID.
            HANDLE pipe = CreateNamedPipeW(
                LR"(\\.\pipe\PowerToys.CmdPal.Rpc)",
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                1 << 16, 1 << 16,
                0, &sa);

            if (pipe == INVALID_HANDLE_VALUE) { /* log and retry */ Sleep(200); continue; }

            BOOL ok = ConnectNamedPipe(pipe, nullptr) ? TRUE :
                      (GetLastError() == ERROR_PIPE_CONNECTED);

            if (!ok) { CloseHandle(pipe); continue; }

            // Serve client on a detached thread; the accept loop immediately repeats.
            std::thread(&RpcServer::ServeClient, this, pipe).detach();
        }
    }

private:
    void ServeClient(HANDLE pipe)
    {
        // RAII close
        auto close = wil::scope_exit([&]() { FlushFileBuffers(pipe); DisconnectNamedPipe(pipe); CloseHandle(pipe); });

        std::string frame;
        for (;;)
        {
            if (!ReadFrame(pipe, frame, /*timeout*/ 120000)) break;

            RpcRequest req;
            std::string perr;
            if (!ParseRequest(frame, req, perr))
            {
                WriteFrame(pipe, ErrorEnvelope("", "Bad.Request", perr), 5000);
                continue;
            }

            RpcResponse rsp = Dispatch(req);
            WriteFrame(pipe, SerializeResponse(rsp), 15000);
        }
    }

    RpcResponse Dispatch(const RpcRequest& r)
    {
        RpcResponse out;
        out.id = r.id;

        auto it = registry_.find(r.module);
        if (it == registry_.end())
        {
            out.ok = false;
            out.payload = ErrorEnvelope(r.id, "Module.NotFound", "Unknown module");
            return out;
        }

        RpcContext ctx;
        ctx.deadlineMs = r.timeoutMs;

        // Enforce per-call timeout on the module invoke.
        std::packaged_task<RpcResult()> task([&]() {
            return it->second->Invoke(r.method, r.params, ctx);
        });
        auto fut = task.get_future();
        std::thread(std::move(task)).detach();

        if (fut.wait_for(std::chrono::milliseconds(r.timeoutMs)) == std::future_status::timeout)
        {
            out.ok = false;
            out.payload = ErrorEnvelope(r.id, "Timeout", nullptr, /*retryable*/ true);
            return out;
        }

        RpcResult mr = fut.get();
        if (mr.ok)
        {
            out.ok = true;
            out.payload = ResultEnvelope(r.id, mr.json);
        }
        else
        {
            out.ok = false;
            out.payload = mr.json; // already an error envelope
        }
        return out;
    }

    // Helpers to format envelopes without re-parsing module JSON.
    static std::string ResultEnvelope(const std::string& id, std::string_view resultJson)
    {
        // {"id": "...", "ok": true, "result": {...}}
        json j; j["id"]=id; j["ok"]=true; j["result"] = json::parse(resultJson);
        return j.dump();
    }

    static std::string ErrorEnvelope(const std::string& id,
                                     std::string_view code,
                                     std::optional<std::string_view> message,
                                     bool retryable = false)
    {
        json e;
        e["id"] = id;
        e["ok"] = false;
        e["error"] = { {"code", code}, {"retryable", retryable} };
        if (message && !message->empty()) e["error"]["message"] = *message;
        return e.dump();
    }

private:
    std::unordered_map<std::string, std::unique_ptr<IModuleDispatcher>> registry_;
};
```
> Each client gets a dedicated thread; the per-request timeout is enforced with std::future::wait_for. This is simple and robust. If you prefer fewer threads, switch to an overlapped I/O pool.

ErrorEnvelope / ResultEnvelope keep responses uniform.

If your module returns an error JSON, return it with mr.ok=false so Runner doesn’t wrap twice.

* Example module dispatcher adapter
```cpp
class WorkspaceDispatcher final : public IModuleDispatcher
{
public:
    const char* Name() const override { return "workspace"; }

    RpcResult Invoke(std::string_view method,
                     std::string_view paramsJson,
                     const RpcContext& ctx) noexcept override
    {
        try
        {
            if (method == "list_workspaces")  return List(paramsJson, ctx);
            if (method == "launch")           return Launch(paramsJson, ctx);
            if (method == "close")            return Close(paramsJson, ctx);
            if (method == "saveCurrent")      return Save(paramsJson, ctx);
            if (method == "killNonWorkspaceWindows") return Kill(paramsJson, ctx);

            return Fail("Method.NotFound", "Unknown method");
        }
        catch (const std::exception& e)
        {
            return Fail("Module.Failure", e.what());
        }
    }

    std::string Describe() const override
    {
        // Return the MCP-style method+schema JSON you settled on.
        return R"json({
          "name":"workspace","version":"1.2.0","capabilities":{ "methods":[ /* ... */ ] }
        })json";
    }

private:
    static RpcResult Ok(json&& result)
    {
        RpcResult r; r.ok = true; r.json = result.dump(); return r;
    }
    static RpcResult Fail(std::string_view code, std::string_view msg)
    {
        RpcResult r; r.ok = false;
        json e; e["id"] = nullptr; e["ok"] = false;
        e["error"] = { {"code", code}, {"message", msg} };
        r.json = e.dump(); return r;
    }

    RpcResult List(std::string_view paramsJson, const RpcContext&)
    {
        auto d = json::parse(paramsJson.empty() ? "{}" : paramsJson);
        std::string filter = d.value("filter", "");
        int limit = std::clamp(d.value("limit", 50), 1, 200);

        // Call into your real module DLL/function here.
        // For example: ModuleWorkspace_List(filter, limit, outVector);
        json res;
        res["items"] = json::array({
          { {"id","daily"}, {"title","Daily Dev"}, {"monitors",2}, {"tags", json::array({"dev"})} }
        });
        return Ok(std::move(res));
    }

    RpcResult Launch(std::string_view paramsJson, const RpcContext& ctx)
    {
        auto d = json::parse(paramsJson);
        std::string id = d.at("id").get<std::string>();
        std::string policy = d.value("monitorPolicy","matchTopology");

        // Invoke module.
        bool launched = true; int restored = 7; // from module
        json res = { {"launched", launched}, {"windowsRestored", restored} };
        return Ok(std::move(res));
    }

    // Close/Save/Kill... similar
};
```
> Replace the stub bodies with calls to your existing module DLL functions. This adapter is the only place you translate JSON into the module’s native types.

* Timeouts and cancellation
The deadlineMs in RpcContext is the budget. Your module functions can optionally accept a HANDLE or std::stop_token to poll for cancellation.
If a module ignores the budget, the server returns Timeout; the worker thread will still complete in the background. If this is unacceptable, run modules on a job object with CPU time limits or use cooperative cancellation hooks inside modules.

* Wire it up:
```cpp
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    std::atomic_bool stop{ false };
    RpcServer server;

    // Register all module dispatchers.
    server.Register(std::make_unique<WorkspaceDispatcher>());
    // server.Register(std::make_unique<AwakeDispatcher>()); etc.

    std::thread t([&] { server.Run(stop); });

    // Integrate with your Runner lifetime (service loop, message pump, etc.)
    // On shutdown:
    stop.store(true);
    // Optionally create a dummy client to unblock CreateNamedPipe/ConnectNamedPipe.
    t.join();
    return 0;
}
```
Runner: 
1. Accepts RPCs over the pipe,
2. Validates/parses,
3. Dispatches into module DLLs,
4. Enforces budgets,
5. Formats responses.

14) Example method(workspace)
```json
{ "version":"1.0","id":"01H...","module":"workspace","method":"launch","params":{"id":"daily"} }
```

15) Capability & Permissions Model
Each module exposes a capabilities blob via Describe():
```json
{
  "name": "workspace",
  "version": "1.2.0",
  "capabilities": {
    "methods": [
      {
        "name": "list_workspaces",
        "description": "List saved workspaces with optional filtering.",
        "sideEffect": "none",                    // none | read | write | dangerous
        "input_schema": {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "filter": { "type": "string" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 200, "default": 50 },
            "cursor": { "type": "string", "description": "Opaque paging token" }
          }
        },
        "result_schema": {
          "type": "object",
          "required": ["items"],
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["id", "title"],
                "properties": {
                  "id":   { "type": "string" },
                  "title":{ "type": "string" },
                  "monitors": { "type": "integer", "minimum": 1 },
                  "tags": { "type": "array", "items": { "type": "string" } }
                }
              }
            },
            "nextCursor": { "type": "string" }
          }
        },
        "errors": ["Timeout","Module.Failure"]
      },
      {
        "name": "launch",
        "description": "Restore windows for a saved workspace.",
        "sideEffect": "write",
        "requiresConfirmation": false,
        "authzScopes": ["workspace:write"],      // optional scopes
        "input_schema": {
          "type": "object",
          "additionalProperties": false,
          "required": ["id"],
          "properties": {
            "id": { "type": "string" },
            "restoreBehavior": {
              "type": "string",
              "enum": ["strictPosition","bestEffort","foregroundOnly"],
              "default": "bestEffort"
            },
            "monitorPolicy": {
              "type": "string",
              "enum": ["matchSerial","matchTopology","any"],
              "default": "matchTopology"
            }
          }
        },
        "result_schema": {
          "type": "object",
          "required": ["launched"],
          "properties": {
            "launched": { "type": "boolean" },
            "windowsRestored": { "type": "integer", "minimum": 0 },
            "warnings": { "type": "array", "items": { "type": "string" } }
          }
        },
        "errors": ["Workspace.NotFound","Unauthorized","Timeout","Module.Failure"]
      },
      {
        "name": "close",
        "description": "Close windows that belong to a workspace.",
        "sideEffect": "write",
        "requiresConfirmation": true,            // host should show a confirm UI
        "input_schema": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "id": { "type": "string", "description": "If omitted, act on the active workspace." },
            "force": { "type": "boolean", "default": false }
          }
        },
        "result_schema": {
          "type": "object",
          "properties": {
            "closed": { "type": "integer", "minimum": 0 }
          }
        },
        "errors": ["Workspace.NotFound","Busy","Timeout","Module.Failure"]
      },
      {
        "name": "saveCurrent",
        "description": "Snapshot current desktop windows as a new or updated workspace.",
        "sideEffect": "write",
        "input_schema": {
          "type": "object",
          "additionalProperties": false,
          "required": ["name"],
          "properties": {
            "name": { "type": "string", "minLength": 1 },
            "id": { "type": "string", "description": "If present, update existing." },
            "tags": { "type": "array", "items": { "type": "string" } },
            "includeMinimized": { "type": "boolean", "default": true }
          }
        },
        "result_schema": {
          "type": "object",
          "required": ["id"],
          "properties": {
            "id": { "type": "string" },
            "updated": { "type": "boolean" }
          }
        },
        "errors": ["Validation","Busy","Timeout","Module.Failure"]
      },
      {
        "name": "killNonWorkspaceWindows",
        "description": "Close windows not part of the active workspace.",
        "sideEffect": "dangerous",
        "requiresConfirmation": true,
        "input_schema": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "dryRun": { "type": "boolean", "default": false }
          }
        },
        "result_schema": {
          "type": "object",
          "properties": {
            "affected": {
              "type": "array",
              "items": { "type": "object", "properties": { "pid": { "type":"integer" }, "title": { "type":"string" } } }
            }
          }
        },
        "errors": ["Unauthorized","Busy","Timeout","Module.Failure"]
      }
    ]
  }
}
```
