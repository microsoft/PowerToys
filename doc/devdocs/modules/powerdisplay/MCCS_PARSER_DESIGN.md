# MCCS Capabilities String Parser - Recursive Descent Design

## Overview

This document describes the recursive descent parser implementation for DDC/CI MCCS (Monitor Control Command Set) capabilities strings.

## Grammar Definition (BNF)

```bnf
capabilities      ::= ['('] segment* [')']
segment           ::= identifier '(' segment_content ')'
segment_content   ::= text | vcp_entries | hex_list
vcp_entries       ::= vcp_entry*
vcp_entry         ::= hex_byte [ '(' hex_list ')' ]
hex_list          ::= hex_byte*
hex_byte          ::= [0-9A-Fa-f]{2}
identifier        ::= [a-z_A-Z]+
text              ::= [^()]+
```

## Example Input

```
(prot(monitor)type(lcd)model(PD3220U)cmds(01 02 03 07)vcp(10 12 14(04 05 06) 16 60(11 12 0F) DC DF)mccs_ver(2.2)vcpname(F0(Custom Setting)))
```

## Parser Architecture

### Component Hierarchy

```
MccsCapabilitiesParser (main parser)
├── ParseCapabilities()      → MccsParseResult
├── ParseSegment()           → ParsedSegment?
├── ParseBalancedContent()   → string
├── ParseIdentifier()        → ReadOnlySpan<char>
├── ApplySegment()           → void
│   ├── ParseHexList()       → List<byte>
│   ├── ParseVcpEntries()    → Dictionary<byte, VcpCodeInfo>
│   └── ParseVcpNames()      → void
│
├── VcpEntryParser (sub-parser for vcp() content)
│   └── TryParseEntry()      → VcpEntry
│
├── VcpNameParser (sub-parser for vcpname() content)
│   └── TryParseEntry()      → (byte code, string name)
│
└── WindowParser (sub-parser for windowN() content)
    ├── Parse()              → WindowCapability
    └── ParseSubSegment()    → (name, content)?
```

### Design Principles

1. **ref struct for Zero Allocation**
   - Main parser uses `ref struct` to avoid heap allocation
   - Works with `ReadOnlySpan<char>` for efficient string slicing
   - No intermediate string allocations during parsing

2. **Recursive Descent Pattern**
   - Each grammar rule has a corresponding parse method
   - Methods call each other recursively for nested structures
   - Single-character lookahead via `Peek()`

3. **Error Recovery**
   - Errors are accumulated, not thrown
   - Parser attempts to continue after errors
   - Returns partial results when possible

4. **Sub-parsers for Specialized Content**
   - `VcpEntryParser` for VCP code entries
   - `VcpNameParser` for custom VCP names
   - Each sub-parser handles its own grammar subset

## Parse Methods Detail

### ParseCapabilities()
Entry point. Handles optional outer parentheses and iterates through segments.

```csharp
private MccsParseResult ParseCapabilities()
{
    // Handle optional outer parens
    // while (!IsAtEnd()) { ParseSegment() }
    // Return result with accumulated errors
}
```

### ParseSegment()
Parses a single `identifier(content)` segment.

```csharp
private ParsedSegment? ParseSegment()
{
    // 1. ParseIdentifier()
    // 2. Expect '('
    // 3. ParseBalancedContent()
    // 4. Expect ')'
}
```

### ParseBalancedContent()
Extracts content between balanced parentheses, handling nested parens.

```csharp
private string ParseBalancedContent()
{
    int depth = 1;
    while (depth > 0) {
        if (char == '(') depth++;
        if (char == ')') depth--;
    }
}
```

### ParseVcpEntries()
Delegates to `VcpEntryParser` for the specialized VCP entry grammar.

```csharp
vcp_entry ::= hex_byte [ '(' hex_list ')' ]

Examples:
- "10"           → code=0x10, values=[]
- "14(04 05 06)" → code=0x14, values=[4, 5, 6]
- "60(11 12 0F)" → code=0x60, values=[0x11, 0x12, 0x0F]
```

## Comparison with Other Approaches

| Approach | Pros | Cons |
|----------|------|------|
| **Recursive Descent** (this) | Clear structure, handles nesting, extensible | More code |
| **Regex** (DDCSharp) | Concise | Hard to debug, limited nesting |
| **Mixed** (original) | Pragmatic | Inconsistent, hard to maintain |

## Performance Characteristics

- **Time Complexity**: O(n) where n = input length
- **Space Complexity**: O(1) for parsing + O(m) for output where m = number of VCP codes
- **Allocations**: Minimal - only for output structures

## Supported Segments

| Segment | Description | Parser |
|---------|-------------|--------|
| `prot(...)` | Protocol type | Direct assignment |
| `type(...)` | Display type (lcd/crt) | Direct assignment |
| `model(...)` | Model name | Direct assignment |
| `cmds(...)` | Supported commands | ParseHexList |
| `vcp(...)` | VCP code entries | VcpEntryParser |
| `mccs_ver(...)` | MCCS version | Direct assignment |
| `vcpname(...)` | Custom VCP names | VcpNameParser |
| `windowN(...)` | PIP/PBP window capabilities | WindowParser |

### Window Segment Format

The `windowN` segment (where N is 1, 2, 3, etc.) describes PIP/PBP window capabilities:

```
window1(type(PIP) area(25 25 1895 1175) max(640 480) min(10 10) window(10))
```

| Sub-field | Format | Description |
|-----------|--------|-------------|
| `type` | `type(PIP)` or `type(PBP)` | Window type (Picture-in-Picture or Picture-by-Picture) |
| `area` | `area(x1 y1 x2 y2)` | Window area coordinates in pixels |
| `max` | `max(width height)` | Maximum window dimensions |
| `min` | `min(width height)` | Minimum window dimensions |
| `window` | `window(id)` | Window identifier |

All sub-fields are optional; missing fields default to zero values.

## Error Handling

```csharp
public readonly struct ParseError
{
    public int Position { get; }    // Character position
    public string Message { get; }  // Human-readable error
}

public sealed class MccsParseResult
{
    public VcpCapabilities Capabilities { get; }
    public IReadOnlyList<ParseError> Errors { get; }
    public bool HasErrors => Errors.Count > 0;
    public bool IsValid => !HasErrors && Capabilities.SupportedVcpCodes.Count > 0;
}
```

## Usage Example

```csharp
// Parse capabilities string
var result = MccsCapabilitiesParser.Parse(capabilitiesString);

if (result.IsValid)
{
    var caps = result.Capabilities;
    Console.WriteLine($"Model: {caps.Model}");
    Console.WriteLine($"MCCS Version: {caps.MccsVersion}");
    Console.WriteLine($"VCP Codes: {caps.SupportedVcpCodes.Count}");
}

if (result.HasErrors)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Parse error at {error.Position}: {error.Message}");
    }
}
```

## Edge Cases Handled

1. **Missing outer parentheses** (Apple Cinema Display)
2. **No spaces between hex bytes** (`010203` vs `01 02 03`)
3. **Nested parentheses** in VCP values
4. **Unknown segments** (logged but not fatal)
5. **Malformed input** (partial results returned)
