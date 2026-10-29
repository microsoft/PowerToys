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
├── TextExpander.Core/            # Shared grammar, templates, matching, starter publication
├── TextExpander.Storage/         # Coordinated module-settings persistence
├── TextExpander.Editor/          # Standalone WinUI snippet-management app
├── TextExpander.Editor.UnitTests/# Editor, storage, and ownership tests
├── TextExpander.FuzzTests/       # OneFuzz targets for the snippet file and expansion
└── TextExpander.UnitTests/       # Engine and shared-rule unit tests
```

The thin Settings page lives in `src/settings-ui` and owns enablement, engine options,
and the **Manage snippets** launch entry point. Configuration DTOs remain in
`Settings.UI.Library`; snippet editing, previews, file watching, backups, and folder
transactions belong to the module. Settings does not reference the editor executable.
Both processes use `TextExpander.Storage` for coordinated module-settings reads and
writes. Readers wait for publication rather than treating a transiently missing
destination as default settings. Matching-option changes revalidate an open snippet
draft without invalidating it; an actual library-file or folder change still does.
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
reloads matching rules and the active snippet folder. Closing it disposes both settings
and snippet watchers. The engine excludes both Settings and the editor from expansion,
so typing a trigger into the replacement editor cannot expand it.

`SnippetFile` provides the single comment-preserving snippet grammar, `SnippetTemplate`
tokenizes replacements for both expansion and preview, and `Variables.FormatDate`
provides shared .NET/strftime formatting. Trigger reachability uses the runtime's
`TriggerIndex` and `WordBoundaryRules`, not a transcription of engine behavior.

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
- Matches triggers against the snippet file (`PowerToysTextExpanderSnippets.txt`, or `snippets.txt` from earlier versions), optionally ignoring capitalization and, by default, waiting for a space or punctuation before expanding. An underscore is part of a word, so it doesn't release a trigger: typing `addr_line` doesn't expand `addr`. An explicitly saved disabled value keeps immediate expansion.
- Replaces the trigger by pasting (the user's clipboard is saved and restored) or by typing with `SendInput`, depending on the Insertion method setting and snippet length. The pasted text, and the user's clipboard when it's put back, carry the exclusion formats for clipboard monitors, Clipboard History and Cloud Clipboard, so a paste doesn't record the user's last copy again; a clipboard that already carried any of them goes back exactly as it was. It's put back once the target has read the paste (at least 150 ms), or after at most 350 ms, timed by the clock.
- Expands variables (`{{date}}`, `{{time}}`, `{{datetime}}`, `{{isodate}}`, `{{date:<format>}}`, `{{clipboard}}`, `{{prompt:<question>}}`, `{{prompt?:<question>}}`) and places the cursor at `$|$`. Only the snippet's own text is read for `\n`, `\t`, `{{clipboard}}` and `$|$`: a prompt answer, the clipboard and a formatted date are inserted exactly as they are, so a `$|$` or `\n` in them stays literal.
- Supports undo: pressing Ctrl+Z immediately after an expansion restores exactly what was typed, in the capitalization it was typed in, even when matching ignores case. If the snippet's `$|$` left the caret inside the expansion, the undo first presses Right as many times as the insertion pressed Left, so it erases from the end of the expansion. Modifier key-downs on the way to Ctrl+Z do not cancel the pending undo, but any other key does; Ctrl+Shift+Z is left to the app as redo. The undo waits up to one second for modifiers to be released, and is abandoned otherwise, so its Backspaces are never sent as Ctrl+Backspace.
- Drops a queued expansion or undo, instead of applying it where the caret is now, when a key other than a modifier is pressed or the mouse is clicked after the trigger matched. While a `{{prompt}}` window is open, only a key pressed while the window being typed into is in front, or a click on that window, counts; typing or clicking in the prompt or in another app (to copy the answer, say) doesn't. If that window isn't known, any key or click outside the engine's own windows counts. The worker compares the hook's input generation before it starts (before any prompt) and again right before the first Backspace. The keystroke the hook swallowed to complete the trigger is not typed back. A foreground change alone doesn't cancel the work, because focus returns to the target after a prompt; typing still checks that the target window is in front. Backspace with Ctrl, Alt or Win clears the rolling buffer instead of removing one character.
- Ignores its own injected input, the runner's replayed hotkeys (`0x110`) and the key events Keyboard Manager swallows itself (`0x111`). Input carrying Keyboard Manager's injected bit (`0x1`, as in its `0x11` and `0x101` remaps) counts as typing. With **Use PowerToys Keyboard Manager remaps when matching triggers** off, that input isn't matched against triggers but still reaches the app, so a key other than a modifier still clears the rolling buffer, ends the one-step undo and drops queued work. The engine's own tag carries that bit too, so Keyboard Manager's key remaps leave its Backspaces, arrows and Ctrl+V alone. Its shortcut remaps skip only `0x101`, so a remap of Ctrl+V itself still applies to a pasted expansion.
- Runs at the desktop user's integrity level and refuses synthetic input to elevated windows. This preserves expansion in ordinary desktop apps when PowerToys itself is elevated, while preventing user-controlled settings and snippet files from authorizing elevated input or being read by an elevated engine.
- Checks its actual integrity level before loading host settings or snippets and exits if the check fails or the process is above medium integrity.
- Exits at startup, before taking its single-instance mutex, loading settings or snippets, or installing hooks, when the `ConfigureEnabledUtilityTextExpander` group policy turns Text Expander off, so starting `PowerToys.TextExpander.exe` directly doesn't get around it.
- Runs one engine per signed-in session: its single-instance mutex is `Local\PowerToys.TextExpander.SingleInstance` plus the `--instance:` suffix, like the runner's instance mutex and the exit event.
- Clears its rolling input buffer and one-step undo state while focus is in a detectable standard Windows password box: an `Edit` or `RichEdit` control, or a WinForms `WindowsForms10.EDIT.*` text box, with the `ES_PASSWORD` style. Detection uses only the focused window's class name and style, and sends no messages to other processes. Password fields on websites (Chromium, Firefox), and WPF and WinUI `PasswordBox` controls, don't expose that style and aren't detected, which the **Good to know** note says.
- Does not retain undo entries for snippets using `{{prompt:...}}` or `{{clipboard}}`, because those expansions contain user-supplied values. Clipboard snapshot and temporary paste buffers are zeroed after restoration or publication.
- Watches `settings.json` and the snippet file and reloads them without a restart.
- Takes its words (the prompt window and its few message boxes) from `Strings\en-us\Resources.resw`, built into `PowerToys.TextExpander.pri` and localized with the rest of PowerToys, through `EngineText`. Each call carries its English, which is used when the resources can't be read, so a missing file never stops a prompt or a message from showing. A test checks that every English fallback matches its resource. The messages point to settings rather than to environment variables: if the clipboard can't be restored, to **Advanced** > **Insertion method** > **Always type**; if the engine finds itself elevated, to restarting PowerToys without administrator rights.
- Exits when the runner process exits or the exit event is signaled.

### Settings and snippet storage

| File | Location |
| --- | --- |
| Module settings | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\settings.json` |
| Snippets (default) | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\PowerToysTextExpanderSnippets.txt` (earlier versions: `snippets.txt`) |

The snippet folder can be changed in the snippet editor under **Save snippets to a folder**. Text Expander does not implement a synchronization service: choosing a OneDrive or other provider-managed folder lets that provider synchronize the active file. The editor and engine select the snippet file with one shared core-library rule (`SnippetPathResolution.FromConfiguredPath`), so both read the same source-of-truth file: surrounding spaces and quotes are trimmed, environment variables are expanded, and a blank value or a relative path (the two processes share no working directory) selects the default folder. A configured folder that the engine cannot create also makes the engine use the default library, while the editor reports that folder's error. Shared Settings does not resolve or read snippet files. If the editor is launched directly as administrator, snippet-file resolving, reading, and editing remain disabled. Elevated Settings disables **Manage snippets** and tells the user to restart PowerToys without administrator rights. The engine runs with the desktop user's token even when the runner is elevated; a configured path that the desktop user cannot read will therefore fail to load rather than being read with elevated access. If PowerToys is elevated with a different administrator account, the engine runs as the signed-in desktop user and may be unable to read that account's settings and snippets.

Changing the folder publishes the destination file and `settings.json` as one
compensating transaction. Settings-file replacement and compensation hold a
path-derived cross-process claim, and compensation proceeds only while the
destination still has the exact bytes this save published. A later participating
save therefore waits, while a sync client or older build that changes the file is
never overwritten or deleted by compensation. The save reports whether the previous
settings were verified, the new settings were verified, or the state is uncertain.
The editor rolls the folder publication back only for the verified-previous case,
keeps it for a verified-new commit, and re-reads `settings.json` before deciding an
uncertain case. If the file is still unreadable or conflicted, no snippet files are
removed; the error names the settings and recovery files to keep until the active
folder has been verified. The editor publishes the folder with
`TextExpanderSettingsUtils.UpdateSnippetsPath`, which re-reads the latest
`settings.json` under the claim and changes only `snippets_path`, so options saved in
Settings meanwhile are kept. `SaveSettingsOrThrow` saves the JSON it's given in full,
and `SaveSettings` takes the same claim and atomic publication but logs a failure
instead of throwing.

**File name.** In a folder, the engine and editor pick the same file with
`TextExpanderSnippetFileNames.ResolveInFolder`:
`PowerToysTextExpanderSnippets.txt` if it exists, otherwise `snippets.txt` if that
exists (the name earlier versions used), otherwise `PowerToysTextExpanderSnippets.txt`,
which either the editor or engine can create. If both names exist and the current
file is byte-for-byte the untouched starter, the legacy file wins; this recovers a
sync client arriving during starter publication without ever ignoring a modified
current file. A file path in the setting is used as it is. Existing files are never
renamed: a folder sync service could propagate the rename while a PC still running an
earlier version would then lose its file. Both use the constants
`TextExpanderSnippetFileNames.Current` and `Legacy` in the core library. Starter publication uses one
shared folder operation: a per-folder cross-process claim serializes engine and
editor creators, both names are resolved again while it is held and after the
atomic move, and the current name is published only when neither exists. An engine
test checks the shared file-name contract. Bug reports leave out both names, and the
copies Text Expander makes of them, anywhere in the PowerToys `TextExpander` folder
(including earlier snippet folders there) and in the configured folder, plus a configured
file whatever its name (`TextExpanderPrivacy.h`, tested by
`tools/BugReportTool/BugReportTool.UnitTests`); both paths are masked in the
report's copy of `settings.json`.

**Starter file.** When the resolved snippet file is missing, whichever process reaches
it first publishes the same shared starter text with a create-if-absent atomic move.
The other process loads the file that won instead of overwriting it. The starter is a
short guide (how to write a snippet, each variable with an example of what it gives,
and tips), examples that are commented out until the user removes the `//`, and six
working snippets, `!ddate`, `!ttime`, `!ddt`, `!isodate`, `!mysig` and `!myemail`,
none of which reads the clipboard or asks for input. Existing files, including empty
files, are never replaced. When the user chooses an empty folder, the editor copies the
current active file there before switching, so selecting a new location never silently
replaces the library with starter snippets.

