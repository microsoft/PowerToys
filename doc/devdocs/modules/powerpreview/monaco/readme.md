# Developer Preview (Monaco)
## Update monaco editor

1. Download Monaco editor with npm: `npm i monaco-editor`.
2. Delete everything except the `min` folder (the minimised code).
3. Copy the `min` folder inside the [`monacoSRC`](https://github.com/microsoft/PowerToys/tree/main/src/modules/previewpane/MonacoPreviewHandler/monacoSRC) folder.
4. Generate the JSON file (see section below)

## Generate JSON file

After you updated monaco editor or adding a new language you should update the [`languages.json`](https://github.com/microsoft/PowerToys/blob/main/src/modules/previewpane/MonacoPreviewHandler/languages.json) file.

1. Build monaco in debug mode.
2. Open [generateLanguagesJson.html](https://github.com/microsoft/PowerToys/blob/feature/monacoPreviewPane/src/modules/previewpane/MonacoPreviewHandler/generateLanguagesJson.html) in a browser.
3. Replace the JSON file.
