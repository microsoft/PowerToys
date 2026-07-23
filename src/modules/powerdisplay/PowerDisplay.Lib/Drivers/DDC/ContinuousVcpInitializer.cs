// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC;

internal sealed class ContinuousVcpInitializer
{
    private static readonly byte[] ContinuousCodes =
    {
        VcpCodeBrightness,
        VcpCodeContrast,
        VcpCodeVolume,
    };

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

    public VcpInitializationResult Initialize(
        Monitor monitor,
        IntPtr handle,
        VcpDiscoveryEvidence evidence)
    {
        foreach (var code in ContinuousCodes)
        {
            var result = InitializeFeature(monitor, handle, evidence, code);
            if (result == VcpInitializationResult.PhysicalMonitorUnavailable)
            {
                return result;
            }
        }

        return VcpInitializationResult.Completed;
    }

    private VcpInitializationResult InitializeFeature(
        Monitor monitor,
        IntPtr handle,
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

        var read = _reader.Read(handle, code);
        if (!read.IsSuccess)
        {
            Logger.LogError($"[{monitor.Id}] Failed to read VCP 0x{code:X2}, error code: {read.ErrorCode}");
            if (DdcErrorClassifier.IsPhysicalMonitorUnavailable(read.ErrorCode))
            {
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
            new KnownGoodVcpFeature
            {
                Code = code,
                Current = value.Current,
                Maximum = value.Maximum,
                Source = VcpObservationSource.CapabilitiesInitialization,
                LastSuccessfulUtc = _clock.UtcNow,
            });

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
