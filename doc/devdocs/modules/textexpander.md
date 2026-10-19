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
- Asks for `{{prompt:<label>}}` answers in a small WinUI 3 window (`PromptWindow`): Mica, the Text Expander icon and name in a custom title bar, the label above a box, then **OK** (default) and **Cancel**. It follows Windows' theme, including high contrast, is per-monitor DPI aware, stays on top, and opens a little above the middle of the target window's screen (or the pointer's, if that window has gone). It is exactly as tall as its content, title bar row included, with ContentDialog's footer: 24-pixel padding around 32-pixel buttons. Because the content extends into the title bar, the client area includes it, so the window is sized from its measured frame (`PromptWindowSize`) rather than with `AppWindow.ResizeClient`, which leaves room for a system caption that isn't drawn. It opens at the height of a one-line question and is refit after layout, at the window's own scale, for a question that wraps or larger text. Enter is OK; Esc, Cancel and the close button leave that spot blank and go on to the next question. The keyboard hook is suspended while it is open, so nothing typed in it expands, and focus goes back to the target window afterwards. The box is emptied as soon as the answer is read, and answers are never logged. XAML runs on its own thread (`PromptHost`), started the first time a snippet asks for something, never on the hook's thread; the expansion worker waits there for each answer, so questions are asked one at a time. Measured on an ARM64 Debug build: the first prompt is in front after about 0.3 s and later ones after about 0.12 s; loading XAML adds about 45 MB of private memory to the engine from the first prompt on. If the Windows App SDK can't start, the earlier Win32 box (`ClassicPromptDialog`) is used.
- Matches triggers against the snippet file (`PowerToysTextExpanderSnippets.txt`, or `snippets.txt` from earlier versions), optionally waiting for a word boundary (space or punctuation) before expanding.
- Replaces the trigger by pasting (the user's clipboard is saved and restored) or by typing with `SendInput`, depending on the Insertion method setting and snippet length.
- Expands variables (`{{date}}`, `{{time}}`, `{{datetime}}`, `{{isodate}}`, `{{date:<format>}}`, `{{clipboard}}`, `{{prompt:<label>}}`) and places the cursor at `$|$`. Only the snippet's own text is read for `\n`, `\t`, `{{clipboard}}` and `$|$`: a prompt answer, the clipboard and a formatted date are inserted exactly as they are, so a `$|$` or `\n` in them stays literal.
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

The snippet folder can be changed in non-elevated Settings, in the **Folder** row of **Where your snippets are saved and synced across PCs**. The Settings page and engine resolve the snippet file with the same rules, so both read the same file in that mode. While Settings is elevated, snippet-file resolving, reading, and editing are disabled; elevation is checked from the Settings process token as well as the runner's launch state. The runner launches Settings with its own elevation, so this applies whenever PowerToys runs as administrator; the page tells the user to restart PowerToys without administrator rights to manage snippets. The engine runs with the desktop user's token even when the runner is elevated; a configured path that the desktop user cannot read will therefore fail to load rather than being read with elevated access. If PowerToys is elevated with a different administrator account, the engine runs as the signed-in desktop user and may be unable to read that account's settings and snippets.

**File name.** In a folder, the engine (`SnippetStore.ResolveInFolder`) and Settings
(`TextExpanderSnippetFileNames.ResolveInFolder`) pick the same file:
`PowerToysTextExpanderSnippets.txt` if it exists, otherwise `snippets.txt` if that
exists (the name earlier versions used), otherwise `PowerToysTextExpanderSnippets.txt`,
which the engine then creates. A file path in the setting is used as it is. Existing
files are never renamed: OneDrive would sync the rename to PCs still running an
earlier version, which would then lose their file. Each side keeps the two names in
one place (`SnippetStore.FileName` and `LegacyFileName`, and
`TextExpanderSnippetFileNames.Current` and `Legacy`), and an engine test compiles the
Settings file to check that they match. Bug reports leave out both names in the
PowerToys folder, in the configured folder and in the remembered sync folder
(`last_sync_folder`, below), and a configured file whatever its name
(`TextExpanderPrivacy.h`); both paths are masked in the report's copy of
`settings.json`.

**Starter file.** When the snippet file is missing, the engine writes one: a short
guide (how to write a snippet, each variable with an example of what it gives, and
tips), examples that are commented out until the user removes the `//`, and six
working snippets, `!ddate`, `!ttime`, `!ddt`, `!isodate`, `!mysig` and `!myemail`,
none of which reads the clipboard or asks for input. An existing file is never
replaced. This applies to any folder selected manually in the **Folder** row: the
setting changes immediately, and if that folder has neither supported file name,
the engine can create the starter file there when it reloads the setting.

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
with Automatic, the one method that chooses by length; **Where your snippets are
saved and synced across PCs**; and Include keys changed by Keyboard Manager. The page ends with the
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

### Where your snippets are saved and synced across PCs

Everything about where snippets are kept and synced is in one **Where your snippets
are saved and synced across PCs** expander in **Advanced**. Its collapsed description is a one-line summary from
`TextExpanderViewModel.SnippetFileSummary`: "Syncing with *account* · *n*
snippets" for a folder inside a OneDrive account, "Saved on this PC · *n*
snippets" for the PowerToys folder, and "Saved in a folder you chose · *n*
snippets" for any other folder. The count is left out when the file can't be read.
The header has only the title, that summary and the chevron. Its items are:

- **Sync across PCs**: a toggle for the guided OneDrive setup described below,
  first because it decides where the snippets live. Its setup and anything that
  needs attention show right under it; a finished change is only announced.
- **Folder**: the folder text box and **Browse** (the Windows App SDK folder picker,
  opening at the folder in use), for any folder, including one in OneDrive or another
  sync app. Its description names the snippet file in use
  (`TextExpanderViewModel.SnippetsFolderDescription`), since a folder can hold a
  `snippets.txt` from an earlier version. Empty means the PowerToys folder, whose
  path the empty box shows as its placeholder, and the box's tooltip shows the full
  path of the snippet file in use. The Settings page and the engine resolve the
  snippet file with the same rules. The row shows while syncing; whenever a folder
  is set, so a folder outside OneDrive (which shows sync off) or a mistyped one can
  be seen and cleared; and when OneDrive isn't set up, so another sync app's folder
  can still be set. It is hidden during the sync setup, whose **Choose a different
  folder in OneDrive…** is the only folder picker there (`TextExpanderViewModel.IsFolderRowVisible`).

With sync off and the PowerToys folder in use, the box shows only the toggle.

While Settings is elevated, all of these actions are unavailable, including
search, **Retry**, the example buttons, and that expander, which can't be opened
and reads "Not available while PowerToys runs as administrator"; the resolved path
is not shown. See *Settings and snippet storage* above.

### Sync across PCs (OneDrive)

The **Sync across PCs** toggle sets up a shared
library in OneDrive. It is implemented by `TextExpanderSyncViewModel`, and all
file, environment and watcher access goes through `ITextExpanderSyncEnvironment`,
so tests run against temporary folders. When the page opens, and when the
expander opens, `RefreshStatus` finds the accounts and whether the snippet file is
in one. It reads no file content in OneDrive, so it can't start downloading an
online-only file; only the setup, once the user turns the toggle on, checks the
destination for an existing snippet file. Everywhere below, a snippet file under
either name counts; files Text Expander creates get the current name.

- **The toggle.** It is on when the snippet folder is inside an account folder,
  including a folder of the user's own such as an absolute path typed earlier,
  and while a setup the user started is unfinished. It is bound one way to
  `IsSyncOn`; the page passes each flip to `UserToggled`, which ignores flips that
  only mirror the current state, so putting the toggle back never starts a flow
  again.
  - **Turning it on** shows the setup under the toggle (first PC or another PC, the
    account only when there are two to choose from, and the steps below) and moves
    focus into it.
    Turning it back off, collapsing the expander or leaving the page ends an
    unfinished setup and the toggle shows Off. While waiting for OneDrive to
    deliver the file the toggle stays on, with **Cancel**; collapsing keeps the
    wait going, and leaving the page ends it. The toggle settles on only once the
    folder has switched.
  - **Turning it off** asks first, naming the backup this PC's own snippet file
    will get, if it has one. **Cancel** leaves it on. **Stop syncing** copies
    the OneDrive file to `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander` as
    `PowerToysTextExpanderSnippets.txt` (the previous local file, under either name,
    is kept as a backup named after it, such as `snippets.backup.txt`), then clears
    the snippet folder. The OneDrive copy is never deleted.
    If the OneDrive file disappeared before it could be copied, stopping fails with
    an error and leaves the OneDrive folder configured rather than silently
    switching to an older local file or a new starter library.
  - **The folder is remembered.** Whenever the snippet folder stops being in
    OneDrive (**Stop syncing**, or the Folder row cleared or changed while syncing),
    the OneDrive folder is saved as `last_sync_folder` in the module's
    `settings.json`, written like the setup writes the snippet folder, for example
    `%OneDriveCommercial%\PowerToys\TextExpander`. It has no control, and the engine
    ignores it. When the toggle is next turned on, if that folder is in an account
    on this PC and still has a snippet file, it replaces `PowerToys\TextExpander`
    for both choices, and its account is selected: the first PC compares against it
    (with OneDrive's snippets chosen), and another PC finds it at once.
    **Choose a different folder in OneDrive…** still overrides it.
  - The account and count are in the expander's summary. Under the toggle, only
    what needs attention shows: the setup, an online-only file, the keep-a-copy
    option, or an error. Every change (copied, now using, arrived, kept a copy,
    stopped, stopped looking, not found) is announced to screen readers only, so
    after a sync the box shows the toggle and the Folder row.
- **Accounts.** `%OneDriveConsumer%` ("OneDrive – Personal") and
  `%OneDriveCommercial%` ("OneDrive – *organization* (work or school)") are used
  when they name existing folders; `%OneDrive%` only when neither does. The
  folder is saved with the matching variable, for example
  `%OneDriveCommercial%\PowerToys\TextExpander`, so the setting works on PCs with
  different user names. When no account exists, the toggle is disabled and a
  message says why, with an **Open OneDrive** action when OneDrive.exe is installed.
- **First PC.** Copies this PC's snippet file byte for byte, through a temporary
  file that is then moved into place, as `PowerToysTextExpanderSnippets.txt`. If
  OneDrive already has a snippet file, nothing is written until the user chooses to
  use OneDrive's file (the default), replace it in place, under its own name so PCs
  on earlier versions still find it (OneDrive's file is kept as
  `<name>.backup.txt`, or a numbered name), or keep both (this PC's file is saved
  as `<name> (<computer name>).txt`). Files are never merged.
- **Another PC.** Off the UI thread, under "Looking for your snippets in OneDrive…"
  (the only spinner in the setup), Settings looks in the folder the snippets were last
  synced in, then in `PowerToys\TextExpander`, and if neither has a snippet file,
  searches OneDrive. Counting a file reads it, which downloads an online-only file, so
  none of this runs on the UI thread:
  - **The search** (`TextExpanderSnippetSearch`) runs off the UI thread, in the
    selected account and then the others, breadth-first and at most four folders
    deep. It stops after 5,000 folders or 3 seconds in all, and is cancelled by
    **Cancel**, turning the toggle off, leaving the page or changing a choice. It
    skips hidden and system folders, folders whose names start with ".", and links
    (symbolic links and junctions). It can't skip every reparse point: every OneDrive
    folder is one, as a cloud placeholder, so a link is told apart by its link target.
    It reads folder listings only (`ITextExpanderSyncEnvironment.ListFolder`), never
    file content, so it downloads nothing.
  - **What matches.** `PowerToysTextExpanderSnippets.txt` anywhere. The common
    `snippets.txt` only in a folder whose path below the account folder names Text
    Expander or PowerToys, ignoring case, spaces, hyphens and underscores, such as
    `powertoys-textexpander`. In a folder with both, the current name, as in the engine.
  - **Found.** One file shows its account, count and date, and its location down to
    the file's own name ("Location: OneDrive - Contoso › Snippets › snippets.txt",
    with the full path as the tooltip), so a wrong folder is easy to spot, before
    **Use these snippets**. Several files are listed, up to five, newest first and
    selected; each shows its folder, its date and its location down to the file
    name. Only the first file is read, for its count, since reading downloads an
    online-only file; if it can't be read, the next newest is offered first.
  - **Not found.** "Couldn't find your snippets in OneDrive", what was checked, and
    the default location (its full path is the tooltip), with **Choose folder…**,
    **Cancel** and **Set up as my first PC instead**. It is announced to screen
    readers. Settings keeps using this PC's snippets and keeps watching the default
    location while the page is open (FileSystemWatcher plus polling, since the
    folder may not exist yet); collapsing the expander keeps watching. When the file
    arrives, it switches by itself and announces it.
  - **Choose a different folder in OneDrive…** (or **Choose folder…**) picks a folder
    instead, in the folder picker opened at the selected account's folder, and
    accepts only folders inside an account folder. A chosen folder is
    the answer on its own: if it has no snippet file, nothing else is searched, and
    the not-found state says that folder was checked.
- **The guided setup changes the folder setting only after a snippet file exists in
  the destination** (copied there for a first PC, selected there, or found after
  waiting on another PC), so that flow never points the engine at an empty OneDrive
  folder. This guarantee is specific to the guided setup. Manually choosing or
  typing an empty folder in the ordinary **Folder** row saves it immediately, and
  the engine can seed its starter file there as described above.
- **Online-only files.** If the snippet file has `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS`
  or `FILE_ATTRIBUTE_OFFLINE` and is not pinned, a warning offers **Keep a copy on
  this PC**, which sets `FILE_ATTRIBUTE_PINNED` (as `attrib +P` and File
  Explorer's "Always keep on this device" do).
- **Elevated Settings.** The **Where your snippets are saved and synced across PCs** expander and the toggle are disabled. Nothing reads
  environment variables, touches files, starts watchers or launches OneDrive.

## Debugging

1. Build `PowerToys.slnx` and start the runner.
2. Enable Text Expander in Settings.
3. Attach the debugger to `PowerToys.TextExpander.exe`.

The engine can also be started directly for debugging: `PowerToys.TextExpander.exe --managed --settings:"<path to settings.json>"`.
A second engine can run beside PowerToys' own with `--instance:<name>` and a settings file of its own, whose
folder then holds its snippets. In Debug builds, setting `POWERTOYS_TEXTEXPANDER_PROMPT_THEME` to `Light` or
`Dark` for that process shows the prompt window in that theme without changing Windows'.
