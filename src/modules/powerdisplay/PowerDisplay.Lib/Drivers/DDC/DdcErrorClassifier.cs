// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Classifies the Win32 error codes <c>GetVCPFeatureAndVCPFeatureReply</c> reports, so a caller
    /// can tell a failure worth another I2C transaction from the device's final answer. Constant
    /// names mirror <c>winerror.h</c> exactly.
    /// </summary>
    /// <remarks>
    /// Consumed by <see cref="VcpFeatureProbeService"/>, which retries only what
    /// <see cref="IsTransient"/> admits, and by <see cref="ContinuousVcpInitializer"/>, which uses
    /// <see cref="IsPhysicalMonitorUnavailable"/> to tell a dead handle from one feature the device
    /// refused. The discrete-VCP reads in <c>DdcCiController.Initialize*</c> and the runtime value
    /// reads behind <c>DdcCiController.GetVcpFeatureAsync</c> are still single-shot and
    /// unclassified.
    /// </remarks>
    internal static class DdcErrorClassifier
    {
        internal const int ErrorGraphicsI2CErrorTransmittingData = unchecked((int)0xC0262582);
        internal const int ErrorGraphicsI2CErrorReceivingData = unchecked((int)0xC0262583);

        /// <summary>
        /// The device's final answer that it does not implement the opcode. Deliberately a member of
        /// neither classification below — it must not be retried, and it is not a handle-class failure —
        /// so it is named here rather than left as a magic number in the tests that pin that exclusion
        /// and in the discovery tests that drive a definitive refusal.
        /// </summary>
        internal const int ErrorGraphicsDdcCiVcpNotSupported = unchecked((int)0xC0262584);

        internal const int ErrorGraphicsDdcCiInvalidData = unchecked((int)0xC0262585);
        internal const int ErrorGraphicsMcaInternalError = unchecked((int)0xC0262588);
        internal const int ErrorGraphicsDdcCiInvalidMessageCommand = unchecked((int)0xC0262589);
        internal const int ErrorGraphicsDdcCiInvalidMessageLength = unchecked((int)0xC026258A);
        internal const int ErrorGraphicsDdcCiInvalidMessageChecksum = unchecked((int)0xC026258B);
        internal const int ErrorGraphicsInvalidPhysicalMonitorHandle = unchecked((int)0xC026258C);
        internal const int ErrorGraphicsMonitorNoLongerExists = unchecked((int)0xC026258D);

        // The doubled "CURRENT_CURRENT" is the SDK's own spelling, kept so the name greps against
        // winerror.h.
        internal const int ErrorGraphicsDdcCiCurrentCurrentValueGreaterThanMaximumValue =
            unchecked((int)0xC02625D8);

        internal const int ErrorTimeout = 1460;

        /// <summary>
        /// True when the error invalidates the physical-monitor handle itself rather than the one VCP
        /// feature, so no further request may be issued against it.
        /// </summary>
        public static bool IsPhysicalMonitorUnavailable(int errorCode) => errorCode is
            ErrorGraphicsInvalidPhysicalMonitorHandle or
            ErrorGraphicsMonitorNoLongerExists;

        /// <summary>
        /// True when the failure is a framing, arbitration or timing fault on the I2C bus that another
        /// attempt can plausibly get past.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Deliberate exclusions, all of which would burn the retry budget for nothing:
        /// <c>ERROR_GRAPHICS_DDCCI_VCP_NOT_SUPPORTED</c> (0xC0262584) is the device's final answer that
        /// it does not implement the opcode; <c>ERROR_GRAPHICS_I2C_NOT_SUPPORTED</c> (0xC0262580) and
        /// <c>ERROR_GRAPHICS_I2C_DEVICE_DOES_NOT_EXIST</c> (0xC0262581) are permanent bus-level facts;
        /// <c>ERROR_GRAPHICS_MCA_INVALID_CAPABILITIES_STRING</c> (0xC0262587) belongs to the
        /// capabilities path, not to a VCP read; and the two handle-class codes are owned by
        /// <see cref="IsPhysicalMonitorUnavailable"/>, which must abort rather than retry.
        /// <c>ERROR_GRAPHICS_DDCCI_MONITOR_RETURNED_INVALID_TIMING_STATUS_BYTE</c> (0xC0262586) reads
        /// like a sibling of the framing codes below but is raised only by the DDC/CI get-timing-report
        /// command, never by <c>GetVCPFeatureAndVCPFeatureReply</c>, so it is unreachable here.
        /// </para>
        /// <para>
        /// <c>ERROR_GRAPHICS_DDCCI_CURRENT_CURRENT_VALUE_GREATER_THAN_MAXIMUM_VALUE</c> (0xC02625D8) is
        /// included on purpose: a device that genuinely reports current &gt; maximum will keep doing so
        /// and simply exhausts the budget, but the same code also results from a corrupted reply, which
        /// a retry does fix.
        /// </para>
        /// </remarks>
        public static bool IsTransient(int errorCode) => errorCode is
            ErrorGraphicsI2CErrorTransmittingData or
            ErrorGraphicsI2CErrorReceivingData or
            ErrorGraphicsDdcCiInvalidData or
            ErrorGraphicsMcaInternalError or
            ErrorGraphicsDdcCiInvalidMessageCommand or
            ErrorGraphicsDdcCiInvalidMessageLength or
            ErrorGraphicsDdcCiInvalidMessageChecksum or
            ErrorGraphicsDdcCiCurrentCurrentValueGreaterThanMaximumValue or
            ErrorTimeout;

        /// <summary>
        /// Renders an error for a log line as the unsigned hex the SDK documents it under, so a
        /// reader can grep it against winerror.h. <c>Marshal.GetLastWin32Error</c> hands these back
        /// as a negative int, which matches nothing.
        /// </summary>
        public static string Format(int? errorCode) =>
            errorCode.HasValue ? $"0x{unchecked((uint)errorCode.Value):X8}" : "none";
    }
}
