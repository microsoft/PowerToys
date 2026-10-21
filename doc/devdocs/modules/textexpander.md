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
└── TextExpander.UnitTests/       # Engine unit tests
```

The Settings page lives in `src/settings-ui` (`TextExpanderPage.xaml`, `TextExpanderViewModel.cs`, and `TextExpanderSettings.cs` / `TextExpanderProperties.cs` / `TextExpanderSnippetFile.cs` in `Settings.UI.Library`).

### Module Interface (TextExpanderModuleInterface)

`dllmain.cpp` implements `PowertoyModuleIface`. It:
- Launches `WinUI3Apps\PowerToys.TextExpander.exe` as the desktop shell user when the module is enabled, resolving the path relative to its own DLL. If the shell process cannot be used to create a medium-integrity engine, startup fails closed rather than inheriting an elevated runner token.
- Passes the runner PID, `--managed --instance:powertoys`, and `--settings:"<path to settings.json>"`.
- Stops the engine by signaling `CommonSharedConstants::TEXT_EXPANDER_EXIT_EVENT`, then terminates it if it has not exited within 1.5 seconds.
- Honors the `ConfigureEnabledUtilityTextExpander` group policy.

### Engine (TextExpander)

A self-contained .NET process. Its keyboard hook, matching and injection use no UI framework; WinUI 3 is loaded only for the `{{prompt}}` window. It:
- Installs a low-level keyboard hook and tracks the characters typed into the focused app. Keys are captured only for the foreground window once it has been checked (integrity level, not PowerToys Settings), off the hook's path, after each foreground change. Activation through `AttachThreadInput`, which is how focus goes back to the app after a prompt, raises the foreground event before `GetForegroundWindow` reports the new window, so the hook also asks, once, for a check of a window in front that was never checked (`KeyboardHook.NeedsAuthorization`).
- Asks for `{{prompt:<question>}}` answers in a small WinUI 3 window (`PromptWindow`): Mica, the Text Expander icon and name in a custom title bar, the question above a box, then **OK** (default) and **Cancel**. Prompts are required by default: OK stays disabled while the answer is blank, and Enter shows "Enter an answer to continue." without closing the window. `{{prompt?:<question>}}` is the explicit optional form and still accepts an empty answer. The window follows Windows' theme, including high contrast, is per-monitor DPI aware, stays on top, and opens a little above the middle of the target window's screen (or the pointer's, if that window has gone). It is exactly as tall as its content, title bar row included, with ContentDialog's footer: 24-pixel padding around 32-pixel buttons. Because the content extends into the title bar, the client area includes it, so the window is sized from its measured frame (`PromptWindowSize`) rather than with `AppWindow.ResizeClient`, which leaves room for a system caption that isn't drawn. Esc, Cancel and the close button leave that spot blank and go on to the next question. The keyboard hook is suspended while it is open, so nothing typed in it expands, and focus goes back to the target window afterwards. The box is emptied as soon as the answer is read, and answers are never logged. XAML runs on its own thread (`PromptHost`), started the first time a snippet asks for something, never on the hook's thread; the expansion worker waits there for each answer, so questions are asked one at a time. Measured on an ARM64 Debug build: the first prompt is in front after about 0.3 s and later ones after about 0.12 s; loading XAML adds about 45 MB of private memory to the engine from the first prompt on. If the Windows App SDK can't start, the Win32 fallback (`ClassicPromptDialog`) enforces the same required-answer behavior.
- Matches triggers against the snippet file (`PowerToysTextExpanderSnippets.txt`, or `snippets.txt` from earlier versions), optionally ignoring capitalization and optionally waiting for a word boundary (space or punctuation) before expanding.
- Replaces the trigger by pasting (the user's clipboard is saved and restored) or by typing with `SendInput`, depending on the Insertion method setting and snippet length.
- Expands variables (`{{date}}`, `{{time}}`, `{{datetime}}`, `{{isodate}}`, `{{date:<format>}}`, `{{clipboard}}`, `{{prompt:<question>}}`, `{{prompt?:<question>}}`) and places the cursor at `$|$`. Only the snippet's own text is read for `\n`, `\t`, `{{clipboard}}` and `$|$`: a prompt answer, the clipboard and a formatted date are inserted exactly as they are, so a `$|$` or `\n` in them stays literal.
- Supports undo: pressing Ctrl+Z immediately after an expansion restores the typed trigger. Modifier key-downs on the way to Ctrl+Z do not cancel the pending undo, but any other key does; Ctrl+Shift+Z is left to the app as redo. The undo waits up to one second for modifiers to be released, and is abandoned otherwise, so its Backspaces are never sent as Ctrl+Backspace.
- Ignores its own injected input and, optionally, input injected by Keyboard Manager remaps.
- Runs at the desktop user's integrity level and refuses synthetic input to elevated windows. This preserves expansion in ordinary desktop apps when PowerToys itself is elevated, while preventing user-controlled settings and snippet files from authorizing elevated input or being read by an elevated engine.
- Checks its actual integrity level before loading host settings or snippets and exits if the check fails or the process is above medium integrity.
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

The snippet folder can be changed in non-elevated Settings under **Save snippets to a folder**. Text Expander does not implement a synchronization service: choosing a OneDrive or other provider-managed folder lets that provider synchronize the active file. The Settings page and engine resolve the snippet file with the same rules, so both read the same source-of-truth file. While Settings is elevated, snippet-file resolving, reading, and editing are disabled; elevation is checked from the Settings process token as well as the runner's launch state. The runner launches Settings with its own elevation, so this applies whenever PowerToys runs as administrator; the page tells the user to restart PowerToys without administrator rights to manage snippets. The engine runs with the desktop user's token even when the runner is elevated; a configured path that the desktop user cannot read will therefore fail to load rather than being read with elevated access. If PowerToys is elevated with a different administrator account, the engine runs as the signed-in desktop user and may be unable to read that account's settings and snippets.

**File name.** In a folder, the engine (`SnippetStore.ResolveInFolder`) and Settings
(`TextExpanderSnippetFileNames.ResolveInFolder`) pick the same file:
`PowerToysTextExpanderSnippets.txt` if it exists, otherwise `snippets.txt` if that
exists (the name earlier versions used), otherwise `PowerToysTextExpanderSnippets.txt`,
which the engine then creates. A file path in the setting is used as it is. Existing
files are never renamed: a folder sync service could propagate the rename while a
PC still running an earlier version would then lose its file. Each side keeps the two names in
one place (`SnippetStore.FileName` and `LegacyFileName`, and
`TextExpanderSnippetFileNames.Current` and `Legacy`), and an engine test compiles the
Settings file to check that they match. Bug reports leave out both names in the
PowerToys folder and in the configured folder, plus a configured file whatever its
name (`TextExpanderPrivacy.h`); both paths are masked in the report's copy of
`settings.json`.

**Starter file.** When the snippet file is missing, the engine writes one: a short
guide (how to write a snippet, each variable with an example of what it gives, and
tips), examples that are commented out until the user removes the `//`, and six
working snippets, `!ddate`, `!ttime`, `!ddt`, `!isodate`, `!mysig` and `!myemail`,
none of which reads the clipboard or asks for input. An existing file is never
replaced. When the user chooses an empty folder, Settings copies the current active file there
before switching, so selecting a new location never silently replaces the library
with starter snippets.

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

