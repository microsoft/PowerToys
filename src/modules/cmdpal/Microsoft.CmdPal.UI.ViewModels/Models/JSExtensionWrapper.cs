// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapter that manages a single Node.js extension process and presents it as an IExtensionWrapper to the CmdPal host.
/// </summary>
public sealed class JSExtensionWrapper : IExtensionWrapper, IDisposable
{
    private readonly JSExtensionManifest _manifest;
    private readonly string _manifestDirectory;
    private readonly Lock _lock = new();
    private readonly List<ProviderType> _providerTypes = [];

    private static int _nextDebugPort = 9229;

    private Process? _nodeProcess;
    private JsonRpcConnection? _rpcConnection;
    private JSCommandProviderProxy? _commandProviderProxy;
    private bool _isDisposed;
    private int _consecutiveCrashCount;

    /// <summary>
    /// Gets the number of times this extension has been restarted (due to crashes or hot-reload).
    /// </summary>
    public int RestartCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the extension is considered healthy.
    /// An extension becomes unhealthy after exceeding 3 consecutive crashes.
    /// </summary>
    public bool IsHealthy { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSExtensionWrapper"/> class.
    /// </summary>
    /// <param name="manifest">The parsed extension manifest.</param>
    /// <param name="manifestDirectory">The directory containing the manifest file.</param>
    public JSExtensionWrapper(JSExtensionManifest manifest, string manifestDirectory)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _manifestDirectory = manifestDirectory ?? throw new ArgumentNullException(nameof(manifestDirectory));

        if (!_manifest.IsValid())
        {
            throw new ArgumentException("Invalid manifest", nameof(manifest));
        }

        // Map manifest capabilities to provider types
        var caps = _manifest.Capabilities;
        if (caps != null)
        {
            foreach (var cap in caps)
            {
                if (string.Equals(cap, "commands", StringComparison.OrdinalIgnoreCase))
                {
                    AddProviderType(ProviderType.Commands);
                }
            }
        }
        else
        {
            // Default: assume commands capability if not specified
            AddProviderType(ProviderType.Commands);
        }
    }

    public string PackageDisplayName => _manifest.DisplayName ?? _manifest.Name ?? "Unknown";

    public string ExtensionDisplayName => _manifest.DisplayName ?? _manifest.Name ?? "Unknown";

    public string PackageFullName => $"js!{_manifest.Name}";

    public string PackageFamilyName => $"js!{_manifest.Name}";

    public string Publisher => _manifest.Publisher ?? "Unknown";

    public string ExtensionClassId
    {
        get
        {
            // Generate a deterministic "GUID-like" identifier from the manifest name
            if (string.IsNullOrWhiteSpace(_manifest.Name))
            {
                return "unknown";
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(_manifest.Name));
            var hashString = Convert.ToHexString(hash);
            return $"js-{hashString.Substring(0, 32)}";
        }
    }

