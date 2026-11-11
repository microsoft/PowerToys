# PowerToys operation templates

PowerScripts owns the built-in operation catalogue and execution. Keyboard Manager,
Command Palette and other consumers discover the resulting ordinary PowerScripts.
There is no separate Keyboard Manager CLI action type, command catalogue or binding store.

In **My scripts**, **Create Script** opens a two-item menu. **Custom script** opens the
unchanged authoring page. **PowerToys operations** opens a dedicated compact form with
two-level **Module** and **Operation** selection, friendly parameter fields, a script
name and **Test run** at the bottom of the form.
It does not ask users to edit runtime names, parameter keys, schemas or JSON files.
The embedded `PowerScripts.Core/Cli/catalog.json` drives operation discovery, labels,
help, types, choices, ranges and defaults through the Host's existing template-list
JSON response.
The editor uses the same renderer for every operation; launch paths and flags remain
inside the controlled catalogue, never in user-provided UI configuration.

Users enter the values to save directly in the parameter fields. Catalogue defaults
prefill those fields, and **Test run** uses the same fixed configuration that **Create**
saves. There is no runtime-source selector, **Ask when running** option or separate test
sample in this creation form. File Locksmith's **Output format** offers **JSON** and
**Text**; the underlying boolean is not shown as a parameter type. Explicit empty
strings, zero, false and omitted optional values remain distinct.

## JSON catalogue and form generation

The embedded catalogue uses `schemaVersion: 2` and a `modules` array. Each module has
an `id`, a display `title` and an `operations` array. Each operation supplies its own
identity, title, input schema, controlled execution definition and requirements.
The Host keeps `template list` flat for existing callers: every operation entry carries
the stable `moduleId` and the module's display title as `group`. The editor groups those
entries into the **Module** and **Operation** selectors.

Schema metadata also controls how values appear. For example, this boolean property
renders as a friendly choice while its saved value remains a typed boolean:

```json
{
  "json": {
    "type": "boolean",
    "title": "Output format",
    "default": true,
    "x-ui": {
      "control": "choice",
      "trueLabel": "JSON",
      "falseLabel": "Text"
    }
  }
}
```

The initial catalogue offers File Locksmith's process query and FancyZones' layout and
monitor queries. These operations need their corresponding installed CLI; FancyZones
may also need its running module. Module deployment and the effective global sandbox
settings can make an operation unavailable. Failures are reported without changing
isolation, elevating the command or selecting an executable from PATH. Image Resizer's
file-output adaptation is outside this initial catalogue.

File Locksmith's query needs host-process visibility, which an MXC process container
does not provide. FancyZones queries need the host desktop, which desktop isolation
blocks. The template list and creation page explain these known incompatibilities;
execution refuses them with exit code 125 instead of returning an unreliable empty
query result. Templates can still be created and edited. Nothing automatically changes
the user's global isolation preferences or accepted-risk settings.

## The generated package

**Test run** uses a private, unpublished preview package and the ordinary Host/Client
trust and execution path. It does not install a script, create a hotkey or approve
trust automatically. Known isolation incompatibilities are reported before creating
a preview package. Completion or failure appears in a dismissible result flyout next
to the test button; the same button can cancel a running request. When **Test run** is
disabled, hovering its area explains the reason. Preview cleanup is best effort and
limited to that preview's directory and approval.

```text
find-processes-<unique-id>/
  action.json
  action.json.tool.json
  README.md
```

The creation form writes an entry selecting a known operation with the entered values
as typed constants:

```json
{
  "schemaVersion": 1,
  "commandId": "filelocksmith.query",
  "bindings": {
    "path": { "constant": "C:\\Users\\you\\Documents\\report.txt" },
    "json": { "constant": true }
  }
}
```

The sibling descriptor declares `x-execute: { "provider": "powertoys-cli" }`, standard
input/output schemas and the required capabilities. The form supplies an empty
`parameters` array, so the generated script exposes no runtime parameter questions.
Both output-format choices retain the PowerScript's text-output contract; **JSON**
selects structured command output within that text. This provider cannot be mixed with
an arbitrary `command`, argument map or other process recipe. It resolves program paths,
flags and argument order from the installed catalogue. Both JSON files are covered by
the normal content fingerprint; editing either requires trust review. Capability
declarations do not grant permissions or override the global sandbox settings.

Creation publishes the descriptor last, never overwrites an existing package, and does
not execute, approve trust or create a hotkey. Creating the script and binding a shortcut
remain separate actions. Existing custom PowerShell/Python and explicit process recipes
keep their execution paths.

## Host and consumer interfaces

`template list` includes the flat operation entries, module metadata and typed parameter
schemas, including their `x-ui` display hints. The form uses the existing
configured-creation command with explicit fixed bindings and no exposed parameters:

```json
{
  "projectName": "Find file usage",
  "input": "none",
  "output": "text",
  "parameters": [],
  "operation": {
    "commandId": "filelocksmith.query",
    "bindings": {
      "path": { "constant": "C:\\Users\\you\\Documents\\report.txt" },
      "json": { "constant": true }
    }
  }
}
```

Pipe this JSON into `PowerScripts.Host.exe template create --configure --root <scriptsRoot>`.
The advanced Host API also supports runtime bindings. Omitting explicit bindings, as in
`"operation": { "commandId": "filelocksmith.query" }`, exposes parameters without defaults
and fixes the catalogue's declared defaults. Advanced callers can use bindings such as
`"path": { "parameter": "file" }` with the normal `parameters` array to choose exposed
names, constraints and defaults. Those runtime parameters must be supplied by the
eventual caller when required. This underlying capability remains available independently
of the creation form's fixed-value workflow.

Instances run through the existing `list`, `run` and `transform` interfaces. In Script
details, **Run** opens an expander for this invocation's inputs, output, diagnostics and
exit code. It uses the ordinary Host trust prompt. Cancel and timeout stop waiting for
the current invocation; they do not promise to undo side effects or confirm that every
child process has exited. Late results do not replace a newer invocation's display.
Image/audio input remains available through compatible consumers rather than this
editor run panel.

Keyboard Manager accepts no-primary-input scripts and stores the script ID and ordinary
parameter invocation. Required inputs must be configured before a hotkey can be enabled.
Trust review happens in PowerScripts; saving a shortcut does not silently approve content.
Command Palette's existing `none → none/text` discovery automatically includes compatible
operation instances. File-path parameters are auxiliary parameters, not primary file input.

The editor attributes its runs to `powerscripts-editor`. Activity still stores invocation
metadata only; it does not persist inputs, parameter values, paths or stdout/stderr.
The parsers have bounded, strict JSON validation and a dedicated
[fuzzing target](PowerScripts.FuzzTests/README.md).