The page leads with the snippets: **Add a snippet**, search, and the list.
**Advanced** then holds Wait for the whole word; **Insertion method** (Automatic,
Always paste, or Always type), whose **Minimum length to paste** is available only
with Automatic, the one method that chooses by length; **Match trigger
capitalization**; **Save snippets to a folder**; and **Use PowerToys Keyboard
Manager remaps when matching triggers**. The page ends with the
**Good to know** note, which is reference rather than a setting. The warning shown
while PowerToys runs as administrator stays at the top, because it explains why the
snippet controls are off.

### Managing snippets in Settings

Search matches triggers and replacement text without changing file order. The
count shows matching and total snippets ("Showing 1 of 1 snippet" for a library of
one); **Clear search** restores the full list.
Select a row to edit it; each row ends with a pencil (tooltip "Edit") instead of a
chevron. **More options** has **Edit**, **Duplicate** and **Delete**.
**Duplicate** opens the copy in the editor, unsaved, with a trigger that can fire
(`!mysig` → `!2mysig`; `!mysig2` would never fire, because typing it completes
`!mysig` first).

Each row shows its trigger as a keycap, in body text (14 px, SemiBold, 32 px tall).
The keycap column is as wide as the widest keycap the list is showing, up to
140 px (`TextExpanderKeycapLayout`); the page measures the triggers and updates it
when the list or the search changes, so descriptions line up 16 px after it.
Longer triggers are trimmed, with the whole trigger in a tooltip.
`TextExpanderRowDescriber` works out the
rest, and the view model refreshes it whenever the list or **Wait for the whole
word** changes:

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
  typing this one; with Wait for the whole word, only when a space, punctuation or
  symbol in this trigger releases it at a word start; or a later row has the same
  trigger, since the last one wins. It is one line with a caution icon and the
  reason spelled out, so color is never the only cue.

A row's screen reader name is "*trigger*: *sentence or spoken text*", in plain
words, then the number of lines when there are several and the sentence doesn't
say it ("…, 2 lines"), then the warning when there is one.

