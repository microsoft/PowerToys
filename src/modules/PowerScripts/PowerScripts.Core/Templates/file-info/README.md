# File-input template

Edit `script.ps1` and `script.ps1.tool.json`, then refresh the catalogue.
Creation never executes code, approves trust, or changes isolation.

`name`, `title`, and `description` identify and explain the action.
Both schemas are required closed objects (`additionalProperties: false`).
`inputSchema.properties.files` is a required array of path strings.
`minItems` and `maxItems` constrain counts; omit `maxItems` for no upper limit.
Zero is a real maximum of zero, not an unlimited sentinel.
Its string `items` hold `x-powerscript.extensions`, for example `[".txt",".md"]`;
`["*"]` or omitted extension hints accept any extension.
These are compatibility filters, not content validation or permission grants.
Named options, if added, belong under the input's `parameters` object.

PowerShell receives the entire selection as `$Files` and also through
newline-separated `POWERSCRIPTS_FILES`. It reads names and sizes, not file contents.
`outputSchema.properties.text` declares literal UTF-8 stdout.
For file output instead, declare a required `files` array and emit
`{"files":["absolute path"]}`. Send diagnostics to stderr.

The `.ps1` extension selects PowerShell. All scripts use the global MXC policy;
there are no per-script settings. Selected inputs are read-only with filesystem
protection enabled. `fileRead` describes intent, not access to arbitrary files.
Source or descriptor edits invalidate trust. Test file input through a compatible
consumer, `run --files`, or JSON stdin to `transform`; the Debug launcher lists
only scripts without primary input.
