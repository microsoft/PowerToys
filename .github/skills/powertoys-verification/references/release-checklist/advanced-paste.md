# Advanced Paste — PowerToys release checklist

> Source: baseline split from `release-checklist-annotated.md` (0.96-aligned), extended with items
> derived from Advanced Paste PRs merged v0.96 → v0.100 (see "New / updated since v0.96"). One module per file.

## Legend

Each item is annotated with two metadata tags:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin

**Clarity**:
- (no marker) - clear, has explicit assert
- `[CLARITY: VAGUE-NO-STEPS]` - original wording is just a module/feature name without procedural steps
- `[CLARITY: VAGUE-NO-ASSERT]` - original wording describes an action but does not state the expected outcome
- `[CLARITY: VAGUE-AMBIGUOUS]` - original wording uses vague verbs like "works" without a measurable outcome
- `[REWRITTEN]` - original wording was vague; rewritten concrete. Original preserved in italics.
- `[EXTERNAL: API-KEY]` - needs an AI provider key/endpoint; without one the item is `BLK-EXTERNAL`, not a failure.

---

## Advanced Paste (41 items)

### Plain text / Markdown / JSON (baseline)

- [ ] **[ADMIN: NO]** (L865) Copy some rich text (e.g. mixed colors, bold, underlined)
- [ ] **[ADMIN: NO]** (L866) Paste with Ctrl+V - rich text pasted (with colors, formatting)
- [ ] **[ADMIN: NO]** (L867) Paste with Paste as Plain Text hotkey - plain text pasted
- [ ] **[ADMIN: NO]** (L868) Paste again with Ctrl+V - text is now pasted plain (clipboard preserved as plain)
- [ ] **[ADMIN: NO]** (L869) Copy some rich text again
- [ ] **[ADMIN: NO]** (L870) Open AP, click Paste as Plain Text button - plain text pasted
- [ ] **[ADMIN: NO]** (L871) Copy some rich text again
- [ ] **[ADMIN: NO]** (L872) Open AP, press Ctrl+1 - plain text pasted
- [ ] **[ADMIN: NO]** (L874) Open Settings and set Paste as Markdown directly hotkey
- [ ] **[ADMIN: NO]** (L875) Copy some text (e.g. HTML text - convertible to Markdown)
- [ ] **[ADMIN: NO]** (L876) Paste with set hotkey - text converted to markdown
- [ ] **[ADMIN: NO]** (L877) Copy some text again
- [ ] **[ADMIN: NO]** (L878) Open AP, click Paste as markdown button - text converted to markdown
- [ ] **[ADMIN: NO]** (L879) Copy some text again
- [ ] **[ADMIN: NO]** (L880) Open AP, press Ctrl+2 - text converted to markdown
- [ ] **[ADMIN: NO]** (L882) Open Settings and set Paste as JSON directly hotkey
- [ ] **[ADMIN: NO]** (L883) Copy some XML or CSV text
- [ ] **[ADMIN: NO]** (L884) Paste with set hotkey - text converted to JSON
- [ ] **[ADMIN: NO]** (L885) Copy some text again
- [ ] **[ADMIN: NO]** (L886) Open AP, click Paste as markdown button - JSON test path
- [ ] **[ADMIN: NO]** (L887) Copy some text again
- [ ] **[ADMIN: NO]** (L888) Open AP, press Ctrl+3 - text converted via JSON path

### AI custom format (baseline)

- [ ] **[ADMIN: NO]** [EXTERNAL: API-KEY] (L890) Open Settings, set OpenAI key for AI custom format
- [ ] **[ADMIN: NO]** [EXTERNAL: API-KEY] (L891) Copy text, open AP, custom input "Insert smiley after every word", verify result
- [ ] **[ADMIN: NO]** [EXTERNAL: API-KEY] (L893) Open AP, input query, regenerate, select and paste
- [ ] **[ADMIN: NO]** (L894) Create custom actions, set hotkey, test ctrl+<num>, enable/disable, reorder
- [ ] **[ADMIN: NO]** (L895) Disable Custom format preview - result pasted right away
- [ ] **[ADMIN: NO]** (L896) Disable Enable Paste with AI - Custom Input text box disabled

### Clipboard history & enable/disable (baseline)

- [ ] **[ADMIN: NO]** (L898) Open AP, click Clipboard history, delete entry, verify gone from Win+V
- [ ] **[ADMIN: NO]** (L899) Open AP, click Clipboard history, click entry, verify put on top of Win+V
- [ ] **[ADMIN: NO]** (L900) Disable clipboard history - Clipboard history button disabled in AP
- [ ] **[ADMIN: NO]** (L901) Disable Advanced Paste, try hotkeys - confirm nothing happens

### New / updated since v0.96 (PR-derived)

- [ ] **[ADMIN: NO]** (#43990) Copy a hex color string (e.g. `#3478F6`), open Clipboard history in AP - that entry shows an RGB color-swatch preview next to the value
- [ ] **[ADMIN: NO]** (#44021) Copy an image to the clipboard, open AP - image is accepted as input (image-input handling); a custom/AI action can run on it (image preview shown, no error)
- [ ] **[ADMIN: NO]** (#44767) Select text in an editor and invoke a custom-action hotkey WITHOUT pressing Ctrl+C first - AP auto-copies the selection and acts on it; clipboard is preserved
- [ ] **[ADMIN: NO]** (#46486) Repeat the auto-copy custom-action hotkey in an Electron/Chromium app (VS Code / Chrome) - selection is still auto-copied (no longer fails on those apps)
- [ ] **[ADMIN: NO]** (#45242) Settings: toggle "show/hide AI paste section" - the AI paste section appears/disappears in the AP window accordingly
- [ ] **[ADMIN: NO]** [EXTERNAL: API-KEY] (#44293, #45362, #43716) Settings: switch AI provider (OpenAI / Azure OpenAI / Gemini / Foundry Local) - the configured endpoint persists per provider (Gemini does not keep an Azure placeholder; Foundry local port change is picked up on the fly)
- [ ] **[ADMIN: NO]** (#44862, #45207, #45699) Open the Advanced Paste Settings page after upgrading from an older settings.json - page loads without crashing (settings upgraded safely)
- [ ] **[ADMIN: NO]** (#44212) Clipboard history: select the same item twice - no duplicate entry is created
- [ ] **[ADMIN: NO]** [CLARITY: VAGUE-NO-ASSERT] (#48124) Paste as JSON with an unreadable/locked clipboard - conversion fails gracefully (no crash); copy convertible XML/CSV again and confirm JSON still works
