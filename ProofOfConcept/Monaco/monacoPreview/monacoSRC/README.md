# Monaco Editor

[![Build Status](https://dev.azure.com/ms/monaco-editor/_apis/build/status/microsoft.monaco-editor?label=website)](https://dev.azure.com/ms/monaco-editor/_build/latest?definitionId=3)

The Monaco Editor is the code editor which powers [VS Code](https://github.com/Microsoft/vscode), with the features better described [here](https://code.visualstudio.com/docs/editor/editingevolved).

Please note that this repository contains no source code for the code editor, it only contains the scripts to package everything together and ship the `monaco-editor` npm module.

![image](https://user-images.githubusercontent.com/5047891/94183711-290c0780-fea3-11ea-90e3-c88ff9d21bd6.png)

## Try it out

Try the editor out [on our website](https://microsoft.github.io/monaco-editor/index.html).

## Installing

```
$ npm install monaco-editor
```

You will get:
* inside `esm`: ESM version of the editor (compatible with e.g. webpack)
* inside `dev`: AMD bundled, not minified
* inside `min`: AMD bundled, and minified
* inside `min-maps`: source maps for `min`
* `monaco.d.ts`: this specifies the API of the editor (this is what is actually versioned, everything else is considered private and might break with any release).

It is recommended to develop against the `dev` version, and in production to use the `min` version.

## Documentation

* Learn how to integrate the editor with these [complete samples](https://github.com/Microsoft/monaco-editor-samples/).
    * [Integrate the AMD version](./docs/integrate-amd.md).
    * [Integrate the AMD version cross-domain](./docs/integrate-amd-cross.md)
    * [Integrate the ESM version](./docs/integrate-esm.md)
* Learn how to use the editor API and try out your own customizations in the [playground](https://microsoft.github.io/monaco-editor/playground.html).
* Explore the [API docs](https://microsoft.github.io/monaco-editor/api/index.html) or read them straight from [`monaco.d.ts`](https://github.com/Microsoft/monaco-editor/blob/master/website/playground/monaco.d.ts.txt).
* Read [this guide](https://github.com/Microsoft/monaco-editor/wiki/Accessibility-Guide-for-Integrators) to ensure the editor is accessible to all your users!
* Create a Monarch tokenizer for a new programming language [in the Monarch playground](https://microsoft.github.io/monaco-editor/monarch.html).
* Ask questions on [StackOverflow](https://stackoverflow.com/questions/tagged/monaco-editor)! Search open and closed issues, there are a lot of tips in there!

## Issues

Create [issues](https://github.com/Microsoft/monaco-editor/issues) in this repository for anything related to the Monaco Editor. Always mention **the version** of the editor when creating issues and **the browser** you're having trouble in. Please search for existing issues to avoid duplicates.

## FAQ

❓ **What is the relationship between VS Code and the Monaco Editor?**

The Monaco Editor is generated straight from VS Code's sources with some shims around services the code needs to make it run in a web browser outside of its home.

❓ **What is the relationship between VS Code's version and the Monaco Editor's version?**

None. The Monaco Editor is a library and it reflects directly the source code.

❓ **I've written an extension for VS Code, will it work on the Monaco Editor in a browser?**

No.

> Note: If the extension is fully based on the [LSP](https://microsoft.github.io/language-server-protocol/) and if the language server is authored in JavaScript, then it would be possible.

❓ **Why all these web workers and why should I care?**

Language services create web workers to compute heavy stuff outside of the UI thread. They cost hardly anything in terms of resource overhead and you shouldn't worry too much about them, as long as you get them to work (see above the cross-domain case).

❓ **What is this `loader.js`? Can I use `require.js`?**

It is an AMD loader that we use in VS Code. Yes.

❓ **I see the warning "Could not create web worker". What should I do?**

HTML5 does not allow pages loaded on `file://` to create web workers. Please load the editor with a web server on `http://` or `https://` schemes. Please also see the cross-domain case above.

❓ **Is the editor supported in mobile browsers or mobile web app frameworks?**

No.

❓ **Why doesn't the editor support TextMate grammars?**

* Please see https://github.com/bolinfest/monaco-tm which puts together `monaco-editor`, `vscode-oniguruma` and `vscode-textmate` to get TM grammar support in the editor.

❓ **What about IE 11 support?**

* The Monaco Editor no longer supports IE 11. The last version that was tested on IE 11 is `0.18.1`.

## Development setup

Please see [CONTRIBUTING](./CONTRIBUTING.md)

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


## License
Licensed under the [MIT](https://github.com/Microsoft/monaco-editor/blob/master/LICENSE.md) License.
