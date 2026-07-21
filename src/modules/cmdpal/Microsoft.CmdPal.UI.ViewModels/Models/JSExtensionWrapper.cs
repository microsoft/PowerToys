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
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
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
    }

    public async Task StartExtensionAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_lock)
        {
            if (_nodeProcess is not null && _connection is not null)
            {
                try
                {
                    if (!_nodeProcess.HasExited)
                    {
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process handle is no longer valid; fall through and restart.
                }
            }
        }

        Logger.LogDebug($"Starting JS extension {_manifest.EffectiveDisplayName}");

        var entryPoint = _manifest.EntryPointPath ?? Path.Combine(_manifestDirectory, _manifest.Main ?? string.Empty);
        if (!File.Exists(entryPoint))
        {
            Logger.LogError($"Entry point not found for {_manifest.Name}: {entryPoint}");
            return;
        }

        Process? nodeProcess = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = BuildNodeArguments(entryPoint),
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

            var connection = new JsonRpcConnection(
                nodeProcess.StandardOutput.BaseStream,
                nodeProcess.StandardInput.BaseStream,
                nodeProcess.StandardError.BaseStream);

            connection.Error += OnConnectionError;
            connection.Disconnected += OnConnectionDisconnected;

            lock (_lock)
            {
                _stopping = false;
                _nodeProcess = nodeProcess;
                _connection = connection;
                _commandProviderProxy = null;
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

    public void SignalDispose()
    {
        Process? process;
        JsonRpcConnection? connection;

        lock (_lock)
        {
            _isDisposed = true;
            _stopping = true;
            process = _nodeProcess;
            connection = _connection;
            _nodeProcess = null;
            _connection = null;
            _commandProviderProxy = null;
        }

        TearDown(process, connection);
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
            if (_connection is null || !IsRunning())
            {
                return null;
            }

            _commandProviderProxy ??= new JSCommandProviderProxy(_connection, _manifest);
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
            _nodeProcess = null;
            _connection = null;
            _commandProviderProxy = null;
        }

        // This runs on the connection's read-loop thread, and JsonRpcConnection.Dispose()
        // joins that thread. Tear the handles down and notify the service on a background
        // thread to avoid a self-join and to keep the read loop from blocking on itself.
        _ = Task.Run(() =>
        {
            TearDown(process, connection);
            ProcessExited?.Invoke(this, EventArgs.Empty);
        });
    }

    private void TearDown(Process? process, JsonRpcConnection? connection)
    {
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

    private string BuildNodeArguments(string entryPoint)
    {
        if (_manifest.Debug)
        {
            var port = _manifest.DebugPort ?? Interlocked.Increment(ref _nextDebugPort);
            Logger.LogInfo($"Debug mode enabled for {_manifest.Name} on inspector port {port}");
            return $"--inspect={port} \"{entryPoint}\"";
        }

        return $"\"{entryPoint}\"";
    }
}
