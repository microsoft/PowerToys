# PowerToys CLI and operation templates

## Editable PowerShell CLI content

In **My scripts**, **Create Script** opens one creation page directly, without a
creation menu. Its **Script type** selector offers **General script** or
**PowerToys module script**. Module scripts use PowerShell and offer a
**PowerToys module** dropdown followed by a **Module action** dropdown containing
only that module's CLI recipes. The module-script choice creates
a script that invokes an existing installed PowerToys module; it does not
reimplement that module. This is distinct
from the data-only **Restricted PowerToys operation (no script code)** creation form below. Python continues
to offer general script content; the Host also rejects CLI content with a Python runtime.
The module-script form has a secondary **Create a restricted operation instead
(no script code)** link to the dedicated restricted-operation form. Back returns
to script creation; restricted operations are not a third script type.

The CLI catalogue currently has 14 recipes covering Advanced Paste (local text transforms,
discovery and full file transforms),
File Locksmith (query only), FancyZones (queries, layouts, hotkeys and UI),
Workspaces (list/launch), Image Resizer, Awake, Light Switch, Settings, and Power Display
(queries, continuous adjustments, color temperature, input source and orientation).
The module-call form offers three value sources for each argument:

- **Fixed value** saves a typed value in the reviewed script and descriptor, not a runtime parameter.
  The value field uses the argument's choices, integer bounds, boolean or file picker.
  Do not store secrets in fixed values.
- **Script input** maps the caller's primary input to the argument. An input-file
  argument makes the script accept **Files**; other scalar arguments use **Text**,
  with integer/boolean parsing and the CLI's choices/bounds still enforced.
- **Ask when the script runs** exposes a named script parameter with the original
  type, choices, bounds and available default.

The form infers the primary input contract and updates consumer compatibility.
Several arguments can use the same primary input, but text and files cannot be
mixed: a PowerScript has one primary input. A module's input-file argument can
instead be a fixed path or a named file-picker parameter, producing a no-primary-input
script unless another argument uses input. Image Resizer accepts a selected-file
batch through primary input; a fixed/runtime file path identifies one file.
Primary input and file-picker parameters identify the current caller's files.
The approved host recipe uses the original paths, not isolated substitutes.

Only runtime-exposed arguments appear in the generated descriptor's parameter
schema. Additional custom parameters are retained for author edits, not blindly
passed to native commands. Changing actions preserves each action's draft values.

These recipes require PowerShell 7 or later. They pin fixed executable paths
below this installation directory, not a runtime parameter. With MXC on, the
Host selects its own installation root; editing a script literal cannot redirect
host execution. With MXC off, the generated script's literal selects the root.
Arguments use `ProcessStartInfo.ArgumentList`,
not shell command interpolation; native stderr and nonzero exits remain failures.
No CLI is run during creation, and no trust, runtime, sandbox or isolation setting
is changed. CLI text is returned as text; Image Resizer instead returns a standard
files-output envelope containing its generated images.

Supported CLI templates automatically include the scoped host recipe permission;
there is no separate allowlist toggle or MXC policy change. The creation page uses
plain module names and explains that supported CLI actions work with the script
sandbox on by default, rather than showing an incompatibility warning.
The host-execution boundary remains disclosed in the normal creation guidance,
trust prompt, generated README and metadata details, including the recipe and
reviewed bindings. Creation does not grant trust.
Building the Editor also builds the Host. Native-platform Host builds stage the
Host, Core, Client, MXC runtime and bundled agent skill into the matching
`<platform>\<configuration>\PowerScripts` directory beside the Runner, so the
creation page and consumers do not keep using an older manually copied Host.
The module-action selector is disabled while loading and shows an inline status.
Loading failures offer Retry instead of leaving an empty selectable dropdown.
Successful loading selects an available action; the popup has a bounded scrolling height.

### Approved host execution boundary

With MXC on, editable PowerShell runs contained and emits one typed request.
Only after that contained process succeeds does the Host execute a canonical
built-in CLI recipe outside MXC, with standard-user permissions. The host action
is outside the script's filesystem, network and desktop restrictions. This is
an explicit exception, not a folder allowlist or a transparent executable exemption.

The descriptor declares `hostPowerToysCli` plus the selected `hostCli.id` and typed
argument bindings. Script bytes and descriptor bytes both participate in normal
trust approval. The Host independently checks the known recipe, required
capabilities, canonical argument schemas, exact reviewed fixed values/current
consumer input, selected-file paths and a per-invocation token. It rejects
unknown/duplicate request members, extra arguments, malformed/oversized requests
and replays before launching. Executable paths come from the Host installation,
not PATH, editable code or the request.

