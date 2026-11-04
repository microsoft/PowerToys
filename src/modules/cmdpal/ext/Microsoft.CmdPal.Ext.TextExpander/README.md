# Text Expander snippets (built-in extension)

Search Text Expander's snippets in Command Palette and paste one into the app Command Palette was opened over. The extension reads Text Expander's own snippets file the way Text Expander does, through `TextExpander.Core`, and never writes it.

## What users see

- **Text Expander snippets** in the main list opens a page of snippets. Each item shows the trigger as its title and a one-line preview as its subtitle, with dates and times as they'd come out now, and in brackets, each clipboard variable's name where its text goes, such as `[Clipboard]` or `[Link from clipboard]`, and each question's words where its answer goes, such as `[Name]`. The details pane shows the whole snippet.
- Search matches triggers first, best first: the exact trigger, one that starts with the search, one that contains it, a fuzzy match. Then come snippets whose text contains the search. Ties keep the file's order.
- The main search also offers the snippet whose trigger best matches what's typed (a fallback item, `com.microsoft.cmdpal.builtin.textexpander.fallback`). Only trigger matches are offered there; its other commands include the page, opened on the same search.
- **Enter** pastes the snippet. **Copy** puts it on the clipboard instead.
- A snippet with questions (`{{prompt:...}}`, `{{prompt?:...}}`) opens a page that asks them in the search bar, as Bookmarks asks for placeholders: each question is followed by a box, Tab moves between them, and Enter pastes once every required answer is in. A required answer can't be blank; an optional one can. Esc goes back without pasting. The answers are cleared once the snippet is in.
- A snippet that takes part of the copy, such as `{{clipboard:email}}`, opens a pick page once any questions are answered. When the copy has several email addresses, the page asks "Pick the email address to insert" above them, as Text Expander's window does; typing filters them and Enter pastes the snippet with the one picked. Each spot that has several matches gets a page, in the order Text Expander asks them. When nothing needs picking, the page shows the snippet as it will go in, and Enter pastes it: Command Palette opens a page only from an item, not from a command's result, so this one step can't be skipped. A part the copy doesn't have leaves its spot empty, the rest goes in, and a toast says so in the words of Text Expander's notice ("There's no email address in what you copied, so that spot was left empty."), with one sentence for each kind of part that wasn't there, as it does for `{{clipboard}}` when the clipboard holds no text. Copy is on the pick page.
- Snippets that pick a copy from clipboard history when they run (`{{clipboard:pick}}`, `{{clipboard:pick:email}}` and the like) need Text Expander's own window. They're listed with a **Text Expander only** tag, and Enter explains that the trigger has to be typed instead.
- When Text Expander is off, the page says so and offers to open its settings. When group policy turns it off, the page says the organization manages it. Missing, unreadable or broken snippets files get their own messages.

## Where the data comes from

