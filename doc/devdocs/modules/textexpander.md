# Text Expander

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Text%20Expander%22)<br>
[Feature request #5074](https://github.com/microsoft/PowerToys/issues/5074)

## Overview

Text Expander replaces short triggers with longer snippets as you type, in any app. For example, typing `!mysig` can expand into a multi-line signature. Snippets can contain variables such as the current date or time, a prompt for a value, and a cursor position marker.

## Architecture

```
textexpander/
├── TextExpanderModuleInterface/  # Native runner module DLL
├── TextExpander/                 # Expansion engine (PowerToys.TextExpander.exe)
├── TextExpander.Core/            # Shared snippets file (JSON), templates, matching, starter
├── TextExpander.Storage/         # Coordinated module-settings persistence
├── TextExpander.Editor/          # Standalone WinUI snippet-management app
├── TextExpander.Editor.UnitTests/# Editor, storage, and ownership tests
├── TextExpander.FuzzTests/       # OneFuzz targets for the snippets file and expansion
├── TextExpander.UITests/         # End-to-end UI tests: Settings, editor and expansion
└── TextExpander.UnitTests/       # Engine and shared-rule unit tests
```

The thin Settings page lives in `src/settings-ui` and owns enablement, engine options,
and the **Manage snippets** launch entry point. Configuration DTOs remain in
`Settings.UI.Library`; snippet editing, previews, file watching, backups, and moving
the snippets file belong to the module. Settings does not reference the editor executable.
Both processes use `TextExpander.Storage` for coordinated module-settings reads and
writes. Readers wait for publication rather than treating a transiently missing
destination as default settings. Matching-option changes revalidate an open snippet
draft without invalidating it; a change of snippets file still does.
The engine and editor reference `TextExpander.Core`, which has no Settings or WinUI dependency.

### Snippet editor (TextExpander.Editor)

**Manage snippets** opens `WinUI3Apps\PowerToys.TextExpander.Editor.exe`. Repeated
launches activate the existing editor, including restoring a minimized window.
Its window is titled "PowerToys Text Expander - Manage snippets" and, like the prompt
window, shows the Text Expander icon from `app.ico`, the same design as the icon in
Settings, which the engine project copies beside both executables.
Managing snippets does not require the expansion engine to be enabled. A disabled
group policy blocks editor startup; elevated Settings retains its existing restriction
on snippet editing rather than launching an elevated editor.

The editor owns its XAML, view models, resources (`PowerToys.TextExpander.Editor.pri`),
and focused tests. It observes module-settings changes from the Settings process and
reloads matching rules and the active snippets file. Closing it disposes both settings
and snippet watchers. The engine excludes both Settings and the editor from expansion,
so typing a trigger into the replacement editor cannot expand it.

`SnippetFile` reads and writes the snippets file, `SnippetTemplate` tokenizes
replacements for both expansion and preview, and `Variables.FormatDate`
provides shared .NET/strftime formatting. Trigger reachability uses the runtime's
`TriggerIndex` and `WordBoundaryRules`, not a transcription of engine behavior.
`SnippetTemplate`'s regular expressions use `RegexOptions.NonBacktracking`, so a
replacement full of unclosed `{{prompt:` takes linear time, not quadratic time.

### Module Interface (TextExpanderModuleInterface)

`dllmain.cpp` implements `PowertoyModuleIface`. It:
- Launches `WinUI3Apps\PowerToys.TextExpander.exe` when the module is enabled, resolving the path relative to its own DLL. A runner that isn't elevated starts it directly, so the engine inherits its medium-integrity token, which also works at sign-in before Explorer has created the shell window. An elevated runner starts it as a child of the desktop shell instead; if the shell can't be used, startup fails closed rather than passing on the elevated token.
- Passes the runner PID, `--managed --instance:powertoys`, and `--settings:"<path to settings.json>"`.
- Stops the engine by signaling `CommonSharedConstants::TEXT_EXPANDER_EXIT_EVENT`, then terminates it if it has not exited within 1.5 seconds.
- Honors the `ConfigureEnabledUtilityTextExpander` group policy, which `PowerToys.Settings.Cli` also applies to `list`, `enable` and `disable`.

### Engine (TextExpander)

A self-contained .NET process. Its keyboard hook, matching and injection use no UI framework; WinUI 3 is loaded only for the `{{prompt}}` window. It:
- Installs a low-level keyboard hook and tracks the characters typed into the focused app. Keys are captured only for the foreground window once it has been checked (integrity level, not PowerToys Settings), off the hook's path, after each foreground change. Activation through `AttachThreadInput`, which is how focus goes back to the app after a prompt, raises the foreground event before `GetForegroundWindow` reports the new window, so the hook also asks, once, for a check of a window in front that was never checked (`KeyboardHook.NeedsAuthorization`).
- Asks for `{{prompt:<question>}}` answers in a small WinUI 3 window (`PromptWindow`): Mica, the Text Expander icon and "PowerToys Text Expander" in a custom title bar (the window pops up over other apps, so it names PowerToys; the engine's message boxes use the same caption), the question above a box, then **OK** (default) and **Cancel**. Prompts are required by default: OK stays disabled while the answer is blank, and Enter shows "Enter an answer to continue." without closing the window. `{{prompt?:<question>}}` is the explicit optional form and still accepts an empty answer. The window follows Windows' theme, including high contrast, is per-monitor DPI aware, stays on top, and opens a little above the middle of the target window's screen (or the pointer's, if that window has gone). It is exactly as tall as its content, title bar row included, with ContentDialog's footer: 24-pixel padding around 32-pixel buttons. Because the content extends into the title bar, the client area includes it, so the window is sized from its measured frame (`PromptWindowSize`) rather than with `AppWindow.ResizeClient`, which leaves room for a system caption that isn't drawn. Esc, Cancel and the close button leave that spot blank and go on to the next question. The keyboard hook is suspended while it is open, so nothing typed in it expands, and focus goes back to the target window afterwards. The box is emptied as soon as the answer is read, and answers are never logged. XAML runs on its own thread (`PromptHost`), started the first time a snippet asks for something, never on the hook's thread; the expansion worker waits there for each answer, so questions are asked one at a time. Measured on an ARM64 Debug build: the first prompt is in front after about 0.3 s and later ones after about 0.12 s; loading XAML adds about 45 MB of private memory to the engine from the first prompt on. If the Windows App SDK can't start, the Win32 fallback (`ClassicPromptDialog`) enforces the same required-answer behavior.
- Matches triggers against the snippets file, optionally ignoring capitalization and, by default, waiting for a space or punctuation before expanding. An underscore is part of a word, so it doesn't release a trigger: typing `addr_line` doesn't expand `addr`. An explicitly saved disabled value keeps immediate expansion.
- Replaces the trigger by pasting (the user's clipboard is saved and restored) or by typing with `SendInput`, depending on the Insertion method setting and snippet length. The pasted text, and the user's clipboard when it's put back, carry the exclusion formats for clipboard monitors, Clipboard History and Cloud Clipboard, so a paste doesn't record the user's last copy again; a clipboard that already carried any of them goes back exactly as it was. It's put back once the target has read the paste (at least 150 ms), or after at most 350 ms, timed by the clock. `SendInput` doesn't keep a batch together while any low-level keyboard hook is installed, so the engine's own hook keeps the user's keys out (`InsertionGuard`). The worker sends a start marker alone and waits for the hook's decision. From that marker on, the hook holds every key event that isn't the engine's, in order; clicks and input from lower-integrity apps aren't held. Only then does a paste borrow the clipboard, so a Ctrl+V pressed right after the trigger pastes what the user copied; if the clipboard refuses, the text is typed under the same guard. If borrowing the clipboard outlasts the guard's 2 s (Excel delay-rendering a large copy, say), the worker sends a new start marker before erasing: the hook decides again, and the held modifiers are read again. Then come the erase (after letting go of any Shift, Ctrl, Alt or Win the user holds), Ctrl+V or the text, the moves to `$|$`, the undo record, the paste's settle and restore, and the end marker. The hook never sends input itself. The worker presses again the modifiers it let go of, sends the held keys back with another marker, and repeats with whatever was held meanwhile. While they go back, only they pass, in order, along with what Keyboard Manager injects in place of one (Caps Lock remapped to Ctrl). Anything else, typed or injected (touch keyboard, AutoHotkey), waits for the next round, so a key-up never overtakes its own key-down. Held keys go back only if the target window is still in front; otherwise they're dropped. The guard lets go after 2 s without an engine event, or at 256 held key events (a timer sends them back), and typing that lasts longer than 2 s goes on unguarded. Typing sends one character per `SendInput` call, 25 ms apart and 25 ms more after `.`, `!`, `?` and line breaks, because Windows 11 Notepad drops and smears characters that arrive faster. The erase and the caret moves go out in calls of at most 1,000 key events. The target window is checked before each call that erases, pastes, types a character or moves the caret: once it has lost focus or can't be driven (an elevated window), the insertion stops there; a paste doesn't fall back to typing, and the clipboard is put back. If the clipboard can't be put back, a message box says so once per session, on a thread of its own after the insertion, so expansions carry on meanwhile. A `$|$` more than 2,000 characters before the end of the expansion is skipped, leaving the caret at the end. An undo goes through the same guard: the moves back to the end of the expansion, the Backspaces, then the original trigger.
- Expands variables (`{{date}}`, `{{time}}`, `{{datetime}}`, `{{isodate}}`, `{{date:<format>}}`, `{{clipboard}}`, `{{prompt:<question>}}`, `{{prompt?:<question>}}`) and places the cursor at `$|$`. `{{date}}`, `{{time}}` and `{{datetime}}` use the Windows regional format's short date and time; `{{isodate}}` is always `yyyy-MM-dd`. `{{date:<format>}}` takes names, AM/PM and the calendar from the regional format but types `/` and `:` as written. The regional format is read again when Windows reports a Region settings change (`WM_SETTINGCHANGE` with `intl`), so a new format applies from the next expansion without a restart. A replacement is the exact text: line breaks and tabs are real characters, a backslash is always literal, and only the first `$|$` places the cursor. A prompt answer, the clipboard and a formatted date are inserted exactly as they are, so a variable or `$|$` in them stays literal.
- Supports undo: pressing Ctrl+Z immediately after an expansion restores exactly what was typed, in the capitalization it was typed in, even when matching ignores case. If the snippet's `$|$` left the caret inside the expansion, the undo first presses Right as many times as the insertion pressed Left, so it erases from the end of the expansion. Modifier key-downs on the way to Ctrl+Z do not cancel the pending undo, but any other key does; Ctrl+Shift+Z is left to the app as redo. The undo waits up to one second for modifiers to be released, and is abandoned otherwise, so its Backspaces are never sent as Ctrl+Backspace.
- Drops a queued expansion or undo, instead of applying it where the caret is now, when a key reaches the app or the mouse is clicked after the trigger matched; a modifier key-down on its own doesn't count. While a `{{prompt}}` window is open, only a key pressed while the window being typed into is in front, or a click on that window, counts. If that window isn't known, any key or click outside the engine's own windows counts. The worker checks the hook's input generation before it opens a prompt and right before the start marker. The hook decides at the start marker, and again at a new one if a slow clipboard borrow outlasted the guard; the worker checks once more right before the erase, since clicks aren't held. Nothing is erased if the guard ended before the start marker, or if the marker didn't come while keys were held: those keys went back where the erase would take them. The keystroke that completed the trigger isn't typed back, so typing straight on within about 15 ms of finishing a trigger skips that one expansion. Keys typed right after the last prompt answer is accepted are held and land after the expansion; a key pressed at the same instant as Enter reaches the prompt before the hold starts, and is lost. A foreground change alone doesn't cancel the work; once the insertion has started, it stops the insertion and drops what was held. Backspace with Ctrl, Alt or Win clears the rolling buffer instead of removing one character.
- Ignores its own injected input, the runner's replayed hotkeys (`0x110`) and the key events Keyboard Manager swallows itself (`0x111`). Input carrying Keyboard Manager's injected bit (`0x1`, as in its `0x11` and `0x101` remaps) counts as typing. With **Use PowerToys Keyboard Manager remaps when matching triggers** off, that input isn't matched against triggers but still reaches the app, so a key other than a modifier still clears the rolling buffer and ends the one-step undo. The engine's own tag, random for each run, carries that bit too, so Keyboard Manager's key remaps leave its Backspaces, arrows and Ctrl+V alone; its shortcut remaps skip only `0x101`, so a remap of Ctrl+V itself still applies. Held keys go back with the tag they came with, so a key Keyboard Manager already remapped isn't remapped twice.
- Runs at the desktop user's integrity level and refuses synthetic input to elevated windows. This preserves expansion in ordinary desktop apps when PowerToys itself is elevated, while preventing user-controlled settings and snippet files from authorizing elevated input or being read by an elevated engine.
- Checks its actual integrity level before loading host settings or snippets and exits if the check fails or the process is above medium integrity.
- Exits at startup, before taking its single-instance mutex, loading settings or snippets, or installing hooks, when the `ConfigureEnabledUtilityTextExpander` group policy turns Text Expander off, so starting `PowerToys.TextExpander.exe` directly doesn't get around it.
- Runs one engine per signed-in session: its single-instance mutex is `Local\PowerToys.TextExpander.SingleInstance` plus the `--instance:` suffix, like the runner's instance mutex and the exit event.
- Clears its rolling input buffer and one-step undo state while focus is in a detectable standard Windows password box: an `Edit` or `RichEdit` control, or a WinForms `WindowsForms10.EDIT.*` text box, with the `ES_PASSWORD` style. Detection uses only the focused window's class name and style, and sends no messages to other processes. Password fields on websites (Chromium, Firefox), and WPF and WinUI `PasswordBox` controls, don't expose that style and aren't detected, which the **Good to know** note says.
- The keyboard hook isn't aware of input method editors. While a Japanese, Korean or Chinese IME is composing, the hook sees the Latin keystrokes, not the composed characters, so a trigger can fire mid-composition, and its Backspaces can remove characters before the trigger. **Good to know** asks people to switch the IME to direct input or English before typing a trigger.
- Does not retain undo entries for snippets using `{{prompt:...}}` or `{{clipboard}}`, because those expansions contain user-supplied values. Clipboard snapshot and temporary paste buffers are zeroed after restoration or publication.
- Watches `settings.json` and the snippets file and reloads them without a restart. A snippets file that's missing or can't be read doesn't stop it (see *Where snippets are saved*).
- Takes its words (the prompt window and its few message boxes) from `Strings\en-us\Resources.resw`, built into `PowerToys.TextExpander.pri` and localized with the rest of PowerToys, through `EngineText`. Each call carries its English, which is used when the resources can't be read, so a missing file never stops a prompt or a message from showing. A test checks that every English fallback matches its resource. The messages point to settings rather than to environment variables: if the clipboard can't be restored, to **Advanced** > **Insertion method** > **Always type**; if the engine finds itself elevated, to restarting PowerToys without administrator rights.
- Exits when the runner process exits or the exit event is signaled.

### Settings and snippet storage

| File | Location |
| --- | --- |
| Module settings | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\settings.json` |
| Snippets (default) | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\PowerToysTextExpanderSnippets.json` |

The snippets file can be moved or switched in the snippet editor, whose UI calls it the "snippets database" (see *Where snippets are saved*), and the `snippets_path` setting (`SnippetsPath`) holds its full path. The editor and engine select it with one shared core-library rule (`SnippetPathResolution.FromConfiguredPath`), so both read the same source-of-truth file: surrounding spaces and quotes are trimmed, environment variables are expanded, and a fully qualified path is the file, whatever its name, while a blank value or a relative path (the two processes share no working directory) selects the default file. Only the default location is ever created, with the starter. A chosen file that's missing, or whose folder is, is reported and never created, because a sync service would copy a new starter there to the user's other PCs. Shared Settings does not resolve or read snippet files. If the editor is launched directly as administrator, snippet-file resolving, reading, and editing remain disabled. Elevated Settings disables **Manage snippets** and tells the user to restart PowerToys without administrator rights. The engine runs with the desktop user's token even when the runner is elevated; a configured path that the desktop user cannot read will therefore fail to load rather than being read with elevated access. If PowerToys is elevated with a different administrator account, the engine runs as the signed-in desktop user and may be unable to read that account's settings and snippets.

The editor saves the path with `TextExpanderSettingsUtils.UpdateSnippetsPath`, which
re-reads the latest `settings.json` under a path-derived cross-process claim and changes
only `snippets_path`, so options saved in Settings meanwhile are kept.
`SaveSettingsOrThrow` saves the JSON it's given in full, and `SaveSettings` takes the
same claim and atomic publication but logs a failure instead of throwing. Each save
holds the claim while it writes and flushes a unique staging file beside
`settings.json` and publishes it with an atomic replace or create-only move, so a later
participating save waits. If publication reports a failure after completing,
compensation proceeds only while the destination still has the exact bytes this save
published, so a sync client or older build that changes the file is never overwritten.
A failed save reports whether the previous settings were verified, the new settings
were verified, or the state is uncertain. The editor then re-reads `settings.json` and
uses the path stored there, relying on that report only when the file can't be read; an
uncertain result removes no snippet files, and its error names the settings and
recovery files to keep.

**Snippets file format.** The file is JSON; Text Expander never reads or writes another
format. It holds a `version` (`SnippetFile.FormatVersion`, 1) and a `snippets` list with
a `trigger` and a `replacement` for each snippet, in order. A replacement is the exact
text: line breaks and tabs are real characters, written as JSON's `\n` and `\t`.
Reading (`SnippetFile.Read`) is lenient about hand edits: it skips a UTF-8 byte order
mark and comments, allows trailing commas and property names in any case, and gives a
snippet without a trigger or replacement an empty one. A file that isn't JSON, or has no
`snippets` list, throws `SnippetFileFormatException`, with the line number when it isn't
JSON; it's reported and never written over. Duplicate triggers stay in the file, and the
last one wins. Writing produces indented UTF-8 JSON without a byte order mark, keeping
letters, symbols and HTML characters as they are rather than as `\u` escapes, and keeps
properties Text Expander doesn't know, in the file and in each snippet
(`[JsonExtensionData]`). A file whose `version` is newer than `FormatVersion` still
reads, but the editor refuses to save over it, saying a newer version of PowerToys saved
it. A file with text that can't be written back as JSON, such as half of an emoji in a
property Text Expander doesn't use, still reads and expands, but the editor won't save
changes to it (`SnippetFileWriteException`) until it's fixed in a text editor. Saving
doesn't keep comments or the file's own layout, so the first save backs up a file that
the editor wouldn't have written the same way. For example:

```json
{
  "version": 1,
  "snippets": [
    { "trigger": "!mysig", "replacement": "Best regards,\nYour name" },
    { "trigger": "!myaddr", "replacement": "One Microsoft Way\nRedmond, WA 98052" }
  ]
}
```

**Starter file.** When the default file is missing, the engine or the editor, whichever
gets there first, writes the starter (`TextExpanderStarter`): six snippets, `!ddate`,
`!ttime`, `!ddt`, `!isodate`, `!mysig` and `!myemail`, none of which reads the clipboard
or asks for input. It's written beside the file and moved into place without
overwriting, so a process or app that creates the file at the same moment wins and its
file is loaded instead, and nothing reads a half-written starter. Existing files,
including empty files, are never replaced. The editor creates or loads the starter
before it shows the list. An engine test checks that the engine and editor use the same
file name (`TextExpanderSnippetFileNames.Current`).

Bug reports keep only `settings.json`, `diagnostics.txt` and the logs of the PowerToys
`TextExpander` folder, so a snippets file of any name there, such as one that **Move
snippets database…** left behind, and its backups, temporary files and a sync service's
keep-both copies stay out. The configured file is left out wherever else it is in the
PowerToys folder, and so are its backups, temporary files and keep-both copies in its own
folder (`TextExpanderPrivacy.h`, tested by `tools/BugReportTool/BugReportTool.UnitTests`).
The configured path is masked in the report's copy of `settings.json`, and
`diagnostics.txt` records the file's location only as `snippets_location=default` or
`snippets_location=custom`, with `snippets_file_exists`, never as a path.

### Settings page layout

The page contains enablement and **Manage snippets**.
**Advanced** holds **Wait for space or punctuation**; **Insertion method** (Automatic,
Always paste, or Always type), whose **Minimum length to paste** is available only
with Automatic, the one method that chooses by length; **Case-sensitive snippets**; and
**Use PowerToys Keyboard Manager remaps when matching triggers**.
The page only reads `settings.json`: a missing file shows the defaults without creating
it, and a file it can't read shows the defaults with **Can't read Text Expander options**
and is left unchanged; the bar's **Open folder** selects the file in File Explorer. Each
option change re-reads the file first, so changes are refused until it can be read.

The separate editor follows Keyboard Manager's layout: a title bar with the icon, the
title and **Search snippets** (Ctrl+F); then **New snippet**, the snippets button
(**Saved on this PC ▾**, **Saved in OneDrive ▾** or **Saved in *folder* ▾**) and
**ⓘ Good to know**; then one card with the **Snippets** heading, the count and the rows.
**Good to know** has four notes: don't save sensitive info in snippets; snippets don't
expand in apps running as administrator or in standard Windows password boxes; Ctrl+Z
right after an expansion changes it back; and switch an input method editor (IME) to
direct input or English before typing a trigger.

### Managing snippets in the editor

**Wait for space or punctuation** is a module-wide setting rather than a field in
each snippet row. A clean settings file defaults it on, and adding a snippet uses
that active value. Loading an existing settings file preserves an explicitly saved
`false` value. The snippets file, including the starter, stores only triggers and
replacements, so no starter or existing snippet is rewritten.

Search matches triggers and replacement text without changing file order. The count
reads "14 snippets", or "Showing 2 of 15 snippets" while searching, and **Clear search**
appears only when nothing matches. Click or Enter edits a row, and **More options** has
**Edit**, **Duplicate** and **Delete**. Rows are one Tab stop, and the arrow keys move
between them. A reload numbers the snippets again, so it closes an open **More options**
menu, and **Edit**, **Duplicate** and **Delete** refuse a row from before it, including a
delete confirmed after a reload; the list then shows the reloaded file. A delete that
removes nothing isn't reported as deleted.
**Duplicate** opens the copy in the editor, unsaved, with a trigger that can fire
(`!mysig` → `!2mysig`; `!mysig2` would never fire, because typing it completes
`!mysig` first).

Each row shows the trigger as a key, trimmed when long, with the whole trigger in a
tooltip. Then comes "inserts", or "asks *questions* then inserts" with each question
once as a bubble, and then an accent chip with an icon and up to three real lines of
what the snippet inserts: blank lines at either end are skipped, tabs show as spacing,
variables and questions show as bubbles and the cursor as a bar, and "…" follows when
more lines do. A snippet that is one value, or only dates and times with punctuation,
shows them as text, with that value's icon. WinUI draws an inline element that falls
past a trimmed line's ellipsis at the start of the line, over the text, so a trimmed
line with bubbles shows as text instead (`TextOnlyWhenTrimmed`,
`TextExpanderTrimFallback`), and tries its bubbles again when the row is clearly wider.
`TextExpanderRowDescriber` works this out (`TextExpanderRowDescription`), and the view
model refreshes it whenever the list or **Wait for space or punctuation** setting changes.

A row that can never expand gets a line under it: a caution badge reading
**Never expands**, then the reason, by the engine's own rules for the current mode
(`TextExpanderTriggerReach`): another trigger completes partway through typing this one;
with **Wait for space or punctuation**, only when a space, punctuation other than `_`, or
a symbol in this trigger releases it at a word start; or a later row has the same
trigger, since the last one wins. The badge has a caution icon and the reason is spelled
out, so color is never the only cue.

A row's screen reader name is "*trigger*: *text*", with each bubble, line break and tab
spoken in words, then the number of lines when there are several ("…, 2 lines"), then
"Never expands: *reason*" when it applies.

The dialog is titled **New snippet** or **Edit snippet** and follows Keyboard Manager's
mapping: **Trigger**, an arrow, then **Replacement**, with the preview across the
bottom. Under the **Replacement** box, a caption says "Don't save passwords or other
sensitive info in snippets. They aren't encrypted.", and screen readers hear it with
the box's help text. **Good to know** and the Welcome page (`Oobe_TextExpander_HowToUse`
in the Settings resources) give the longer form: "Don't save passwords, Social Security
numbers, credit card numbers or other sensitive info in snippets. They're saved as plain
text and aren't encrypted."

The editor validates the trigger as you type: it can't be empty, contain a line break,
start with a hidden character or start or end with a space, and no other snippet may
use it (`SnippetProblem`, mapped to resources by the view model). The reason shows in
one red bar across the dialog, in the user's language, and **Save** stays off while it
shows; the **Trigger** box is outlined in red when the trigger is what's wrong. The
reason is announced when it appears or changes and is the **Trigger** box's full
description. An empty dialog stays quiet, but once there's text, a missing trigger says
"Type a trigger to save this snippet." Save errors stay in the dialog with your text intact. Closing an edited
snippet (Cancel, Esc, or the window's close button or Alt+F4) shows "Discard your
changes?" at the bottom of the dialog, above Save and Cancel, with **Keep editing**
(focused; it returns to the field you were editing) and **Discard**. When closing the
window asked, the window comes to the front and **Discard** also closes it. Empty libraries
offer optional signature, email, and date examples that open in the editor;
choosing one does not save it automatically.

The preview shows what the snippet types, in up to eight whole lines (fewer when the
dialog is short), the last ending in "…" when more follow. Values that are only known
when it runs are drawn as bubbles in the style of a KeyVisual key: **Clipboard**, a
question's label, and dates and times evaluated as they are now, in the user's regional
format. The first `$|$` is a thin cursor marker, and line breaks and tabs show as they
are typed.
It never reads your clipboard or asks for input. `TextExpanderSnippetPreview`
uses the core library's template parser and date formatter, so runtime values remain
literal and date formats agree with expansion without a second strftime table.
The preview's accessible name spells out each bubble, for example
"Clipboard, filled in when the snippet runs", and the hint under it appears only
when there are bubbles.

The **Replacement** box is a `RichEditBox` kept to plain text by
`TextExpanderReplacementEditor`. Variables the engine understands are shown in
accent text on a faint tint, with their braces; anything else in braces is left
plain. Clicking inside a variable selects it whole, and a second click places
the caret, so a label or format can still be edited. Backspace or Delete next to
a variable removes it whole. Paste is plain text, formatting shortcuts are off,
spell check is off, and Tab moves focus as before. Undo comes from
`TextExpanderEditHistory`, about a word at a time, because RichEdit's own undo
would record the recoloring. It keeps 200 steps at most, fewer of long texts (about
4 million characters in all), and none once the dialog closes. The text the box
reports uses `\n` line breaks, which is how the file stores them. **Insert variable** never
puts a variable inside another one; after a question or custom date it selects
the label or format, ready to type over. Its items show what each date or time
variable gives, filled in each time the menu opens. In high contrast the
variables are not colored but underlined, so they still stand out.

**Custom date format** is a submenu of ready-made formats (`dddd`, `MMMM d, yyyy`,
`dddd, MMMM d, yyyy`, `MMM d`, `MM/dd/yyyy`, `yyyy-MM-dd`, `h:mm tt`, `HH:mm`), each
shown as it comes out now and named with its code; picking one inserts
`{{date:<format>}}` with the caret after it. **Custom format…**, after a separator,
inserts `{{date:dddd}}` with the format selected, as before. While the caret or
selection is in a `{{date:…}}` variable, a **Date format codes** guide shows under
the box: chips for `yyyy yy`, `MMMM MMM MM M`, `dddd ddd dd d` and `h HH mm tt`,
each with what it gives now, that put their code in at the caret (never over the
braces); a "Shows as" line for the format at the caret; a tip to put words outside
the braces; and a **More codes** link to .NET's custom date and time format strings.
`TextExpanderDateGuide`, the preview, the row descriptions and the **Insert variable**
samples use the engine's shared `Variables.FormatToken`/`FormatDate` implementation in
`TextExpander.Core`, so they show the user's regional format; tests pin the same
examples for one moment and culture.

Colors in the box are set as character formatting, so they don't follow the theme
by themselves. Ordinary text gets the box's own text color, read from the
document's default format, which XAML keeps up to date for the box's theme and
state (`TextExpanderEditorColors`). RichEdit's "automatic" color is not used:
in a `RichEditBox` it is the Win32 window text color, black even in dark mode.
Typed text takes the color of the character before it, so every change recolors
the whole text; so do loading, opening the dialog, a theme or focus change, and
a change of Windows colors such as high contrast.

Successful snippet changes appear as an accessible status message.
File errors remain separate from empty-library and no-search-results states.
Use **Retry** to reload after a file-access error. Saving while a search is active
keeps the filter and includes a reminder in the status message.

Failed writes do not reserve triggers or apply deletions in memory. A reload advances
the snippet revision: it closes an open **More options** menu and the question flyout,
and an editor open on an existing snippet keeps your draft but must be reopened before
saving, so an old row handle cannot overwrite a different entry. A new snippet
(**New snippet**, **Duplicate** or an example) is checked against the reloaded file
instead and can still be saved. While it's shown, the editor watches the snippets file:
a `FileSystemWatcher` on its folder, paired with a 2-second poll that compares only the
file's existence, size and last-write time, off the UI thread, and reaches the UI only
when they change or the watcher reports an event or error. It reloads only when the
file's bytes differ from what it last read or wrote, so its own saves don't reload the
list.

The list is a virtualized `ListView`, so only the rows in view are built, however long
the list. A row's **More options** menu isn't in the list's tree, so the button and its
items carry their snippet in `Tag="{x:Bind}"`, which their handlers read; a test checks
the markup for it. A search changes the shown rows in one step
(`TextExpanderRowCollection`). A save updates only the row that changed and doesn't read the
file back: what was written is kept as the file's contents, so the next save still
notices an edit made elsewhere in the meantime.

### Where snippets are saved

The snippets button in the editor's toolbar says where the file is: **Saved on this PC ▾**
for the default location, **Saved in OneDrive ▾** when the file is under `%OneDrive%`,
`%OneDriveConsumer%` or `%OneDriveCommercial%`, and **Saved in *folder* ▾**, with the
folder's name, for anywhere else. Text Expander only compares paths
(`TextExpanderFolderText`) and doesn't inspect cloud accounts. It doesn't sync anything
itself either: OneDrive or another sync service transfers the file. The button's flyout
shows, top to bottom:

1. The snippet count and place ("Saved on this PC · 14 snippets"), then the file's full
   path on one middle-trimmed, selectable line, with the whole path in a tooltip and for
   screen readers.
2. **Open in text editor**, which opens a .json file in the app Windows uses for .json
   files, or in Notepad when none is set, and any other file in Notepad, so a script or
   shortcut chosen as the database never runs; and **Refresh**, which reads the file
   again and says "Snippets refreshed." The editor also does this by itself when the
   file changes.
3. The sync tip: a bold "Use your snippets on all your PCs", then two numbered steps,
   "Select **Move snippets database…** and save it in OneDrive or another synced folder."
   and "On each of your other PCs, select **Sync from another PC…** and pick that
   database." For a file that's already in OneDrive, the tip is "Use these snippets on
   your other PCs" and one step, "On each PC, select **Sync from another PC…** and pick
   this database."
4. A footer with **Move snippets database…** and **Sync from another PC…**.

**Move snippets database…** opens the Save dialog in the file's folder, or in Documents
while the file is in the default location, with the file's name and the
"Snippets database (*.json)" type. It writes the snippets to the chosen file and
switches to it; the previous file is left untouched. When the chosen file already has
snippets, the editor asks, after Windows' own replace prompt, "This database already has
snippets": **Use its snippets** switches to that file and writes nothing,
**Replace them** replaces them, and **Cancel**, the default, changes nothing. A file
that's replaced is backed up first, and the status message says so. Choosing the file
already in use changes nothing, and the button is off while the current file can't be
read. **Sync from another PC…** opens the Open dialog and switches to an existing file,
writing nothing; a file that isn't JSON with a `snippets` list is refused, with
**Choose a different database**. Both save the path in `snippets_path` before loading
the file, so the editor never shows a file that the engine isn't using, and the status
message says how many snippets are now in use and where.

Every save and move writes the snippets to a new temporary file beside the target
(`.tmp-<GUID>`), created exclusively and flushed to disk, which is then moved into place
or swapped in with `File.Replace`, so nothing reads a half-written file. A write that
fails deletes it, checking first that it still holds what was written, unless the failure
left nothing at the target, as a failed replace can: then it's moved into place instead,
or kept.
Backups go beside the file they copy, as `<file>.backup-<timestamp>`, with a numeric
suffix when needed. Before each save or move, the editor compares the file with the
bytes it loaded; if another app, PC or sync service changed it, the editor reloads the
list instead of saving. A chosen file that's gone isn't written again, and a file that
wasn't loaded as snippets is never written over.

When the file can't be loaded, an error bar above the list says why, the list stays
empty rather than suggesting there are no snippets, and **New snippet** and
**Move snippets database…** stay off until the file loads, on **Retry** or when it
changes:

- **Can't find your snippets database**: the chosen file was moved, renamed or deleted,
  or a folder is where the file should be. The bar shows the path, with **Retry** and
  **Sync from another PC…**.
- **Can't reach your snippets in OneDrive** (or in *folder*): its folder, drive, network
  location or OneDrive isn't available. It has the same buttons.
- **Can't open your snippets**: "Your snippets database has a mistake on line *n*…",
  "Your snippets database is missing its "snippets" list…", or the system's message,
  with **Retry**.

The engine (`SnippetStore`) keeps running when the file is missing or can't be read. A
file it can't read, such as one with a JSON mistake, leaves the snippets it last read,
or none at startup; a chosen file that's gone gives no snippets, logged once, until it's
back; and a missing default file gets the starter again. The engine watches the file's
folder with a `FileSystemWatcher` filtered to the file's name, which also sees editors
that save by renaming a temporary file over it. While the file isn't loaded and watched,
as for one in OneDrive before OneDrive starts at sign-in, it tries again every 30
seconds. It publishes the path, the parsed snippets and the watcher together as one
generation-tagged state, which the keyboard hook reads without locking. A failed reload
keeps the previous state and retries a bounded number of times, 250 ms apart; a new file
is read once more after its watcher starts, so a change in between isn't missed; and
bursts of watcher events coalesce, so an older read can't replace a newer one. A read
that finds the same bytes as the snippets in use (by SHA-256), at the same path and with
the same case setting, keeps those snippets instead of parsing the file again, as when a
sync service touches the file without changing it.

While the editor is elevated, these actions and all other snippet editing remain
unavailable; no snippet path is resolved or displayed. See *Settings and snippet
storage* above.

### Trigger capitalization

**Case-sensitive snippets** is on by default, preserving exact matching. When
it is off, the engine's suffix trie compares characters with
`StringComparer.OrdinalIgnoreCase`, the same comparer as its replacement map, and
steps over a surrogate pair as one character, so supplementary-plane letters match
in either case. Triggers that differ only by case are
therefore duplicates and the last file row wins, matching the existing duplicate
rule. The editor applies the same comparison to validation, copy suggestions and
reachability warnings.

In the snippet list, a trigger containing letters is shown as both forms while the
setting is off, for example `!hello / !HELLO`. The saved trigger is unchanged and
the editor still shows its original capitalization. The row's accessible name also
states that either form matches.

### Prompt authoring and validation

**Ask a question…** in **Insert variable** (tooltip "Ask for text when the snippet runs.")
puts a provisional `{{prompt:<example>}}` token at the replacement caret or selection.
`<example>` is the question box's localized example ("Ticket number"); if a translation
contains `}`, the token is `{{prompt:}}`. It then opens **Ask when the snippet runs**
against that token: a question box (focused, showing the same example), a **Required**
toggle (on by default), a preview of the window the snippet will show, and **Add** and
**Cancel**. Reopening an existing question shows **Update** instead of **Add**. It's a
Flyout styled like a teaching tip, because a `TeachingTip` doesn't open when it can't fit
around its target and stops catching outside clicks after a canceled light dismiss. The
page places it (`TextExpanderPromptPlacement`) from the token's bounds, the Replacement
box, the dialog's buttons and the window, within 4 px of the window's edges. It lines up
with the box, except that when the box starts above **Save**, as in the two-column
dialog, it starts where **Cancel** starts; it ends at the box's right edge. It never
cuts through **Save**, **Cancel** or **Insert variable**: one it spans from side to side
it covers completely or stops 4 px clear of, and one it only partly spans it stops clear
of. So it hides **Cancel** completely or stops above it, and **Save** stays in view. It
goes 4 px below the token's line, or above the line when it fits only there, and never
over the line; when it fits neither way, it goes where there's more room without the
preview and scrolls if it still doesn't fit. As the question is typed, the token and the
preview follow it. The preview is the runtime window scaled down (icon, "PowerToys Text
Expander", the question over an empty box, OK and Cancel) on a mid-grey backdrop that
follows light, dark and high contrast; it can't be clicked or tabbed to, and screen
readers name it "Preview of the window shown when the snippet runs." The flyout, question
box, toggle, Add/Update and Cancel have AutomationIds: `TextExpander_PromptFlyout`,
`TextExpander_PromptQuestion`, `Toggle_TextExpander_PromptRequired`,
`Button_TextExpander_PromptAdd` and `Button_TextExpander_PromptCancel`.

Enter, **Add**, or clicking back in the replacement commits a valid question; Enter on
a button activates that button. An empty question ("Type a question.") or one
containing `}` keeps the flyout open with an assertive inline error, shown only after
a commit is refused and then updated as you type.

Click an existing prompt token, or put the caret in one and press F2 (the Replacement
box's screen reader help says "To change a question, click it or press F2."), to reopen it
with its question and **Required** setting. Escape or **Cancel** removes a new
provisional token and restores the original replacement selection; for an existing
token it discards the changes and reselects the unchanged token. Closing the snippet
dialog or a reload also discards a pending prompt. Drafts are never undo steps, so a
completed new prompt is one editor undo step.

The flyout writes `{{prompt:<question>}}` when required or
`{{prompt?:<question>}}` when optional. The runtime grammar and snippet schema are
unchanged.

At runtime, required prompts keep OK disabled while the answer is blank. Pressing
Enter in that state shows the inline `Enter an answer to continue.` error and keeps
focus in the answer box. Optional prompts accept an empty answer. The WinUI and
Win32 fallback prompt implementations enforce the same rule.

## Debugging

1. Build `PowerToys.slnx` and start the runner.
2. Enable Text Expander in Settings.
3. Attach the debugger to `PowerToys.TextExpander.exe`.

The engine can also be started directly for debugging: `PowerToys.TextExpander.exe --managed --settings:"<path to settings.json>"`.
A second engine can run beside PowerToys' own with `--instance:<name>` and a settings file of its own, whose
folder then holds its snippets. In Debug builds, setting `POWERTOYS_TEXTEXPANDER_PROMPT_THEME` to `Light` or
`Dark` for that process shows the prompt window in that theme without changing Windows'.

## UI tests

`TextExpander.UITests` drives PowerToys through UI Automation with the `UITestAutomation.Next` harness
(winappcli). Each test starts PowerToys with only Text Expander enabled, after seeding the module's
`settings.json` and the default snippets database, and restores the files that were there afterwards. Triggers
are typed with real key presses into a text box in the test's own window, and the result is read from that box.

- `TextExpanderSettingsTests`: the enable switch stops and starts the engine, the **Advanced** options are saved to
  `settings.json`, and **Manage snippets** opens one editor, also while Text Expander is off.
- `TextExpanderEditorTests`: the list and the **Saved on this PC** menu, the starter database, adding a snippet,
  trigger validation and the discard question, editing, duplicating and deleting, search, and changes made to the
  file by another app.
- `TextExpanderExpansionTests`: expansion after a space or punctuation, Ctrl+Z, `$|$` with a line break and
  `{{isodate}}`, required and cancelled questions, **Always paste** (the clipboard is put back) and **Always type**,
  the editor's exclusion, and changes from the editor and Settings applying without a restart.

When an expansion doesn't happen, the failure message shows what the target held after typing, how busy the CPU
was, and the engine's counters, which the test asks for as `PowerToys.TextExpander.exe --diagnostics-file` does.

The tests must run without administrator rights: the engine refuses input to elevated windows, and elevated
Settings disables **Manage snippets**. The UI Test Automation pipeline runs `TextExpander.UITests` through
`.pipelines/runUiTestAsUser.ps1` and signs the Runner and Settings so a Release Runner accepts Settings' enable
and disable. They replace your Text Expander settings and snippets while they run, so run them in a VM or on a
test machine (see the `ui-tests-local-vm` skill), for example with `TextExpander.UITests.exe --filter "TestCategory=TextExpander"`.