The Host generates native argument construction and output processing from its
own catalogue; user values travel as JSON data, not executable code. This is a
terminal one-request handoff: editable script code cannot resume after the host
action, request multiple host actions or provide host-side postprocessing.
Changing only the script body cannot change the approved action; bindings must
be updated in the descriptor and approved again. Existing ordinary scripts
without this explicit permission remain contained. Direct `Process.Start` calls
remain contained, even when they target a PowerToys executable.

The original timeout budget covers containment and the host action. Neither an
MXC failure nor a host failure causes a retry or direct fallback. Global MXC
settings and all four script protections remain unchanged. Native prerequisites,
Group Policy, backend ownership and CLI-specific restrictions still apply.

Settings offers read-only `list`/`status` and state-changing `enable`/`disable`
recipes, with the same fixed/input/runtime argument choices. The current Settings
CLI saves state for the next PowerToys start and requires the Runner to be stopped.
It does not live-toggle modules from a Keyboard Manager hotkey; disabling MXC does
not bypass the Runner's settings lock or Group Policy.
The approved host recipe reads actual host state rather than isolated/default
settings. Its state-changing commands still require the Runner to be stopped.

Advanced Paste's **local text transformation** action uses a lightweight,
identity-free `PowerToys.AdvancedPaste.LocalCLI.exe` worker backed by the existing
Core transforms and module Group Policy check. It accepts text on stdin and
returns plain text, Markdown or JSON on stdout. It does not load AI providers,
credentials or the clipboard UI. These local transformations work with full MXC.
The existing full CLI and its credential identity are unchanged.

### Historical direct-contained Advanced Paste limitation

The following diagnosis applies to direct child launches inside MXC, not the
approved host recipe. Host-brokered discovery and plain-text/Markdown/JSON file
transforms now work in the feature Debug deployment without relaxing MXC.
The host route avoids the activation barrier; it does not repair Windows/MXC
activation or establish signed installed-build credential compatibility.

The full Advanced Paste CLI has a specific MXC startup limitation: its sparse-MSIX
identity activation fails with Win32 error 1753 before CLI code runs, even for
`--help`. A diagnostic copy without that identity starts under the same isolation
policies, but removing identity is not a production workaround: Advanced Paste
uses `PasswordVault`, so credential-scope and packaging compatibility need review.
Enabling the module does not resolve this activation failure. Its discovery and
full file-transform recipes previously required incompatibility warnings.

Further startup controls reproduce error 1753 in both Debug worktrees and the
existing signed, installed Advanced Paste CLI. A minimal native program carrying
the same development MSIX manifest also fails inside MXC, while its unpackaged
control starts. This reproduces the failure without .NET, WinAppSDK, the Advanced
Paste engine or credential access. Native `CreateProcessW` reports the same error;
RPC extended error information identifies component 2, detection location 950,
but supplies no endpoint/interface parameters.

An owned-process RPC trace now identifies the failing activation call precisely:
Windows `CreateProcessInternalW` calls `AicGetPackageActivationTokenForSxS`,
which invokes AppInfo interface `0497b57d-2e66-424f-a0c6-157cd5d41700`,
version 1.0, operation 3. Matching Microsoft public symbols and this Windows
build's AppInfo dispatch table identify that operation as
`RAiGetPackageActivationTokenForSxS`. RPC asynchronous completion raises 1753
before the CLI entry point. This is a Windows package-token activation call,
not an Advanced Paste RPC server.

AppInfo is running. A query restricted to that interface finds the same local
endpoint inside and outside full MXC, and explicit `RpcEpResolveBinding`
succeeds in both modes. Repeating the contained launch after successful
resolution still fails with 1753. Therefore, a missing service, missing global
endpoint registration or merely warming endpoint resolution is not an
established explanation or fix. Private controls disabling only networking
or least privilege also reproduce the activation failure. Filesystem- and
UI-relaxed controls could not start the probe or PowerShell and are
inconclusive for Advanced Paste activation.

