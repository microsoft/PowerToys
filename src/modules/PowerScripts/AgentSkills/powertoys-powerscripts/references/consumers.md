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
| `file-explorer` (integration builds only) | Selected files | None/text/files/image/audio; typed prompts and separate exported results |
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

## File Explorer (separate integration builds)

Requires primary `files`, not merely a `file-path` option. Accepts 1-1024 local
regular files, no directories; all must match extensions/count.

| Output | Presentation |
|---|---|
| Text | Separate UTF-8 `.txt` |
| Files | Copies of produced regular files; returning selected originals is rejected |
| Image/audio | Separate media files |
| None | No exported file; not permission to modify originals |

Use runtime workspace outputs; consumer handles destination names/collisions.
Default isolation makes selected inputs read-only. Cancellation does not undo
published files or script side effects.

## Maintainers

Verify actual adapter I/O, presentation, file/media bounds, parameters/defaults,
discovery/bindings, enablement/trust, timeout/errors, and build availability.
Update affected references and `bundle.json` with integration contract changes.
