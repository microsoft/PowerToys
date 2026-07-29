# Sample JS/TS Command Palette Extension

A JavaScript/TypeScript parity sample for the PowerToys Command Palette (CmdPal).
It mirrors the built-in C# `SamplePagesExtension` page for page using the
TypeScript SDK, `@microsoft/cmdpal-sdk`. Use it as a reference for building your
own JS/TS extension and for manually validating the JS/TS extension host.

JS/TS extensions run as an isolated Node.js process that talks to CmdPal over
JSON-RPC 2.0 on stdio. See the spec under
`src/modules/cmdpal/doc/json-rpc-spec/` for the full architecture.

## What it demonstrates

Each sample mirrors the matching C# page. Titles, subtitles, section names,
tags, and command behavior match the C# sample as closely as the JS SDK allows:

- List page with tags, links, a nested (multi-level) context menu, confirmation
  dialogs, and status messages.
- Toast notification samples (`showToast` results, including a custom message).
- List page with details (markdown body, tags, links, a local hero image, and
  command metadata).
- Live updating details.
- List pages with sections (list and grid variants).
- List page with items that change on a timer.
- Dynamic list page that rebuilds items from the query, with filters.
- Grid and gallery layouts.
- OnLoad demo.
- Icon page covering many icon-string forms.
- Slow loading list page.
- Prefix suggestions (`@` people, `/` commands).
- Content pages: mixed markdown plus form, plain text, image, and nested tree.
- Nested comments built from tree plus form content.
- Markdown pages: single block, many blocks, with details, and with images.
- Settings page built with the settings helpers.
- Clipboard demo.

### Capabilities intentionally not mirrored

Some C# capabilities are not yet exposed by the JS SDK or the JSON-RPC protocol.
They are omitted here, or approximated with a clear code comment, rather than
inventing protocol methods:

- Dock bands (`SampleDockBand`, `SampleButtonsDockBand`). No protocol surface.
- Parameter pages (`SimpleParameterTest`, `ButtonParameterTest`,
  `MixedParamTestPage`) and the create-note list-parameter page. No parameter
  run protocol.
- Drag and drop via `DataPackage`. `IListItem` has no `DataPackage`, so the
  clipboard demo copies to the clipboard instead.
- Toast icon and toast action button (`IToastArgs2`). `ToastArgs` carries a
  message and an optional follow-up result only.
- Details size (Small/Medium/Large). The JS `Details` type has no size, so the
  variants collapse to the default.
- Live-updating details through targeted property change. Approximated with a
  dynamic page that refreshes items on a timer.
- Win32 foreground-window and other in-process host tricks.
- Evil samples and issue-specific host-ABI repros.

## Build

The sample depends on the local SDK through a `file:` dependency, so the SDK
must be built first.

1. Build the SDK once:

   ```powershell
   cd src\modules\cmdpal\ts-sdk
   npm ci
   npm run build
   ```

2. Build the sample:

   ```powershell
   cd src\modules\cmdpal\ext\SampleJSExtension
   npm ci
   npm run build
   ```

`npm run build` compiles `src\*.ts` to `dist\` and copies the `assets\` folder
(which holds the details hero image) to `dist\assets\`. Only source is
committed; `dist\` and `node_modules\` are git-ignored.

## Sideload for manual validation

CmdPal discovers JS/TS extensions under:

```
%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\JSExtensions\<name>\
```

A discovered extension folder must contain `package.json` (with the `cmdpal`
section and a `main` that resolves to a built file), the compiled `dist\`, and
its `node_modules\`. To sideload this sample after building it:

```powershell
$dest = "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\JSExtensions\SampleJSExtension"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item package.json, dist, node_modules -Destination $dest -Recurse -Force
```

Then open Command Palette. The provider appears as "Sample Pages Commands (JS)"
with a top-level "Sample Pages (JS)" command that opens the samples index.

To remove it, delete the `SampleJSExtension` folder from `JSExtensions` and
reload.
