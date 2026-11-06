# PowerScripts package contract

Use a script plus required sibling descriptor:
`script.ps1` and `script.ps1.tool.json`, or Python equivalents.
The configured root and its immediate subfolders are scanned.
The descriptor's `name` is its identifier component; discovery returns the actual
opaque ID to use. `title` is a friendly display name.
Use a unique descriptor `name` across the catalogue, even for packages in
different folders. Duplicate names are rejected; choose one implementation of
an example or give separate implementations distinct names.

## Design primary data separately from options

Choose input from what the consumer can supply and output from what it can
present, not from the script's language or a guessed module category:

| Shape | Use it for | Specify and verify |
|---|---|---|
| `none` | A trigger with no primary data, or an action with no result | Named options may still exist. A no-result action is not an Advanced Paste transform. |
| `text` | Text content, including literal HTML markup | Strings, encoding, and meaningful empty-input/output behavior. A path-looking string remains text. |
| `files` | Selected paths or newly produced documents/media files | Extensions, min/max selection count, existing output paths, and separate outputs. One file is still a list. |
| `image` | Inline static image bytes | Accepted MIME types, scalar versus array, cardinality, and size limits. A path is not image data. |
| `audio` | Inline audio bytes | Accepted MIME types, scalar versus array, cardinality, and size limits. Video/large assets use files. |

Use parameters for options such as format, profile, or maximum length. Primary
input is not an option named `inputFile`. Declare user-picked auxiliary files
with `format: "file-path"` so the consumer and Linux runtime can handle them.
Check the chosen [consumer profile](consumers.md) before finalizing either schema.

## Descriptor and typed parameters

Both `inputSchema` and `outputSchema` are required closed JSON objects.
For a no-input/no-result action, the shape is:

```json
{
  "name": "ensure-state",
  "title": "Ensure requested state",
  "description": "Perform the reviewed action without primary input or result.",
  "inputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": {}
  },
  "outputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": {}
  }
}
```

Prefer the Host template generator to hand-authoring schemas. At most one
primary input/output is supported: `text`, `files`, `image`, or `audio`.
Named input options live in `properties.parameters`, another closed object.
Supported option types are string, 32-bit integer, and boolean; file options
are strings with `format: "file-path"`. Required means present; use `minLength`
to reject empty text. Defaults retain JSON types. Avoid PowerShell automatic
variable names. No `surface`, `kind`, audience, per-script MXC, runtime, or
module-target fields are allowed at descriptor root.

Parameters live in `inputSchema.properties.parameters.properties`; the enclosing
parameters node is a closed object. Its `required` list names required options.
For example, `{"type":"integer","minimum":1,"maximum":100,"default":10}`
declares a bounded integer, not a string containing a number. Supported string
constraints include `enum`, `minLength`, `maxLength`, and `pattern`.
Do not use arbitrary nested options, floating-point `number`, `null`, schema
references, unions, or composition; this is a constrained JSON Schema profile.

