# Built-in CLI demo

PowerScripts includes a read-only catalogue of native PowerToys CLI actions. Keyboard Manager
reads the parameter contract through the Host, renders one generic parameter form, and saves an
independent command ID and parameter dictionary for each hotkey. Creating or editing a hotkey
does not create a script or copy a command definition.

## Try the demo

Build `PowerScripts.Host` and `KeyboardManagerEditorUI` using the repository build scripts. Keep
the complete Host output together in `<PowerToys build root>\PowerScripts\`, including its pinned
MXC dependency. The editor discovers that deployment or the Host project's development output.
For a standalone editor launch, also start the matching `KeyboardManagerEngine` and place
`PowerToys.KeyboardManagerEditorLibraryWrapper.dll` alongside the editor. A standalone project
build can leave that native DLL in the PowerToys build root rather than `WinUI3Apps`.

1. Open Keyboard Manager and choose **Add new remapping**.
2. Set **Action** to **PowerToys CLI**.
3. Select **PowerScripts Demo · Inspect command arguments**. This is a native Host diagnostic command
   that only prints its received argv and typed parameters; it does not modify files or settings.
4. Change Target, Mode, Count, or Verbose, then choose **Test command** and expand the
   output. The test starts the real CLI through the shared process execution policy.
5. Record a shortcut and save. Add another shortcut using the same command with different values.
   Reopening either entry restores only its own parameters. The **CLI commands** group supports
   enabling, disabling, editing, and deleting these bindings independently.

The diagnostic entry requires the deployed `PowerScripts\PowerScripts.Host.exe`. Other catalogue
entries describe existing PowerToys CLIs and may be unavailable in an incomplete development
deployment. A present executable alone does not establish that its module is running or that its
operation is compatible with the effective MXC policy.

## Definition and execution

The catalogue is embedded from `PowerScripts.Core/Cli/catalog.json`; it is not loaded from a
user-editable file. Entries declare:

- A stable `id`, title, group, and description.
- An `inputSchema` with string, integer, boolean, string-enum, or `format: file-path` properties.
- Defaults, required fields, integer bounds, labels, and descriptions.
- A fixed `x-execute.command` and ordered `arguments` bindings: `positional`, `option`, or `switch`.

For example:

```json
{
  "parameter": "verbose",
  "kind": "switch",
  "flag": "--verbose"
}
```

A false switch emits no token. The Host rejects unknown parameters and invalid values, resolves
the fixed executable inside its PowerToys installation, and constructs separate argv tokens.
The parameter values cannot replace the executable, flags, or subcommand. There is no shell
interpolation, external catalogue override, or script entry file.

```text
PowerScripts.Host.exe cli list --json
PowerScripts.Host.exe cli run powerscripts.inspect-arguments --set target=demo --set mode=full --set count=3 --set verbose=true --no-consent --json
```

The Client provides `GetCliCommandsAsync`, `RunCliAsync`, and `BuildCliRunArguments`. Native hotkeys
use the latter's deterministic `--set-base64` transport and a bounded timeout. Both catalogue and
execution failures are explicit structured results. Ordinary script `list/run/transform` contracts
retain their existing behavior.

The embedded catalogue establishes the command definition's source; execution still honors the
PowerScripts enable state, non-elevated process boundary, and effective MXC policy. Native CLI
policy IDs use `builtin-cli:<commandId>`. No script trust record or isolation preference is created
when a hotkey is saved.

## Keyboard Manager persistence

The editor adds optional `CliBinding` metadata to its existing `editorSettings.json` record:

```json
{
  "CommandId": "powerscripts.inspect-arguments",
  "Parameters": {
    "target": "demo",
    "mode": "full",
    "count": "3",
    "verbose": "true"
  }
}
```

Confirmed defaults are saved along with explicit values. The native mapping remains a RunProgram
invocation of the Host; the native enum and keyboard-hook ABI are unchanged. CLI saves preserve
the binding ID, profiles, and enabled state and roll back failed changes. A disabled binding can
be edited without enabling it. Its generated invocation is checked before presenting it as a CLI
binding, so changed raw program mappings do not inherit stale parameter metadata.

For this demo, editing a CLI binding keeps its Action type fixed; its CLI command, parameters,
shortcut, and app scope remain editable. Create a new remapping to use a different action type.

Test execution returns output to the editor. Background hotkeys use the existing native program
launcher, which does not collect the Host's final result; a separate background result/notification
channel is outside this demo.
