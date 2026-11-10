# Package and metadata syntax

Use `script.ps1` + `script.ps1.tool.json`, or `.py` equivalents.
Discovery scans the configured root and immediate subfolders.
Descriptor `name` must be unique catalogue-wide; use the returned opaque ID for
invocation, not the friendly `title`.

This is MCP-style tool metadata with a constrained JSON Schema profile, not an
MCP server. Prefer Host scaffolding over handwritten descriptors.

## Descriptor and options

Both schemas are required closed objects. Zero primary fields means `none`;
otherwise choose exactly one of `text`, `files`, `image`, `audio`.
Named options belong under `inputSchema.properties.parameters`, independently
of primary data. Example text transform with a typed option:

```json
{
  "name": "trim-text",
  "title": "Trim text",
  "description": "Trim leading and trailing whitespace.",
  "inputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": {
      "text": { "type": "string" },
      "parameters": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "limit": { "type": "integer", "minimum": 1, "maximum": 100, "default": 10 }
        },
        "required": ["limit"]
      }
    },
    "required": ["text"]
  },
  "outputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": { "text": { "type": "string" } },
    "required": ["text"]
  }
}
```

For no input/result, each schema is
`{"type":"object","additionalProperties":false,"properties":{}}`.
This describes shape only; implement every declared option.

| Option | Supported syntax |
|---|---|
| String | `type: "string"`; `enum`, `minLength`, `maxLength`, `pattern`, typed `default` |
| Integer | `type: "integer"` (32-bit); `minimum`, `maximum`, typed `default` |
| Boolean | `type: "boolean"`; typed `default` |
| Auxiliary file | String with `format: "file-path"`; consumer picker/Linux path translation |
| Required | Names in the parameters object's `required`; does not reject empty strings without `minLength` |

Keep false/zero/empty defaults typed. Avoid PowerShell automatic-variable names.
No arbitrary nested options, floating-point `number`, null, `$ref`, unions, or
composition. No root `surface`, `kind`, audience, runtime, module-target, or
per-script MXC fields.

## Primary files

Put counts on the array and extension constraints on string items:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "files": {
      "type": "array",
      "minItems": 1,
      "maxItems": 10,
      "items": {
        "type": "string",
        "x-powerscript": { "extensions": [".txt", ".md"] }
      }
    }
  },
  "required": ["files"]
}
```

Omitted `maxItems` is unlimited; schema `maxItems: 0` means empty only, unlike
creation's `maxFiles: 0`. Omitted extensions/`["*"]` accepts all; matching is
case-insensitive, not content validation. One file still uses an array.

## Script transport

PowerShell uses `param(...)`: `$Text`, `$Files`, `$Image`, `$Audio` for primary
input and named arguments for options. Set UTF-8:
`[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)`.
Python reads a UTF-8 stdin JSON object at top level with `json.load(sys.stdin)`;
options live in `request["parameters"]`.

| Output | Script stdout |
|---|---|
| None | No result; diagnostics on stderr |
| Text | Literal text, not a JSON wrapper |
| Files | `{"files":["absolute existing path"]}` only |
| Image/audio | Schema-shaped JSON; see [media](media.md) |

The Host wraps literal text for `transform`; do not double-wrap it.
Return closed workspace files, not selected originals or package writes.
Consumer owns saving/pasting. Read [examples](examples.md) only for code samples.

## Optional icon

```json
{ "x-powerscript": { "icon": "assets\\script.ico" } }
```

Valid package-relative ICO, at most 1 MiB. No symlink/junction, absolute/URL,
or traversal escape. Metadata describes intent, not sandbox permissions.