For primary files, put `minItems`/`maxItems` on the `files` array and extensions
on its string items:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "files": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "string",
        "x-powerscript": { "extensions": [".txt", ".md"] }
      }
    }
  },
  "required": ["files"]
}
```

Omitted `maxItems` is unbounded; schema `maxItems: 0` permits only an empty
array. This differs from creation options' `maxFiles: 0`, which means omit
`maxItems`. Omitted extensions or `["*"]` accepts all extensions; matching is
case-insensitive, not a guarantee of valid file content.

## Script transport and results

PowerShell scripts use normal `param(...)`, with `$Text`, `$Files`, `$Image`,
or `$Audio` for primary input and named arguments for options. Set UTF-8 output:

```powershell
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
```

Python reads one UTF-8 JSON request from stdin and starts at top level:

```python
import json
import sys
request = json.load(sys.stdin)
```

| Declared output | Script stdout |
|---|---|
| None | No result; put diagnostics on stderr |
| Text | Literal text |
| Files | Only JSON: `{"files":["absolute existing path"]}` |
| Image/audio | Only the schema-shaped JSON media object |

Use temporary workspace files for output, never overwrite selected originals
or write results into the installed package. The consumer owns saving/pasting
results. Sandbox side effects are not guaranteed to affect the host system.
Capabilities describe intent, not granted permissions.

### Worked example: text to text

This descriptor supports a simple text transform, including Advanced Paste
when compatible clipboard text is present:

```json
{
  "name": "uppercase-text",
  "title": "Uppercase text",
  "description": "Return the input text in uppercase.",
  "inputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": { "text": { "type": "string" } },
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

Choose one implementation, using its matching `.ps1`/`.py` descriptor filename.
These demonstrate transport, not a reason to change the user's Python mode.

```powershell
param([string]$Text)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::Out.Write($Text.ToUpperInvariant())
```

```python
import json
import sys

request = json.load(sys.stdin)
sys.stdout.write(request["text"].upper())
```

The script writes literal text. The Host wraps it as `{"text":"..."}` for
`transform`; do not add a second JSON wrapper in the script. Empty input yields
empty output in this example, which AP rejects as an empty result; decide and
document the desired task-specific behavior instead of hiding that failure.

### Worked example: files to a separate report file

Scaffold `runtime: python`, `input: files`, `output: files`, with the desired
input extension/count limits. Retain the generated descriptor and replace the
example task with this portable implementation:

```python
import json
import sys
import tempfile
from pathlib import Path

request = json.load(sys.stdin)
with tempfile.NamedTemporaryFile(
    mode="w", encoding="utf-8", suffix=".txt", delete=False
) as report:
    for selected in request["files"]:
        path = Path(selected)
        report.write(f"{path.name}\t{path.stat().st_size} bytes\n")
json.dump({"files": [report.name]}, sys.stdout)
```

The result file exists and is closed before its path is returned. `tempfile`
uses the Host-provided environment in Linux modes; no hardcoded Windows/WSL
paths or writes to selected originals are required. Operational failures must
remain failures; apply the [error handling guidance](error-handling.md) when
adding domain-specific diagnostics.

## Inline image and audio contracts

Use an object requiring `mimeType` and `data` under the primary media field.
For example, this is a complete image input/output envelope schema:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "image": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "mimeType": { "type": "string", "enum": ["image/png"] },
        "data": { "type": "string", "contentEncoding": "base64", "minLength": 1 }
      },
      "required": ["mimeType", "data"]
    }
  },
  "required": ["image"]
}
```

Its request/result shape is
`{"image":{"mimeType":"image/png","data":"<base64 PNG bytes>"}}`.
Audio uses `audio` instead of `image`. Supported image MIME types are
`image/png`, `image/jpeg`, and `image/webp` (static images); audio supports
`audio/wav`, `audio/mpeg`, and `audio/flac`. Do not claim that changing the MIME
label transcodes bytes.

For collections, wrap the media item schema in
`{"type":"array","minItems":1,"maxItems":2,"items":{...}}` under the primary
field. Preserve array shape even for one item; do not emit a scalar because
the current selection happened to contain one file.

Limits apply separately to input and output: 16 MiB decoded per item, 32 MiB
decoded total, at most 64 items. Base64 must be nonempty without whitespace and
the actual format must match the MIME declaration. Codec decoding failures
still need handling. Animated images, SVG, video, and large media use `files`.
Shared engine support does not imply a consumer has the necessary adapter.

## Metadata, icons, and isolation

Optional icons use a package-relative ICO:

```json
{ "x-powerscript": { "icon": "assets\\script.ico" } }
```

The ICO must be valid, at most 1 MiB, and inside the package without symlink,
junction, absolute-path, URL, or traversal escapes. Do not invent an image
conversion capability or fetch copyrighted artwork.

Preserve configured isolation. Linux managed-container mode requires compatible
MXC support and preparation; never silently substitute host/WSL execution.
Host VPN configuration, desktop interaction, and machine-level settings can be
unavailable under isolation. Explain feasibility before generating promises.