Snippet file format:

```
// Comments start with //
!mysig=Best regards,\nYour Name
!myaddr<<<
One Microsoft Way
Redmond, WA 98052
>>>
```

### Settings page layout

The page contains enablement and **Manage snippets**.
**Advanced** holds **Wait for space or punctuation**; **Insertion method** (Automatic,
Always paste, or Always type), whose **Minimum length to paste** is available only
with Automatic, the one method that chooses by length; **Match trigger
capitalization**; and **Use PowerToys Keyboard Manager remaps when matching triggers**.
The page only reads `settings.json`: a missing file shows the defaults without creating
it, and a file it can't read shows the defaults with **Can't read Text Expander options**
and is left unchanged. Each option change re-reads the file first, so changes are
refused until it can be read.
The separate editor contains **Add a snippet**, search, the snippet list,
**Save snippets to a folder**, and the **Good to know** note.

### Managing snippets in the editor

**Wait for space or punctuation** is a module-wide setting rather than a field in
each snippet row. A clean settings file defaults it on, and adding a snippet uses
that active value. Loading an existing settings file preserves an explicitly saved
`false` value. The snippet text format, including the shared starter file, stores
only triggers and replacements, so no starter or existing snippet is rewritten.

Search matches triggers and replacement text without changing file order. The
count shows matching and total snippets ("Showing 1 of 1 snippet" for a library of
one); **Clear search** restores the full list.
Select a row to edit it; each row ends with a pencil (tooltip "Edit") instead of a
chevron. **More options** has **Edit**, **Duplicate** and **Delete**. A reload numbers the
snippets again, so it closes an open **More options** menu, and **Edit**, **Duplicate**
and **Delete** refuse a row from before it, including a delete confirmed after a
reload; the list then shows the reloaded file. A delete that removes nothing isn't
reported as deleted.
**Duplicate** opens the copy in the editor, unsaved, with a trigger that can fire
(`!mysig` → `!2mysig`; `!mysig2` would never fire, because typing it completes
`!mysig` first).

