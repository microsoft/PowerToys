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
        /// MCCS version (e.g., "2.2", "2.1")
        /// </summary>
        public string? MccsVersion { get; set; }

        /// <summary>
        /// Supported command codes
        /// </summary>
        public List<byte> SupportedCommands { get; set; } = new();

        /// <summary>
        /// Supported VCP codes with their information
        /// </summary>
        public Dictionary<byte, VcpCodeInfo> SupportedVcpCodes { get; set; } = new();

        /// <summary>
        /// Window capabilities for PIP/PBP support
        /// </summary>
        public List<WindowCapability> Windows { get; set; } = new();

        /// <summary>
        /// Check if display supports PIP/PBP windows
        /// </summary>
        public bool HasWindowSupport => Windows.Count > 0;

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

    /// <summary>
    /// Window size (width and height)
    /// </summary>
    public readonly struct WindowSize
    {
        /// <summary>
        /// Width in pixels
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Height in pixels
        /// </summary>
        public int Height { get; }

        public WindowSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString() => $"{Width}x{Height}";
    }

    /// <summary>
    /// Window area coordinates (top-left and bottom-right)
    /// </summary>
    public readonly struct WindowArea
    {
        /// <summary>
        /// Top-left X coordinate
        /// </summary>
        public int X1 { get; }

        /// <summary>
        /// Top-left Y coordinate
        /// </summary>
        public int Y1 { get; }

        /// <summary>
        /// Bottom-right X coordinate
        /// </summary>
        public int X2 { get; }

        /// <summary>
        /// Bottom-right Y coordinate
        /// </summary>
        public int Y2 { get; }

        /// <summary>
        /// Width of the area
        /// </summary>
        public int Width => X2 - X1;

        /// <summary>
        /// Height of the area
        /// </summary>
        public int Height => Y2 - Y1;

        public WindowArea(int x1, int y1, int x2, int y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public override string ToString() => $"({X1},{Y1})-({X2},{Y2})";
    }

    /// <summary>
    /// Window capability information for PIP/PBP displays
    /// </summary>
    public readonly struct WindowCapability
    {
        /// <summary>
        /// Window number (1, 2, 3, etc.)
        /// </summary>
        public int WindowNumber { get; }

        /// <summary>
        /// Window type (e.g., "PIP", "PBP")
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Window area coordinates
        /// </summary>
        public WindowArea Area { get; }

        /// <summary>
        /// Maximum window size
        /// </summary>
        public WindowSize MaxSize { get; }

        /// <summary>
        /// Minimum window size
        /// </summary>
        public WindowSize MinSize { get; }

        /// <summary>
        /// Window identifier
        /// </summary>
        public int WindowId { get; }

        public WindowCapability(
            int windowNumber,
            string type,
            WindowArea area,
            WindowSize maxSize,
            WindowSize minSize,
            int windowId)
        {
            WindowNumber = windowNumber;
            Type = type ?? string.Empty;
            Area = area;
            MaxSize = maxSize;
            MinSize = minSize;
            WindowId = windowId;
        }

        public override string ToString() =>
            $"Window{WindowNumber}: Type={Type}, Area={Area}, Max={MaxSize}, Min={MinSize}";
    }
}
