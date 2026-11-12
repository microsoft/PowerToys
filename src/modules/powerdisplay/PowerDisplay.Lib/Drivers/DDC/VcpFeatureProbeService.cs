// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC
{
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
                if (observation.IsPhysicalMonitorUnavailable)
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
                    var value = new VcpFeatureValue((int)result.Current, 0, (int)result.Maximum);
                    var observation = value.IsValid
                        ? VcpProbeObservation.Success(code, value, attempt, lastError)
                        : VcpProbeObservation.Indeterminate(code, lastError, attempt, replied: true);

                    Logger.LogDebug(
                        $"DDC: [max-compat] VCP probe attempt " +
                        $"(handle=0x{handle:X}, code=0x{code:X2}, attempt={attempt}/{MaxAttempts}, " +
                        $"status={(value.IsValid ? "success" : "invalid-range")}, " +
                        $"current={result.Current}, maximum={result.Maximum})");

                    // A reply settles the only question this probe asks, so the remaining budget is
                    // not spent on the I2C bus even when the reported range is degenerate.
                    return Complete(handle, observation);
                }
                else
                {
                    lastError = result.ErrorCode;
                    Logger.LogDebug(
                        $"DDC: [max-compat] VCP probe attempt " +
                        $"(handle=0x{handle:X}, code=0x{code:X2}, attempt={attempt}/{MaxAttempts}, " +
                        $"status=failed, error={DdcErrorClassifier.Format(lastError)})");
                    if (!DdcErrorClassifier.IsTransient(result.ErrorCode))
                    {
                        break;
                    }
                }
            }

            // Every reply returns from inside the loop, so reaching here means the device never
            // answered: the retry budget was exhausted, a definitive refusal stopped it, or the
            // read threw.
            return Complete(
                handle,
                VcpProbeObservation.Indeterminate(code, lastError, attempts));
        }

        private static VcpProbeObservation Complete(IntPtr handle, VcpProbeObservation observation)
        {
            var status = observation.IsSuccess
                ? "success"
                : observation.IsPhysicalMonitorUnavailable
                    ? "physical-monitor-unavailable"
                    : "indeterminate";
            var message =
                $"DDC: [max-compat] VCP probe outcome " +
                $"(handle=0x{handle:X}, code=0x{observation.Code:X2}, attempts={observation.Attempts}, " +
                $"status={status}, replied={observation.Replied}, lastError={DdcErrorClassifier.Format(observation.LastError)})";

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

    internal readonly record struct VcpProbeObservation(
        byte Code,
        VcpFeatureValue Value,
        int Attempts,
        int? LastError,
        bool Replied = false)
    {
        public bool IsSuccess => Value.IsValid;

        /// <summary>
        /// Gets a value indicating whether the failure invalidated the physical-monitor handle
        /// itself, so no further request may be issued against it.
        /// </summary>
        public bool IsPhysicalMonitorUnavailable =>
            LastError is int errorCode && DdcErrorClassifier.IsPhysicalMonitorUnavailable(errorCode);

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
}
