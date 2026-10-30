# Greeting template

Edit `script.ps1` and `script.ps1.tool.json`, then refresh the catalogue.
Creation never executes code, approves trust, or changes isolation.

`name` is the unique action ID; `title` and `description` provide display text.
Both schemas are required closed objects (`additionalProperties: false`).
There is no primary input. Named options are under
`inputSchema.properties.parameters`, with their own `properties` and `required`.
Options support strings, integers, booleans, string `enum` choices, and strings
with `format: "file-path"` for consumer-owned file pickers.
Defaults, integer bounds, and string constraints are validated before execution.

`outputSchema.properties.text` declares literal stdout, not a JSON-encoded string.
PowerShell receives native `param(...)` arguments. UTF-8 output is explicit.
The `.ps1` extension selects PowerShell; no runtime or function metadata is needed.
Python scripts instead execute top-level statements and read JSON from stdin.

All scripts use the global MXC policy. There are no per-script settings.
Capability declarations describe intent and do not grant permissions.
Optional root `x-powerscript` metadata includes `publisher`, `version`,
`source`, and a package-relative `.ico` `icon`.
Script or descriptor edits invalidate trust and require renewed consent.
