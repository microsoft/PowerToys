# Image Resizer — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06). One module per file.

## Legend

Each item is annotated with an admin-requirement tag:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

## Fixtures & conventions

- Prepare test images at a **known DPI** (the pixel outputs below assume **96 DPI**): `landscape 1200×800`, `portrait 800×1200`, `small 100×100`, `square 400×400`, `anim.gif` (animated), and three `batch*` images `1000×1000`.
- Default presets are **Small 854×480, Medium 1366×768, Large 1920×1080, Phone 320×568** — all **Fit**, unit **Pixel**.
- Fit modes: **Fit** = scale by `min(scaleX,scaleY)` (letterbox, no crop); **Fill** = scale by `max(scaleX,scaleY)` then centered-crop to the box; **Stretch** = apply scaleX and scaleY independently (distorts).
- Unit → pixels: **Pixel** = literal value; **Percent** = `value/100 × original` (DPI-independent); **Inch** = `value × DPI`; **Centimeter** = `value × DPI / 2.54`.
- Always resize **copies** in a disposable folder unless the item is specifically testing in-place replace.

---

## Image Resizer (18 items)

- [ ] **[ADMIN: NO]** (L309) In PowerToys Settings, **disable** Image Resizer. Right-click an image and confirm `Resize with Image Resizer` is **absent** from **both** the Win11 tier-1 (modern) menu **and** the classic "Show more options" (`#32768`) menu, while sibling PowerToys entries (e.g. `Rename with PowerRename`, `Unlock with File Locksmith`) **remain present** — proving the menu still renders and only the Image Resizer command was gated out (not a render failure).
- [ ] **[ADMIN: NO]** (L310) **Enable** Image Resizer. Right-click an image and confirm `Resize with Image Resizer` is **present, with its icon**, in **both** the Win11 tier-1 (modern) menu **and** the classic "Show more options" (`#32768`) menu. Invoking the entry launches `PowerToys.ImageResizer.exe` on the selection (window titled "Image Resizer").
- [ ] **[ADMIN: NO]** (L311) In Settings → Image Resizer → *Image sizes*, **remove** a built-in preset (e.g. `Phone`) and **add** a custom size (e.g. `Web` = 1024×768 px, Fit). Open the Image Resizer window from the context menu and open the size selector; confirm the removed preset (`Phone`) is **gone** and the added preset (`Web`) is **listed** — the presets round-trip from settings to the window.
- [ ] **[ADMIN: NO]** (L312) Select a **single** `1200×800` image and resize with the **Small** preset (854×480, Fit). Confirm exactly one output `<name> (Small).jpg` is produced at **720×480** (Fit scale = min(854/1200, 480/800) = 0.6 → 720×480) and the source file is untouched.
- [ ] **[ADMIN: NO]** (L313) Select **three** `1000×1000` images and resize them in **one** operation with the **Small** preset (854×480, Fit). Confirm all three outputs `<name> (Small).jpg` are produced, each at **480×480** (Fit = min(854/1000, 480/1000) = 0.48 → 480×480).
- [ ] **[ADMIN: NO]** (L314) Open Image Resizer on an **animated `.gif`** and confirm the yellow InfoBar warning **"Gif files with animations may not be correctly resized."** appears (shown whenever any selected file has a `.gif` extension).
- [ ] **[ADMIN: NO]** (L316) Resize a `1200×800` image to a **custom 400×400** target with the **Fill** mode. Confirm output is exactly **400×400**, produced by scaling with max(400/1200, 400/800) = 0.5 (→600×400) then **centered-cropping** to 400×400 (fills the box, no letterboxing, overflow cropped).
- [ ] **[ADMIN: NO]** (L317) Resize a `1200×800` image to a **custom 400×400** target with the **Fit** mode. Confirm output is **400×267** — aspect ratio preserved via min-scale (0.333), letterboxed inside the box, **not** cropped.
- [ ] **[ADMIN: NO]** (L318) Resize a `1200×800` image to a **custom 400×400** target with the **Stretch** mode. Confirm output is exactly **400×400** by applying scaleX=0.333 and scaleY=0.5 **independently** (image distorted). Contrast: same target gives 400×267 with Fit and 400×400-via-crop with Fill.
- [ ] **[ADMIN: NO]** (L320) Resize using unit **Centimeters**: on a `1200×800` image at **96 DPI**, custom `10cm × 5cm` with **Stretch**. Confirm output = **378×189** (cm→px = value × DPI / 2.54 → 10×96/2.54 ≈ 378, 5×96/2.54 ≈ 189). At another DPI the output scales with DPI (e.g. at 120 DPI → 472×236).
- [ ] **[ADMIN: NO]** (L321) Resize using unit **Inches**: on a `1200×800` image at **96 DPI**, custom `4in × 3in` with **Stretch**. Confirm output = **384×288** (inch→px = value × DPI → 4×96, 3×96). At 120 DPI → 480×360.
- [ ] **[ADMIN: NO]** (L322) Resize using unit **Percent**: on a `1200×800` image, width = **50%**. Confirm output = **600×400** (percent→px = value/100 × original, DPI-independent; height scales by the same 50%).
- [ ] **[ADMIN: NO]** (L323) Resize using unit **Pixels**: on a `1200×800` image, custom `500×300` with **Stretch**. Confirm output = exactly **500×300** (pixel target used literally).
- [ ] **[ADMIN: NO]** (L325) Set Filename format to `%1 - %2 - %3 - %4 - %5 - %6` and resize a `1200×800` image with the **Small** preset. Confirm the output filename is `<orig-name> - Small - 854 - 480 - 720 - 480.jpg`, where **%1**=original filename, **%2**=size/preset name, **%3**=selected width, **%4**=selected height, **%5**=actual output width, **%6**=actual output height.
- [ ] **[ADMIN: NO]** (L326) Check **Use original date modified**. Set a source image's last-modified time to a known past date (e.g. `2020-01-15 08:30`), resize it, and confirm the **output's** modified date **equals the source's** (`2020-01-15 08:30`). Control: with the option **unchecked**, the output's modified date is the **current** time instead.
- [ ] **[ADMIN: NO]** (L327) Check **Make pictures smaller but not larger**. Resize a **100×100** image with a target larger than it (e.g. Small 854×480) and confirm the output stays **100×100** (never enlarged). Control: a larger `1200×800` image with the same target is **still shrunk** (→720×480).
- [ ] **[ADMIN: NO]** (L328) Check **Resize the original pictures (don't create copies)**. Resize a `1200×800` image with the **Small** preset and confirm the original file is **overwritten in place** at **720×480** and **no** separate `(Small)` copy is created — the folder still holds exactly one file with the original name.
- [ ] **[ADMIN: NO]** (L329) **Uncheck** "Ignore the orientation of pictures" and resize a **landscape** `1200×800` image with a **portrait** target `100×200` (Fit, Pixel): confirm the target is applied **as-is** → **100×67** (no swap). Then **check** the option and repeat: the target is **swapped to 200×100** to match the source orientation → **150×100**.

