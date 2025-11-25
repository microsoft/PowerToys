// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace PowerDisplay.Common.Models
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
        /// Get all VCP codes as hex strings, sorted by code value.
        /// </summary>
        /// <returns>List of hex strings like ["0x10", "0x12", "0x14"]</returns>
        public List<string> GetVcpCodesAsHexStrings()
        {
            var result = new List<string>(SupportedVcpCodes.Count);
            foreach (var kvp in SupportedVcpCodes)
            {
                result.Add($"0x{kvp.Key:X2}");
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// Get all VCP codes sorted by code value.
        /// </summary>
        /// <returns>Sorted list of VcpCodeInfo</returns>
        public IEnumerable<VcpCodeInfo> GetSortedVcpCodes()
        {
            var sortedKeys = new List<byte>(SupportedVcpCodes.Keys);
            sortedKeys.Sort();

            foreach (var key in sortedKeys)
            {
                yield return SupportedVcpCodes[key];
            }
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

        /// <summary>
        /// Gets the VCP code formatted as a hex string (e.g., "0x10").
        /// </summary>
        public string FormattedCode => $"0x{Code:X2}";

        /// <summary>
        /// Gets the VCP code formatted with its name (e.g., "Brightness (0x10)").
        /// </summary>
        public string FormattedTitle => $"{Name} ({FormattedCode})";

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
