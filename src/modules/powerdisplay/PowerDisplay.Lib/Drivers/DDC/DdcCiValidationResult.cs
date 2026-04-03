// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// DDC/CI validation result containing both validation status and cached capabilities data.
    /// This allows reusing capabilities data retrieved during validation, avoiding duplicate I2C calls.
    /// </summary>
    public struct DdcCiValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the monitor has a valid DDC/CI connection with brightness support.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the raw capabilities string retrieved during validation.
        /// Null if retrieval failed.
        /// </summary>
        public string? CapabilitiesString { get; }

        /// <summary>
        /// Gets the parsed VCP capabilities info retrieved during validation.
        /// Null if parsing failed.
        /// </summary>
        public Models.VcpCapabilities? VcpCapabilitiesInfo { get; }

        /// <summary>
        /// Gets a value indicating whether capabilities retrieval was attempted.
        /// True means the result is from an actual attempt (success or failure).
        /// </summary>
        public bool WasAttempted { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DdcCiValidationResult"/> struct.
        /// </summary>
        public DdcCiValidationResult(bool isValid, string? capabilitiesString = null, Models.VcpCapabilities? vcpCapabilitiesInfo = null, bool wasAttempted = true)
        {
            IsValid = isValid;
            CapabilitiesString = capabilitiesString;
            VcpCapabilitiesInfo = vcpCapabilitiesInfo;
            WasAttempted = wasAttempted;
        }

        /// <summary>
        /// Gets an invalid validation result with no cached data.
        /// </summary>
        public static DdcCiValidationResult Invalid => new(false, null, null, true);

        /// <summary>
        /// Gets a result indicating validation was not attempted yet.
        /// </summary>
        public static DdcCiValidationResult NotAttempted => new(false, null, null, false);
    }
}
