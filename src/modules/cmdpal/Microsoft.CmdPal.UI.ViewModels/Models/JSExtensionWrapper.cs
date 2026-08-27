// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CmdPal.JsonRpc.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Manages a single JavaScript/TypeScript extension running as an isolated Node.js
/// process and presents it to the CmdPal host as an <see cref="IExtensionWrapper"/>.
/// The process is spawned with stdio redirection and driven over a
/// <see cref="JsonRpcConnection"/>; the <see cref="JSCommandProviderProxy"/> forwards
/// provider calls to the extension.
/// </summary>
public sealed partial class JSExtensionWrapper : IExtensionWrapper, IDisposable
{
    // Consecutive crashes above this threshold mark the extension unhealthy.
    private const int MaxConsecutiveCrashes = 3;

    // Default Node.js inspector port. Auto-assigned ports start at 9229 (the first
    // Interlocked.Increment below yields 9229 from this seed).
    private static int _nextDebugPort = 9228;

    private readonly JSExtensionManifest _manifest;
    private readonly string _manifestDirectory;
    private readonly Lock _lock = new();
    private readonly List<ProviderType> _providerTypes = [];

    private Process? _nodeProcess;
    private JsonRpcConnection? _connection;
    private JSCommandProviderProxy? _commandProviderProxy;
    private Task? _startInProgress;
    private bool _isDisposed;
    private bool _stopping;
    private int _consecutiveCrashCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSExtensionWrapper"/> class.
    /// </summary>
    /// <param name="manifest">The parsed and validated extension manifest.</param>
    /// <param name="manifestDirectory">The directory that contains the extension's package.json.</param>
    public JSExtensionWrapper(JSExtensionManifest manifest, string manifestDirectory)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _manifestDirectory = manifestDirectory ?? throw new ArgumentNullException(nameof(manifestDirectory));

