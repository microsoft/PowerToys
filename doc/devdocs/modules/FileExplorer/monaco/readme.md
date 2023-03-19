# Developer Preview (Monaco)

Developer preview is based on [Microsoft's Monaco Editor](https://microsoft.github.io/monaco-editor/) which is maintained by the Visual Studio Code team.

## Update Monaco Editor

1. Download Monaco editor with [npm](https://www.npmjs.com/): `npm i monaco-editor`.
2. Delete everything except the `min` folder (the minimised code) from the downloaded files.
3. Copy the `min` folder into the [`monacoSRC`](/src/modules/previewpane/MonacoPreviewHandler/monacoSRC) folder.
4. Generate the JSON file as described in the [Generate monaco_languages.json file](#generate-monaco_languagesjson-file) section.

## Add a new language definition

1. Add the language definition (Written with [Monarch](https://microsoft.github.io/monaco-editor/monarch.html)) to the [`monacoSRC`](/src/modules/previewpane/MonacoPreviewHandler/customLanguages/) folder. The file should be formatted like in the example below. (Please change `idDefinition` to the name of your language.)

```javascript
export function idDefinition() {
    return {
        ...
    }
}
```

2. Add the following line to the [`monacoSpecialLanguages.js` file](/src/modules/previewpane/MonacoPreviewHandler/monacoSpecialLanguages.js):

```javascript
import { idDefinition } from './customLanguages/file.js';
```

3. In the [`monacoSpecialLanguages.js` file](/src/modules/previewpane/MonacoPreviewHandler/monacoSpecialLanguages.js) add the following line to the `registerAdditionalLanguages` function:

```javascript
registerAdditionalNewLanguage("id", [".fileExtension"], idDefinition(), monaco)
```

  * The id can be anything. Recommended is one of the file extensions. For example "php" or "reg".

4. Execute the steps described in the [Generate monaco_languages.json file](#generate-monaco_languagesjson-file) section.

## Add a new file extension to an existing language

1. In the [`monacoSpecialLanguages.js` file](/src/modules/previewpane/MonacoPreviewHandler/monacoSpecialLanguages.js) add the following line to the `registerAdditionalLanguages` function:

```javascript
registerAdditionalLanguage("id", [".fileExtension"], "oldId", monaco)
```

  * If for instance you want to add more extensions to the php language set the id to `phpExt` and the oldId to `php`.

2. Execute the steps described in the [Generate monaco_languages.json file](#generate-monaco_languagesjson-file) section.

## monaco_languages.json

[`monaco_languages.json`](/src/modules/previewpane/MonacoPreviewHandler/monaco_languages.json) contains all extensions and Id's for the supported languages of Monaco. The [`FileHandler`](/src/modules/previewpane/MonacoPreviewHandler/FileHandler.cs) class and the installer are using this file.

### Generate monaco_languages.json file

After you updated monaco editor or adding a new language you should update the [`monaco_languages.json`](/src/modules/previewpane/MonacoPreviewHandler/monaco_languages.json) file.

You have to run the file on a local webserver!

1. Build monaco in debug mode.
2. Open [generateLanguagesJson.html](/src/modules/previewpane/MonacoPreviewHandler/generateLanguagesJson.html) in a browser over the webserver.
3. Replace the old JSON file.