Each row shows its trigger as a keycap, in body text (14 px, SemiBold, 32 px tall).
The keycap column is as wide as the widest keycap the list is showing, up to
140 px (`TextExpanderKeycapLayout`); the page measures the triggers and updates it
when the list or the search changes, so descriptions line up 16 px after it.
Longer triggers are trimmed, with the whole trigger in a tooltip.
`TextExpanderRowDescriber` works out the rest, and the view model refreshes it
whenever the list or **Wait for space or punctuation** setting changes:

- **A plain sentence for dynamic snippets only**, from whole localized templates:
  "Link to *host*" (plus "using what you copied" or "asks for *labels*"), "Today's
  date as *value*", "The current time as *value*", "Asks for *labels*, then
  inserts the text" (or *N* lines) and "Inserts what you copied". Each variable it
  mentions is drawn as the editor's bubble: each question label, the date or time
  (without the brackets around it), and "what you copied". A template's
  placeholders are slots for text or bubbles, so translations keep their own word
  order and the localized "and" and ", " between labels. Plain text, and anything
  not recognized with certainty, shows the saved text instead. A sentence too long
  for its two lines shows as text, as the saved text below does
  (`TextOnlyWhenTrimmed`, `TextExpanderTrimFallback`): it switches once WinUI
  reports it trimmed, and tries its bubbles again when the row is clearly wider.
