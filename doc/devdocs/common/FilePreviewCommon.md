# [FilePreviewCommon](/src/common/FilePreviewCommon)

This project contains common code used for previewing and displaying files.

## Monaco preview

Monaco preview enables to display developer files. It is based on [Microsoft's Monaco Editor](https://microsoft.github.io/monaco-editor/) which is maintained by the Visual Studio Code team.

This previewer is used for the File Explorer Dev File Previewer, as well as PowerToys Peek.

For a general overview of how Monaco is used in PowerToys, see the [Monaco Editor documentation](monaco-editor.md).

### Update Monaco Editor

Monaco is built from the `monaco-editor` npm package pinned in [`src/Monaco/package.json`](/src/Monaco/package.json). See [How Monaco is built](monaco-editor.md#how-monaco-is-built).

1. In `src/Monaco`, run `npm install monaco-editor@<version> --save-exact`. Then check the lockfile's `resolved` URLs and `integrity` hashes as described in the [Monaco update process](monaco-editor.md#update-process).
2. Run `npm run build` and fix any errors in [`src/Monaco/src`](/src/Monaco/src). Check the [Monaco changelog](https://github.com/microsoft/monaco-editor/blob/main/CHANGELOG.md) for renamed or removed APIs and `features/*` entry points.
3. Update the language list as described in the [monaco_languages.json](#monaco_languagesjson) section.
4. Build PowerToys and check the Preview Pane, Peek, and Registry Preview.

### Add a new language definition

As an example on how to add a new language definition you can look at the one for [registry files](/src/Monaco/src/customLanguages/reg.js).

1. Add the new language definition (written with [Monarch](https://microsoft.github.io/monaco-editor/monarch.html)) as a new file in [`src/Monaco/src/customLanguages`](/src/Monaco/src/customLanguages/). The file should be formatted like in the example below. (Please change `idDefinition` to the name of your language.)

```javascript
export function idDefinition() {
    return {
        ...
    }
}
```

2. In [`src/Monaco/src/languages.js`](/src/Monaco/src/languages.js), import the definition:

```javascript
import { idDefinition } from './customLanguages/file.js';
```

> Replace file.js with the name of your definition file from step 1. Please replace idDefinition with the string you used in step 1.

3. In the same file, add an entry to the `customLanguages` list:

```javascript
['id', ['.fileExtension'], idDefinition()],
```

> Replace id and idDefinition with your id and the function from step 1. Replace fileExtension with the file extensions you want the language to register to.

  * The id can be anything. Recommended is one of the file extensions. For example "php" or "reg".

4. In case you wish to add a custom color for a token, you can do so by adding the following line to [`customTokenThemeRules.js`](/src/Monaco/src/customTokenThemeRules.js):
```javascript
{token: 'token-name', foreground: 'ff0000'}
```
> Replace `token-name` with the name of the token and `ff0000` with the hex code of the desired color.
> Note: you can also specify a `background` and a `fontStyle` attribute for your token.

* Keep in mind that these rules apply to all languages. Therefore, you should not change the colors of any default tokens. Instead, create new tokens specific to the language you are adding.

5. Execute the steps described in the [monaco_languages.json](#monaco_languagesjson) section.

### Add a new file extension to an existing language

1. In [`src/Monaco/src/languages.js`](/src/Monaco/src/languages.js), import the existing grammar from `monaco-editor` if it isn't imported yet. The grammars live in `node_modules/monaco-editor/esm/vs/languages/definitions/<language>`:

```javascript
import * as php from 'monaco-editor/languages/definitions/php/php';
```

2. Add an entry to the `aliasLanguages` list. Use `null` instead of a grammar to show the files as plain text.

```javascript
['phpExt', ['.fileExtension'], php],
```

  * If for instance you want to add more extensions to the php language set the id to `phpExt`. The ids of the existing languages are in [`monaco_languages.json`](/src/Monaco/monaco_languages.json).

3. Execute the steps described in the [monaco_languages.json](#monaco_languagesjson) section.

### monaco_languages.json

[`monaco_languages.json`](/src/Monaco/monaco_languages.json) contains all extensions and IDs for the languages supported by Monaco. The [`MonacoHelper`](/src/common/FilePreviewCommon/MonacoHelper.cs) class and the runner use this file to register preview handlers for the defined extensions.

After updating Monaco Editor and/or adding a new language, regenerate it:

1. In `src/Monaco`, run `npm run gen:languages`. It runs the registrations in [`src/Monaco/src/languages.js`](/src/Monaco/src/languages.js) in Node (no browser needed) and writes the language list.
2. Review the diff and commit the file. CI runs the same tool with `--check` (`npm run check:languages`) and fails if the file is out of date.
