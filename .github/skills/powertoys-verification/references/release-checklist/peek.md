# Peek — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06); items rewritten to be
> definitive against the Peek sign-off runs (`verify-Peek-*`) and `../modules/peek.md`.
> One module per file.

## Legend

Each item is annotated with an admin-requirement tag:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

## Fixtures & conventions

- **Opening Peek**: select a file in Explorer and press **Ctrl+Space** (default); Peek opens a window
  titled **`<file> - Peek`**.
- **Prepare a fixtures folder** with one file per previewer type, each carrying an in-file marker so you
  can confirm the *content* rendered (not just that a pane appeared): an **image** (PNG), a **source/text**
  file (e.g. `.cs`), a **markdown** file (H1 + bold + bullet list), a **PDF** with known marker text, an
  **HTML** file with an `<h1>` marker, a **`.zip`** containing ≥1 file, and an **unsupported** file
  (e.g. `.xyz` of random bytes). For the pin/position items also prepare **3 differently-sized images**
  (e.g. 320×240, 800×600, 1920×1080).
- See `../modules/peek.md` for driving mechanics (CLI back-door `PowerToys.Peek.UI.exe <file>`,
  Shell-COM multi-select, chord codes, and the exact UIA selectors `ImagePreview` / `PreviewBrowser` /
  `PinButton` / `LaunchAppButton`).

---

## Peek (18 items)

> **L697–L703 preview the following file types.** For each, open Peek on the fixture and confirm the window `<file> - Peek` shows the expected previewer rendering the fixture's content.

- [ ] **[ADMIN: NO]** (L697) **Image** — Peek an image (e.g. `small-image.png`, 200×150). Confirm the **image previewer** renders the bitmap, sized to the source, with the pin + "Open with" buttons visible.
- [ ] **[ADMIN: NO]** (L698) **Text or dev file** — Peek a source file (e.g. `Program.cs`). Confirm the **developer/text previewer** renders the code with **syntax highlighting and line numbers** (not raw plain text, not blank).
- [ ] **[ADMIN: NO]** (L699) **Markdown file** — Peek a `.md` file containing an H1, bold text, and a bullet list. Confirm the preview renders **formatted markdown** (H1 styled, bold applied, list bulleted) — not the raw `#`/`**` source.
- [ ] **[ADMIN: NO]** (L700) **PDF** — Peek a 1-page PDF with known marker text. Confirm the **PDF page renders** with the marker text visible and a PDF toolbar (zoom / page nav), not an error.
- [ ] **[ADMIN: NO]** (L701) **HTML** — Peek an `.html` file whose `<h1>` contains a marker. Confirm the HTML is **rendered** (heading shown as a styled heading), not shown as raw markup.
- [ ] **[ADMIN: NO]** (L702) **Archive files (.zip, .tar, .rar)** — Peek a `.zip` containing ≥1 file. Confirm the **archive previewer** lists the entries in a tree and shows directory/file counts + sizes (e.g. `0 directories | 1 files | … bytes`). The same previewer handles `.tar`/`.rar`.
- [ ] **[ADMIN: NO]** (L703) **Unsupported file** — Peek a file of an unhandled type (e.g. `unsupported.xyz` / `.exe`). Confirm the **unsupported-file view** is shown with **File Type / Size / Date Modified** metadata (and a generic icon) instead of a previewer, and Peek does not crash.

> **L706–L709 pin / position behavior.** Use a selection of the 3 differently-sized images; move the window with a known position/size and read the window rect to assert.

- [ ] **[ADMIN: NO]** (L706) **Pin, switch between different-sized images** — Open Peek on the 3-image selection, **pin** the window (pin toggle), move it to a known position/size, then switch between the images. Confirm the window stays at the **same position and size** across every switch (does not re-center/resize to each image).
- [ ] **[ADMIN: NO]** (L707) **Pin, close and reopen** — With the window pinned at a known place/size, close Peek (`Esc`) and reopen it on the same selection. Confirm the new window opens at the **same pinned place and size**.
- [ ] **[ADMIN: NO]** (L708) **Unpin, switch file** — From the pinned window, **unpin**, then switch to a different file. Confirm the window **moves to the default placement** (leaves the pinned coordinates and re-centers/sizes to the new file).
- [ ] **[ADMIN: NO]** (L709) **Unpin, close and reopen** — With the window unpinned, close (`Esc`) and reopen Peek. Confirm the new window opens at the **default place/size**, not the previously-pinned coordinates.

> **L712–L713 open the file in its default app** (the "Open with <default app>" action).

- [ ] **[ADMIN: NO]** (L712) **By clicking a button** — With a file peeked, click the **"Open with <default app>"** button. Confirm the file opens in its default application (a new app process/window for that file appears; match by the file's window title).
- [ ] **[ADMIN: NO]** (L713) **By pressing Enter** — With the Peek window focused, press **Enter**. Confirm the file opens in its default application (same result as L712).
- [ ] **[ADMIN: NO]** (L716) **Peek via command** — Run `PowerToys.Peek.UI.exe <file>`. Confirm it opens a `<file> - Peek` window previewing that file (the CLI accepts a file-path argument and shows the previewer).
- [ ] **[ADMIN: NO]** (L717) **Peek works while a session is already on** — Open several Peek sessions on different files, leaving prior windows open. Confirm each launch opens its own window and previews correctly with the others still open (no crash/hang, no fatal errors in `Peek\Logs`).
- [ ] **[ADMIN: NO]** (L719) **Folder navigation with Left/Right arrows** — With a **single** file selected in a folder, open Peek and press `RightArrow` / `LeftArrow`. Confirm you can cycle through **all files in the folder** (in order, wrapping at the ends).
- [ ] **[ADMIN: NO]** (L720) **Selection-scoped navigation** — Select **multiple** files (a subset of a folder), open Peek and press `RightArrow` / `LeftArrow`. Confirm navigation cycles **only among the selected files** and never escapes into the folder's other files.
- [ ] **[ADMIN: NO]** (L721) **Change the activation shortcut** — In Settings → Peek, change the activation shortcut (e.g. `Ctrl+Space` → `Ctrl+Shift+Space`). After the change takes effect (the runner re-registers the hook — it is **not** hot-reloaded), confirm the **new** chord opens Peek and the **old** chord no longer does. Restore the original shortcut afterward.
