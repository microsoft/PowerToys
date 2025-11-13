// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerDisplay.Core.Models
{
    /// <summary>
    /// DDC/CI VCP capabilities information
    /// </summary>
    public class VcpCapabilities
    {
        /// <summary>
        /// Raw capabilities string (MCCS format)
        /// </summary>
        public string Raw { get; set; } = string.Empty;

        /// <summary>
        /// Monitor model name from capabilities
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Monitor type from capabilities (e.g., "LCD")
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// MCCS protocol version
        /// </summary>
        public string? Protocol { get; set; }

        /// <summary>
        /// Supported command codes
        /// </summary>
        public List<byte> SupportedCommands { get; set; } = new();

        /// <summary>
        /// Supported VCP codes with their information
        /// </summary>
        public Dictionary<byte, VcpCodeInfo> SupportedVcpCodes { get; set; } = new();

        /// <summary>
        /// Check if a specific VCP code is supported
        /// </summary>
        public bool SupportsVcpCode(byte code) => SupportedVcpCodes.ContainsKey(code);

        /// <summary>
        /// Get VCP code information
        /// </summary>
        public VcpCodeInfo? GetVcpCodeInfo(byte code)
        {
            return SupportedVcpCodes.TryGetValue(code, out var info) ? info : null;
        }

        /// <summary>
        /// Check if a VCP code supports discrete values
        /// </summary>
        public bool HasDiscreteValues(byte code)
        {
            var info = GetVcpCodeInfo(code);
            return info?.HasDiscreteValues ?? false;
        }

        /// <summary>
        /// Get supported values for a VCP code
        /// </summary>
        public IReadOnlyList<int>? GetSupportedValues(byte code)
        {
            return GetVcpCodeInfo(code)?.SupportedValues;
        }

        /// <summary>
        /// Creates an empty capabilities object
        /// </summary>
        public static VcpCapabilities Empty => new();

        public override string ToString()
        {
            return $"Model: {Model}, VCP Codes: {SupportedVcpCodes.Count}";
        }
    }

    /// <summary>
    /// Information about a single VCP code
    /// </summary>
    public readonly struct VcpCodeInfo
    {
        /// <summary>
        /// VCP code (e.g., 0x10 for brightness)
        /// </summary>
        public byte Code { get; }

        /// <summary>
        /// Human-readable name of the VCP code
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Supported discrete values (empty if continuous range)
        /// </summary>
        public IReadOnlyList<int> SupportedValues { get; }

        /// <summary>
        /// Whether this VCP code has discrete values
        /// </summary>
        public bool HasDiscreteValues => SupportedValues.Count > 0;

        /// <summary>
        /// Whether this VCP code supports a continuous range
        /// </summary>
        public bool IsContinuous => SupportedValues.Count == 0;

        public VcpCodeInfo(byte code, string name, IReadOnlyList<int>? supportedValues = null)
        {
            Code = code;
            Name = name;
            SupportedValues = supportedValues ?? Array.Empty<int>();
        }

        public override string ToString()
        {
            if (HasDiscreteValues)
            {
                return $"0x{Code:X2} ({Name}): {string.Join(", ", SupportedValues)}";
            }

            return $"0x{Code:X2} ({Name}): Continuous";
        }
    }
}
