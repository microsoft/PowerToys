# File-input template

Creation writes your chosen project title, description and allowed file extensions directly into
`script.ps1.tool.json`. A unique action ID and folder are derived from the project name, so creating
another copy never overwrites an existing package. Edit the descriptor for later changes.
Creation does not execute code, approve trust or change MXC settings.

## Every field in this descriptor

| Field | Meaning |
| --- | --- |
| `name` | Stable, unique action ID generated for this copy. |
| `title` / `description` | Your project name and explanation, displayed by compatible consumers. |
| `inputSchema.type` | `object`: the named-parameter bag. |
| `inputSchema.properties` / `required` | Empty: selected files are primary input, not named parameters. |
| `x-powerscript.runtime` | Built-in `powershell` execution; no launch recipe is needed. |
| `x-powerscript.input` | `files`: a consumer supplies selected files, not pasted text. |
| `x-powerscript.output` | `text`: stdout is returned literally; UTF-8 is set explicitly. |
| `x-powerscript.extensions` | Accepted final extensions, such as `[".txt",".md"]`. `["*"]` accepts all types. This filters compatible selections, not filesystem permissions. |
| `x-powerscript.minFiles` / `maxFiles` | At least one file; `0` means no upper count limit. |
| `x-powerscript.capabilities` | `fileRead` declares intent, not permission to read arbitrary files. |

## File delivery and policy

The Host supplies selected paths through `-Files` and `POWERSCRIPTS_FILES` (newline-separated).
With MXC filesystem restrictions enabled, selected files are staged into the run workspace. Work
only on the supplied paths; original folders and unrelated files are not granted automatically.
This example reads each file's name and size without modifying it, contacting the network or
displaying UI. It needs no per-script policy exception and works with all default restrictions.

Diagnostics go to stderr. Print only result text to stdout. To return files instead, change the
output contract and return `{"file_paths":["absolute path"]}` from an appropriate script.

Optional `x-powerscript.consumers` narrows discovery by module ID, not execution authorization.
Optional `icon` is a package-relative `.ico` path. Metadata edits invalidate existing trust and
require approval again. The Debug test popup only lists no-primary-input scripts; test this
file-input example through a compatible consumer or the Host CLI with `--files`.
