// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    internal enum VcpProbeDisposition
    {
        Success,
        Indeterminate,
        PhysicalMonitorUnavailable,
    }

    internal readonly record struct VcpReadAttempt(bool IsSuccess, uint Current, uint Maximum, int ErrorCode)
    {
        public static VcpReadAttempt Success(uint current, uint maximum) => new(true, current, maximum, 0);

        public static VcpReadAttempt Failure(int errorCode) => new(false, 0, 0, errorCode);
    }

    internal interface IVcpFeatureReader
    {
        VcpReadAttempt Read(IntPtr handle, byte code);
    }

    /// <summary>
    /// The production <see cref="IVcpFeatureReader"/>: one <c>GetVCPFeatureAndVCPFeatureReply</c>
    /// transaction, with no retry, pacing or logging. Those belong to the callers —
    /// <see cref="VcpFeatureProbeService"/> and <see cref="ContinuousVcpInitializer"/>.
    /// </summary>
    internal sealed class NativeVcpFeatureReader : IVcpFeatureReader
    {
        public VcpReadAttempt Read(IntPtr handle, byte code) =>
            GetVCPFeatureAndVCPFeatureReply(handle, code, IntPtr.Zero, out uint current, out uint maximum)
                ? VcpReadAttempt.Success(current, maximum)
                : VcpReadAttempt.Failure(Marshal.GetLastWin32Error());
    }

    internal readonly record struct VcpProbeObservation(
        byte Code,
        VcpFeatureValue Value,
        int Attempts,
        int? LastError,
        bool Replied = false)
    {
        public bool IsSuccess => Value.IsValid;

        public VcpProbeDisposition Disposition => IsSuccess
            ? VcpProbeDisposition.Success
            : LastError is int errorCode && DdcErrorClassifier.IsPhysicalMonitorUnavailable(errorCode)
                ? VcpProbeDisposition.PhysicalMonitorUnavailable
                : VcpProbeDisposition.Indeterminate;

        public static VcpProbeObservation Success(
            byte code,
            VcpFeatureValue value,
            int attempts = 1,
            int? lastError = null) =>
            new(code, value, attempts, lastError, true);

        public static VcpProbeObservation Indeterminate(
            byte code,
            int? lastError,
            int attempts = 1,
            bool replied = false) =>
            new(code, VcpFeatureValue.Invalid, attempts, lastError, replied);
    }

    internal sealed class VcpFeatureProbeService
    {
        private static readonly TimeSpan TransactionInterval = TimeSpan.FromMilliseconds(100);
        private const int MaxAttempts = 3;

        private readonly IVcpFeatureReader _reader;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly IReadOnlyList<byte> _codes;

        public VcpFeatureProbeService(
            IVcpFeatureReader reader,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
            IReadOnlyList<byte>? codes = null)
        {
            _reader = reader;
            _delayAsync = delayAsync ?? Task.Delay;
            _codes = codes ?? ContinuousVcpCodes;
        }

        public async Task<IReadOnlyDictionary<byte, VcpProbeObservation>> ProbeAsync(
            IntPtr handle,
            CancellationToken cancellationToken)
        {
            var observations = new Dictionary<byte, VcpProbeObservation>();

            foreach (var code in _codes)
            {
                var observation = await ProbeCodeAsync(handle, code, cancellationToken).ConfigureAwait(false);
                observations[code] = observation;

                // These errors invalidate the physical-monitor handle, not just the current
                // VCP feature. Avoid issuing more I2C requests against a stale handle.
                if (observation.Disposition == VcpProbeDisposition.PhysicalMonitorUnavailable)
                {
                    break;
                }
            }

            return observations;
        }

        private async Task<VcpProbeObservation> ProbeCodeAsync(
            IntPtr handle,
            byte code,
            CancellationToken cancellationToken)
        {
            int? lastError = null;
            var attempts = 0;
            var replied = false;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _delayAsync(TransactionInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                attempts = attempt;

                VcpReadAttempt result;
                try
                {
                    result = await Task.Run(
                        () => _reader.Read(handle, code),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (
                    ex is not OperationCanceledException &&
                    ex is not OutOfMemoryException)
                {
                    // Containment belongs to the orchestration layer: a throwing read must cost the
                    // caller this one VCP code, not every monitor sharing the hMonitor via the
                    // pipeline-wide catch in DdcCiController.
                    Logger.LogError(
                        $"DDC: [max-compat] VCP probe threw " +
                        $"(handle=0x{handle:X}, code=0x{code:X2}, attempt={attempt}/{MaxAttempts}): {ex.Message}");
                    break;
                }

                if (result.IsSuccess)
                {
                    // The device answered this opcode. Unimplemented codes fail with
                    // DDCCI_VCP_NOT_SUPPORTED instead, so a reply proves support even when the
                    // reported range cannot scale a percentage.
                    replied = true;

                    var value = new VcpFeatureValue((int)result.Current, 0, (int)result.Maximum);
                    if (value.IsValid)
                    {
                        Logger.LogDebug(
                            $"DDC: [max-compat] VCP probe attempt " +
                            $"(handle=0x{handle:X}, code=0x{code:X2}, attempt={attempt}/{MaxAttempts}, " +
                            $"status=success, current={result.Current}, maximum={result.Maximum})");
                        return Complete(
                            handle,
                            VcpProbeObservation.Success(code, value, attempt, lastError));
                    }

                    Logger.LogDebug(
                        $"DDC: [max-compat] VCP probe attempt " +
                        $"(handle=0x{handle:X}, code=0x{code:X2}, attempt={attempt}/{MaxAttempts}, " +
                        $"status=invalid-range, current={result.Current}, maximum={result.Maximum})");
                }
                else
                {
                    lastError = result.ErrorCode;
                    Logger.LogDebug(
                        $"DDC: [max-compat] VCP probe attempt " +
                        $"(handle=0x{handle:X}, code=0x{code:X2}, attempt={attempt}/{MaxAttempts}, " +
                        $"status=failed, error={FormatError(lastError)})");
                    if (!DdcErrorClassifier.IsTransient(result.ErrorCode))
                    {
                        break;
                    }
                }
            }

            return Complete(
                handle,
                VcpProbeObservation.Indeterminate(code, lastError, attempts, replied));
        }

        private static VcpProbeObservation Complete(IntPtr handle, VcpProbeObservation observation)
        {
            var status = observation.Disposition switch
            {
                VcpProbeDisposition.Success => "success",
                VcpProbeDisposition.PhysicalMonitorUnavailable => "physical-monitor-unavailable",
                _ => "indeterminate",
            };
            var message =
                $"DDC: [max-compat] VCP probe outcome " +
                $"(handle=0x{handle:X}, code=0x{observation.Code:X2}, attempts={observation.Attempts}, " +
                $"status={status}, replied={observation.Replied}, lastError={FormatError(observation.LastError)})";

            if (observation.IsSuccess)
            {
                Logger.LogInfo(message);
            }
            else
            {
                Logger.LogWarning(message);
            }

            return observation;
        }

        private static string FormatError(int? errorCode) =>
            errorCode.HasValue ? $"0x{unchecked((uint)errorCode.Value):X8}" : "none";
    }
}
