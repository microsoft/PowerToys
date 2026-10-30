# File-output template

Edit `script.ps1` and its sibling `.tool.json`, then refresh the catalogue.
Creation never executes code, approves trust, or changes isolation.

`name`, `title`, and `description` identify and explain the action.
Both schemas are required closed objects (`additionalProperties: false`).
The empty `inputSchema.properties` means no primary input or named options.
`outputSchema.properties.files` is a required array of path strings.
Stdout must contain exactly `{"files":["absolute path"]}`, even for one file.
UTF-8 output is explicit; diagnostics belong on stderr.
The `.ps1` extension selects PowerShell, without runtime or function metadata.

With global MXC filesystem restrictions enabled, TEMP is the per-run writable
workspace. Write there, not to Desktop, Documents, or the script package.
Successful file outputs remain temporarily available to consumers; save a
separate copy for durable use. Consumers decide how to present or save files.
`x-powerscript.capabilities: ["fileWrite"]` describes intent, not a permission grant.

All scripts use the global MXC policy. There are no per-script settings.
Script or descriptor edits invalidate trust and require renewed consent.
Optional root `x-powerscript` fields include provenance
and a package-relative `.ico` `icon`.
