# Monaco Editor in PowerToys

## Overview

Monaco is the text editor that powers Visual Studio Code. In PowerToys, Monaco is integrated as a component to provide advanced text editing capabilities with features like syntax highlighting, line numbering, and intelligent code editing.

## Where Monaco is Used in PowerToys

Monaco is primarily used in:
- Registry Preview module - For editing registry files
- File Preview handlers - For syntax highlighting when previewing code files
- Peek module - For preview a file

## Technical Implementation

Monaco is embedded into PowerToys' WinUI 3 applications using WebView2. This integration allows PowerToys to leverage Monaco's web-based capabilities within desktop applications.

### How Monaco is built

PowerToys doesn't check in Monaco's source. It builds a bundle from a pinned npm package:

- [`src/Monaco/package.json`](/src/Monaco/package.json) pins `monaco-editor` (exact version) and `esbuild`. [`package-lock.json`](/src/Monaco/package-lock.json) holds the integrity hashes.
- [`src/Monaco/src`](/src/Monaco/src) is the PowerToys layer on top of the pinned package:
  - [`main.js`](/src/Monaco/src/main.js): the bundle entry point. It sets the `monaco` global and creates the web workers.
  - [`features.js`](/src/Monaco/src/features.js): the editor features PowerToys enables. Refactoring and language-service features are left out, which also keeps their entries out of the context menu.
  - [`languages.js`](/src/Monaco/src/languages.js): extra file extensions for existing languages, and the PowerToys languages in [`customLanguages`](/src/Monaco/src/customLanguages).
  - [`customTokenThemeRules.js`](/src/Monaco/src/customTokenThemeRules.js): token colors for the PowerToys languages.
- The layer only uses public `monaco-editor` entry points. Never edit `node_modules` or copy Monaco source into the repo.
- [`src/Monaco.props`](/src/Monaco.props) runs [`build.mjs`](/src/Monaco/build.mjs) while building `FilePreviewCommon` and `RegistryPreviewUILib`. It runs `npm ci` when the lockfile changes, bundles into `src/Monaco/dist` (git-ignored), and copies the result to `Assets\Monaco`. It's a no-op when nothing changed.
- Building requires **Node.js 20 or later** on `PATH` (`winget install OpenJS.NodeJS.LTS`, or `tools\build\setup-dev-environment.ps1`). To use a different `node.exe`, set the `PowerToysNodeExe` MSBuild property.
- Official builds resolve npm packages through the PowerToys Azure Artifacts feed ([`steps-setup-monaco-npm.yml`](/.pipelines/v2/templates/steps-setup-monaco-npm.yml)).

### Directory Structure

`Assets\Monaco` in the install folder contains:

| File | Purpose |
|------|---------|
| `index.html` | Host page for the Preview Pane and Peek. The `[[PT_*]]` placeholders are filled in by `MonacoPreviewHandlerControl` and Peek's `MonacoHelper`. |
| `monaco.js`, `monaco.css`, `codicon.ttf` | The editor bundle. |
| `editor.worker.js`, `json.worker.js` | Web workers (link detection, JSON validation). |
| `monaco_languages.json` | Languages and file extensions Monaco handles. |

Registry Preview has its own host page in `RegistryPreviewUILib/Assets/RegistryPreview/index.html`. Every host loads the bundle from the `PowerToysLocalMonaco` WebView2 virtual host, which maps to `Assets\Monaco`.

## Versioning and Updates

### Current Version

The Monaco version is the `monaco-editor` entry in [`src/Monaco/package.json`](/src/Monaco/package.json).

### Update Process

1. In `src/Monaco`, run `npm install monaco-editor@<version> --save-exact`. This updates `package.json` and `package-lock.json`.
2. Run `npm run gen:languages` to regenerate `monaco_languages.json`, and review its diff. CI fails if the file doesn't match the bundle.
3. Build, then smoke-test the Preview Pane, Peek, and Registry Preview. Check the [Monaco changelog](https://github.com/microsoft/monaco-editor/blob/main/CHANGELOG.md) for breaking changes.

For more detail, see the [FilePreviewCommon documentation](FilePreviewCommon.md#update-monaco-editor).

## Customizing Monaco

### Adding New Language Definitions

Add a [Monarch](https://microsoft.github.io/monaco-editor/monarch.html) definition under `src/Monaco/src/customLanguages` and register it in `src/Monaco/src/languages.js`. For step-by-step instructions, see the [FilePreviewCommon documentation](FilePreviewCommon.md#add-a-new-language-definition).

### Adding File Extensions to Existing Languages

Add an entry to `aliasLanguages` in `src/Monaco/src/languages.js` that maps the new extensions onto an existing Monaco grammar. For step-by-step instructions, see the [FilePreviewCommon documentation](FilePreviewCommon.md#add-a-new-file-extension-to-an-existing-language).

Example: If Monaco processes TXT files and you want it to preview LOG files the same way, you can add LOG extensions to the TXT language definition.

## Installer Handling

The bundle has a fixed set of file names, so `installer/PowerToysSetupVNext/generateAllFileComponents.ps1` lists them by name in its inclusion list and harvests `Assets\Monaco` like any other assets folder. If a Monaco update or a `build.mjs` change adds or renames an output file, update that list.