        // JS extensions currently expose a single command provider.
        AddProviderType(ProviderType.Commands);
    }

    /// <summary>
    /// Raised when the underlying Node.js process exits unexpectedly (a crash), after the
    /// wrapper has torn down its process and connection handles. It is not raised for an
    /// intentional stop via <see cref="SignalDispose"/>. The service uses this to remove the
    /// now-dead provider and decide whether to restart or disable the extension.
    /// </summary>
    public event EventHandler? ProcessExited;

    public string PackageDisplayName => _manifest.EffectiveDisplayName;

    public string ExtensionDisplayName => _manifest.EffectiveDisplayName;

    public string PackageFullName => $"js!{_manifest.Name}";

    public string PackageFamilyName => $"js!{_manifest.Name}";

    public string Publisher => _manifest.Publisher ?? "Unknown";

    public string ExtensionClassId
    {
        get
        {
            // Derive a stable identifier from the manifest name.
            if (string.IsNullOrWhiteSpace(_manifest.Name))
            {
                return "unknown";
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(_manifest.Name));
            return $"js-{Convert.ToHexString(hash)[..32]}";
        }
    }

    public DateTimeOffset InstalledDate
    {
        get
        {
            try
            {
                var manifestPath = Path.Combine(_manifestDirectory, "package.json");
                if (File.Exists(manifestPath))
                {
                    return File.GetCreationTimeUtc(manifestPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Fall through to the default below.
            }

            return DateTimeOffset.UtcNow;
        }
    }

    public PackageVersion Version
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_manifest.Version))
            {
                return new PackageVersion { Major = 1, Minor = 0, Build = 0, Revision = 0 };
            }

            var parts = _manifest.Version.Split('.');
            return new PackageVersion
            {
                Major = parts.Length > 0 && ushort.TryParse(parts[0], out var major) ? major : (ushort)1,
                Minor = parts.Length > 1 && ushort.TryParse(parts[1], out var minor) ? minor : (ushort)0,
                Build = parts.Length > 2 && ushort.TryParse(parts[2], out var build) ? build : (ushort)0,
                Revision = 0,
            };
        }
    }

    public string ExtensionUniqueId => $"js!{_manifest.Name}";

    /// <summary>
    /// Gets the directory that contains the extension's package.json.
    /// </summary>
    internal string ManifestDirectory => _manifestDirectory;

    /// <summary>
    /// Gets the manifest this extension was loaded with. Used by the service to detect a
    /// manifest edit during an explicit refresh and reload the extension when it changed.
    /// </summary>
    internal JSExtensionManifest Manifest => _manifest;

    /// <summary>
    /// Gets the normalized identity key for this extension, used to enforce cross-extension
    /// uniqueness during discovery.
    /// </summary>
    internal string NameKey => _manifest.NameKey;

    /// <summary>
    /// Gets the number of times this extension has recorded a consecutive crash
    /// without a successful start in between.
    /// </summary>
    internal int ConsecutiveCrashCount
    {
        get
        {
            lock (_lock)
            {
                return _consecutiveCrashCount;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the extension is considered healthy. It
    /// becomes unhealthy after more than <see cref="MaxConsecutiveCrashes"/>
    /// consecutive crashes and stays that way until a successful start resets the counter.
    /// </summary>
    internal bool IsHealthy { get; private set; } = true;

    /// <summary>
    /// Gets the capabilities advertised by the extension in its initialize response.
    /// Currently advisory: recorded for diagnostics but not used to gate behavior.
    /// </summary>
    internal IReadOnlyList<string> Capabilities { get; private set; } = [];

    public bool IsRunning()
    {
        lock (_lock)
        {
            return IsRunningLocked();
        }
    }

    private bool IsRunningLocked()
    {
        if (_nodeProcess is null || _connection is null)
        {
            return false;
        }

        try
        {
            return !_nodeProcess.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public Task StartExtensionAsync()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (IsRunningLocked())
            {
                return Task.CompletedTask;
            }

            // Single-flight: a concurrent caller (for example GetProviderAsync calling in
            // right after the service starts the wrapper) joins the in-progress start
            // instead of spawning a second Node process. The start body runs on the thread
            // pool so no process is spawned while this lock is held; the task is cleared
            // when it completes so a later restart can start again.
            _startInProgress ??= Task.Run(RunStartAsync);
            return _startInProgress;
        }
    }

    private async Task RunStartAsync()
    {
        try
        {
            await StartCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (_lock)
            {
                _startInProgress = null;
            }
        }
    }

    private async Task StartCoreAsync()
    {
        lock (_lock)
        {
            // The wrapper may have been disposed, or another start may have completed,
            // between scheduling this start and running it.
            if (_isDisposed || IsRunningLocked())
            {
                return;
            }
        }

        Logger.LogDebug($"Starting JS extension {_manifest.EffectiveDisplayName}");

        var entryPoint = _manifest.EntryPointPath ?? Path.Combine(_manifestDirectory, _manifest.Main ?? string.Empty);
        if (!File.Exists(entryPoint))
        {
            Logger.LogError($"Entry point not found for {_manifest.Name}: {entryPoint}");
            return;
        }

        // Launch through the SDK bootstrap when it is installed so the bootstrap
        // claims stdout for the protocol before the extension entry is dynamically imported.
        // The effective launch command is:
        //   node [--inspect=<port>] "<bootstrap>" "<entry>"
        // and, when the bootstrap cannot be resolved:
        //   node [--inspect=<port>] "<entry>"
        var bootstrapScript = ResolveBootstrapScript(_manifestDirectory);
        if (bootstrapScript is null)
        {
            Logger.LogWarning(
                $"Bootstrap loader not found for {_manifest.Name}; launching the entry directly. A stray top-level stdout write can corrupt the protocol until the SDK bootstrap is installed.");
        }

        // Resolve an absolute node.exe from PATH rather than launching the bare name
        // "node". The process working directory is the extension's own folder, so a bare
        // name could otherwise resolve a node.exe planted there; an absolute path avoids
        // that and lets us report a specific "Node.js not found" error.
        var nodeExecutable = NodeRuntimeLocator.ResolveNodeExecutable();
        if (nodeExecutable is null)
        {
            Logger.LogError(
                $"Node.js runtime (node.exe) was not found on PATH; cannot start JS extension {_manifest.Name}. Install Node.js and ensure it is on PATH.");
            return;
        }

        Process? nodeProcess = null;
        JsonRpcConnection? connection = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = nodeExecutable,
                Arguments = BuildNodeArguments(entryPoint, bootstrapScript),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _manifestDirectory,
            };

            nodeProcess = Process.Start(psi);
            if (nodeProcess is null)
            {
                Logger.LogError($"Failed to start Node.js process for {_manifest.Name}");
                return;
            }

            connection = new JsonRpcConnection(
                nodeProcess.StandardOutput.BaseStream,
                nodeProcess.StandardInput.BaseStream,
                nodeProcess.StandardError.BaseStream);

            connection.Error += OnConnectionError;
            connection.Disconnected += OnConnectionDisconnected;

            var disposedDuringStart = false;
            lock (_lock)
            {
                // If a dispose landed while the process was starting, do not resurrect the
                // wrapper by assigning the fresh handles. Reap the just-started process
                // below instead so it cannot leak past disposal.
                if (_isDisposed || _stopping)
                {
                    disposedDuringStart = true;
                }
                else
                {
                    _nodeProcess = nodeProcess;
                    _connection = connection;

                    // Create the provider proxy before the initialize handshake so its host
                    // notification handlers are registered in time to receive notifications
                    // (logs, statuses, clipboard requests, items-changed) that the extension
                    // emits while it activates during initialize.
                    _commandProviderProxy = new JSCommandProviderProxy(
                        connection,
                        _manifest.Name ?? "unknown",
                        _manifest.EffectiveDisplayName,
                        _manifest.Icon);
                }
            }

            if (disposedDuringStart)
            {
                Logger.LogDebug($"JS extension {_manifest.Name} was disposed while starting; reaping the new process.");
                ReapOrphanedStart(nodeProcess, connection);
                return;
            }

            connection.StartListening();

            var initResponse = await connection.SendRequestAsync(
                "initialize",
                new JsonObject { ["extensionId"] = _manifest.Name },
                CancellationToken.None).ConfigureAwait(false);

            if (initResponse.Error is not null)
            {
                Logger.LogError($"Initialization failed for {_manifest.Name}: {initResponse.Error.Message}");
                SignalDispose();
                return;
            }

            RecordAdvertisedCapabilities(initResponse.Result);

            // Thread the real provider metadata from the handshake into the proxy so the
            // author-specified frozen value flows through instead of the wire default.
            var providerMetadata = ExtractProviderMetadata(initResponse.Result);
            if (providerMetadata is { } metadata)
            {
                JSCommandProviderProxy? proxy;
                lock (_lock)
                {
                    proxy = _commandProviderProxy;
                }

                proxy?.SetProviderMetadata(metadata);
            }

            // A successful start clears the consecutive-crash history.
            ResetCrashCount();

            Logger.LogInfo($"Successfully started JS extension {_manifest.EffectiveDisplayName}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start JS extension {_manifest.Name}: {ex.Message}");

            try
            {
                if (nodeProcess is not null && !nodeProcess.HasExited)
                {
                    nodeProcess.Kill(entireProcessTree: true);
                }
            }
            catch (Exception killEx) when (killEx is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Best effort.
            }

            SignalDispose();
        }
    }

    private void ReapOrphanedStart(Process nodeProcess, JsonRpcConnection connection)
    {
        connection.Error -= OnConnectionError;
        connection.Disconnected -= OnConnectionDisconnected;

        try
        {
            connection.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Error disposing orphaned connection for {_manifest.Name}: {ex.Message}");
        }

        try
        {
            if (!nodeProcess.HasExited)
            {
                nodeProcess.Kill(entireProcessTree: true);
                nodeProcess.WaitForExit(2000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Best effort.
        }

        nodeProcess.Dispose();
    }

    public void SignalDispose()
    {
        Process? process;
        JsonRpcConnection? connection;
        JSCommandProviderProxy? proxy;

        lock (_lock)
        {
            _isDisposed = true;
            _stopping = true;
            process = _nodeProcess;
            connection = _connection;
            proxy = _commandProviderProxy;
            _nodeProcess = null;
            _connection = null;
            _commandProviderProxy = null;
        }

        TearDown(process, connection, proxy);
    }

    public void Dispose() => SignalDispose();

    public IExtension? GetExtensionObject()
    {
        // JS extensions have no WinRT COM object; the wrapper itself is the bridge.
        return null;
    }

    public void AddProviderType(ProviderType providerType)
    {
        lock (_lock)
        {
            if (!_providerTypes.Contains(providerType))
            {
                _providerTypes.Add(providerType);
            }
        }
    }

    public bool HasProviderType(ProviderType providerType)
    {
        lock (_lock)
        {
            return _providerTypes.Contains(providerType);
        }
    }

    public async Task<T?> GetProviderAsync<T>()
        where T : class
    {
        if (typeof(T) != typeof(ICommandProvider))
        {
            return null;
        }

        await StartExtensionAsync().ConfigureAwait(false);

        lock (_lock)
        {
            if (_connection is null || !IsRunningLocked())
            {
                return null;
            }

            _commandProviderProxy ??= new JSCommandProviderProxy(
                _connection,
                _manifest.Name ?? "unknown",
                _manifest.EffectiveDisplayName,
                _manifest.Icon);
            return _commandProviderProxy as T;
        }
    }

    public async Task<IEnumerable<T>> GetListOfProvidersAsync<T>()
        where T : class
    {
        var provider = await GetProviderAsync<T>().ConfigureAwait(false);
        return provider is not null ? [provider] : [];
    }

    /// <summary>
    /// Records a consecutive crash and updates <see cref="IsHealthy"/>. Extracted so the
    /// crash-counter state machine can be exercised without spawning a Node.js process.
    /// </summary>
    /// <returns>The new consecutive crash count.</returns>
    internal int RecordUnexpectedExit()
    {
        lock (_lock)
        {
            _consecutiveCrashCount++;
            if (_consecutiveCrashCount > MaxConsecutiveCrashes)
            {
                IsHealthy = false;
            }

            return _consecutiveCrashCount;
        }
    }

    /// <summary>
    /// Resets the consecutive crash counter and marks the extension healthy again.
    /// </summary>
    internal void ResetCrashCount()
    {
        lock (_lock)
        {
            _consecutiveCrashCount = 0;
            IsHealthy = true;
        }
    }

    private void OnConnectionError(object? sender, JsonRpcErrorEventArgs e)
    {
        Logger.LogError($"JSON-RPC error in {_manifest.Name}: {e.Exception.Message}");
    }

    private void OnConnectionDisconnected(object? sender, EventArgs e)
    {
        Process? process;
        JsonRpcConnection? connection;
        JSCommandProviderProxy? proxy;

        lock (_lock)
        {
            // Ignore disconnections that we triggered while stopping or disposing.
            if (_stopping || _isDisposed)
            {
                return;
            }

            _consecutiveCrashCount++;
            Logger.LogWarning($"Node.js process for {_manifest.Name} disconnected unexpectedly (crash #{_consecutiveCrashCount})");

            if (_consecutiveCrashCount > MaxConsecutiveCrashes)
            {
                IsHealthy = false;
                Logger.LogError($"JS extension {_manifest.Name} marked unhealthy after {_consecutiveCrashCount} consecutive crashes");
            }

            process = _nodeProcess;
            connection = _connection;
            proxy = _commandProviderProxy;
            _nodeProcess = null;
            _connection = null;
            _commandProviderProxy = null;
        }

        // This runs on the connection's read-loop thread, and JsonRpcConnection.Dispose()
        // joins that thread. Tear the handles down and notify the service on a background
        // thread to avoid a self-join and to keep the read loop from blocking on itself.
        _ = Task.Run(() =>
        {
            TearDown(process, connection, proxy);
            ProcessExited?.Invoke(this, EventArgs.Empty);
        });
    }

    private void TearDown(Process? process, JsonRpcConnection? connection, JSCommandProviderProxy? proxy)
    {
        // Dispose the provider proxy first so it detaches its notification handlers and
        // hides any active statuses while the host is still valid, including when the
        // extension process exits unexpectedly. The proxy is idempotent, so this does not
        // double-dispose if the service also disposes it during provider teardown.
        if (proxy is not null)
        {
            try
            {
                proxy.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Error disposing provider proxy for {_manifest.Name}: {ex.Message}");
            }
        }

        if (connection is not null)
        {
            connection.Error -= OnConnectionError;
            connection.Disconnected -= OnConnectionDisconnected;

            try
            {
                var stillRunning = process is not null && !process.HasExited;
                if (stillRunning)
                {
                    // Ask the extension to clean up, giving it a short grace period.
                    connection.SendNotificationAsync("dispose", null, CancellationToken.None)
                        .Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex) when (ex is AggregateException or InvalidOperationException or JsonRpcException)
            {
                Logger.LogWarning($"Error sending dispose notification to {_manifest.Name}: {ex.Message}");
            }

            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Error disposing JSON-RPC connection for {_manifest.Name}: {ex.Message}");
            }
        }

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Logger.LogWarning($"Error terminating Node.js process for {_manifest.Name}: {ex.Message}");
            }

            process.Dispose();
        }
    }

    private static JsonElement? ExtractProviderMetadata(JsonElement? result)
    {
        if (result is not { } initResult ||
            initResult.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if ((initResult.TryGetProperty("provider", out var provider) ||
             initResult.TryGetProperty("Provider", out provider)) &&
            provider.ValueKind == JsonValueKind.Object)
        {
            // Clone so the metadata survives the disposal of the response document.
            return provider.Clone();
        }

        return null;
    }

    private void RecordAdvertisedCapabilities(JsonElement? result)
    {
        if (result is not { } initResult ||
            initResult.ValueKind != JsonValueKind.Object ||
            !initResult.TryGetProperty("capabilities", out var capsElement) ||
            capsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var capabilities = new List<string>();
        foreach (var cap in capsElement.EnumerateArray())
        {
            if (cap.ValueKind == JsonValueKind.String)
            {
                var value = cap.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    capabilities.Add(value);
                }
            }
        }

        Capabilities = capabilities;
        if (capabilities.Count > 0)
        {
            Logger.LogInfo($"Extension {_manifest.Name} advertised capabilities: {string.Join(", ", capabilities)}");
        }
    }

    private string BuildNodeArguments(string entryPoint, string? bootstrapScript)
    {
        // node [--inspect=<port>] "<bootstrap>" "<entry>" when the bootstrap resolves,
        // otherwise node [--inspect=<port>] "<entry>". The bootstrap reads the entry from
        // process.argv[2]; Node runtime flags such as --inspect never enter process.argv,
        // so the entry stays at argv[2] regardless of debug mode.
        var target = bootstrapScript is null
            ? $"\"{entryPoint}\""
            : $"\"{bootstrapScript}\" \"{entryPoint}\"";

        if (_manifest.Debug)
        {
            var port = _manifest.DebugPort ?? Interlocked.Increment(ref _nextDebugPort);
            Logger.LogInfo($"Debug mode enabled for {_manifest.Name} on inspector port {port}");
            return $"--inspect={port} {target}";
        }

        return target;
    }

    /// <summary>
    /// Resolves the SDK bootstrap loader for an installed extension. The bootstrap
    /// claims and guards stdout before it dynamically imports the extension entry, so a
    /// static top-level stdout write cannot corrupt the JSON-RPC framing. Resolution is
    /// relative to the extension's installed SDK
    /// (<c>&lt;manifestDirectory&gt;/node_modules/@microsoft/cmdpal-sdk</c>), preferring the
    /// package's declared <c>bin</c> entry and falling back to the known published
    /// artifacts. Returns <see langword="null"/> when the SDK or its bootstrap is not present.
    /// </summary>
    internal static string? ResolveBootstrapScript(string manifestDirectory)
    {
        if (string.IsNullOrEmpty(manifestDirectory))
        {
            return null;
        }

        var sdkRoot = Path.Combine(manifestDirectory, "node_modules", "@microsoft", "cmdpal-sdk");
        if (!Directory.Exists(sdkRoot))
        {
            return null;
        }

        // Prefer the SDK package's declared bin entry so the launch tracks the published
        // contract rather than a hardcoded artifact path.
        var fromBin = ResolveBootstrapFromPackageJson(sdkRoot);
        if (fromBin is not null && File.Exists(fromBin))
        {
            return fromBin;
        }

        foreach (var candidate in new[]
        {
            Path.Combine(sdkRoot, "dist", "runtime", "bootstrap.js"),
            Path.Combine(sdkRoot, "bin", "cmdpal-bootstrap.mjs"),
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveBootstrapFromPackageJson(string sdkRoot)
    {
        var packageJsonPath = Path.Combine(sdkRoot, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            if (!document.RootElement.TryGetProperty("bin", out var bin))
            {
                return null;
            }

            string? relative = null;
            if (bin.ValueKind == JsonValueKind.String)
            {
                relative = bin.GetString();
            }
            else if (bin.ValueKind == JsonValueKind.Object)
            {
                if (bin.TryGetProperty("cmdpal-bootstrap", out var named) && named.ValueKind == JsonValueKind.String)
                {
                    relative = named.GetString();
                }
                else
                {
                    foreach (var property in bin.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            relative = property.Value.GetString();
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(relative))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(sdkRoot, relative));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
