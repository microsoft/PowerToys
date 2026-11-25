# PowerToys CLI template scenario matrix

This is the functionality/input/output checklist for adding or changing an
editable PowerScripts CLI recipe. The current creation catalogue is defined in
`PowerScripts.Core\Templates\PowerShellCliTemplates.Scenarios.cs`.
The original 14 definitions in `PowerShellCliTemplates.Definitions.cs` remain
available by ID for saved scripts; mixed-purpose legacy recipes are not offered
for new creation. Their original contracts and bindings must not change.

## Contract rules

Primary types are **none, text, files, image, audio**. JSON and JSON Lines are
intentional **text** results, not a sixth primary type. MP4 is a **file**, not an
audio result. Native status/progress is diagnostic stderr for action/file
recipes, not the primary result.

The inputs below are default primary inputs. Each argument can instead be fixed
or exposed as a named runtime parameter; that normally produces **none** primary
input. A scalar argument can also use primary text. Named parameters are not
primary input, and text and files cannot be mixed in one script.

File results are new files in a unique execution workspace. The consumer owns
saving or copying them. A files declaration needs an actual validated files
envelope, not a printed path or a native JSON status report. Files must exist
before a success envelope is emitted; native nonzero exit remains failure.

## Supported scenarios

| Module / functionality | Native command or options | Primary input | Primary output | Creation recipe / important conditions |
| --- | --- | --- | --- | --- |
| Settings: module catalogue/status | `list`, `status <module>`, `--json` | none | text | `settings-query`; saved state/GPO is not proof of a running process |
| Settings: save enabled/disabled state | `enable <module>`, `disable <module>` | none | none | `settings-module-action`; requires Runner stopped; next-start state, not a live hotkey toggle |
| Advanced Paste: local text formatting | local `--format plain-text/markdown/json`, stdin | text | text | `advanced-paste-local`; no clipboard, identity, provider or network |
| Advanced Paste: action/provider discovery | `actions list`, `providers list`, `--json` | none | text | `advanced-paste-list`; JSON is text |
| Advanced Paste: text/HTML file formatting | `transform --action plain-text/markdown/json --input ... --stdout` | files (one) | text | `advanced-paste-file-text`; HTML files retain HTML format |
| Advanced Paste: Windows OCR | `transform --action image-to-text --input ... --stdout` | files (one image) | text | `advanced-paste-file-text`; Windows OCR, no AI provider |
| Advanced Paste: text-file export | `paste-as-txt-file --input ... --output ...` | files (one text file) | files (TXT) | `advanced-paste-file-result`; workspace destination |
| Advanced Paste: image export | `paste-as-png-file`, `paste-as-jpg-file`, explicit input/output | files (one image) | files (PNG/JPG) | `advanced-paste-file-result`; JPG uses module quality settings |
| Advanced Paste: HTML-file export | `paste-as-html-file`, explicit HTML input/output | files (one HTML file) | files (HTML) | `advanced-paste-file-result`; stdin alone does not supply HTML |
| Advanced Paste: media transcode | `transcode-to-mp3`, `transcode-to-mp4`, explicit input/output | files (one supported media file) | files (MP3/MP4) | `advanced-paste-file-result`; not a text result |
| Advanced Paste: supplied text to TXT | `paste-as-txt-file --stdin --output ...` | text | files (TXT) | `advanced-paste-text-file` |
| Advanced Paste: supplied HTML to text/Markdown/JSON | stage `.html`, `plain-text/markdown/json --input ... --stdout` | text (HTML) | text | `advanced-paste-html-text`; explicit staging preserves HTML semantics |
| Advanced Paste: supplied HTML to HTML file | stage `.html`, `paste-as-html-file --output ...` | text (HTML) | files (HTML) | `advanced-paste-html-file` |
| Advanced Paste: spelling/grammar, prompt or saved action on text | `fix-spelling-and-grammar`, `paste-with-ai`, or `--custom-action`; `--stdin`; optional `--prompt/--provider` | text | text | `advanced-paste-ai-text-text`; configured provider, possible cost/network; file-only results fail instead of being relabelled |
| Advanced Paste: AI on selected file | same AI options, `--input ... --stdout` | files (one) | text | `advanced-paste-ai-files-text`; selected action must support input |
| Advanced Paste: AI text result saved as file / generated file | same AI options with `--output ...` | text | files | `advanced-paste-ai-text-file`; single filename, expected extension chosen by author |
| Advanced Paste: AI file input to returned file | same AI options with explicit input/output | files (one) | files | `advanced-paste-ai-files-file`; text can intentionally be saved as a file; media output depends on configured action |
| File Locksmith: selected-file report | paths, optional `--json` | files (batch) | text | `file-locksmith-query`; never `--kill` |
| File Locksmith: wait until files unlock | paths, `--wait --timeout <milliseconds>` | files (batch) | none | `file-locksmith-wait`; native/Host budgets both apply; timeout is failure |
| File Locksmith: recursive directory or mixed-path report | one file/directory path per line, optional `--json` | text (paths) | text | `file-locksmith-paths-query`; accepts fixed/parameter paths too; never reads global clipboard itself |
| File Locksmith: wait for directory/mixed paths | path lines, `--wait --timeout` | text (paths) | none | `file-locksmith-paths-wait`; no incompatible JSON/wait combination |
| FancyZones: saved layouts, monitors, active layout, hotkeys | `get-layouts`, `get-monitors`, `get-active-layout`, `get-hotkeys` | none | text | `fancyzones-query`; missing saved layout data remains a real error |
| FancyZones: apply layout | `set-layout <name-or-UUID> --monitor <n>` or `--all` | none | none | `fancyzones-layout`; one/all monitor choice |
| FancyZones: assign/remove layout hotkey | `set-hotkey <0-9> <UUID>`, `remove-hotkey <0-9>` | none | none | `fancyzones-hotkey`; UUID required only for assignment |
| FancyZones: launch UI | `open-editor`, `open-settings` | none | none | `fancyzones-open` |
| Workspaces: list/filter/inspect | `list`, `--id/--name`, `--details`, `--json` | none | text | `workspaces-query`; ID/name mutually exclusive |
| Workspaces: launch apps and arrange windows | `launch --id/--name --timeout` | none | none | `workspaces-launch`; not replay-safe; prompts/partial failures remain visible diagnostics |
| Image Resizer: saved configuration/presets | `--show-config` / `--config` | none | text | `image-resizer-config` |
| Image Resizer: explicit dimensions | `--width/--height --fit --unit` | files (batch) | files | `image-resizer-options`; invariant decimals; one dimension may be zero, not both |
| Image Resizer: saved preset | `--size <zero-based-index>` | files (batch) | files | `image-resizer-options`; preserves preset fit/unit/dimensions |
| Image Resizer: copy-only resize options | `--shrink-only`, `--ignore-orientation`, `--remove-metadata`, `--keep-date-modified`, `--quality`, `--filename` | files (batch) | files | `image-resizer-options`; true enables boolean overrides, false inherits native saved defaults; quality zero inherits; filename cannot escape workspace |
| Awake: duration/expiry/lifetime/configuration | `--time-limit`, `--expire-at`, `--display-on`, `--pid`, `--use-parent-pid`, `--use-pt-config` | none | none | `awake-action`; default 10 seconds; awaited, not automatically detached; existing instance/GPO/Host timeout still apply |
| Light Switch: inspect state | `status --json` | none | text | `light-switch-status` |
| Light Switch: select/toggle theme | `light`, `dark`, `toggle` | none | none | `light-switch-theme` |
| Light Switch: scheduling | `schedule enable --mode fixed-hours/sunset-to-sunrise/follow-night-light`, `schedule disable` | none | none | `light-switch-schedule`; actual hours/location configured in module Settings |
| Power Display: discovery/read/profiles | `list`, `get`, `capabilities`, `profiles`, `--setting`, `--monitor-id/--monitor-number`, `--json` | none | text | `power-display-inspect`; get/capabilities use one monitor; structured JSON Lines is text |
| Power Display: absolute/relative continuous adjustment | `set/up/down --brightness/--contrast/--volume`, `--step` | none | none | `power-display-adjust-action`; single stable ID or comma-separated monitor indices |
| Power Display: discrete settings | `set --color-temperature/--input-source/--orientation` | none | none | `power-display-discrete-action`; supports monitor batches; input/orientation may disrupt desktop |
| Power Display: saved profile | `apply-profile <positive numeric ID>` | none | none | `power-display-profile`; inspect profile first; name/GUID is not the CLI selector |

