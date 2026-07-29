// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC;

internal sealed class ContinuousVcpInitializer
{
    private readonly IVcpFeatureReader _reader;
    private readonly IKnownGoodVcpStore _store;
    private readonly ISystemClock _clock;

    public ContinuousVcpInitializer(
        IVcpFeatureReader reader,
        IKnownGoodVcpStore store,
        ISystemClock clock)
    {
        _reader = reader;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Applies the continuous VCP features to <paramref name="monitor"/>, reading from
    /// <see cref="Monitor.Handle"/> where the evidence does not already carry a value that can be
    /// trusted without one.
    /// </summary>
    public VcpInitializationResult Initialize(
        Monitor monitor,
        VcpDiscoveryEvidence evidence)
    {
        foreach (var code in ContinuousVcpCodes)
        {
            var result = InitializeFeature(monitor, evidence, code);
            if (result == VcpInitializationResult.PhysicalMonitorUnavailable)
            {
                return result;
            }
        }

        return VcpInitializationResult.Completed;
    }

    private VcpInitializationResult InitializeFeature(
        Monitor monitor,
        VcpDiscoveryEvidence evidence,
        byte code)
    {
        if (!IsSupported(monitor, code))
        {
            return VcpInitializationResult.Completed;
        }

        VcpInitialValue? cachedFallback = null;
        if (evidence.InitialValues.TryGetValue(code, out var initial))
        {
            if (!initial.PreferLiveRead)
            {
                ApplyValue(monitor, code, initial.Value, markAsRead: initial.IsLive);
                return VcpInitializationResult.Completed;
            }

            cachedFallback = initial;
        }

        var read = _reader.Read(monitor.Handle, code);
        if (!read.IsSuccess)
        {
            Logger.LogError($"[{monitor.Id}] Failed to read VCP 0x{code:X2}, error code: {read.ErrorCode}");
            if (DdcErrorClassifier.IsPhysicalMonitorUnavailable(read.ErrorCode))
            {
                // Dropping the monitor is deliberate, and the cached fallback is deliberately not
                // applied: Monitor.Handle is captured once per discovery pass and never refreshed,
                // so a monitor kept here would answer every later read and write against a handle
                // already known to be dead. A rediscovery is what repairs it, and DisplayChangeWatcher
                // schedules one for the topology changes that invalidate a handle.
                return VcpInitializationResult.PhysicalMonitorUnavailable;
            }

            ApplyCachedFallback(monitor, code, cachedFallback);
            return VcpInitializationResult.Completed;
        }

        var value = new VcpFeatureValue((int)read.Current, 0, (int)read.Maximum);
        if (!value.IsValid)
        {
            Logger.LogWarning(
                $"DDC: [{monitor.Id}] Ignoring invalid {VcpNames.GetCodeName(code).ToLowerInvariant()} " +
                $"range current={read.Current}, max={read.Maximum}");
            ApplyCachedFallback(monitor, code, cachedFallback);
            return VcpInitializationResult.Completed;
        }

        ApplyValue(monitor, code, value, markAsRead: true);
        _store.UpsertKnownGoodFeature(
            monitor.Id,
            KnownGoodVcpFeature.From(
                code,
                value,
                VcpObservationSource.CapabilitiesInitialization,
                _clock.UtcNow));

        return VcpInitializationResult.Completed;
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
