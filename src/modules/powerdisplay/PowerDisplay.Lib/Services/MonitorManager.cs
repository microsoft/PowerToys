// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Drivers.WMI;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Monitor manager for unified control of all monitors. Implements <see cref="IMonitorManager"/>
    /// so consumers (e.g. the headless CLI) can depend on the abstraction and be unit-tested against a fake.
    /// </summary>
    // 'partial' is required by the CsWinRT source generator (CsWinRT1028) for AOT/trimming
    // compatibility because the type crosses the WinRT ABI; do not remove it.
    public partial class MonitorManager : IDisposable, IMonitorManager
    {
        private readonly List<Monitor> _monitors = new();
        private readonly Dictionary<string, Monitor> _monitorLookup = new(MonitorIdComparer.Instance);
        private readonly SemaphoreSlim _discoveryLock = new(1, 1);
        private readonly DisplayRotationService _rotationService = new();

        // Built-in entries are loaded automatically by the service constructor.
        private readonly MonitorBlacklistService _blacklistService = new();

        // Controllers stored by type for O(1) lookup based on CommunicationMethod
        private DdcCiController? _ddcController;
        private WmiController? _wmiController;
        private bool _disposed;

        public IReadOnlyList<Monitor> Monitors => _monitors.AsReadOnly();

        public MonitorManager()
        {
            // Initialize controllers
            InitializeControllers();
        }

        /// <summary>
        /// Initialize controllers
        /// </summary>
        private void InitializeControllers()
        {
            try
            {
                // DDC/CI controller (external monitors)
                _ddcController = new DdcCiController();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize DDC/CI controller: {ex.Message}");
            }

            try
            {
                // WMI controller (internal monitors)
                // Always create - DiscoverMonitorsAsync returns empty list if WMI is unavailable
                _wmiController = new WmiController();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize WMI controller: {ex.Message}");
            }
        }

        /// <summary>
        /// Pushes the max-compatibility-mode flag onto the DDC/CI controller. Callers (the GUI's
        /// MainViewModel and the headless CLI) invoke this before discovery so the value is current.
        /// No-op if the DDC controller failed to initialize.
        /// </summary>
        public void SetMaxCompatibilityMode(bool enabled)
        {
            if (_ddcController != null)
            {
                _ddcController.MaxCompatibilityMode = enabled;
            }
        }

        /// <summary>
        /// Discover all monitors from all controllers.
        /// Each controller is responsible for fully initializing its monitors
        /// (including brightness, capabilities, input source, color temperature, etc.)
        /// </summary>
        public async Task<IReadOnlyList<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            await _discoveryLock.WaitAsync(cancellationToken);

            try
            {
                var discoveredMonitors = await DiscoverFromAllControllersAsync(cancellationToken);

                // Update collections
                _monitors.Clear();
                _monitorLookup.Clear();

                var sortedMonitors = discoveredMonitors
                    .OrderBy(m => m.MonitorNumber)
                    .ToList();

                _monitors.AddRange(sortedMonitors);
                foreach (var monitor in sortedMonitors)
                {
                    _monitorLookup[monitor.Id] = monitor;
                }

                // Controllers leave Orientation at its default (0) during discovery; query the
                // live rotation here so the very first read reflects the panel's real orientation
                // (the CLI relies on this for `get`/`set --orientation` round-tripping, and the GUI
                // shows the correct value on initial load).
                RefreshAllOrientations();

                return _monitors.AsReadOnly();
            }
            finally
            {
                _discoveryLock.Release();
            }
        }

        /// <summary>
        /// Discover monitors by capability, not by nominal output technology. WMI runs first
        /// over the full QueryDisplayConfig inventory; every display it claims is a
        /// WMI-controllable internal panel. Whatever WMI does not claim is then sent to DDC/CI.
        /// This avoids incorrectly routing a built-in panel that the active (discrete) GPU reports as
        /// DisplayPort-External — the root cause of issue #48587.
        /// </summary>
        private async Task<List<Monitor>> DiscoverFromAllControllersAsync(CancellationToken cancellationToken)
        {
            var inventory = DisplayConfigInventory.GetAllMonitorDisplayInfo();

            // Filter blacklisted monitors before any controller runs, so blocked displays are
            // never opened, probed, or queried (unlike the per-monitor IsHidden flag). Matching
            // is by MonitorIdentity.EdidIdFromMonitorId on each entry's DevicePath.
            var beforeCount = inventory.Count;
            var filteredInventory = new Dictionary<string, MonitorDisplayInfo>(
                inventory.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in inventory)
            {
                if (_blacklistService.IsBlocked(kvp.Value.DevicePath))
                {
                    var edidId = MonitorIdentity.EdidIdFromMonitorId(kvp.Value.DevicePath);
                    Logger.LogInfo(
                        $"[MonitorBlacklist] Skipping '{kvp.Value.FriendlyName}' (EdidId '{edidId}', path '{kvp.Value.DevicePath}') — EdidId is on the blacklist");
                    continue;
                }

                filteredInventory.Add(kvp.Key, kvp.Value);
            }

            if (filteredInventory.Count < beforeCount)
            {
                Logger.LogInfo(
                    $"[MonitorBlacklist] Filtered out {beforeCount - filteredInventory.Count} monitor(s); {filteredInventory.Count} remain");
            }

            inventory = filteredInventory;

            if (inventory.Count == 0)
            {
                Logger.LogWarning("[MonitorManager] QueryDisplayConfig returned no displays — discovery aborted");
                return new List<Monitor>();
            }

            var allDisplays = inventory.Values.ToList();

            // Phase 1: WMI over the full inventory — whatever it claims is an internal panel.
            var wmiMonitors = _wmiController != null
                ? (await SafeDiscoverAsync(_wmiController, allDisplays, cancellationToken)).ToList()
                : new List<Monitor>();

            var wmiClaimedIds = new HashSet<string>(
                wmiMonitors.Select(m => m.Id), MonitorIdComparer.Instance);

            // Phase 2: everything WMI did not claim goes to DDC/CI. Accepted trade-off — a
            // monitor exposing both is controlled via WMI only and won't get DDC-only features
            // (contrast/volume/input). Partition once so FromDevicePath runs a single time each.
            var byRoute = allDisplays.ToLookup(
                d => wmiClaimedIds.Contains(MonitorIdentity.FromDevicePath(d.DevicePath)));
            IReadOnlyList<MonitorDisplayInfo> wmiTargets = byRoute[true].ToList();
            IReadOnlyList<MonitorDisplayInfo> ddcTargets = byRoute[false].ToList();

            LogClassificationSummary(wmiTargets, ddcTargets);

            var ddcMonitors = _ddcController != null
                ? (await SafeDiscoverAsync(_ddcController, ddcTargets, cancellationToken)).ToList()
                : new List<Monitor>();

            return wmiMonitors.Concat(ddcMonitors).ToList();
        }

        /// <summary>
        /// Logs how each display was routed (WMI vs DDC/CI) at Info level, one line per
        /// display plus a summary. Runs after WMI discovery but before the crash-prone DDC/CI
        /// capability fetch, so every attached model's EdidId is on disk for crash correlation.
        /// </summary>
        private static void LogClassificationSummary(
            IReadOnlyList<MonitorDisplayInfo> wmiTargets,
            IReadOnlyList<MonitorDisplayInfo> ddcTargets)
        {
            Logger.LogInfo($"[DisplayClassification] Found {wmiTargets.Count + ddcTargets.Count} displays:");

            var wmiPaths = new HashSet<string>(wmiTargets.Select(t => t.DevicePath), StringComparer.OrdinalIgnoreCase);

            foreach (var info in wmiTargets.Concat(ddcTargets).OrderBy(i => i.MonitorNumber))
            {
                var route = wmiPaths.Contains(info.DevicePath) ? "WMI (internal)" : "DDC/CI (external)";

                // EdidId (manufacturer+product code) is logged here, before the BSOD-prone DDC
                // capability fetch, so recovered logs identify every attached model (and
                // same-model duplicates) for crash correlation.
                var edidId = MonitorIdentity.EdidIdFromMonitorId(info.DevicePath);
                var edidIdField = string.IsNullOrEmpty(edidId) ? "?" : edidId;

                Logger.LogInfo(
                    $"  [Path {info.MonitorNumber}] EdidId={edidIdField} {info.GdiDeviceName} / \"{info.FriendlyName}\" → {route}");
            }

            Logger.LogInfo($"[DisplayClassification] Summary: {wmiTargets.Count} WMI, {ddcTargets.Count} DDC/CI");
        }

        /// <summary>
        /// Safely discover monitors from a controller, returning empty list on failure.
        /// </summary>
        private static async Task<IEnumerable<Monitor>> SafeDiscoverAsync(
            IMonitorController controller,
            IReadOnlyList<MonitorDisplayInfo> targets,
            CancellationToken cancellationToken)
        {
            try
            {
                return await controller.DiscoverMonitorsAsync(targets, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Controller {controller.Name} discovery failed: {ex.Message}");
                return Enumerable.Empty<Monitor>();
            }
        }

        /// <summary>
        /// Set brightness of the specified monitor
        /// </summary>
        public Task<MonitorOperationResult> SetBrightnessAsync(string monitorId, int brightness, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                brightness,
                (ctrl, mon, val, ct) => ctrl.SetBrightnessAsync(mon, val, ct),
                (mon, val) => mon.CurrentBrightness = val,
                cancellationToken);

        /// <summary>
        /// Set contrast of the specified monitor (DDC/CI controllers only; returns Failure on others).
        /// </summary>
        public Task<MonitorOperationResult> SetContrastAsync(string monitorId, int contrast, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                contrast,
                (ctrl, mon, val, ct) => ctrl.SetContrastAsync(mon, val, ct),
                (mon, val) => mon.CurrentContrast = val,
                cancellationToken);

        /// <summary>
        /// Set volume of the specified monitor (DDC/CI controllers only; returns Failure on others).
        /// </summary>
        public Task<MonitorOperationResult> SetVolumeAsync(string monitorId, int volume, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                volume,
                (ctrl, mon, val, ct) => ctrl.SetVolumeAsync(mon, val, ct),
                (mon, val) => mon.CurrentVolume = val,
                cancellationToken);

        /// <summary>
        /// Set monitor color temperature (DDC/CI controllers only; returns Failure on others).
        /// </summary>
        public Task<MonitorOperationResult> SetColorTemperatureAsync(string monitorId, int colorTemperature, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                colorTemperature,
                (ctrl, mon, val, ct) => ctrl.SetColorTemperatureAsync(mon, val, ct),
                (mon, val) => mon.CurrentColorTemperature = val,
                cancellationToken);

        /// <summary>
        /// Set input source for a monitor (DDC/CI controllers only; returns Failure on others).
        /// </summary>
        public Task<MonitorOperationResult> SetInputSourceAsync(string monitorId, int inputSource, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                inputSource,
                (ctrl, mon, val, ct) => ctrl.SetInputSourceAsync(mon, val, ct),
                (mon, val) => mon.CurrentInputSource = val,
                cancellationToken);

        /// <summary>
        /// Set power state for a monitor using VCP 0xD6 (DDC/CI controllers only; returns Failure on others).
        /// Note: Setting any state other than On (0x01) will turn off the display.
        /// </summary>
        public Task<MonitorOperationResult> SetPowerStateAsync(string monitorId, int powerState, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                powerState,
                (ctrl, mon, val, ct) => ctrl.SetPowerStateAsync(mon, val, ct),
                (mon, val) => mon.CurrentPowerState = val,
                cancellationToken);

        /// <summary>
        /// Set rotation/orientation for a monitor.
        /// Uses Windows ChangeDisplaySettingsEx API (not DDC/CI).
        /// After successful rotation, refreshes orientation for all monitors sharing the same GdiDeviceName
        /// (important for mirror/clone mode where multiple monitors share one display source).
        /// </summary>
        /// <param name="monitorId">Monitor ID</param>
        /// <param name="orientation">Orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public Task<MonitorOperationResult> SetRotationAsync(string monitorId, int orientation, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                Logger.LogError($"[MonitorManager] SetRotation: Monitor not found: {monitorId}");
                return Task.FromResult(MonitorOperationResult.Failure("Monitor not found"));
            }

            // Rotation uses Windows display settings API, not DDC/CI controller
            // Prefer using Monitor object which contains GdiDeviceName for accurate adapter targeting
            var result = _rotationService.SetRotation(monitor, orientation);

            if (result.IsSuccess)
            {
                // Refresh orientation for all monitors - rotation affects the GdiDeviceName (display source),
                // and in mirror mode multiple monitors may share the same GdiDeviceName
                RefreshAllOrientations();
            }
            else
            {
                Logger.LogError($"[MonitorManager] SetRotation: Failed for {monitorId}: {result.ErrorMessage}");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Refresh orientation values for all monitors by querying current display settings.
        /// This ensures all monitors reflect the actual system state, which is important
        /// in mirror mode where multiple monitors share the same GdiDeviceName.
        /// </summary>
        public void RefreshAllOrientations()
        {
            foreach (var monitor in _monitors)
            {
                if (string.IsNullOrEmpty(monitor.GdiDeviceName))
                {
                    continue;
                }

                var currentOrientation = _rotationService.GetCurrentOrientation(monitor.GdiDeviceName);
                if (currentOrientation >= 0)
                {
                    // Assigning an unchanged value is a no-op (the setter guards on equality), but the
                    // read flag must be set whenever the query succeeds so consumers can tell a real
                    // "0°/landscape" reading apart from the never-read default.
                    monitor.Orientation = currentOrientation;
                    monitor.ReadValues |= MonitorReadFlags.Orientation;
                }
            }
        }

        /// <summary>
        /// Get monitor by ID. Uses dictionary lookup for O(1) performance.
        /// </summary>
        public Monitor? GetMonitor(string monitorId)
        {
            return _monitorLookup.TryGetValue(monitorId, out var monitor) ? monitor : null;
        }

        /// <summary>
        /// Get controller for the monitor based on CommunicationMethod.
        /// O(1) lookup - no async validation needed since controller type is determined at discovery.
        /// </summary>
        private IMonitorController? GetControllerForMonitor(Monitor monitor)
        {
            return monitor.CommunicationMethod switch
            {
                "WMI" => _wmiController,
                "DDC/CI" => _ddcController,
                _ => null,
            };
        }

        /// <summary>
        /// Generic helper to execute monitor operations with common error handling.
        /// Feature-level capability handling lives on the controller contract itself —
        /// methods that a controller doesn't implement fall through to the default
        /// "unsupported" body on <see cref="IDdcController"/> and return a Failure result.
        /// </summary>
        private async Task<MonitorOperationResult> ExecuteMonitorOperationAsync<TValue>(
            string monitorId,
            TValue value,
            Func<IMonitorController, Monitor, TValue, CancellationToken, Task<MonitorOperationResult>> operation,
            Action<Monitor, TValue> onSuccess,
            CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                Logger.LogError($"[MonitorManager] Monitor not found: {monitorId}");
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                Logger.LogError($"[MonitorManager] No controller available for monitor {monitorId}");
                return MonitorOperationResult.Failure("No controller available for this monitor");
            }

            try
            {
                var result = await operation(controller, monitor, value, cancellationToken);

                if (result.IsSuccess)
                {
                    onSuccess(monitor, value);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MonitorManager] Operation failed for {monitorId}: {ex.Message}");
                return MonitorOperationResult.Failure($"Exception: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _discoveryLock?.Dispose();

                // Release controllers
                _ddcController?.Dispose();
                _wmiController?.Dispose();

                _monitors.Clear();
                _monitorLookup.Clear();
                _disposed = true;
            }
        }
    }
}
