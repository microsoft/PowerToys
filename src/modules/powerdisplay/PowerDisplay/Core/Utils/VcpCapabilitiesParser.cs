// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ManagedCommon;
using PowerDisplay.Core.Models;

namespace PowerDisplay.Core.Utils
{
    /// <summary>
    /// Parser for DDC/CI MCCS capabilities strings
    /// </summary>
    public static class VcpCapabilitiesParser
    {
        private static readonly char[] SpaceSeparator = new[] { ' ' };
        private static readonly char[] ValueSeparators = new[] { ' ', '(', ')' };

        /// <summary>
        /// Parse a capabilities string into structured VcpCapabilities
        /// </summary>
        /// <param name="capabilitiesString">Raw MCCS capabilities string</param>
        /// <returns>Parsed capabilities object, or Empty if parsing fails</returns>
        public static VcpCapabilities Parse(string? capabilitiesString)
        {
            if (string.IsNullOrWhiteSpace(capabilitiesString))
            {
                return VcpCapabilities.Empty;
            }

            try
            {
                var capabilities = new VcpCapabilities
                {
                    Raw = capabilitiesString,
                };

                // Extract model, type, protocol
                capabilities.Model = ExtractValue(capabilitiesString, "model");
                capabilities.Type = ExtractValue(capabilitiesString, "type");
                capabilities.Protocol = ExtractValue(capabilitiesString, "prot");

                // Extract supported commands
                capabilities.SupportedCommands = ParseCommandList(capabilitiesString);

                // Extract and parse VCP codes
                capabilities.SupportedVcpCodes = ParseVcpCodes(capabilitiesString);

                Logger.LogInfo($"Parsed capabilities: Model={capabilities.Model}, VCP Codes={capabilities.SupportedVcpCodes.Count}");

                return capabilities;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to parse capabilities string: {ex.Message}");
                return VcpCapabilities.Empty;
            }
        }

        /// <summary>
        /// Extract a simple value from capabilities string
        /// Example: "model(PD3220U)" -> "PD3220U"
        /// </summary>
        private static string? ExtractValue(string capabilities, string key)
        {
            try
            {
                var pattern = $@"{key}\(([^)]+)\)";
                var match = Regex.Match(capabilities, pattern, RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parse command list from capabilities string
        /// Example: "cmds(01 02 03 07 0C)" -> [0x01, 0x02, 0x03, 0x07, 0x0C]
        /// </summary>
        private static List<byte> ParseCommandList(string capabilities)
        {
            var commands = new List<byte>();

            try
            {
                var match = Regex.Match(capabilities, @"cmds\(([^)]+)\)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var cmdString = match.Groups[1].Value;
                    var cmdTokens = cmdString.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var token in cmdTokens)
                    {
                        if (byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cmd))
                        {
                            commands.Add(cmd);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to parse command list: {ex.Message}");
            }

            return commands;
        }

        /// <summary>
        /// Parse VCP codes section from capabilities string
        /// </summary>
        private static Dictionary<byte, VcpCodeInfo> ParseVcpCodes(string capabilities)
        {
            var vcpCodes = new Dictionary<byte, VcpCodeInfo>();

            try
            {
                // Find the "vcp(" section
                var vcpStart = capabilities.IndexOf("vcp(", StringComparison.OrdinalIgnoreCase);
                if (vcpStart < 0)
                {
                    Logger.LogWarning("No 'vcp(' section found in capabilities string");
                    return vcpCodes;
                }

                // Extract the complete VCP section by matching parentheses
                var vcpSection = ExtractVcpSection(capabilities, vcpStart + 4); // Skip "vcp("
                if (string.IsNullOrEmpty(vcpSection))
                {
                    return vcpCodes;
                }

                Logger.LogDebug($"Extracted VCP section: {vcpSection.Substring(0, Math.Min(100, vcpSection.Length))}...");

                // Parse VCP codes from the section
                ParseVcpCodesFromSection(vcpSection, vcpCodes);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to parse VCP codes: {ex.Message}");
            }

            return vcpCodes;
        }

        /// <summary>
        /// Extract VCP section by matching parentheses
        /// </summary>
        private static string ExtractVcpSection(string capabilities, int startIndex)
        {
            var depth = 1;
            var result = string.Empty;

            for (int i = startIndex; i < capabilities.Length && depth > 0; i++)
            {
                var ch = capabilities[i];

                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }

                result += ch;
            }

            return result;
        }

        /// <summary>
        /// Parse VCP codes from the extracted VCP section
        /// </summary>
        private static void ParseVcpCodesFromSection(string vcpSection, Dictionary<byte, VcpCodeInfo> vcpCodes)
        {
            var i = 0;

            while (i < vcpSection.Length)
            {
                // Skip whitespace
                while (i < vcpSection.Length && char.IsWhiteSpace(vcpSection[i]))
                {
                    i++;
                }

                if (i >= vcpSection.Length)
                {
                    break;
                }

                // Read VCP code (2 hex digits)
                if (i + 1 < vcpSection.Length &&
                    IsHexDigit(vcpSection[i]) &&
                    IsHexDigit(vcpSection[i + 1]))
                {
                    var codeStr = vcpSection.Substring(i, 2);
                    if (byte.TryParse(codeStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                    {
                        i += 2;

                        // Check if there are supported values (followed by '(')
                        while (i < vcpSection.Length && char.IsWhiteSpace(vcpSection[i]))
                        {
                            i++;
                        }

                        var supportedValues = new List<int>();

                        if (i < vcpSection.Length && vcpSection[i] == '(')
                        {
                            // Extract supported values
                            i++; // Skip '('
                            var valuesSection = ExtractVcpValuesSection(vcpSection, i);
                            i += valuesSection.Length + 1; // +1 for closing ')'

                            // Parse values
                            ParseVcpValues(valuesSection, supportedValues);
                        }

                        // Get VCP code name
                        var name = VcpCodeNames.GetName(code);

                        // Store VCP code info
                        vcpCodes[code] = new VcpCodeInfo(code, name, supportedValues);

                        Logger.LogDebug($"Parsed VCP code: 0x{code:X2} ({name}), Values: {supportedValues.Count}");
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Extract VCP values section by matching parentheses
        /// </summary>
        private static string ExtractVcpValuesSection(string section, int startIndex)
        {
            var depth = 1;
            var result = string.Empty;

            for (int i = startIndex; i < section.Length && depth > 0; i++)
            {
                var ch = section[i];

                if (ch == '(')
                {
                    depth++;
                    result += ch;
                }
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }

                    result += ch;
                }
                else
                {
                    result += ch;
                }
            }

            return result;
        }

        /// <summary>
        /// Parse VCP values from the values section
        /// </summary>
        private static void ParseVcpValues(string valuesSection, List<int> supportedValues)
        {
            var tokens = valuesSection.Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                // Try to parse as hex
                if (int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                {
                    supportedValues.Add(value);
                }
            }
        }

        /// <summary>
        /// Check if a character is a hex digit
        /// </summary>
        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'A' && c <= 'F') ||
                   (c >= 'a' && c <= 'f');
        }
    }
}