| What | Where | Notes |
|------|-------|-------|
| On or off | Group policy `ConfigureEnabledUtilityTextExpander`, then `ConfigureGlobalUtilityEnabledState` (HKLM before HKCU), then `enabled.TextExpander` in `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` | Off when the setting is missing, Text Expander's default. Read as `gpo.h` reads it. |
| Settings | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\settings.json` | `snippets_path`, `case_sensitive` and `injection_backend`, bare or as `{"value": x}`. A file caught mid-save keeps the values read before, as the engine does. |
| Snippets | The chosen file, or `PowerToysTextExpanderSnippets.json` in Text Expander's folder | Selected by `SnippetPathResolution`, read by `SnippetFile.Read`. Duplicate triggers resolve as in Text Expander (the last wins, with its capitalization setting); triggers that can't be typed are left out. |

The page checks these each time it's opened and as the search changes. The main search reuses what it read for up to a second (`SnippetSource.SearchMaxAge`), since it asks on every keystroke. A snippets file is read again only when its size or write time changes, and a file that couldn't be read (locked mid-save, a drive that isn't ready) is tried again next time.

## Expanding

`Snippets/SnippetText.cs` is the only place that expands snippets. It calls `Variables.ExpandWithPromptRequests`, so variables Text Expander adds later work here too:

- Dates and times come out as of now, in the regional format.
- Clipboard variables, all of the copy (`{{clipboard}}`) or one part of it (`{{clipboard:email}}`, `link`, `phone` or `number`), are expanded by Core from one read of the clipboard, made before the paste borrows it and only if a variable needs it.
- For a snippet that takes part of the copy, the first pick page reads the clipboard and `SnippetText.PlanClipboard` does a dry run of the expansion, recording each choice Core asks for through its `choose` callback (only for a spot with 2 to 100 options), so Core decides when to ask and in what order. The paste expands again with that copy, answering the choices in order with the matches picked (`SnippetFill`).
- Core reports the spots it left empty, and why, in `ExpandedSnippet.Gaps`. The pick page and the toast after a paste are built from them (`ClipboardVariableText.NothingFound`), so the extension never looks for parts itself.
- Questions are found by recording the requests `ExpandWithPromptRequests` makes, so Core's parser decides what a question is. A question that appears twice is asked twice, as in Text Expander.
- The first `$|$` is dropped and the caret stays at the end of the snippet. Later markers stay literal.

Previews (`SnippetText.Preview`) read the snippet's parts with `SnippetTemplate.Parse` and format dates with `Variables.FormatToken`, as Text Expander's snippet editor does, and never read the clipboard. `ClipboardVariableText` names each clipboard variable with the editor's words, and has the pick pages' and toasts' words.

`Snippets/TextExpanderOnlyVariables.cs` decides which snippets only Text Expander can insert: those with a clipboard variable that picks a copy from history (`PickCopy` on Core's template tokens).

## Inserting

`Insertion/SnippetInserter.cs` puts a snippet in as carefully as Text Expander does, with the rules it shares through `TextExpander.Core` (`ClipboardFormats`, `ClipboardPrivacy`, `ClipboardLoan`):

1. Command Palette hides (`HideWindowMessage`), and the extension waits up to 1.5 s for a window of another process to come to the front. A window running as administrator, or one that can't be inspected, is refused: Windows would drop the input silently, and as in Text Expander, a snippet never goes above medium integrity.
2. Unless Text Expander's **Insertion method** is **Always type**, every clipboard format is copied. A clipboard that can't be put back exactly (a handle format, one that renders nothing, or over 32 MB) isn't borrowed at all, and the snippet is typed instead.
3. The snippet goes on the clipboard after the exclusion formats that keep it out of Clipboard History, Cloud Clipboard and clipboard managers, and the write is checked. Ctrl+V goes out in one batch, after key-ups for any modifier the user still holds after a second.
4. The clipboard goes back once the app has read it (its process opens and closes the clipboard, at least 150 ms after the V) or after 350 ms. An app that isn't answering messages keeps its paste queued, so the clipboard waits for up to 5 s; after that the snippet stays on the clipboard and the user is told. These are the engine's numbers (`PasteSettle`).
5. The user's clipboard goes back with the exclusion formats, unless it already carried any, so the restore isn't recorded as a new copy. If someone copied something after the paste, their copy wins, and that's not a lost clipboard. The clipboard's number is taken after the write only while the extension's window still owns the clipboard (`ClipboardLoan.TakeAfterWrite`), so a copy made right after the write isn't taken for the extension's. Whether the clipboard is still the extension's is checked before each of the 3 attempts, and again once the clipboard is open, before it's emptied, since the open may have waited for an app that was copying. Only an attempt that emptied the clipboard re-arms the loan for the next try. The attempts are Text Expander's own loop, `ClipboardHandBack.Run` in TextExpander.Core, so the two can't drift apart.
6. Typing sends one character per `SendInput` call, 25 ms apart and 25 ms more after `.`, `!`, `?` and line breaks. Line breaks are Enter and tabs are Tab; a surrogate pair goes as one batch. Typing stops if another window comes to the front.

Every key event carries `InputTags.CommandPalette` from TextExpander.Core: Keyboard Manager's injected bit, so a remap such as Caps Lock and Ctrl swapped leaves the Ctrl+V alone, in a value Text Expander's hook ignores, so typed snippet text can't set off its triggers. If a batch is cut off part way, Ctrl, or a typed Enter or Tab, is let go of, and the clipboard waits for the app before it goes back. A held Alt or Windows key is let go of after a key that does nothing (`0xFF`, as Keyboard Manager sends), so the app's menu or the Start menu doesn't open. The clipboard is owned by a message-only window on a thread of its own (`ClipboardOwnerWindow`), as in the engine.

Failures show a toast and leave the page as it was. When the snippet didn't go in, the toast offers to copy it instead.

## Code

| Folder | Contents |
|--------|----------|
| `TextExpanderCommandsProvider.cs` | Registers the page and the fallback item. Registered in `Microsoft.CmdPal.UI/App.xaml.cs`. |
| `SnippetActions.cs` | Makes a snippet's commands, the same on the page and in the main search. |
| `Snippets/` | State (`SnippetSource`, `TextExpanderPolicy`, `TextExpanderModuleState`, `TextExpanderSettings`), the snippets file (`SnippetLibrary`), expansion and previews (`SnippetText`) and search (`SnippetSearch`). |
| `Pages/` | The snippets page, its items and empty states, the questions page, the pick pages and the fallback item. |
| `Commands/` | Paste, copy, the Text Expander-only notice, and opening Text Expander's settings. |
| `Insertion/` | Pasting and typing. The Win32 classes are thin; `SnippetInserter`, `PasteSettle` and `KeyStrokes` take their clipboard, windows and clock as interfaces, so they're unit-tested with fakes. |

`TextExpander.Core` is compiled ahead of time with Command Palette in release builds, so it imports `Common.Dotnet.AotCompatibility.props`. Keep it free of reflection-based JSON and other code the trimming analyzers flag.

## Not supported

- Clipboard variables that pick a copy from clipboard history when the snippet runs (`{{clipboard:pick}}`, `{{clipboard:pick:email}}` and the like), which need Text Expander's own window.
- Skipping the pick page when nothing needs picking: Command Palette can't open a page from a command's result, so the extension can't decide after reading the clipboard.
- Placing the caret at `$|$`.
- Text Expander's insertion guard, which holds the user's keys while it types, and its Ctrl+Z undo.
- Pasting into windows running as administrator.
- Editing snippets: use **Manage snippets** in PowerToys Settings > Text Expander.

## Tests

`src/modules/cmdpal/Tests/Microsoft.CmdPal.Ext.TextExpander.UnitTests` covers the state and settings, the snippets file, expansion and previews, search, the pages and fallback, the pick pages, and pasting and typing with an in-memory clipboard, desktop and clock. Nothing in it drives the real keyboard, clipboard or windows.

```powershell
vstest.console.exe x64\Debug\WinUI3Apps\CmdPal\tests\Microsoft.CmdPal.Ext.TextExpander.UnitTests.dll
```