The editor validates trigger conflicts and unrepresentable replacement text as
you type, with the reason under the fields in the user's language
(`TextExpanderSnippetProblem`, mapped to resources by the view model). An empty
dialog stays quiet, but once there's text, a missing trigger says "Type a trigger
to save this snippet." Save errors stay in the dialog with your text intact. Closing an edited
snippet (Cancel or Esc) shows "Discard your changes?" at the bottom of the dialog,
above Save and Cancel, with **Keep editing** (focused; it returns to the field you
were editing) and **Discard**. Empty libraries
offer optional signature, email, and date examples that open in the editor;
choosing one does not save it automatically.

The preview shows what the snippet types. Values that are only known when it
runs are drawn as bubbles in the style of a KeyVisual key: **Clipboard**, a
question's label, and dates and times evaluated as they are now. The first
`$|$` is a thin cursor marker, and `\n` and `\t` are real line breaks and tabs.
It never reads your clipboard or asks for input. `TextExpanderSnippetPreview`
builds the preview by applying the engine's steps in the engine's order, so a
variable produced by an earlier step is previewed the way it expands; it keeps
its own copy of the strftime table, as the Settings project cannot reference the
engine. The preview's accessible name spells out each bubble, for example
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
Everything is formatted the way the engine formats it (`TextExpanderDateGuide`,
using the preview's copy of the engine's formatter), and tests in both projects pin
the same examples for one moment.

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
external edit refreshes the library while an existing snippet is open, the editor
keeps your draft but requires reopening the snippet before saving, so an old row
handle cannot overwrite a different entry. A file created by the engine during
first-run setup is reloaded rather than overwritten. While the page is shown and
there is no snippet file yet, it watches the folder (under either name), so the
starter snippets the engine writes when it starts appear without reopening the
page. Saves preserve comments, the file's encoding (UTF-8 with or without a BOM,
UTF-16 or UTF-32), line-ending style, and final-newline presence.

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

The **Save snippets to a folder** expander in **Advanced** manages the one active
snippet file. Its collapsed description says whether the default local folder or a
folder chosen by the user is active and includes the snippet count when available.
Text Expander itself does not synchronize files or inspect cloud accounts. A user
who wants synchronization chooses a folder managed by OneDrive or another service;
that service remains responsible for transferring the file.

The expander shows four actions:

- **Current folder** shows the resolved folder and opens the Windows folder picker.
  Selecting an empty folder copies the current active file there before switching.
- If the selected folder already contains `PowerToysTextExpanderSnippets.txt` or
  the legacy `snippets.txt`, Settings asks whether to **Use folder file** or
  **Replace folder file**. Using the folder file first backs up the file this PC was
  using; replacing first backs up the destination. No files are merged or silently
  overwritten. The dialog defaults to Cancel.
- **Reload file** rereads the active file from disk. This is the explicit recovery
  action when another device, sync service, app, or text editor updated the file.
  The active-folder file remains the source of truth.
- **Clear all snippets** requires confirmation, creates a timestamped backup beside
  the active file, atomically replaces it with an empty file, and reloads the list.
  The confirmation defaults to Cancel.

All replacements use a temporary file in the destination directory and then an
atomic move or `File.Replace`. Backup names are unique (`.backup-<timestamp>`, with
a numeric suffix when necessary). If a destination file appears after the picker
inspected an empty folder, the operation stops and asks the user to choose again
rather than replacing the new file without confirmation.

While Settings is elevated, these actions and all other snippet editing remain
unavailable; no snippet path is resolved or displayed. See *Settings and snippet
storage* above.

### Trigger capitalization

**Match trigger capitalization** is on by default, preserving exact matching. When
it is off, the engine builds its suffix trie with invariant case normalization and
uses an ordinal-ignore-case replacement map. Triggers that differ only by case are
therefore duplicates and the last file row wins, matching the existing duplicate
rule. Settings applies the same comparison to validation, copy suggestions and
reachability warnings.

In the snippet list, a trigger containing letters is shown as both forms while the
setting is off, for example `!hello / !HELLO`. The saved trigger is unchanged and
the editor still shows its original capitalization. The row's accessible name also
states that either form matches.

### Prompt authoring and validation

Selecting **Ask me when it runs** from **Insert variable** opens an inline builder
inside the existing snippet dialog. **What should Text Expander ask?** captures the
runtime question, and **Require an answer** is selected by default. The builder
inserts `{{prompt:<question>}}` when required or `{{prompt?:<question>}}` when
optional. It restores the replacement selection captured before the question field
took focus, so the variable goes where the user intended. Questions cannot contain
`}`, which would end the variable early.

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
