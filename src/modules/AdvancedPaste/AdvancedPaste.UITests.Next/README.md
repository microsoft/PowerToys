# Advanced Paste UI tests

Black-box UI tests using `Microsoft.PowerToys.UITest.Next` and the repository-pinned
winapp CLI. The legacy `UITest-AdvancedPaste` project is retained unchanged. Its
HTML, XML, expected results, and initial settings are linked into this project's
output rather than duplicated.

## Coverage

| Surface | Coverage |
|---|---|
| Legacy plain-text scenarios | Real rich-text paste control proves the source is formatted, then verifies formatting removal and subsequent normal paste through the direct hotkey, action list, and Ctrl+1. No WordPad dependency. |
| Legacy Markdown scenarios | All three invocation routes and the existing HTML/result fixtures; HTML clipboard preference, emphasis, links, script and footnote removal. |
| Legacy JSON scenarios | All three invocation routes and the existing XML/result fixtures; comma/semicolon/tab CSV, declared separators, escaped quotes, INI, plain-text fallback, and already-valid JSON. |
| Text fidelity | Unicode, supplementary characters, line breaks, tabs, trailing spaces, and a large clipboard value. |
| Windows OCR | Bitmap and file clipboard sources, preview acceptance by button and Enter, direct-hotkey bypass, Settings-controlled automatic paste, and no-text error recovery. Uses Windows OCR, not an AI provider. |
| File paste | TXT, HTML, and PNG via action list and direct shortcuts; real Explorer paste, exact copied bytes, UTF-8 content, PNG dimensions and pixels, and source-file preservation. |
| Media conversion | PCM audio to MP3, video audio extraction, and H.264/AAC MP4. Deterministic fixtures use Windows media APIs; output streams, dimensions, duration, and same-extension naming are checked. |
| Clipboard history | Select/delete specific test-owned Windows history items, preserve unrelated IDs, and disable history through Settings. Windows history APIs are the authoritative shared-state observation. |
| Lifecycle and keyboard | Real Settings OFF/ON with process exit/restart and all configured hotkeys disabled; live shortcut editing, cancel, reset, and clearing optional shortcuts. |
| Settings and window behavior | Clipboard preview refresh/visibility, close-on-focus-loss, Escape, empty clipboard, offline action/group visibility, preference persistence, and unavailable AI with no providers. |

## Deliberate exclusions

The fixture enables only Advanced Paste and starts with AI disabled and no
configured providers. Tests never supply credentials, download models, or execute
cloud or local-provider inference.

OpenAI/other provider requests, Foundry Local, Ollama/local inference, Phi Silica,
custom AI transformations, spelling/coaching inference, and AI result
regeneration/provider switching are excluded. Settings-only availability and
visibility remain covered. OCR preview covers the shared preview/paste surface
without requiring a provider.

## Execution requirements

- A logged-on interactive, non-administrator Windows desktop.
- Matching PowerToys and test architecture, the pinned winapp CLI, and .NET 10.
- English UI, Segoe UI, the built-in `en-US` Windows OCR language, and the standard
  Windows audio/video codecs. No separately installed inference engine is needed.
- Release Runner/Settings companion signing through the existing test pipeline
  setup. The `AdvancedPaste` project family participates in
  `$requiresAuthenticatedSettingsIpc`; lifecycle tests do not bypass IPC by editing
  the enabled map and restarting the Runner.

Build with the existing repository tools:

```powershell
tools\build\build.cmd -Path src\modules\AdvancedPaste\AdvancedPaste.UITests.Next -Platform x64 -Configuration Release
```

Use the `ui-tests-local-vm` skill to stage the output and run the complete suite on
separate Windows 10 and Windows 11 VMs. The executable is
`AdvancedPaste.UITests.Next.exe`, and the category is `AdvancedPaste`.
Focused filters are for diagnosis, not sign-off. Follow `ui-tests-pipeline-ci`
only after the required full local matrix is green.

The destination is a message-pumping STA rich-text window. Extended clipboard
fixtures use real Windows clipboard formats; files are pasted into newly opened,
uniquely named Explorer destinations. Hotkey input is queued as a complete native
batch so the test's modifier releases cannot interrupt the module's own Ctrl+V.
Settings and clipboard state are restored, generated files and windows are
removed, and failure media is captured before cleanup.

The paste destination and Settings keyboard-input surface are selected with a
single click on their normal taskbar buttons. The destination acquires editor
focus on its own STA thread. It is never forced topmost, minimized, or
reactivated to rescue a failed paste. Its visibility is checked across AP
activation, which sends the activation shortcut only once.

Before file actions, the fixture focuses Explorer's empty content area and
requires native keyboard focus under `SHELLDLL_DefView`; an empty `UIItemsView`
does not expose a keyboard-focusable UIA item. Action-list clicks require stable
geometry, foreground ownership, and pointer arrival before one
real click. Readiness failures report the individual gates rather than retrying
the paste or copying the generated file into the destination.
File delivery is observed on disk before reading CF_HDROP, so the test does not
open the clipboard while Explorer is consuming the product's Ctrl+V.
Text and rich-text clipboard access uses the fixture's message-pumping STA.
Read errors are reported rather than converted to an empty string, and RTF
fixtures are round-tripped before the formatting-removal scenarios start.

History tests use a fresh process per case so restoring the OS history preference
does not carry an old ItemsView and pending notifications into the next fixture.
They remove only their own entries and fail explicitly rather than
evicting unrelated history when the OS history has insufficient capacity or the
disable scenario cannot safely start with an empty history.
Cleanup clears only the current test-owned clipboard content before re-enabling
history, then drains late test-owned IDs while preserving all original entries.

## HTML-only conversion regression

`HtmlOnlyClipboardIsPastedAsTextFile` guards against the former native process
termination in `Windows.Data.Html.HtmlUtilities.ConvertToText` (`iertutil.dll`,
exception `0xc0000409`). The product now extracts the CF_HTML fragment and uses
managed HTML-to-text conversion for both clipboard HTML fallbacks, without
changing its compatibility manifests or relying on native startup prewarming.

`HtmlToTextHelperTests` additionally covers entity decoding, Unicode clipboard
offsets, block and line-break boundaries, tables, preformatted text, excluded
non-content nodes, deep markup, and plain-text precedence.
