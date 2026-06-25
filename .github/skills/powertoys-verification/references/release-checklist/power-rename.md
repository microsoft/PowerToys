# PowerRename — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06). One module per file.

## Legend

Each item is annotated with two metadata tags:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

**Clarity**:
- (no marker) - clear, has explicit assert
- `[CLARITY: VAGUE-NO-STEPS]` - original wording is just a module/feature name without procedural steps
- `[CLARITY: VAGUE-NO-ASSERT]` - original wording describes an action but does not state the expected outcome
- `[CLARITY: VAGUE-AMBIGUOUS]` - original wording uses vague verbs like "works" without a measurable outcome
- `[REWRITTEN]` - original wording was vague; this checklist has rewritten the description to be concrete. Original wording preserved in italics below the item.

---

## PowerRename (18 items)

- [ ] **[ADMIN: NO] [CLARITY: VAGUE-AMBIGUOUS]** (L393) Check if disable and enable of the module works. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
- [ ] **[ADMIN: NO]** (L394) Check that with the `Show icon on context menu` icon is shown and vice versa.
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-AMBIGUOUS]** (L395) Check if `Appear only in extended context menu` works.
- [ ] **[ADMIN: NO]** (L396) Enable/disable autocomplete.
- [ ] **[ADMIN: NO]** (L397) Enable/disable `Show values from last use`.
- [ ] **[ADMIN: NO]** (L399) Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
- [ ] **[ADMIN: NO]** (L400) Exclude Folders/Files/Subfolder Items (could be selected several)
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L401) Item Name/Extension Only (one at the time)
- [ ] **[ADMIN: NO]** (L402) Enumerate Items. Test advanced enumeration using different values for every field ${start=10,increment=2,padding=4}.
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L403) Case Sensitive
- [ ] **[ADMIN: NO]** (L404) Match All Occurrences. If checked, all matches of text in the `Search` field will be replaced with the Replace text. Otherwise, only the first instance of the `Search` for text in the file name will be replaced (left to right).
- [ ] **[ADMIN: NO]** (L406) Search with an expression (e.g. `(.*).png`)
- [ ] **[ADMIN: NO]** (L407) Replace with an expression (e.g. `foo_$1.png`)
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L408) Replace using file creation date and time (e.g. `$hh-$mm-$ss-$fff` `$DD_$MMMM_$YYYY`)
- [ ] **[ADMIN: NO]** (L409) Turn on `Use Boost library` and test with Perl Regular Expression Syntax (e.g. `(?<=t)est`)
- [ ] **[ADMIN: NO]** (L411) In the `preview` window uncheck some items to exclude them from renaming.
- [ ] **[ADMIN: NO]** (L412) Use the **Filter** (funnel) button above the file list → choose "Only show files that will be renamed" / "Show all files" to filter the preview.
- [ ] **[ADMIN: NO]** (L413) Use the **Select/deselect all** checkbox above the file list to toggle all rows checked/unchecked.

