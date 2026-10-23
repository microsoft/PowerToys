# File-output template

Creation never runs this script, approves trust or changes MXC settings. Review the package, edit
`script.ps1` and `script.ps1.tool.json`, then refresh the catalogue. A compatible consuming module
decides how to use the returned file. The Debug Settings test window offers Save as and Copy files.

## Every field in this descriptor

| Field | Meaning |
| --- | --- |
| `name` | Stable, unique action ID. Each template copy gets a fresh ID. |
| `title` | Friendly name shown in a consumer's picker. |
| `description` | Explains the action and where it writes. |
| `inputSchema.type` | `object`: named parameters, if any, form a property bag. |
| `inputSchema.properties` / `required` | Empty because this example has no named parameters. |
| `x-powerscript.runtime` | `powershell` uses the built-in executor, with no explicit launch recipe. |
| `x-powerscript.input` | `none`: no primary text or file selection is required. |
| `x-powerscript.output` | `files`: stdout must contain exactly `{"file_paths":["absolute path"]}`. A single file is still an array. |
| `x-powerscript.capabilities` | `fileWrite` declares intent for review; it does not turn off filesystem isolation or grant arbitrary paths. |

UTF-8 is explicit so JSON paths survive Windows PowerShell. Do not print status messages to stdout
alongside the result JSON; use stderr for diagnostics. `output: "text"` would instead return literal
stdout. `output: "none"` represents completion with no result.

## Safe workspace and policy

With all default MXC restrictions enabled, TEMP is inside the per-run workspace. Write there rather
than to Desktop/Documents. Successful nonempty output workspaces are retained temporarily so consumers
can read the files; save a copy elsewhere for durable use. Return existing absolute paths, not file
contents or guessed destinations. The consumer, not this isolated script, chooses a final save path.

Network, UI and least-privilege restrictions remain enabled too. No local policy record is needed.
Per-script Settings is reserved for necessary MXC policy and explicit risk acceptance, never metadata
overrides. Review any relaxation separately; capability declarations do not grant it.

Optional `x-powerscript.consumers` restricts discovery by module ID, not execution authorization.
Omit it for all compatible consumers. A file-input variant would use `input: "files"` plus
`extensions`, `minFiles`, `maxFiles`; it is not the same as this file-output example.
Optional `icon` is a package-relative `.ico` path. Python built-in execution also needs `function`;
advanced `x-execute` recipes are unnecessary here.
