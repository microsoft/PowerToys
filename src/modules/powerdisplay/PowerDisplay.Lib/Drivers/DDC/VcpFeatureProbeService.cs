// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC
{
    internal readonly record struct VcpReadAttempt(bool IsSuccess, uint Current, uint Maximum, int ErrorCode)
    {
        public static VcpReadAttempt Success(uint current, uint maximum) => new(true, current, maximum, 0);

        public static VcpReadAttempt Failure(int errorCode) => new(false, 0, 0, errorCode);
    }

    internal interface IVcpFeatureReader
    {
        VcpReadAttempt Read(IntPtr handle, byte code);
    }

    internal sealed class NativeVcpFeatureReader : IVcpFeatureReader
    {
        public VcpReadAttempt Read(IntPtr handle, byte code) => DdcCiNative.ReadVcpFeature(handle, code);
    }

    internal readonly record struct VcpProbeObservation(byte Code, VcpFeatureValue Value, int? ErrorCode)
    {
        public bool IsSuccess => Value.IsValid;

        public static VcpProbeObservation Success(byte code, VcpFeatureValue value) => new(code, value, null);

        public static VcpProbeObservation Indeterminate(byte code, int? errorCode) =>
            new(code, VcpFeatureValue.Invalid, errorCode);
    }

    internal sealed class VcpFeatureProbeService
    {
        internal static readonly TimeSpan TransactionInterval = TimeSpan.FromMilliseconds(100);
        private const int MaxAttempts = 3;
        private static readonly byte[] DefaultCodes = { VcpCodeBrightness, VcpCodeContrast, VcpCodeVolume };

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
            _codes = codes ?? DefaultCodes;
        }

        public async Task<IReadOnlyDictionary<byte, VcpProbeObservation>> ProbeAsync(
            IntPtr handle,
            CancellationToken cancellationToken)
        {
            var observations = new Dictionary<byte, VcpProbeObservation>();

            foreach (var code in _codes)
            {
                observations[code] = await ProbeCodeAsync(handle, code, cancellationToken);
            }

            return observations;
        }

        private async Task<VcpProbeObservation> ProbeCodeAsync(
            IntPtr handle,
            byte code,
            CancellationToken cancellationToken)
        {
            int? lastError = null;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _delayAsync(TransactionInterval, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var result = _reader.Read(handle, code);
                if (result.IsSuccess)
                {
                    var value = new VcpFeatureValue((int)result.Current, 0, (int)result.Maximum);
                    if (value.IsValid)
                    {
                        return VcpProbeObservation.Success(code, value);
                    }
                }
                else
                {
                    lastError = result.ErrorCode;
                    if (!IsTransient(result.ErrorCode))
                    {
                        break;
                    }
                }
            }

            return VcpProbeObservation.Indeterminate(code, lastError);
        }

        internal static bool IsTransient(int errorCode) => errorCode is
            unchecked((int)0xC0262582) or
            unchecked((int)0xC0262583) or
            unchecked((int)0xC0262585) or
            unchecked((int)0xC0262588) or
            unchecked((int)0xC0262589) or
            unchecked((int)0xC026258A) or
            unchecked((int)0xC026258B) or
            1460;
    }
}