## Deliberate exclusions and review cases

These are not silently advertised as supported, and not all native transport or
presentation switches warrant a separate script recipe.

| Scenario | Status / reason |
| --- | --- |
| CLI help/version, aliases, completion/response-file syntax | Presentation or alternate syntax, not new functionality. Recipes use canonical flags; no response-file or raw argument passthrough. |
| Advanced Paste `--clipboard` / `--output-clipboard` | Not scaffolded: the consumer owns input/output. Host actions must not quietly read unrelated global clipboard data or replace it themselves. |
| Advanced Paste inline image/audio primary input | **Review / bridge required.** Current host CLI bindings accept primary text/files, not inline media envelopes. Saving a consumer-provided image/audio asset to a file needs a reviewed adapter; pretending the envelope is a path is invalid. File-based OCR/image/media actions are supported above. |
| PNG/JPG as inline image output; MP3 as inline audio output | **Review / bridge required.** Current recipes return files; MIME/encoding/size validation and a declared media envelope are required for inline results. MP4 must remain files. |
| Image Resizer inline image-to-image | **Review / bridge required.** Safe batch-file resizing is supported; inline media needs explicit staging and validated conversion back. |
| Saved/custom AI action output | Author selects text or files recipe explicitly. Text variant fails on file-only output; files variant requires one supported text/file result. No untyped polymorphic success or guessed media type. |
| File Locksmith `--kill` | Intentionally excluded: destructive process termination needs separate design/consent. Report and bounded wait are available. |
| Image Resizer `--destination` / `--replace` | Recipe fixes a new workspace destination and `--replace false`, operating only on copies. Writing arbitrary host destinations/replacing originals is intentionally not offered. |
| Image Resizer `--progress-lines`, Power Display `--quiet`, action JSON status | Presentation/diagnostics, not primary outputs. File/action recipes keep diagnostics on stderr; warnings are not silently suppressed. Existing legacy report recipes retain their original behavior. |
| Power Display `--power-state` / `--confirm-power-off` | Intentionally excluded: display power-off/standby can remove the desktop and needs separate design/consent. |
| Awake detached/background persistence | **Review required.** A background process must not outlive Host timeout/cancellation accidentally. Current recipe waits and preserves native lifecycle restrictions. |
| Settings live enable/disable while Runner runs | Not supported by the current native Settings CLI. Templates cannot bypass the settings lock or GPO. |
| Arbitrary executable, elevation, raw native arguments, multiple host actions | Intentionally excluded. Normal trust authorizes one known canonical recipe and reviewed typed bindings, not arbitrary host code. |

## Adding a CLI or functionality

1. Read the native command parser, input reader and output writer, not just help
   text. Record command/flag coverage and restrictions in this matrix.
2. Choose primary input/output per scenario. Separate queries, actions and
   generated-file results when their contracts differ. Preserve existing recipe
   IDs/bindings/contracts; use new IDs for incompatible changes.
3. Add the recipe in `PowerShellCliTemplates.Scenarios.cs`. Update module
   identity/group metadata if adding a module, scoped capabilities if needed,
   and `SupportsFileBatch` when the native operation genuinely accepts batches.
4. Keep native arguments separate and typed. Stage new results in the execution
   workspace, validate them, preserve nonzero failures, and never auto-approve,
   bypass the Host broker or retry uncertain side effects.
5. Extend the focused template/broker tests. Check catalogue JSON, creation
   schemas, generated PowerShell, correct results/failures and legacy
   compatibility. Real AI/desktop/power/process actions require user consent;
   use isolated fixtures/mocks for automated regression coverage.
