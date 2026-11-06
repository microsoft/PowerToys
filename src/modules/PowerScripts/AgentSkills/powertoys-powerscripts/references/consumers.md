# Choose the consumer before designing the script

The same package can appear in several consumers when its I/O fits. There is no
per-script module assignment, audience, `surface`, or `kind` field. Do not add
such fields to make a script visible. Each consumer owns matching, parameters,
result presentation, and any bindings.

## Confirm availability, not just catalogue membership

Run the resolved Host's `consumers list` and inspect effective enablement and
policy. This is not a capability schema or proof that a consumer UI is installed.
Administrative `list --json --include-disabled --include-unconfigured` explains
packages; `list --json --consumer <id>` applies that consumer's enablement gate,
not its clipboard/selection matching logic. Verify the installed module and
actual input/output compatibility separately.

The core registers Advanced Paste and Keyboard Manager. File Explorer integration
is developed separately and is available only in builds containing that adapter;
its profile below must not be interpreted as availability in every core build.
If another integration or a newer contract is not documented, inspect its
installed help/UI or obtain its contract. Do not guess its consumer ID, supported
shapes, parameter UI, or binding API.

## Compatibility overview

| Consumer | Purpose and primary input | Output and presentation | Parameters and setup |
|---|---|---|---|
| Advanced Paste (`advanced-paste`) | Transform the current clipboard: `text`, `files`, `image`, or `audio`; never `none` | `text`, `files`, `image`, or `audio`; never `none`. The paste window consumes the result. | Collects typed options in its own window. Compatible scripts appear automatically; no per-script binding. |
| Keyboard Manager (`keyboard-manager`) | Trigger an action from a configured shortcut. Design the ordinary hotkey case as `none`. | Prefer `none`: launching the Host is not a paste/save-results UI. Do not promise visible text/file/media results. | Required values need defaults or a verified saved-argument mechanism; no generic runtime parameter prompt. Manual shortcut assignment is separate from creation. |
| File Explorer (`file-explorer`, integration builds only) | Process selected local regular files: `files`, with extension/count matching | `none`, `text`, `files`, `image`, or `audio`. Its adapter exports separate results; see below. | Collects typed options and owns script selection and result destination. No direct mutation of selected originals is implied. |
| Direct Host (`command-line`) | Explicit user-authorized test of any supported input | Structured `transform` results or `run` output, not consumer-specific UI | Agent supplies the declared request/parameters. This tests the engine, not another module's adapter or shortcut. |

## Advanced Paste

Choose this for requests such as "clean up copied text" or "turn copied files
into a report to paste." A no-input action cannot appear in its action list,
even if its output would be pasteable.

- `text` uses the clipboard's text/HTML-text representation. Returned text,
  including markup, remains literal text; do not promise rich HTML rendering.
- `files` uses clipboard file paths and requires every selected extension and
  the selection count to match. Return newly produced files, not originals.
- `image` can use a clipboard bitmap or compatible image files; `audio` uses
  compatible audio files. Check MIME types and scalar/array bounds, not merely
  the word "image" or "audio".
- Scalar image output becomes a bitmap; image arrays/audio become files.
  `files` output becomes a file selection, not an automatic preview of contents.
- Empty text or an empty result collection is not a usable AP result.
- Its current execution adapter has a 60-second cancellation bound; the runtime
  may impose a shorter one. Do not promise unbounded background execution.

Enable the module and its PowerScripts integration through Settings when the
user chooses to do so. Then open the paste window with compatible clipboard
content. There is no AP script-install list or per-script allowlist to edit.

## Keyboard Manager

Choose this for an action such as "ensure this existing VPN profile is connected
when I press Ctrl+Shift+3", not for a clipboard transform merely because the user
mentioned a keyboard shortcut.

`none` means no primary input; it does not mean no named parameters. Determine
the provider/profile and all required options. A hotkey cannot collect missing
answers unless its actual integration supports that configuration.
Host-side VPN or desktop effects still need runtime/sandbox feasibility checks.

Use the [conditional manual handoff](keyboard-manager.md). Check the installed
picker before promising it exists. Older prototype UIs may not understand the
current contract; do not resurrect removed descriptor fields to accommodate them.
Do not edit bindings/KBM JSON or automatically approve trust.

## File Explorer integration builds

Choose this for "run a script on these selected files". The adapter accepts
1-1024 local regular files, not directories; every selected file must match
the schema's extensions and count. A scalar `file-path` parameter alone does
not make a script a primary `files` script.

Enable this integration in **Settings > PowerScripts > Use PowerScripts in >
File Explorer**. File Explorer Add-ons owns parameter prompting and the
execution-status delay (0-600 seconds, default 2). Errors appear immediately;
closing a running status window hides it without cancelling. Scripts should
report failures through stderr/nonzero exits, not open their own status UI.

Results are exported to the consumer-selected destination:

- `text`: a separate UTF-8 `.txt` file, not a notification or pasted text.
- `files`: copies of produced regular files; returning selected originals is
  rejected. Let the consumer handle destination names and collisions.
- `image`/`audio`: separate files with the corresponding formats.
- `none`: no result file. It does not grant permission to modify originals.

Write outputs into the supplied runtime workspace, not beside the originals.
Default filesystem isolation makes selected inputs read-only. Report failures
and partial exported results honestly; cancellation does not roll back already
published files or guarantee that a script's side effects were undone.

## Adding or changing a consumer profile

Keep this bundle aligned with integration releases. For each profile record:
purpose and stable ID; supported inputs/outputs and their precise presentation;
file/media constraints; parameter/default collection; automatic discovery versus
saved bindings; enablement and trust setup; timeout/cancellation/error behavior;
and availability/version caveats. Verify these against the actual adapter, not
just the shared Host. Update this overview, any detailed handoff, and
`bundle.json` together. An unknown consumer must remain explicitly unknown.