A subsequent comparison uses the actual AppInfo client-interface descriptor
captured from Windows, rather than only a reconstructed interface identifier.
Resolution still succeeds, but `RpcBindingBind` to the discovered endpoint returns
error 5 (`ACCESS_DENIED`) under full MXC and succeeds outside it. Both
unauthenticated and current-token Windows-authenticated bindings produce this
pattern. The contained caller has an AppContainer token; there is no thread
impersonation at the activation call. These probes establish an access barrier,
not a missing endpoint. They bind only and do not invoke AppInfo procedures.
This is consistent with the AppInfo access limitation reported upstream in
[microsoft/mxc#1758](https://github.com/microsoft/mxc/issues/1758), whose
reproduction targets the PSEC backend. It is not proof of which port DACL or
interface authorization check rejects this deployment.

Launching the full CLI as MXC's initial process was also tested through the
production MXC runner with all four protections, rather than spawning it from
contained PowerShell. The signed, installed CLI still fails sandbox creation
with access denied. Unpackaged controls start, and the native control confirms
an AppContainer token. A Debug CLI does start through this route, but that is
not production identity proof: its ordinary launch reports
`APPMODEL_ERROR_NO_PACKAGE` (15700), whereas the signed CLI's ordinary launch
reports the registered sparse-package identity. Debug-only startup must not
be presented as an identity-preserving fix.

No identity-preserving application-level fix was established. The failure occurs
before the child starts, so catching errors or changing managed CLI initialization
cannot repair it. Further effort should target a supported Windows/MXC
package-activation path and the upstream access limitation, not more Advanced
Paste initialization changes. These results do not prove packaged applications
are permanently unsupported. The local worker avoids this dependency for its
supported transforms; it does not fix full packaged-CLI activation. Production
manifests, package registration and MXC protections remain unchanged.

Image Resizer accepts files and returns files. The generated script first copies
inputs into a unique workspace directory, resizes those copies into a separate
output directory, and returns the resulting paths only after successful execution
and output-count validation. There is no host destination or source-replacement
option in this recipe. Native progress is sent to stderr, not mixed into the JSON
result. Explicit `--replace false` now overrides saved replacement settings;
omitting the option continues to inherit them. Copying inputs also protects
originals when using an older CLI with the previous false-value parsing behavior.

Enabling a module also does not initialize all of its persisted data. For example,
FancyZones `get-layouts` fails if `layout-templates.json` has never been saved;
this is separate from the module-not-running and sandbox-compatibility errors.

### Approved host recipe checks

The full-MXC broker matrix verifies real host Settings state, Advanced Paste
action/provider discovery and local/full plain-text/Markdown/JSON transforms,
File Locksmith's known original-file holder, FancyZones monitor/active-layout
queries, Workspaces listing, Image Resizer single/batch/percent dimensions and
original preservation, Light Switch's real host theme state, and Power Display
backend queries. It also checks native/schema failures without success envelopes.
Matching-installation private Hosts are used where backends enforce ownership;
those checks are not weakened.

FancyZones layout/hotkey listings still require absent user data. Power Display
reports no controllable monitors/profiles on this machine: query transport and
nonexistent-target rejection are verified, not successful hardware mutations.
Desktop/layout/theme changes, workspace launches and AI/network actions are not
performed merely to manufacture passing results.

The current private full-MXC matrix contains 71 checks: 41 verified successful
operations, 28 expected refusals and two explicit FancyZones missing-data cases.
It covers all 14 recipe routes, including fixed/runtime file paths, fixed text,
numeric primary input and a scalar primary input paired with a fixed source file.
Advanced Paste discovery and three non-AI full-CLI transformations succeed.
With the Runner briefly stopped, Settings enable/disable changes actual host
state, and a bounded Awake request asserts then releases the host system/display
power flags (`0 -> 3 -> 0`). Exact user settings bytes are restored before the
original Debug Runner restarts.

The refusal controls include wrong execution tokens/recipes/values, extra
executable fields, duplicate/malformed requests, revoked trust, contained
failure, direct contained packaged-CLI startup and an attempted host-file write.
All private approvals are revoked afterward. Core's 768 tests and the 163
PowerScripts UI ViewModel tests pass. Runtime Editor checks confirm the updated
unpackaged process remains responsive; optional `winapp` UI automation is not
available on this machine.

### Historical direct-execution checks

The generated scripts were exercised through the Host entry point with private
test settings/trust stores, first without MXC and then with all MXC protections.
User isolation preferences were not changed. Most results are specific to this
Debug deployment and the selected safe commands, not guarantees for every command,
machine or reduced-policy combination. Advanced Paste startup was additionally
checked against the existing signed installation; this is not full installed-build
functional validation.

| Module | Direct, without MXC | Direct child, with full MXC (before broker) |
| --- | --- | --- |
| Settings | List/status read host state. With the Runner stopped, enable/disable saves the requested state. | Can read isolated/default state and save only isolated settings while reporting success. |
| Advanced Paste | Local plain-text, HTML-to-Markdown and XML-to-JSON transforms work; full CLI discovery and file transformation also work. | The new local text worker produces verified results. The full CLI still fails sparse-MSIX activation with error 1753. |
| File Locksmith | Finds the known process holding the original test file. | Does not find that host holder: process visibility and staged inputs differ. |
| FancyZones | Monitor query works with its backend running. | Reports the host module is not running. |
| Workspaces | Workspace listing works. | Can report the module disabled because isolated settings differ. |
| Image Resizer | The files-output recipe resizes copied inputs without changing originals. | The same recipe returned two readable 32-by-24 images after Host exit, with originals unchanged. Invalid inputs failed without a success envelope. |
| Awake | A bounded run succeeds with the Runner stopped; another running Awake instance prevents startup. | A six-second display-on run asserted host system/display execution-state flags (0 to 3), then released them (back to 0). No sleep transition was attempted. |
| Light Switch | Read-only status works with its service running. | Host service is unavailable. |
| Power Display | Display query works; invalid-target mutation requests are safely rejected. | Host service is unavailable. |

The expanded MXC-off matrix covers the other eight modules and eleven recipes:
62 validations comprising 27 successful operations, 33 expected refusals (including
the trust-revocation control), and two FancyZones missing-data cases. Those last
cases require the user's absent `layout-templates.json` and `layout-hotkeys.json`;
no layouts or hotkeys were created to manufacture a passing result.

File Locksmith identified the known host file holder. Image Resizer single-file,
batch and percent cases returned readable resized copies with original and
intermediate input hashes unchanged; invalid and mixed batches reported failure.
Settings offline enable/disable/status was checked with the original module state
and exact settings bytes restored before Runner restart. Awake's direct-mode
bounded run also asserted and released host execution-state flags (0 to 3 to 0).

Light Switch and Power Display reject connections to backends belonging to a
different installation. Matching-installation private fixtures were reviewed
separately before testing those commands. The latest Power Display backend
reported no controllable monitors and no saved profiles, so successful hardware
value reads or mutations were not verified. These environment limitations are not
counted as successful hardware functionality.

Display/layout/theme changes, workspace application launches, AI/network actions
and process termination are not performed by these compatibility checks. Display
mutation recipes use a deliberately nonexistent monitor ID to validate argument
handling and safe rejection without disrupting the desktop.

Review the chosen command's effects. AI actions can contact configured providers.
Workspaces can start applications; display/layout/theme commands can modify the
desktop. Awake can run longer than the configured PowerScripts timeout.
Awake's observed effect is a process-scoped power requirement, not proof that
arbitrary desktop or power-setting changes are allowed. Its requirement disappears
when the bounded request ends.
File Locksmith termination, monitor power-off, elevation and raw command
passthrough are not scaffolded.

`template cli-list` exposes creation metadata only. Configured creation accepts
`cliContent` with one of those IDs, optional `cliBindings`, and the matching
PowerShell/I/O contract. Each binding has `source: "fixed" | "input" | "parameter"`;
only fixed bindings accept a typed `value`. Omitted bindings use primary input for
the recipe's designated input argument (text or files); other arguments are
exposed parameters:

```json
{
  "projectName": "List layouts",
  "runtime": "powershell",
  "input": "none",
  "output": "text",
  "cliContent": "fancyzones",
  "cliBindings": {
    "Command": { "source": "fixed", "value": "get-layouts" },
    "Layout": { "source": "fixed", "value": "" },
    "Monitor": { "source": "parameter" },
    "AllMonitors": { "source": "fixed", "value": false },
    "Hotkey": { "source": "fixed", "value": 1 }
  }
}
```

### Optional consumer guidance

The custom form also offers **I want the script to be used by (optional)** for
Advanced Paste, Keyboard Manager, Command Palette and File Explorer. Selected
modules intersect their supported primary contracts. Incompatible modules and I/O
cards are disabled, while selected modules remain removable. Explorer accepts
file and inline image/audio input; Keyboard Manager and Command Palette use no
primary input. CLI recipes are filtered against the same selection.

These choices are creation guidance, not targeting metadata or authorization.
Discovery and execution still perform their ordinary schema, runtime, trust and
policy checks. Clearing the selection restores the unrestricted custom contract
choices.

## Controlled PowerToys operation templates

PowerScripts owns the built-in operation catalogue and execution. Keyboard Manager,
Command Palette and other consumers discover the resulting ordinary PowerScripts.
There is no separate Keyboard Manager CLI action type, command catalogue or binding store.

In **My scripts**, choose **Create Script**, then **PowerToys module script**,
and follow **Create a restricted operation instead (no script code)**.
This opens a dedicated compact form with
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
