// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Applies the percent-scaled VCP features — brightness, contrast, volume — to a freshly built
    /// <see cref="Monitor"/>, and records every value it reads off the hardware in the known-good
    /// store so a later pass that cannot read the code has something proven to fall back on.
    /// </summary>
    internal sealed class ContinuousVcpInitializer
    {
        private readonly IVcpFeatureReader _reader;
        private readonly IKnownGoodVcpStore _store;

        public ContinuousVcpInitializer(
            IVcpFeatureReader reader,
            IKnownGoodVcpStore store)
        {
            _reader = reader;
            _store = store;
        }

        /// <summary>
        /// Applies the continuous VCP features to <paramref name="monitor"/>, reading from
        /// <see cref="Monitor.Handle"/> where the evidence does not already carry a value that can be
        /// trusted without one.
        /// </summary>
        /// <returns>
        /// False when a read failed with a handle-class error, which invalidates
        /// <see cref="Monitor.Handle"/> itself rather than the one feature — the caller must then
        /// discard the monitor instead of publishing it.
        /// </returns>
        public bool Initialize(Monitor monitor, VcpDiscoveryEvidence evidence)
        {
            foreach (var code in ContinuousVcpCodes)
            {
                if (!InitializeFeature(monitor, evidence, code))
                {
                    return false;
                }
            }

            return true;
        }

        private bool InitializeFeature(Monitor monitor, VcpDiscoveryEvidence evidence, byte code)
        {
            if (!IsSupported(monitor, code))
            {
                return true;
            }

            VcpInitialValue? cachedFallback = null;
            if (evidence.InitialValues.TryGetValue(code, out var initial))
            {
                if (!initial.PreferLiveRead)
                {
                    ApplyValue(monitor, code, initial.Value, markAsRead: initial.IsLive);
                    return true;
                }

                cachedFallback = initial;
            }

            var read = _reader.Read(monitor.Handle, code);
            if (!read.IsSuccess)
            {
                Logger.LogError(
                    $"DDC: [{monitor.Id}] Failed to read VCP 0x{code:X2}, " +
                    $"error={DdcErrorClassifier.Format(read.ErrorCode)}");
                if (DdcErrorClassifier.IsPhysicalMonitorUnavailable(read.ErrorCode))
                {
                    // Dropping the monitor is deliberate, and the cached fallback is deliberately not
                    // applied: Monitor.Handle is captured once per discovery pass and never refreshed,
                    // so a monitor kept here would send every later read and write to a handle already
                    // known to be dead. A rediscovery is what repairs it — DisplayChangeWatcher
                    // schedules one for the topology changes that invalidate a handle, and the
                    // flyout's Refresh button forces one on demand.
                    return false;
                }

                ApplyCachedFallback(monitor, code, cachedFallback);
                return true;
            }

            var value = new VcpFeatureValue((int)read.Current, 0, (int)read.Maximum);
            if (!value.IsValid)
            {
                Logger.LogWarning(
                    $"DDC: [{monitor.Id}] Ignoring invalid {VcpNames.GetCodeName(code).ToLowerInvariant()} " +
                    $"range current={read.Current}, max={read.Maximum}");
                ApplyCachedFallback(monitor, code, cachedFallback);
                return true;
            }

            ApplyValue(monitor, code, value, markAsRead: true);

            // Recorded regardless of Maximum compatibility mode, which only gates the *read* side in
            // DdcCiController. A monitor that reads cleanly today can start failing after a cable or
            // dock change, and the cache is only worth anything if it was already warm by then —
            // populating it lazily would leave the first pass after the switch with nothing to fall
            // back on, which is exactly the pass that needs it.
            _store.UpsertKnownGoodFeature(
                monitor.Id,
                KnownGoodVcpFeature.From(code, value));

            return true;
        }

        private static void ApplyCachedFallback(
            Monitor monitor,
            byte code,
            VcpInitialValue? cachedFallback)
        {
            if (cachedFallback is { } fallback)
            {
                ApplyValue(monitor, code, fallback.Value, markAsRead: false);
            }
        }

        /// <summary>
        /// Whether <paramref name="monitor"/> advertises <paramref name="code"/>.
        /// </summary>
        /// <remarks>
        /// This switch and <see cref="ApplyValue"/>'s must between them cover every entry in
        /// <see cref="NativeConstants.ContinuousVcpCodes"/>: a code added to that array without an
        /// arm here is skipped, and one without an arm there is read and then discarded. Both are
        /// silent, so the array and the two switches are edited together.
        /// </remarks>
        private static bool IsSupported(Monitor monitor, byte code) => code switch
        {
            VcpCodeBrightness => monitor.SupportsBrightness,
            VcpCodeContrast => monitor.SupportsContrast,
            VcpCodeVolume => monitor.SupportsVolume,
            _ => false,
        };

        private static void ApplyValue(
            Monitor monitor,
            byte code,
            VcpFeatureValue value,
            bool markAsRead)
        {
            switch (code)
            {
                case VcpCodeBrightness:
                    monitor.BrightnessVcpMax = value.Maximum;
                    monitor.CurrentBrightness = value.ToPercentage();
                    if (markAsRead)
                    {
                        monitor.ReadValues |= MonitorReadFlags.Brightness;
                    }

                    break;

                case VcpCodeContrast:
                    monitor.ContrastVcpMax = value.Maximum;
                    monitor.CurrentContrast = value.ToPercentage();
                    if (markAsRead)
                    {
                        monitor.ReadValues |= MonitorReadFlags.Contrast;
                    }

                    break;

                case VcpCodeVolume:
                    monitor.VolumeVcpMax = value.Maximum;
                    monitor.CurrentVolume = value.ToPercentage();
                    if (markAsRead)
                    {
                        monitor.ReadValues |= MonitorReadFlags.Volume;
                    }

                    break;
            }
        }
    }
}