    public DateTimeOffset InstalledDate
    {
        get
        {
            try
            {
                var manifestPath = Path.Combine(_manifestDirectory, "cmdpal.json");
                if (File.Exists(manifestPath))
                {
                    return File.GetCreationTimeUtc(manifestPath);
                }
            }
            catch
            {
                // Fallback if file operations fail
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

    public bool IsRunning()
    {
        lock (_lock)
        {
            if (_nodeProcess == null || _rpcConnection == null)
            {
                return false;
            }

            try
            {
                return !_nodeProcess.HasExited;
            }
            catch
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
            if (_nodeProcess != null && _rpcConnection != null)
            {
                try
                {
                    if (!_nodeProcess.HasExited)
                    {
                        return; // Already running
                    }
                }
                catch
                {
                    // Process handle invalid — fall through to restart
                }
            }
        }

        Logger.LogDebug($"Starting JS extension {_manifest.DisplayName ?? _manifest.Name}");

        try
        {
            var entryPoint = Path.Combine(_manifestDirectory, _manifest.Main ?? string.Empty);

            // Retry up to 5 times with 1s backoff — the entry point file may
            // still be in flight when the directory watcher fires.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (File.Exists(entryPoint))
                {
                    break;
                }

                if (attempt == 4)
                {
                    Logger.LogError($"Entry point not found after retries: {entryPoint}");
                    return;
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }

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

            var nodeProcess = Process.Start(psi);
            if (nodeProcess == null)
            {
                Logger.LogError($"Failed to start Node.js process for {_manifest.Name}");
                return;
            }

            var loggerAdapter = new LoggerAdapter();
            var rpcConnection = new JsonRpcConnection(nodeProcess, loggerAdapter);
            rpcConnection.OnError += ex => Logger.LogError($"JSON-RPC error in {_manifest.Name}: {ex.Message}");
            rpcConnection.OnDisconnected += HandleDisconnection;

            // Store the process and connection BEFORE awaiting initialize so
            // IsRunning() reflects the actual process state immediately.
            lock (_lock)
            {
                _nodeProcess = nodeProcess;
                _rpcConnection = rpcConnection;
            }

            nodeProcess.Exited += (_, _) => HandleDisconnection();
            nodeProcess.EnableRaisingEvents = true;

            rpcConnection.StartListening();

            // Send initialize request to the extension
            var initResponse = await rpcConnection.SendRequestAsync(
                "initialize",
                new JsonObject { ["extensionId"] = _manifest.Name },
                CancellationToken.None).ConfigureAwait(false);

            if (initResponse.Error != null)
            {
                Logger.LogError($"Initialization failed for {_manifest.Name}: {initResponse.Error.Message}");
                SignalDispose();
                return;
            }

            // Extension started and initialized successfully — reset crash tracking
            ResetCrashCount();

            Logger.LogInfo($"Successfully started JS extension {_manifest.DisplayName ?? _manifest.Name}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start JS extension {_manifest.Name}: {ex.Message}");
            SignalDispose();
        }
    }

    public void SignalDispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            try
            {
                if (_rpcConnection != null && IsRunning())
                {
                    // Send dispose notification (fire-and-forget)
                    _rpcConnection.SendNotificationAsync("dispose", null, CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error sending dispose notification to {_manifest.Name}: {ex.Message}");
            }

            try
            {
                _rpcConnection?.Dispose();
            }
            catch
            {
                // Best effort
            }

            try
            {
                if (_nodeProcess != null && !_nodeProcess.HasExited)
                {
                    _nodeProcess.Kill(entireProcessTree: true);
                    _nodeProcess.WaitForExit(2000);
                }

                _nodeProcess?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error terminating Node.js process for {_manifest.Name}: {ex.Message}");
            }

            _nodeProcess = null;
            _rpcConnection = null;
            _commandProviderProxy = null;
        }
    }

    public void Dispose()
    {
        SignalDispose();
    }

    public IExtension? GetExtensionObject()
    {
        // JS extensions don't have COM objects - the wrapper itself is the bridge
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
            // Only ICommandProvider is supported for Phase 2
            return null;
        }

        await StartExtensionAsync().ConfigureAwait(false);

        lock (_lock)
        {
            if (!IsRunning() || _rpcConnection == null)
            {
                return null;
            }

            if (_commandProviderProxy == null)
            {
                _commandProviderProxy = new JSCommandProviderProxy(_rpcConnection, _manifest);
            }

            return _commandProviderProxy as T;
        }
    }

    public async Task<IEnumerable<T>> GetListOfProvidersAsync<T>()
        where T : class
    {
        var provider = await GetProviderAsync<T>().ConfigureAwait(false);
        if (provider != null)
        {
            return new[] { provider };
        }

        return [];
    }

    private void HandleDisconnection()
    {
        lock (_lock)
        {
            if (!_isDisposed)
            {
                _consecutiveCrashCount++;
                Logger.LogWarning($"Node.js process for {_manifest.Name} disconnected unexpectedly (crash #{_consecutiveCrashCount})");

                if (_consecutiveCrashCount > 3)
                {
                    IsHealthy = false;
                    Logger.LogError($"JS extension {_manifest.Name} disabled after {_consecutiveCrashCount} consecutive crashes");
                }

                _nodeProcess = null;
                _rpcConnection = null;
                _commandProviderProxy = null;
            }
        }
    }

    /// <summary>
    /// Resets the consecutive crash counter. Call after a successful operation to indicate the extension is stable.
    /// </summary>
    internal void ResetCrashCount()
    {
        lock (_lock)
        {
            _consecutiveCrashCount = 0;
        }
    }

    /// <summary>
    /// Stops the running Node.js process and starts a fresh one.
    /// Used by hot-reload to restart the extension after source file changes.
    /// </summary>
    internal async Task RestartAsync()
    {
        lock (_lock)
        {
            if (_isDisposed || !IsHealthy)
            {
                return;
            }
        }

        Logger.LogInfo($"Restarting JS extension {_manifest.DisplayName ?? _manifest.Name}");

        // Gracefully shut down the current process
        lock (_lock)
        {
            try
            {
                if (_rpcConnection != null && IsRunning())
                {
                    _rpcConnection.SendNotificationAsync("dispose", null, CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error sending dispose during restart of {_manifest.Name}: {ex.Message}");
            }

            try
            {
                _rpcConnection?.Dispose();
            }
            catch
            {
                // Best effort
            }

            try
            {
                if (_nodeProcess != null && !_nodeProcess.HasExited)
                {
                    _nodeProcess.Kill(entireProcessTree: true);
                    _nodeProcess.WaitForExit(2000);
                }

                _nodeProcess?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error terminating Node.js process during restart of {_manifest.Name}: {ex.Message}");
            }

            _nodeProcess = null;
            _rpcConnection = null;
            _commandProviderProxy = null;
        }

        RestartCount++;
        await StartExtensionAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the manifest directory path for this extension.
    /// </summary>
    internal string ManifestDirectory => _manifestDirectory;

    private string BuildNodeArguments(string entryPoint)
    {
        if (_manifest.Debug)
        {
            var port = _manifest.DebugPort ?? Interlocked.Increment(ref _nextDebugPort);
            var debugUrl = $"chrome-devtools://devtools/bundled/js_app.html?experiments=true&v8only=true&ws=127.0.0.1:{port}";
            Logger.LogInfo($"Debug mode enabled for {_manifest.Name} on port {port}. Attach debugger at: {debugUrl}");
            return $"--inspect={port} \"{entryPoint}\"";
        }

        return $"\"{entryPoint}\"";
    }

    private sealed class LoggerAdapter : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Error:
                case Microsoft.Extensions.Logging.LogLevel.Critical:
                    Logger.LogError(message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Warning:
                    Logger.LogWarning(message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Information:
                    Logger.LogInfo(message);
                    break;
                default:
                    Logger.LogDebug(message);
                    break;
            }
        }
    }
}
