# Greeting template

This package is editable source, not an approved action. Review it before a consumer runs it.
Creation never approves trust, executes code or relaxes MXC. Edit `script.ps1` and its sibling
`script.ps1.tool.json`, then refresh the catalogue. Metadata changes require renewed trust approval.

## Every field in this descriptor

| Field | Meaning |
| --- | --- |
| `name` | Stable, unique action ID used by consumers. Creation generates a fresh ID, so copies do not collide. |
| `title` | Friendly display name; changing this does not change the ID. |
| `description` | Explains what this script does before it runs. |
| `inputSchema.type` | `object`: named parameters form a property bag. |
| `inputSchema.properties` | Parameter names must match the PowerShell `param` names. |
| Parameter `type` | JSON Schema `string` or `integer` here; also supported: `boolean`, choices via `enum`, files via `format: "file-path"`. |
| Parameter `title` / `description` | Consumer-facing label and instructions. |
| Parameter `default` | Used when the consumer omits that parameter. Keep script defaults consistent. |
| Parameter `minimum` / `maximum` | Host-enforced inclusive bounds for integers. |
| `inputSchema.required` | Names that must be supplied (or have defaults). An empty array makes all optional. |
| `x-powerscript.runtime` | `powershell` selects the built-in PowerShell executor. Python requires its runtime to be enabled. |
| `x-powerscript.input` | `none` means no primary input. Named parameters are still allowed. `text` and `files` are the other contracts. |
| `x-powerscript.output` | `text` means literal stdout, not JSON, HTML or an inferred file path. Diagnostics belong on stderr. |
| `x-powerscript.capabilities` | Empty because this example needs no external resources. Capabilities describe intent, not permission grants. |

UTF-8 output is set explicitly so non-ASCII names survive Windows PowerShell.
The consumer chooses how to collect parameters and display text; PowerScripts provides no product launcher.
The Debug Settings test window can exercise this `none`-input example.

## Policies and optional fields

All four default MXC restrictions remain enabled: filesystem isolation, blocked network, blocked UI,
and least privilege. This greeting needs no exceptions. Never disable isolation to make a sample run.
The metadata is the contract; per-script Settings stores only local MXC policy and risk acceptance.
Such settings are not metadata overrides or tamper-resistant authorization.

Optional `x-powerscript.consumers` narrows discovery to module IDs, not authorization.
Omit it to allow every compatible consumer. For `input: "files"`, declare `extensions` (for example
`[".png"]`), `minFiles` and `maxFiles` (`0` means unbounded). Optional `icon` is a package-relative
`.ico` path. Python function templates additionally need `function`; advanced explicit launch recipes
use `x-execute` instead. Do not add either to this built-in PowerShell example.