- **The saved text** on one line under the sentence, only when it has letters or
  digits of its own beyond the variables the sentence shows, such as a link's path
  or "Hi …, thanks"; not for `{{prompt:…}}`, `{{clipboard}}` or `[{{date:…}}]` on
  their own. It is all text (`TextExpanderBubbleText` with `TextOnly`): each
  variable is its bubble's icon and label without the box, the cursor is a bar
  character in the accent color, and ↵ and → mark line breaks and tabs. WinUI draws
  an inline element that falls past a trimmed line's ellipsis at the start of the
  line, over the text, so this line has none, and it's spoken as the bubbles would
  be. Links drop `https://` and, when long, keep the host and the end of the path.
- **Without a sentence, the saved text is the title**, on up to two real lines:
  blank lines are skipped, so a message, a blank line and a sign-off still show the
  message and the sign-off, and "…" ends the second line when more follows. Tabs
  show as spacing and the cursor as its bar, or as the text-only bar character
  when the line is cut off. Rows have no chips.
- **Never expands**, with the reason, by the engine's own rules for the current
  mode (`TextExpanderTriggerReach`): another trigger completes partway through
  typing this one; with **Wait for space or punctuation**, only when a space,
    punctuation other than `_`, or a symbol in this trigger releases it at a word start; or a later
  row has the same trigger, since the last one wins. It is one line with a caution
  icon and the reason spelled out, so color is never the only cue.

A row's screen reader name is "*trigger*: *sentence or spoken text*", in plain
words, then the number of lines when there are several and the sentence doesn't
say it ("…, 2 lines"), then the warning when there is one.

