# Consumer contracts

Read the relevant section only. Matching scripts can appear in multiple consumers;
there is no per-script module assignment, `surface`, or `kind`.

`consumers list` reports enablement/policy, not UI installation/capabilities.
Attributed discovery applies that gate, not actual clipboard/selection matching.
Verify the installed adapter; unknown/newer contracts must remain unknown.
Module enablement lives in PowerToys Settings; integration switches live in
PowerScripts app > Consumer modules.

| Consumer ID | Input | Output/setup |
|---|---|---|
| `advanced-paste` | Clipboard text/files/image/audio; never none | Pasteable text/files/image/audio; typed prompts, automatic discovery |
| `keyboard-manager` | Ordinary action: none | Prefer none; manual saved shortcut, required options need defaults/saved arguments |
| `command-palette` | No primary input: none | None/text; no-parameter explicit activation runs and opens results; declared parameters use a form and Run |
| `file-explorer` (integration builds only) | Selected files; one compatible image/audio file | None/text/files/image/audio; typed prompts and separate exported results |
| `command-line` | Explicit authorized Host test | Any supported contract; does not test another consumer's UI/binding |

## Advanced Paste

- Text/HTML-text input; output markup stays literal, not rich HTML.
- Files must all match extensions/count. `files` results become a file selection,
  not automatic content previews; produce new files.
- Image input: clipboard bitmap or compatible files. Audio: compatible files.
  Check MIME/scalar/array constraints in [media](media.md).
- Scalar image output becomes a bitmap; image arrays/audio become files.
- Empty text/result collections are rejected. Current adapter bound: 60 seconds;
  runtime may be shorter. No per-script installation list/allowlist.

## Keyboard Manager

Hotkeys do not supply clipboard input, prompt for missing options, or present
returned text/media. Prefer `none` to `none`; options are still allowed.
Verify host-action/sandbox feasibility, then use [manual setup](keyboard-manager.md).
Check the actual picker; older prototypes may not support current descriptors.
Do not revive removed fields or edit KBM JSON.

## Command Palette

Search script names or descriptions on Command Palette's main page to see compatible
scripts in the **PowerScripts** results section; use Load more for additional matches.
The **PowerScripts** entry also opens the full browsable list. Both support primary
input `none`, output `none` or `text`, and share their catalogue and script pages.
Searching and paging do not run scripts. Refresh stays in the actions menu, with retry
for discovery errors on the full list; it is not a script-list row.

- Explicitly activate a script with no declared parameters to start it and open
  its results page. Scripts declaring any parameters, including optional values
  with defaults, open a form and require **Run**.
- Discovery, browsing, filtering, page construction, and `GetContent()` never
  start execution. Normal Host trust, policy, and runtime checks still apply.
- Forms collect string, 32-bit integer, boolean, string-enum choice, and existing
  Windows file-path options. Optional values can be omitted; omitted values receive
  descriptor defaults. Supplied empty strings, zero, and false remain typed values.
- Text results appear above parameter controls on the same results page, with
  status, diagnostics, **Stop**, and explicit rerun. Empty text is valid; stderr
  alone is not failure. Nonzero exits and invalid output remain failures.
- The run bound is 60 seconds; runtime limits may be shorter. Stop/timeout attempt
  termination without guaranteeing rollback or that separate runtime work stopped.
  Navigating away preserves the current CmdPal session's run/result; returning
  without a new execution action does not rerun it. No automatic retry occurs.

Enable Command Palette and its PowerScripts extension, and check
**PowerScripts app > Consumer modules > Command Palette**. This integration defaults on; enabling it does
not enable the parent module or approve a script. Clipboard, primary file/media input,
file/media output, rich markup, and result saving are not supported by this adapter.
An auxiliary file-path option does not change primary input to `files`.

## File Explorer (separate integration builds)

Primary `files` accepts 1-1024 local regular files matching extensions/count.
Primary `image`/`audio` accepts one locally available PNG/JPEG/WebP/WAV/MP3/FLAC,
matching the declared MIME and scalar/array bounds, at most 16 MiB. Headers are
checked; no automatic transcoding or cloud hydration. Multiple selections/large
media need `files`. A `file-path` option alone does not supply primary input.

| Output | Presentation |
|---|---|
| Text | Separate UTF-8 `.txt` |
| Files | Copies of produced regular files; returning selected originals is rejected |
| Image/audio | Separate media files |
| None | No exported file; not permission to modify originals |

Use runtime workspace outputs; consumer handles destination names/collisions.
Default isolation makes selected inputs read-only. Cancellation does not undo
published files or script side effects.

Enable in **PowerScripts app > Consumer modules > File Explorer**.
File Explorer Add-ons owns parameter prompting and status delay (0-600 seconds,
default 2). Errors appear immediately; closing a running window hides, not cancels.
Scripts report stderr/nonzero failures, not their own status UI.

## Maintainers

Verify actual adapter I/O, presentation, file/media bounds, parameters/defaults,
discovery/bindings, enablement/trust, timeout/errors, and build availability.
Update affected references and `bundle.json` with integration contract changes.
