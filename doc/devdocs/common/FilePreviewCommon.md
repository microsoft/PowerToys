# [FilePreviewCommon](/src/common/FilePreviewCommon)

This project contains common code used for previewing and displaying files.

## Monaco preview

Monaco preview enables to display developer files. It is based on [Microsoft's Monaco Editor](https://microsoft.github.io/monaco-editor/) which is maintained by the Visual Studio Code team.

This previewer is used for the File Explorer Dev File Previewer, aswell as PowerToys Peek.

### Update Monaco Editor

1. Download Monaco editor with [npm](https://www.npmjs.com/): Enter `npm i monaco-editor` in the command prompt.
2. Delete everything except the `min` folder (the minimised code) from the downloaded files.
3. Copy the `min` folder into the `src/common/FilePreviewCommon/monacoSRC` folder of the PowerToys project.
4. Generate the JSON file as described in the [Generate monaco_languages.json file](#monaco_languagesjson) section.

### Add a new language definition

As an example on how to add a new language definition you can look at the one for [registry files](/src/common/FilePreviewCommon/customLanguages/reg.js).

1. Add the language definition (Written with [Monarch](https://microsoft.github.io/monaco-editor/monarch.html)) to the [`monacoSRC`](/src/common/FilePreviewCommon/customLanguages/) folder. The file should be formatted like in the example below. (Please change `idDefinition` to the name of your language.)

```javascript
export function idDefinition() {
    return {
        ...
    }
}
```

2. Add the following line to the [`monacoSpecialLanguages.js`](/src/common/FilePreviewCommon/monacoSpecialLanguages.js) file:

```javascript
import { idDefinition } from './customLanguages/file.js';
```

3. In the [`monacoSpecialLanguages.js`](/src/modules/previewpane/MonacoPreviewHandler/monacoSpecialLanguages.js) file add the following line to the `registerAdditionalLanguages` function:

```javascript
registerAdditionalNewLanguage("id", [".fileExtension"], idDefinition(), monaco)
```

  * The id can be anything. Recommended is one of the file extensions. For example "php" or "reg".
4. Copy the existing language definition into the `languageDefinitions` function in the same file. You can find the existing definitions in the following folder: [`/src/common/FilePreviewCommon/monacoSRC/min/vs/basic-languages`](/src/common/FilePreviewCommon/monacoSRC/min/vs/basic-languages).

5. Execute the steps described in the [Generate monaco_languages.json file](#monaco_languagesjson) section.

### Add a new file extension to an existing language

1. In the [`monacoSpecialLanguages.js`](/src/common/FilePreviewCommon/monacoSpecialLanguages.js) file add the following line to the `registerAdditionalLanguages` function (`existingId` is the id of the language you want to add the extension to. You can find these id's in the `monaco_languages.json` file)):

```javascript
registerAdditionalLanguage("id", [".fileExtension"], "existingId", monaco)
```

  * If for instance you want to add more extensions to the php language set the id to `phpExt` and the existingId to `php`.

2. Execute the steps described in the [Generate monaco_languages.json file](#monaco_languagesjson) section.

### monaco_languages.json

[`monaco_languages.json`](/src/common/FilePreviewCommon/monaco_languages.json) contains all extensions and Id's for the languages supported by Monaco. The [`MonacoHelper`](/src/common/FilePreviewCommon/MonacoHelper.cs) class and the installer are using this file to register preview handlers for defined extensions.

After updating Monaco Editor and/or adding a new language you should update the [`monaco_languages.json`](/src/common/FilePreviewCommon/monaco_languages.json) file.

1. Run the file on a local webserver (as webbrowsers will block certain needed features when running the file locally.)
  *  This can for example be achieved by using the [Preview Server](https://marketplace.visualstudio.com/items?itemName=yuichinukiyama.vscode-preview-server) extension for Visual Studio Code: Open the file in Visual Studio Code right click in the code editor and select `vscode-preview-server: Launch on browser`. The file will then be opened in a browser.
2. The browser will download the new `monaco_languages.json` file
3. Replace the old file with the newly downloaded one in the source code folder.