The editor validates trigger conflicts and unrepresentable replacement text as
you type, with the reason under the fields in the user's language
(`SnippetProblem`, mapped to resources by the view model). The reason is announced
when it appears or changes and is the **Trigger** box's full description. An empty
dialog stays quiet, but once there's text, a missing trigger says "Type a trigger
to save this snippet." Save errors stay in the dialog with your text intact. Closing an edited
snippet (Cancel, Esc, or the window's close button or Alt+F4) shows "Discard your
changes?" at the bottom of the dialog, above Save and Cancel, with **Keep editing**
(focused; it returns to the field you were editing) and **Discard**. When closing the
window asked, the window comes to the front and **Discard** also closes it. Empty libraries
offer optional signature, email, and date examples that open in the editor;
choosing one does not save it automatically.

The preview shows what the snippet types. Values that are only known when it
runs are drawn as bubbles in the style of a KeyVisual key: **Clipboard**, a
question's label, and dates and times evaluated as they are now. The first
`$|$` is a thin cursor marker, and `\n` and `\t` are real line breaks and tabs.
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
would record the recoloring. The text the box reports uses `\n` line breaks, so
saves write the same bytes the plain text box did. **Insert variable** never
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
`TextExpanderDateGuide` and the preview use the engine's shared
`Variables.FormatDate` implementation in `TextExpander.Core`; tests pin the same
examples for one moment.

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

Failed writes do not reserve triggers or apply deletions in memory. If an
external edit refreshes the library while an existing snippet is open, the editor keeps your draft but requires reopening the
snippet before saving, so an old row handle cannot overwrite a different entry. A new
snippet (**Add a snippet**, **Duplicate** or an example) is checked against the reloaded
file instead and can still be saved. For
folder/default configurations, the editor keeps watching both supported names. Each
watch pairs a `FileSystemWatcher` with a 2-second poll that compares only the files'
existence, size and last-write time, off the UI thread, and reaches the UI only when
they change or the watcher reports an event or error. A
preferred current-name file appearing replaces a loaded legacy file; deleting it
falls back to legacy when present. The engine commits a newly resolved path and its
parsed snippets as one generation-tagged state. Initial loads, folder repoints, and
case-sensitivity changes return explicit transition results; a failed initial load is
reported and stops startup, while a failed live transition keeps the previous path,
metadata, snapshot, and watcher paired, retains the requested target, and retries a
bounded number of times. A live transition stages its parsed state and a candidate
watcher without publishing either. It activates that watcher and re-resolves the file
once more under the same generation, then atomically publishes the complete pair; a
failed watcher registration or handoff read disposes only the candidate. A watcher
`Error` retains the last state and starts the same generation-gated, bounded
re-registration and reload path. Failed attempts never raise the successful
`Reloaded` notification. Watchers have a publication identity separate from desired
transition generations. Each event or retry gets a monotonic request sequence; bursts
coalesce to the newest request, and an older in-flight read cannot clear a newer one.
The watcher paired with a fallback state remains valid while a newer desired transition
is pending or has exhausted its retries: its events refresh the fallback snapshot and
restart attempts toward the desired target. Once a newer transition commits, events
from the disposed watcher are ignored. This handoff prevents a legacy file arriving
beside an untouched starter from being hidden. Either identity change advances the
snippet revision, closes an open **More options** menu and the editor's Prompt window,
and invalidates an editor open on an existing snippet. Watch
events whose bytes equal the editor's own atomic save are ignored. During first-run
setup, the editor creates or observes the shared starter before constructing its
editable model, so the first save cannot produce a headerless file. A file that wins a
concurrent creation race is loaded rather than overwritten. Saves preserve comments,
the file's encoding (UTF-8 with or without a BOM, UTF-16 or UTF-32), line-ending style,
and final-newline presence. A file with bytes that aren't valid in its encoding, such as
one saved as ANSI (`é` is 0xE9), isn't loaded, because saving would replace those bytes
in every line: the editor shows the file error, which asks for the file to be saved as
UTF-8 and reloaded with **Retry**, and changes nothing until then. The engine still
reads such a file, with U+FFFD in place of those bytes.

The list is virtualized (an `ItemsRepeater`), so only the rows in view are built,
however long the library. An `ItemsRepeater` sets no `DataContext` on rows whose
template uses `x:Bind`, so the row and its menu items carry their snippet in
`Tag="{x:Bind}"`, which their handlers read (`TextExpanderRowSnippet`); a test checks
the markup for it. A search changes the shown rows in one step
(`TextExpanderRowCollection`), and the keycap column measures only the triggers
that can be widest. A save updates only the row that changed and doesn't read the
file back: what was written is kept as the file's contents, so the next save still
notices an edit made elsewhere in the meantime.

### Save snippets to a folder

The **Save snippets to a folder** expander in the snippet editor manages the one active
snippet file. Its collapsed description says whether the default local folder or a
folder chosen by the user is active and includes the snippet count when available.
When expanded, it shows "To use the same snippets across devices, choose a folder
synced by OneDrive or another service." directly above **Current folder**. Text
Expander itself does not synchronize files or inspect cloud accounts; that service
remains responsible for transferring the file.

The expander provides folder selection, conflict handling, and manual reload:

- **Current folder** shows the resolved folder and opens the Windows folder picker.
  Selecting an empty folder copies the current active file there before switching.
  If the active source disappeared and the destination is also empty, the editor
  publishes the authoritative starter instead of creating a zero-byte placeholder.
  A genuine existing zero-byte source is copied unchanged.
- **Open snippets file** launches the resolved active file itself with the user's
  default app for text files. It follows current and legacy file names and folder
  changes because it uses the same loaded path as the editor. Missing, inaccessible,
  or failed launches show an error instead of reporting success.
- If the selected folder already contains `PowerToysTextExpanderSnippets.txt` or
  the legacy `snippets.txt`, the editor asks whether to **Use folder file** or
  **Replace folder file**. Using the folder file first backs up the file this PC was
  using; replacing first backs up the destination. No files are merged or silently
  overwritten. The dialog defaults to Cancel.
- **Reload file** rereads the active file from disk. This is the explicit recovery
  action when another device, sync service, app, or text editor updated the file.
  The active-folder file remains the source of truth.
All replacements use a temporary file in the destination directory and then an
atomic move or `File.Replace`. Backup names are unique (`.backup-<timestamp>`, with
a numeric suffix when necessary). The editor holds the shared per-folder claim through
the final re-resolution, decision verification, and publication. It compares the
exact prepared bytes and existence of the active source and both supported
destination names, checks them again immediately before the atomic operation, and
checks ownership after publication. Destination publication and throwing persistence
of `SnippetsPath` form one compensating transaction; the editor does not switch its
loaded library until both succeed. A changed, arrived, or disappeared file, a
post-publication identity-read failure, or a settings-write failure stops the
operation; a vanished source is never replaced with synthesized empty bytes.
Throwing settings persistence writes and flushes a unique staging file beside
`settings.json`, then uses an atomic replace or create-only move. If publication
reports failure after completing, compensation atomically moves the live destination
to a unique same-directory quarantine path before checking its bytes. Only the
transaction's exact published payload is removed or replaced with the verified
previous file. Folder rollback uses the same quarantine-and-verify rule. An unexpected
newer file is moved back only into a provably vacant destination; otherwise its
quarantine is preserved without overwrite. Any remaining quarantine, replacement
backup, or generated recovery copy is reported (as a semicolon-separated path list
when necessary), and the operation returns an explicit uncertain or rollback-failed
result instead of hiding a partial publication.

While the editor is elevated, these actions and all other snippet editing remain
unavailable; no snippet path is resolved or displayed. See *Settings and snippet
storage* above.

### Trigger capitalization

**Match trigger capitalization** is on by default, preserving exact matching. When
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

**Prompt window** in **Insert variable** means “Ask for text when the snippet runs.”
Selecting it immediately puts a safe provisional prompt token at the replacement
caret or selection and opens a compact flyout beside that token. The question field
is focused and selected, and **Require an answer** is on by default. Enter, clicking
back in the replacement, or another light-dismiss commits a valid question without
a second Insert or Apply action. An empty question or one containing `}` keeps the
flyout open with an assertive inline error.

Click an existing prompt token, or put the caret in one and press F2, to reopen the
same flyout. It restores whether that prompt requires an answer. Escape or the
flyout's close button (clicked, or Enter while it has focus) removes a new provisional token and restores the original
replacement selection; for an existing token it discards the draft and reselects the
unchanged token. Closing the parent snippet dialog also discards a pending prompt
draft. A completed new prompt is one editor undo step.

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
