// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using ManagedCommon;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Recursive descent parser for DDC/CI MCCS capabilities strings.
    ///
    /// MCCS Capabilities String Grammar (BNF):
    /// <code>
    /// capabilities     ::= '(' segment* ')'
    /// segment          ::= identifier '(' segment_content ')'
    /// segment_content  ::= text | vcp_entries | hex_list
    /// vcp_entries      ::= vcp_entry*
    /// vcp_entry        ::= hex_byte [ '(' hex_list ')' ]
    /// hex_list         ::= hex_byte*
    /// hex_byte         ::= [0-9A-Fa-f]{2}
    /// identifier       ::= [a-z_]+
    /// text             ::= [^()]+
    /// </code>
    ///
    /// Example input:
    /// (prot(monitor)type(lcd)model(PD3220U)cmds(01 02 03)vcp(10 12 14(04 05) 60(11 12))mccs_ver(2.2))
    /// </summary>
    public ref struct MccsCapabilitiesParser
    {
        private readonly List<ParseError> _errors;
        private ReadOnlySpan<char> _input;
        private int _position;

        /// <summary>
        /// Parse a capabilities string into structured VcpCapabilities.
        /// </summary>
        /// <param name="capabilitiesString">Raw MCCS capabilities string</param>
        /// <returns>Parsed capabilities object with any parse errors</returns>
        public static MccsParseResult Parse(string? capabilitiesString)
        {
            if (string.IsNullOrWhiteSpace(capabilitiesString))
            {
                return new MccsParseResult(VcpCapabilities.Empty, new List<ParseError>());
            }

            var parser = new MccsCapabilitiesParser(capabilitiesString);
            return parser.ParseCapabilities();
        }

        private MccsCapabilitiesParser(string input)
        {
            _input = input.AsSpan();
            _position = 0;
            _errors = new List<ParseError>();
        }

        /// <summary>
        /// Main entry point: parse the entire capabilities string.
        /// capabilities ::= '(' segment* ')' | segment*
        /// </summary>
        private MccsParseResult ParseCapabilities()
        {
            var capabilities = new VcpCapabilities
            {
                Raw = _input.ToString(),
            };

            SkipWhitespace();

            // Handle optional outer parentheses (some monitors omit them)
            bool hasOuterParens = Peek() == '(';
            if (hasOuterParens)
            {
                Advance(); // consume '('
            }

            // Parse segments until end or closing paren
            while (!IsAtEnd())
            {
                SkipWhitespace();

                if (IsAtEnd())
                {
                    break;
                }

                if (Peek() == ')')
                {
                    if (hasOuterParens)
                    {
                        Advance(); // consume closing ')'
                    }

                    break;
                }

                // Parse a segment: identifier(content)
                var segment = ParseSegment();
                if (segment.HasValue)
                {
                    ApplySegment(capabilities, segment.Value);
                }
            }

            return new MccsParseResult(capabilities, _errors);
        }

        /// <summary>
        /// Parse a single segment: identifier '(' content ')'
        /// </summary>
        private ParsedSegment? ParseSegment()
        {
            SkipWhitespace();

            int startPos = _position;

            // Parse identifier
            var identifier = ParseIdentifier();
            if (identifier.IsEmpty)
            {
                // Not a valid segment start - skip this character and continue
                if (!IsAtEnd())
                {
                    Advance();
                }

                return null;
            }

            SkipWhitespace();

            // Expect '('
            if (Peek() != '(')
            {
                AddError($"Expected '(' after identifier '{identifier.ToString()}' at position {_position}");
                return null;
            }

            Advance(); // consume '('

            // Parse content until matching ')'
            var content = ParseBalancedContent();

            // Expect ')'
            if (Peek() != ')')
            {
                AddError($"Expected ')' to close segment '{identifier.ToString()}' at position {_position}");
            }
            else
            {
                Advance(); // consume ')'
            }

            return new ParsedSegment(identifier.ToString(), content);
        }

        /// <summary>
        /// Parse content between balanced parentheses.
        /// Handles nested parentheses correctly.
        /// </summary>
        private string ParseBalancedContent()
        {
            int start = _position;
            int depth = 1;

            while (!IsAtEnd() && depth > 0)
            {
                char c = Peek();
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break; // Don't consume the closing paren
                    }
                }

                Advance();
            }

            return _input.Slice(start, _position - start).ToString();
        }

        /// <summary>
        /// Parse an identifier (letters, digits, and underscores).
        /// identifier ::= [a-zA-Z0-9_]+
        /// Note: MCCS uses identifiers like window1, window2, etc.
        /// </summary>
        private ReadOnlySpan<char> ParseIdentifier()
        {
            int start = _position;

            while (!IsAtEnd() && IsIdentifierChar(Peek()))
            {
                Advance();
            }

            return _input.Slice(start, _position - start);
        }

        /// <summary>
        /// Apply a parsed segment to the capabilities object.
        /// </summary>
        private void ApplySegment(VcpCapabilities capabilities, ParsedSegment segment)
        {
            switch (segment.Name.ToLowerInvariant())
            {
                case "prot":
                    capabilities.Protocol = segment.Content.Trim();
                    break;

                case "type":
                    capabilities.Type = segment.Content.Trim();
                    break;

                case "model":
                    capabilities.Model = segment.Content.Trim();
                    break;

                case "mccs_ver":
                    capabilities.MccsVersion = segment.Content.Trim();
                    break;

                case "cmds":
                    capabilities.SupportedCommands = ParseHexList(segment.Content);
                    break;

                case "vcp":
                    capabilities.SupportedVcpCodes = ParseVcpEntries(segment.Content);
                    break;

                case "vcpname":
                    ParseVcpNames(segment.Content, capabilities);
                    break;

                default:
                    // Check for windowN pattern (window1, window2, etc.)
                    if (segment.Name.Length > 6 &&
                        segment.Name.StartsWith("window", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(segment.Name.AsSpan(6), out int windowNum))
                    {
                        var windowParser = new WindowParser(segment.Content);
                        var windowCap = windowParser.Parse(windowNum);
                        capabilities.Windows.Add(windowCap);
                    }
                    else
                    {
                        // Unknown segments are silently ignored
                    }

                    break;
            }
        }

        /// <summary>
        /// Parse VCP entries: vcp_entry*
        /// vcp_entry ::= hex_byte [ '(' hex_list ')' ]
        /// </summary>
        private Dictionary<byte, VcpCodeInfo> ParseVcpEntries(string content)
        {
            var vcpCodes = new Dictionary<byte, VcpCodeInfo>();
            var parser = new VcpEntryParser(content);

            while (parser.TryParseEntry(out var entry))
            {
                var name = VcpNames.GetCodeName(entry.Code);
                vcpCodes[entry.Code] = new VcpCodeInfo(entry.Code, name, entry.Values);
            }

            return vcpCodes;
        }

        /// <summary>
        /// Parse a hex byte list: hex_byte*
        /// Handles both space-separated (01 02 03) and concatenated (010203) formats.
        /// </summary>
        private static List<byte> ParseHexList(string content)
        {
            var result = new List<byte>();
            var span = content.AsSpan();
            int i = 0;

            while (i < span.Length)
            {
                // Skip whitespace
                while (i < span.Length && char.IsWhiteSpace(span[i]))
                {
                    i++;
                }

                if (i >= span.Length)
                {
                    break;
                }

                // Try to read two hex digits
                if (i + 1 < span.Length && IsHexDigit(span[i]) && IsHexDigit(span[i + 1]))
                {
                    if (byte.TryParse(span.Slice(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                    {
                        result.Add(value);
                    }

                    i += 2;
                }
                else
                {
                    i++; // Skip invalid character
                }
            }

            return result;
        }

        /// <summary>
        /// Parse vcpname entries: hex_byte '(' name ')'
        /// </summary>
        private void ParseVcpNames(string content, VcpCapabilities capabilities)
        {
            // vcpname format: F0(Custom Name 1) F1(Custom Name 2)
            var parser = new VcpNameParser(content);

            while (parser.TryParseEntry(out var code, out var name))
            {
                if (capabilities.SupportedVcpCodes.TryGetValue(code, out var existingInfo))
                {
                    // Update existing entry with custom name
                    capabilities.SupportedVcpCodes[code] = new VcpCodeInfo(code, name, existingInfo.SupportedValues);
                }
                else
                {
                    // Add new entry with custom name
                    capabilities.SupportedVcpCodes[code] = new VcpCodeInfo(code, name, Array.Empty<int>());
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek() => IsAtEnd() ? '\0' : _input[_position];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance() => _position++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtEnd() => _position >= _input.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipWhitespace()
        {
            while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
            {
                Advance();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIdentifierChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

        private void AddError(string message)
        {
            _errors.Add(new ParseError(_position, message));
            Logger.LogWarning($"[MccsParser] {message}");
        }
    }

    /// <summary>
    /// Sub-parser for VCP entries within the vcp() segment.
    /// </summary>
    internal ref struct VcpEntryParser
    {
        private ReadOnlySpan<char> _content;
        private int _position;

        public VcpEntryParser(string content)
        {
            _content = content.AsSpan();
            _position = 0;
        }

        /// <summary>
        /// Try to parse the next VCP entry.
        /// vcp_entry ::= hex_byte [ '(' hex_list ')' ]
        /// </summary>
        public bool TryParseEntry(out VcpEntry entry)
        {
            entry = default;
            SkipWhitespace();

            if (IsAtEnd())
            {
                return false;
            }

            // Parse hex byte (VCP code)
            if (!TryParseHexByte(out var code))
            {
                // Skip invalid character and try again
                _position++;
                return TryParseEntry(out entry);
            }

            var values = new List<int>();

            SkipWhitespace();

            // Check for optional value list
            if (!IsAtEnd() && Peek() == '(')
            {
                _position++; // consume '('

                // Parse values until ')'
                while (!IsAtEnd() && Peek() != ')')
                {
                    SkipWhitespace();

                    if (Peek() == ')')
                    {
                        break;
                    }

                    if (TryParseHexByte(out var value))
                    {
                        values.Add(value);
                    }
                    else
                    {
                        _position++; // Skip invalid character
                    }
                }

                if (!IsAtEnd() && Peek() == ')')
                {
                    _position++; // consume ')'
                }
            }

            entry = new VcpEntry(code, values);
            return true;
        }

        private bool TryParseHexByte(out byte value)
        {
            value = 0;

            if (_position + 1 >= _content.Length)
            {
                return false;
            }

            if (!IsHexDigit(_content[_position]) || !IsHexDigit(_content[_position + 1]))
            {
                return false;
            }

            if (byte.TryParse(_content.Slice(_position, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                _position += 2;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek() => IsAtEnd() ? '\0' : _content[_position];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtEnd() => _position >= _content.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipWhitespace()
        {
            while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
            {
                _position++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }

    /// <summary>
    /// Sub-parser for vcpname entries.
    /// </summary>
    internal ref struct VcpNameParser
    {
        private ReadOnlySpan<char> _content;
        private int _position;

        public VcpNameParser(string content)
        {
            _content = content.AsSpan();
            _position = 0;
        }

        /// <summary>
        /// Try to parse the next vcpname entry.
        /// vcpname_entry ::= hex_byte '(' name ')'
        /// </summary>
        public bool TryParseEntry(out byte code, out string name)
        {
            code = 0;
            name = string.Empty;

            SkipWhitespace();

            if (IsAtEnd())
            {
                return false;
            }

            // Parse hex byte
            if (!TryParseHexByte(out code))
            {
                _position++;
                return TryParseEntry(out code, out name);
            }

            SkipWhitespace();

            // Expect '('
            if (IsAtEnd() || Peek() != '(')
            {
                return false;
            }

            _position++; // consume '('

            // Parse name until ')'
            int start = _position;
            while (!IsAtEnd() && Peek() != ')')
            {
                _position++;
            }

            name = _content.Slice(start, _position - start).ToString().Trim();

            if (!IsAtEnd() && Peek() == ')')
            {
                _position++; // consume ')'
            }

            return true;
        }

        private bool TryParseHexByte(out byte value)
        {
            value = 0;

            if (_position + 1 >= _content.Length)
            {
                return false;
            }

            if (!IsHexDigit(_content[_position]) || !IsHexDigit(_content[_position + 1]))
            {
                return false;
            }

            if (byte.TryParse(_content.Slice(_position, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                _position += 2;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek() => IsAtEnd() ? '\0' : _content[_position];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtEnd() => _position >= _content.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipWhitespace()
        {
            while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
            {
                _position++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }

    /// <summary>
    /// Sub-parser for window segment content.
    /// Parses: type(PIP) area(25 25 1895 1175) max(640 480) min(10 10) window(10)
    /// </summary>
    internal ref struct WindowParser
    {
        private ReadOnlySpan<char> _content;
        private int _position;

        public WindowParser(string content)
        {
            _content = content.AsSpan();
            _position = 0;
        }

        /// <summary>
        /// Parse window segment content into a WindowCapability.
        /// </summary>
        public WindowCapability Parse(int windowNumber)
        {
            string type = string.Empty;
            var area = default(WindowArea);
            var maxSize = default(WindowSize);
            var minSize = default(WindowSize);
            int windowId = 0;

            // Parse sub-segments: type(...) area(...) max(...) min(...) window(...)
            while (!IsAtEnd())
            {
                SkipWhitespace();
                if (IsAtEnd())
                {
                    break;
                }

                var subSegment = ParseSubSegment();
                if (subSegment.HasValue)
                {
                    switch (subSegment.Value.Name.ToLowerInvariant())
                    {
                        case "type":
                            type = subSegment.Value.Content.Trim();
                            break;
                        case "area":
                            area = ParseArea(subSegment.Value.Content);
                            break;
                        case "max":
                            maxSize = ParseSize(subSegment.Value.Content);
                            break;
                        case "min":
                            minSize = ParseSize(subSegment.Value.Content);
                            break;
                        case "window":
                            _ = int.TryParse(subSegment.Value.Content.Trim(), out windowId);
                            break;
                    }
                }
            }

            return new WindowCapability(windowNumber, type, area, maxSize, minSize, windowId);
        }

        private (string Name, string Content)? ParseSubSegment()
        {
            int start = _position;

            // Parse identifier
            while (!IsAtEnd() && IsIdentifierChar(Peek()))
            {
                _position++;
            }

            if (_position == start)
            {
                // No identifier found, skip character
                if (!IsAtEnd())
                {
                    _position++;
                }

                return null;
            }

            var name = _content.Slice(start, _position - start).ToString();

            SkipWhitespace();

            // Expect '('
            if (IsAtEnd() || Peek() != '(')
            {
                return null;
            }

            _position++; // consume '('

            // Parse content with balanced parentheses
            int contentStart = _position;
            int depth = 1;

            while (!IsAtEnd() && depth > 0)
            {
                char c = Peek();
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }

                _position++;
            }

            var content = _content.Slice(contentStart, _position - contentStart).ToString();

            if (!IsAtEnd() && Peek() == ')')
            {
                _position++; // consume ')'
            }

            return (name, content);
        }

        private static WindowArea ParseArea(string content)
        {
            var values = ParseIntList(content);
            if (values.Length >= 4)
            {
                return new WindowArea(values[0], values[1], values[2], values[3]);
            }

            return default;
        }

        private static WindowSize ParseSize(string content)
        {
            var values = ParseIntList(content);
            if (values.Length >= 2)
            {
                return new WindowSize(values[0], values[1]);
            }

            return default;
        }

        private static int[] ParseIntList(string content)
        {
            var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<int>(parts.Length);

            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out int value))
                {
                    result.Add(value);
                }
            }

            return result.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek() => IsAtEnd() ? '\0' : _content[_position];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsAtEnd() => _position >= _content.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipWhitespace()
        {
            while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
            {
                _position++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIdentifierChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
    }

    /// <summary>
    /// Represents a parsed segment from the capabilities string.
    /// </summary>
    internal readonly struct ParsedSegment
    {
        public string Name { get; }

        public string Content { get; }

        public ParsedSegment(string name, string content)
        {
            Name = name;
            Content = content;
        }
    }

    /// <summary>
    /// Represents a parsed VCP entry.
    /// </summary>
    internal readonly struct VcpEntry
    {
        public byte Code { get; }

        public IReadOnlyList<int> Values { get; }

        public VcpEntry(byte code, IReadOnlyList<int> values)
        {
            Code = code;
            Values = values;
        }
    }

    /// <summary>
    /// Represents a parse error with position information.
    /// </summary>
    public readonly struct ParseError
    {
        public int Position { get; }

        public string Message { get; }

        public ParseError(int position, string message)
        {
            Position = position;
            Message = message;
        }

        public override string ToString() => $"[{Position}] {Message}";
    }

    /// <summary>
    /// Result of parsing MCCS capabilities string.
    /// </summary>
    public sealed class MccsParseResult
    {
        public VcpCapabilities Capabilities { get; }

        public IReadOnlyList<ParseError> Errors { get; }

        public bool HasErrors => Errors.Count > 0;

        public bool IsValid => !HasErrors && Capabilities.SupportedVcpCodes.Count > 0;

        public MccsParseResult(VcpCapabilities capabilities, IReadOnlyList<ParseError> errors)
        {
            Capabilities = capabilities;
            Errors = errors;
        }
    }
}
