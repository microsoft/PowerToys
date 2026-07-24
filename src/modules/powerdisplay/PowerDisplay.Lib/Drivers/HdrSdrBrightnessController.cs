// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using PowerDisplay.Models;
using Windows.Win32.Foundation;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Writes the Windows HDR SDR-reference-white setting for active display targets.
    /// </summary>
    internal sealed class HdrSdrBrightnessController
    {
        private readonly Dictionary<string, DisplayTarget> _targets = new(MonitorIdComparer.Instance);

        public void UpdateTargets(IEnumerable<MonitorDisplayInfo> targets)
        {
            _targets.Clear();

            foreach (var target in targets)
            {
                if (!target.IsHdrEnabled || !target.SdrContentBrightness.HasValue)
                {
                    continue;
                }

                var monitorId = MonitorIdentity.FromDevicePath(target.DevicePath);
                if (!string.IsNullOrEmpty(monitorId))
                {
                    _targets[monitorId] = new DisplayTarget(target.AdapterId, target.TargetId);
                }
            }
        }

        public unsafe MonitorOperationResult SetSdrContentBrightness(string monitorId, int percentage)
        {
            if (!_targets.TryGetValue(monitorId, out var target))
            {
                return MonitorOperationResult.Failure(
                    "HDR is not active or SDR content brightness is unavailable for this display.");
            }

            var request = new DisplayConfigSetSdrWhiteLevel
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DisplayconfigDeviceInfoSetSdrWhiteLevel,
                    Size = (uint)sizeof(DisplayConfigSetSdrWhiteLevel),
                    AdapterId = target.AdapterId,
                    Id = target.TargetId,
                },
                SdrWhiteLevel = SdrContentBrightnessLevel.ToRaw(percentage),
                FinalValue = 1,
            };

            var result = DisplayConfigSetDeviceInfo(&request);
            if (result == 0)
            {
                return MonitorOperationResult.Success();
            }

            Logger.LogWarning(
                $"[HDR] DisplayConfigSetDeviceInfo failed for '{monitorId}' with Win32 error {result}");
            return MonitorOperationResult.Failure("Failed to set SDR content brightness.", result);
        }

        private readonly record struct DisplayTarget(LUID AdapterId, uint TargetId);
    }
}
