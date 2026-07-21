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

    public void Initialize(Monitor monitor, IntPtr handle, VcpDiscoveryEvidence evidence)
    {
        InitializeFeature(monitor, handle, evidence, VcpCodeBrightness);
        InitializeFeature(monitor, handle, evidence, VcpCodeContrast);
        InitializeFeature(monitor, handle, evidence, VcpCodeVolume);
    }

    private void InitializeFeature(
        Monitor monitor,
        IntPtr handle,
        VcpDiscoveryEvidence evidence,
        byte code)
    {
        if (!IsSupported(monitor, code))
        {
            return;
        }

        if (evidence.InitialValues.TryGetValue(code, out var initial))
        {
            ApplyValue(monitor, code, initial.Value, markAsRead: initial.IsLive);
            return;
        }

        var read = _reader.Read(handle, code);
        if (!read.IsSuccess)
        {
            Logger.LogError($"[{monitor.Id}] Failed to read VCP 0x{code:X2}, error code: {read.ErrorCode}");
            return;
        }

        var value = new VcpFeatureValue((int)read.Current, 0, (int)read.Maximum);
        if (!value.IsValid)
        {
            Logger.LogWarning(
                $"DDC: [{monitor.Id}] Ignoring invalid {VcpNames.GetCodeName(code).ToLowerInvariant()} " +
                $"range current={read.Current}, max={read.Maximum}");
            return;
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
