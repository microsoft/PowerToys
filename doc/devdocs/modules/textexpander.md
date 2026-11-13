# Text Expander

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Text%20Expander%22)<br>
[Feature request #5074](https://github.com/microsoft/PowerToys/issues/5074)

## Overview

Text Expander replaces short triggers with longer snippets as the user types, in any app at the
user's integrity level. An engine process watches keys with a low-level hook. Its worker thread
erases each trigger and pastes or types the snippet. A separate editor manages the snippets file.

## Components

| Path | Role |
| --- | --- |
| `src/modules/textexpander/TextExpanderModuleInterface` | `PowerToys.TextExpanderModuleInterface.dll`, the runner's module, which starts and stops the engine. |
| `src/modules/textexpander/TextExpander` | The engine, `WinUI3Apps\PowerToys.TextExpander.exe`. |
| `src/modules/textexpander/TextExpander.Core` | Shared rules: `SnippetFile`, `SnippetTemplate`, `Variables`, `TriggerIndex`, `WordBoundaryRules`, `TextExpanderTriggerReach`, `SnippetPathResolution`, `TextExpanderStarter`, the clipboard rules `ClipboardFormats`, `ClipboardPrivacy`, `ClipboardLoan`, `ClipboardEntry` and `ClipboardHandBack`, and `InjectionPolicy`, which decides between pasting and typing. |
| `src/modules/textexpander/TextExpander.Storage` | `TextExpanderSettingsUtils`, for guarded `settings.json` access. |
| `src/modules/textexpander/TextExpander.Editor` | The editor, `WinUI3Apps\PowerToys.TextExpander.Editor.exe`. |
| `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.TextExpander` | Command Palette's **Text Expander snippets** extension. See [Command Palette](#command-palette). |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/TextExpanderPage.xaml`, `src/settings-ui/Settings.UI/ViewModels/TextExpanderViewModel.cs` | The Settings page. |
| `src/settings-ui/Settings.UI.Library/TextExpanderSettings.cs`, `TextExpanderProperties.cs` | The `settings.json` schema. |
| `src/modules/textexpander/TextExpander.UnitTests`, `.Editor.UnitTests`, `.FuzzTests`, `.UITests` | [Tests](#tests). |

- The engine references only Core and `GPOWrapper`. Core references neither Settings nor WinUI.
- The engine's own words (the prompt, its message boxes and the diagnostics report) come from
  `TextExpander\Strings\en-us\Resources.resw`, built into `PowerToys.TextExpander.pri` and read
  through `EngineText`.
- The module interface starts the engine, found relative to its own DLL, with
  `<runner pid> --managed --instance:powertoys --settings:"<settings.json>"`. An elevated runner makes
  the shell process the engine's parent, so no elevated token passes on.
- On load, `enable()` and `disable()`, the module stops stray engines. It posts
  `PowerToys.TextExpander.RequestShutdown` to their `PowerToys.TextExpander.MessageWindow.powertoys`
  windows.
- `disable()` then signals `TEXT_EXPANDER_EXIT_EVENT` and terminates its engine after 10 s. The event
  is auto-reset, so it wakes only one engine.
- The mutex `Local\PowerToys.TextExpander.SingleInstance` plus `.<instance>` allows one engine per
  session. The engine exits with the runner or on the exit event. Shutdown cancels unanswered prompts
  and choices, then waits up to 10 s for the worker to finish any clipboard restoration.

## How an expansion works

### Keyboard hook and typed buffer

- `KeyboardHook` installs `WH_KEYBOARD_LL`, `WH_MOUSE_LL` and a foreground WinEvent hook. Each key
  event goes to `InsertionGuard.Swallows` first, and only key-downs reach matching.
- `HostInputTags.Classify` ignores the engine's tag and Keyboard Manager's suppressed keys (`0x111`).
  `0x110` bypasses the runner's hotkeys but remains input here, including Quick Accent's edits.
  Command Palette's extension (`InputTags.CommandPalette`) never matches,
  but ends what was typed, like a remap. Other injected input, like the touch keyboard, is typing.
- With `treat_remaps_as_typing` off, keys with Keyboard Manager's bit `0x1` aren't matched. The
  engine's random tag sets that bit, so Keyboard Manager's key remaps skip it.
- Keys count in the foreground window unless its check refused it, and never in standard password
  boxes. `ToUnicodeEx` uses that window's layout and flag `0x4`, which keeps dead-key state intact.
- The key after a dead key never releases or completes a trigger. The app keeps the accent until that
  key comes, so swallowing it would leave the accent to take the erase's first Backspace. An accent
  that doesn't combine counts with the key after it, and Backspace after a dead key only drops it.
- The foreground event can name a window before Windows reports it in front, as Windows Terminal
  does for a new window. Then the first key typed there asks for the check. Keys typed while it runs
  count, but nothing expands until the window passes.
- Navigation keys, shortcuts, clicks and focus changes clear the buffer, but AltGr types. The buffer
  holds max(256, 2 × (longest trigger + 2)) characters and keeps its newest half when full.

### Trigger matching

- The hook reads `SnippetStore.Current`, an immutable snapshot, without locks, and takes the
  replacement from it.
- `TriggerIndex`, a reversed trie, finds the longest trigger ending the buffer without allocating.
- With `require_word_boundary` on (the default), only whitespace, punctuation or a symbol other than
  `_` fires a trigger. The match uses the text before that key.
- In that mode, a trigger that starts with a letter, digit or `_` must start a word.
  `WordBoundaryRules` counts a surrogate pair as one character, and a combining mark with the one
  before.
- With `case_sensitive` off, matching uses `StringComparer.OrdinalIgnoreCase`. On a match, the hook
  swallows the key and clears the buffer.

### Handoff to the worker

- The hook adds a `PendingExpansion` to `KeyboardHook.Pending`, which one worker thread drains in
  order. Each item captures the typed text, replacement, window and input generation.
- Each non-modifier key-down or click that reaches an app moves the input generation. The worker drops
  work whose generation moved, checking before prompts, the erase and each `SendInput`.

### Variables and prompts

- `{{date}}`, `{{time}}` and `{{datetime}}` use the culture's `d`, `t` and `g` formats, and
  `{{isodate}}` is `yyyy-MM-dd`. `{{date:<format>}}` takes a .NET format, or strftime if it has `%`.
  `/` and `:` stay literal. `WM_SETTINGCHANGE` with `intl` refreshes the culture.
- Questions come first, then `{{clipboard}}` reads the clipboard once. Values are literal: a `$|$` in
  an answer stays text.
- `{{prompt:…}}` needs an answer, and `{{prompt?:…}}` accepts a blank one.
- `PromptHost` shows the WinUI 3 prompt on its own thread, and `ClassicPromptDialog` is the fallback.
  Meanwhile the hook is suspended, and only keys or clicks in the target window drop the work.
- The question and choice windows open on the target window's screen. When that window reaches onto
  another screen, they open on the caret's instead, as the notice does (`PromptScreen`). The caret is
  looked for only then, since finding it can take up to 400 ms. Without one, it's the screen most of
  the window is on, and once the window has gone, the pointer's.
- Cancel drops the whole snippet. The swallowed key goes back, unless a modifier stays down for 1 s.
- Keys typed after the last answer land after the snippet. The worker waits up to 350 ms for the
  target to return.

### Clipboard variables

- `{{clipboard}}` inserts the text on the clipboard now. `{{clipboard:email}}`, `:link`, `:phone` and
  `:number` insert only that part of it. `{{clipboard:pick}}` and `{{clipboard:pick:<part>}}` take
  from clipboard history instead. Nothing else is a clipboard variable.
- `ClipboardParts.Find` finds each different part once, in order, in linear time, and reads only the
  first 1,000,000 characters. If the copy is longer, only complete lines ending in CR or LF within
  that budget are searched; an oversized single line produces no matches. This avoids treating a
  truncated address or number as a complete value. `{{clipboard}}` still inserts the whole copy.
  Links start with `https://`, `http://` or `www.`. Phone numbers have 7
  to 15 digits in groups, or all together after a `+`. Numbers follow the region's separators. Dates,
  times and IP addresses are neither, and nothing inside a link or an email address is either.
- One match or copy goes straight in. Two or more open the chooser (`ClipboardChoiceWindow`, on the
  prompt thread) with at most 100 options. Cancel cancels the snippet, as with a question. Its 420 DIP
  width grows with Windows' text size, up to its screen's. A long option wraps to a second line before
  it's cut short, and its tooltip has all of it.
- For history, the engine reads up to 25 text copies on another thread while any questions are asked,
  waiting at most 3 s. One read runs at a time: while one that timed out still hangs, history counts
  as unreadable. `{{clipboard:pick:<part>}}` offers the different matches across the copies,
  newest first. What's read is used for that expansion only.
- None leaves the spot empty, and the rest goes in. `ExpandedSnippet.Gaps` says why: not found,
  history off, no copies, or unreadable. Once the insertion and its guard are done, the worker shows
  one [notice at the caret](#notice-at-the-caret). `ClipboardNotice` words it, with one sentence for
  each reason. It never shows what was copied.
- In the editor, each part under **Insert variable > Clipboard** has a tooltip saying what it needs.
  Clicking a clipboard variable, or F2 on it, opens the Clipboard panel to change its copy and part,
  with examples of what counts. Both come from `ClipboardPartExamples`, which a unit test holds to
  `Find` in five regions.

### Insertion guard

`SendInput` batches don't stay whole while any low-level hook runs, so `InsertionGuard` keeps user
keys out of an insertion.

- From the match on, the hook holds the user's key events, after the key it swallowed. The insertion
  takes them over at its start marker, so keys typed straight on land after the snippet. With no
  insertion within 2 s of the first key pressed since the match, or none at all, they go back as they
  came, the swallowed key first, and the snippet is dropped. With nothing pressed, the hold waits for
  the insertion. Another window coming to the front drops them.
- The match arms no timer, and the engine runs the hold once at startup, so the first trigger compiles
  nothing on the hook's thread. Windows gives the hook about 300 ms a key before passing it on.
- Before a question or choice window opens, a held key press other than a modifier cancels the
  snippet, and what was typed goes back as typed.
- The worker sends a start marker, a key-up of VK `0xFF` with the engine's tag and the insertion id.
  It waits up to 2 s. Without it, as when another hook swallows it, the insertion goes in unguarded,
  unless a key other than a modifier was pressed since the match: then what was typed goes back.
- At the marker, the hook goes on only if the input generation hasn't moved. Then it holds every key
  event that isn't the engine's, in order. Clicks aren't held.
- The worker releases the user's modifiers in the first erase call, and presses them again before held
  keys go back.
- After the end marker, held keys go back in rounds, with their own tags, so a key-up never passes its
  key-down. They go back only to their window, still in front. A focus change cancels the insertion.
- The guard ends after 2 s without an engine event, or at 256 held key events. Typing releases its
  guard after 2 s and continues only if no non-modifier input reached the app, including returned
  keys. Final cursor placement takes the guard again to release modifiers during its moves.
  A slow clipboard borrow gets a new start marker before the erase.

### Erase

- After 15 ms, the worker sends one Backspace per text element of the typed part. Up to 20 keys go one
  per call, 25 ms apart, since Windows 11 Notepad drops faster keys. Longer erases go at once.
- No call carries over 1,000 events. Before each call, the worker checks the window, its integrity, the
  input generation and clicks. A failed check stops the insertion.

### Paste or type

- Automatic pastes text with a line break, or text at least `clipboard_threshold_chars` (default 5)
  long, not counting the given-back key.
- In Windows Terminal (window class `CASCADIA_HOSTING_WINDOW_CLASS`), Automatic types any snippet
  without a line break, since Terminal's `trimPaste` would drop the given-back space. Snippets with
  line breaks still paste, and Always paste still pastes there, losing the space. So does text from a
  copy or with a control character, since Terminal filters a paste but acts on what's typed.
- Text from the clipboard or clipboard history loses its control characters but tabs and line breaks
  (`ControlCharacters`). Typing into Windows Terminal or the console (`ConsoleWindowClass`) leaves
  them out too, and lines a terminal couldn't get as a paste aren't typed: the trigger stays as typed,
  with the key the hook swallowed given back in any mode, and a notice says why. With Always type,
  lines that hold copied text aren't typed into a terminal either, while lines the user wrote are.
- Ctrl+V goes 300 ms after the erase, since Notepad drops a Ctrl+V during an edit. Ctrl goes down in
  its own call. Notepad reads it late, so it stays down until the target opens the clipboard, at most
  350 ms after the V. Unguarded, Ctrl+V is one call.
- If the clipboard can't be borrowed, the worker types under the same guard. After the first key,
  there's no fallback.
- Typing sends one character per call, 25 ms apart, plus 25 ms after `.`, `!`, `?` and line breaks.
  `\n` and `\t` are Enter and Tab presses, and `\r` is skipped.

### Clipboard borrow and restore

- The order is: guard decision, snapshot, write, erase, Ctrl+V, settle, restore, end marker. A Ctrl+V
  the user presses meanwhile is held, so it pastes their own copy.
- The snapshot copies every format. It refuses above 32 MB or for formats it can't reproduce, and the
  snippet is typed.
- The snippet carries `ExcludeClipboardContentFromMonitorProcessing`,
  `CanIncludeInClipboardHistory` = 0 and `CanUploadToCloudClipboard` = 0. So does the restored
  clipboard, unless it had one already.
- On the clipboard, each line break is CR LF (`ClipboardFormats.WithClipboardLineBreaks`), as text
  there has it: the console ran two lines pasted with a lone LF between them as one.
- The restore comes at least 150 ms after the V, once the target reads. Otherwise it comes at 350 ms,
  if the target answers `WM_NULL` within 50 ms. A busy target gets up to 5 s. Then the snippet stays,
  and the user is told once per run.
- The restore needs the clipboard sequence number from just after our write, so a newer copy wins. The
  number is read after the clipboard is closed, since closing adds the formats Windows makes from the
  text. It counts only if our window still owns the clipboard after that (`ClipboardLoan.TakeAfterWrite`),
  so a copy made in between isn't taken for ours. The restore checks before opening the clipboard, and
  again once it's open, before emptying it: the open may have waited for an app that was copying. A
  refusal counts as skipped, not lost.
- It tries 3 times, 40 ms apart, in Core's `ClipboardHandBack`, which Command Palette shares. Only an
  attempt that emptied the clipboard takes the new sequence number. One that couldn't open it changed
  nothing, so it adopts nothing.

### Cursor marker and given-back key

- The first `$|$` sets the caret. The worker presses Left once per text element after it, unless that's
  over 2,000.
- In word mode, the swallowed terminator goes back after the snippet, unless
  `word_boundary_keeps_space` is off.

### Notice at the caret

- When a snippet leaves something out, the worker shows a small notice at the text cursor
  (`NoticeBubble.Show`), such as "No email address found". It never reads or shows the clipboard.
- It's a layered tool window that's never activated, on its own thread. Keys typed while it shows go
  on into the app, and a click on it only dismisses it. One shows at a time.
- It sits under the caret with its arrow pointing at it, or above it when there's no room, inside the
  monitor's work area. It follows the light, dark and high contrast themes, the monitor's DPI and the
  text size setting. Narrator reads it through a UI Automation notification, raised 250 ms after the
  notice shows: screen readers connect to the window as it appears, and one raised before they have
  reaches none, which left a new window's first notice unannounced.
- It fades after 4 s, or later if Windows keeps notifications up longer. The user's next key-down or
  click fades it sooner, but not before it has been up 1 s. The hooks report those with
  `NoticeBubble.OnUserInput()`, which costs one `volatile` read, or one `PostMessage` while a notice
  shows. The engine's own input doesn't count.
- `CaretLocator` finds the caret: the one Windows keeps for the window's thread; then, while the
  window is in front, UI Automation's caret or selection in the focused element, or that element's
  bottom-left; then the window's bottom middle. UI Automation runs on its own thread and is given up
  after 400 ms, so a hung app can't stall the engine.

### Undo with Ctrl+Z

- The hook records each insertion before held keys go back, except snippets with a prompt or a
  clipboard variable.
- Ctrl+Z alone, in the same window, undoes it. Ctrl+Shift+Z stays redo. Any other non-modifier key, a
  click or a focus change ends the undo.
- The worker waits up to 1 s for modifiers to go up, or gives up: Ctrl+Backspace deletes words. It
  moves the caret to the end, erases the snippet and types what was typed again, through the guard.

## Snippets and settings

### settings.json

`%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\settings.json` keeps the options in `properties`.
Strings and numbers are `{"value": …}` objects, and booleans are bare.

| Key | Default | UI or override |
| --- | --- | --- |
| `snippets_path` | empty | Editor |
| `injection_backend` | `auto` | Insertion method; `POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND` |
| `clipboard_threshold_chars` | 5 | Minimum length to paste; `POWERTOYS_TEXT_EXPANDER_CLIPBOARD_THRESHOLD` |
| `require_word_boundary` | true | Wait for space or punctuation; `POWERTOYS_TEXT_EXPANDER_REQUIRE_WORD_BOUNDARY` |
| `word_boundary_keeps_space` | true | `POWERTOYS_TEXT_EXPANDER_WORD_BOUNDARY_KEEP_SPACE` |
| `case_sensitive` | true | Case-sensitive snippets |
| `treat_remaps_as_typing` | true | Use PowerToys Keyboard Manager remaps…; `POWERTOYS_TEXT_EXPANDER_IGNORE_REMAPPED_INPUT=1` |

- Environment variables win over the file. Backends are `auto`, `clipboard` and `type`.
- The editor resets `word_boundary_keeps_space` to true at startup, but the engine honors false.
- `DefaultClipboardThresholdChars` must match in `TextExpanderSettings` and `InjectionPolicy`. The
  on/off switch is `enabled.TextExpander` in the general settings, which Settings sends over IPC.
- Settings and the editor use `TextExpanderSettingsUtils` under the mutex
  `Local\PowerToys.Settings.AtomicFile.<hash>` (10 s timeout). A write re-reads the file, changes only
  its keys, publishes a flushed staging file with `File.Replace` and verifies it.
- The Settings page never writes over a file it can't read. The editor's startup read
  (`GetSettingsOrDefault`) writes defaults over a missing or invalid file.
- The engine reads 250 ms after a change. An unreadable file changes nothing, and it retries 4 times.

### Snippets file

The file looks like `{"version": 1, "snippets": [{"trigger": "!mysig", "replacement": "Best regards,\nYour name"}]}`.

- `SnippetFile` reads and writes it for both processes, in file order. It writes indented UTF-8
  without a BOM, keeping unknown properties but not comments.
- A replacement is exact text: real line breaks and tabs, and literal backslashes. The editor saves
  `\n`, and typing skips `\r`.
- Duplicate triggers may stay, and the engine uses the last one. With case sensitivity off, triggers
  differing only by case are duplicates.
- Reading allows a BOM, comments and trailing commas. A file that isn't JSON, or has no `snippets`
  list, throws `SnippetFileFormatException` (an `IOException`) and is never written over.
- A `version` above 1 reads, but the editor won't save it.

### Where the file lives

- The default is `PowerToysTextExpanderSnippets.json` in the settings folder. The bug report tool
  repeats this name.
- `snippets_path` holds a full file path, with any name. `SnippetPathResolution` trims quotes and
  expands environment variables. A blank or relative value means the default.
- A chosen file is never created. While it's missing, there are no snippets.
- Text Expander doesn't sync. **Move snippets database…** copies the file somewhere, such as OneDrive,
  and uses it. **Sync from another PC…** uses an existing file. Both save `snippets_path` first, so
  the engine follows.

### Starter file, reloads and safe writes

- `TextExpanderStarter` writes six snippets only when the engine or the editor starts on the default
  path and finds no file. It moves a temporary file in without overwrite, so the first writer wins.
- A default file that disappears later isn't recreated; that would break editors that save by delete
  and rename.
- The engine watches the file's folder, and changes coalesce for 250 ms. A failed load keeps the last
  snapshot and tries up to 4 times, 250 ms apart. While the file isn't loaded and watched, the engine
  retries every 30 s. Once it is, no timer runs.
- The editor watches the file and polls it every 2 s. Both keep their snippets if the bytes match.
- Only the editor writes the file, apart from the starter. If the file no longer matches the bytes it
  loaded, it reloads and refuses the save. It never merges.
- It writes `<file>.tmp-<GUID>`, then uses `File.Replace` or a create-only move. It backs up to
  `<file>.backup-<yyyyMMdd-HHmmss>` when Move replaces a file, or when a first save would lose comments
  or layout.

## Snippet editor

**Manage snippets** starts `PowerToys.TextExpander.Editor.exe`, even with the engine off. It manages
snippets and moves or switches the file. The `AppInstance` key `PowerToys_TextExpander_Editor` keeps
one instance. A disabled policy blocks it, and as administrator it edits nothing.

Rules a change must keep:

- A trigger can't be empty, contain a line break, start with an invisible byte order mark
  (`\uFEFF`), have outer spaces or repeat another. Save stays off while a problem shows.
- **Never expands** marks a row whose trigger a later row repeats, or that another trigger fires
  first. `TextExpanderTriggerReach` uses the engine's matcher and word rules.
- **Ask a question…** inserts `{{prompt:…}}`; turning Required off writes `{{prompt?:…}}`. A question
  can't be empty or contain `}`.
- **Insert variable** offers only what `Variables` implements. The date guide and the preview use
  Core's `Variables`, and never read the clipboard.
- Text that looks like a variable but isn't one, such as `{{Date}}` or `{{clipboard:link:2}}`, stays
  as typed. `TextExpanderNearMiss` says so under Replacement and in the row, and **Use** puts in the
  likely variable.
- A snippet that picks from clipboard history while it's off gets a note under Replacement and in the
  Clipboard panel, with a link to Windows Settings unless a policy turned history off.
- **Refresh** re-reads the file in the editor only.
- **Open in text editor** opens a `.json` file in the app Windows uses for `.json`, and any other file
  in Notepad. So a script or shortcut chosen as the database never runs. With no app set for `.json`,
  Notepad opens it too (`TextExpanderFileAssociation`), rather than Windows asking which app to use.
- A reload advances `SnippetRevision`, so an open edit of an existing snippet must be reopened to
  save.

Accessibility contracts, checked by `TextExpanderPageMarkup` and `TextExpanderRows`:

- A row is one Tab stop, and its menu items carry their snippet in `Tag="{x:Bind}"`.
- Shift+F10, the Menu key or a right-click on a row opens its **More options** menu, the same menu as
  the row's button. Delete and Ctrl+D delete (after asking) and duplicate the snippet whose row has the
  focus. The menu shows those keys.
- After **Save**, the focus goes to the saved row, scrolled into view: the edited one, or the new one
  after **New snippet** or **Duplicate**. After a delete it goes to the next row, or the one before at
  the end, or to **New snippet** when none is left or the row isn't in the search's results.
- A row's name is "*trigger*: *text*", plus "Never expands: *reason*" when that applies.
- The trigger box's full description is the current problem. Problem and error bars announce changes.
- The snippet dialog's content is as wide as the window allows, up to what its 808 DIP maximum leaves
  inside its border and padding (`TextExpanderSnippetDialogSize`). The trigger box wraps, so its
  placeholder stays whole with large text.
- Every visible string has an English resource. UI tests find controls by AutomationId; keep those
  stable.

## Command Palette

Command Palette's built-in **Text Expander snippets** extension lists the snippets, searches them by
trigger and text, and pastes the one picked into the app Command Palette was opened over. It runs in
Command Palette's process; its
[README](../../../src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.TextExpander/README.md) has the details.

- It follows the engine's policy and enable setting, and says "Text Expander is off" rather than
  pasting while either turns it off.
- It finds and reads the snippets file with Core's `SnippetPathResolution` and `SnippetFile`, and
  never writes it. It expands with `Variables.ExpandWithPromptRequests`, so new variables work there
  without changes.
- Questions are boxes in Command Palette's search bar. A snippet that takes part of the copy, such as
  `{{clipboard:email}}`, then opens a pick page. The copy is read once, and each choice Core asks for
  through its `choose` callback gets a page, in Core's order. Core's `ExpandedSnippet.Gaps` say which
  spots were left empty, and a toast says so.
- A copy picked from clipboard history (`{{clipboard:pick}}` and the like) needs the engine's window,
  so those snippets are listed but not inserted.
- It pastes or types as Settings' **Insertion method** says, by the engine's rule (Core's
  `InjectionPolicy`), so Automatic types short snippets and one line in Windows Terminal, unless it has
  text from a copy or a control character. It pastes
  with Core's `ClipboardFormats`, `ClipboardPrivacy`, `ClipboardLoan` and `ClipboardEntry`, hands the
  clipboard back with the engine's loop, Core's `ClipboardHandBack`, and types when the clipboard can't
  be put back exactly.
- Its key events carry `InputTags.CommandPalette`: Keyboard Manager's bit, so remaps leave its Ctrl+V
  alone, in a value the engine's hook never matches. It sends a key that does nothing before letting go of a
  held Alt or Windows key, and refuses targets above medium integrity, as the engine does.
- Command Palette compiles Core ahead of time, so Core imports `Common.Dotnet.AotCompatibility.props`.
  Keep reflection-based JSON and other code the trimming analyzers flag out of it.

## Threading

- Main thread (STA): the message window, the clipboard owner and all hook callbacks.
- Worker (`PowerToys Text Expander`): prompts, variables, the clipboard and `SendInput`.
- Prompt thread (`PowerToys Text Expander prompt`): WinUI 3 prompts, started on first use. A closing window
  puts its title bar back to the system's and drops its content, and the engine collects once it has gone:
  Windows App SDK otherwise keeps each closed window's title bar (microsoft/microsoft-ui-xaml#12210).
- Notice threads (`PowerToys Text Expander notice` and `PowerToys Text Expander caret`, MTA): the
  notice window, and the UI Automation caret lookup.
- Thread pool and timers: foreground checks, file events, retries and guard expiry.
- The main thread must keep pumping. Windows silently removes a low-level hook that misses
  `LowLevelHooksTimeout`.
- Never on the hook thread: `SendInput`, the clipboard, files, process queries, UI, waits or logging.
- The per-key path doesn't allocate, and reads the snapshot and `volatile` options without locks.
- Only the worker and the guard's timer send input.

## Integrity levels and security

- The engine refuses to run above medium integrity. It checks before it loads settings or snippets.
- `SendInput` can't see UIPI drops, so `InputTarget.CanDrive` refuses targets above the engine's level,
  capped at medium.
- A thread-pool check authorizes each new foreground window. Only there does a trigger expand, and a
  refused window's keys are dropped.
- Keys typed in `PowerToys.Settings.exe` or `PowerToys.TextExpander.Editor.exe` never expand.
- The guard never holds `LLKHF_LOWER_IL_INJECTED` input, which would gain the engine's level if sent
  again. Matching doesn't check this flag.
- The `ConfigureEnabledUtilityTextExpander` policy forces the module on or off. Disabled, it also stops
  the engine and the editor at startup. `PowerToys.Settings.Cli` honors it.
- Elevated Settings disables **Manage snippets**.

Privacy:

- Keys, snippets, prompt answers and clipboard text are never logged.
- The buffer, held keys and clipboard copies are zeroed when dropped. Password boxes clear the buffer
  and the undo.
- `diagnostics.txt` shows `snippets_location=default` or `custom`, never a path. Bug reports leave out
  snippet files and mask `snippets_path`.

## Limitations

- Elevated windows never expand. If PowerToys runs elevated as another account, the engine runs as
  the desktop user and may not read that account's files.
- Only `Edit`, `RichEdit` and WinForms password boxes are detected. Web, WPF and WinUI password fields
  still expand.
- The hook doesn't follow IME composition, so a trigger can fire while the user is composing.
- A key typed while a question or choice window is still opening reaches the app. That drops the
  snippet, and the swallowed key is lost.
- A click during the Ctrl hold becomes Ctrl+click.
- Clipboard managers may ignore the exclusion formats; only Always type avoids the clipboard.
- The first question or choice window loads WinUI into the engine for the rest of the session.

## Debugging

- Build `PowerToys.slnx`, start the runner and attach to `PowerToys.TextExpander.exe`.
- A second engine runs with `--managed --settings:"<path>" --instance:<name>`, with its snippets beside
  that settings file. Both engines expand.
- `--diagnostics` shows the engine's counters, and `--diagnostics-file` writes `diagnostics.txt`. The
  engine has no log file; its `Trace` output shows in DebugView. The editor logs to
  `TextExpander\Editor\Logs`.

## Tests

- `TextExpander.UnitTests`: the engine's rules, from matching to the clipboard, and `EngineText`.
- `TextExpander.Editor.UnitTests`: the editor's view model, storage, shared rules and page markup.
- `Settings.UI.UnitTests` (`TextExpanderSettingsTests`): the Settings page.
- `BugReportTool.UnitTests` (`TextExpanderPrivacyTests`): no snippets in bug reports.
- `TextExpander.FuzzTests`: OneFuzz targets `FuzzSnippetFile`, `FuzzExpandSnippet` and
  `FuzzClipboardParts`.
- `Microsoft.CmdPal.Ext.TextExpander.UnitTests` (in `src/modules/cmdpal/Tests`): the Command Palette
  extension, with an in-memory clipboard, desktop and clock.
- `TextExpander.UITests`: Settings, editor and expansion, end to end, plus Windows Terminal where it's
  installed. Each test seeds the files, runs PowerToys with only Text Expander on, then restores the
  files. It also turns Run at startup off: while it's on, the runner deletes and re-creates its scheduled
  task each time it starts, and the pipeline's launcher fails when it polls its own task during that churn.
  The notice tests (`TestCategory=TextExpander.Notice`) run the notice window in the test
  process, from the engine's sources, with an English `EngineText`.

UI tests must run without administrator rights, because the engine refuses elevated targets and
Settings run as administrator turns off Manage snippets. CI doesn't run this suite without
administrator rights yet, so when the test process is elevated, every test is skipped with that
reason before PowerToys starts, and the run exits 0 rather than failing because no test ran. Running
it non-elevated in CI is a follow-up for when the shared UI tests are stable: add
`TextExpander.UITests` to `$authenticatedSettingsIpcModules` and `$nonElevatedSuites` in
`.pipelines/v2/templates/job-test-project.yml`, and to their lists in
`.pipelines/tests/runUiTestAsUser.Tests.ps1` and `.pipelines/tests/uiTestSigningSelection.Tests.ps1`.
The pipeline then signs the runner and Settings and runs the suite through
`.pipelines/runUiTestAsUser.ps1`. The tests replace your Text Expander files, so use a VM, and run
them without administrator rights: `TextExpander.UITests.exe --filter "TestCategory=TextExpander"`.
