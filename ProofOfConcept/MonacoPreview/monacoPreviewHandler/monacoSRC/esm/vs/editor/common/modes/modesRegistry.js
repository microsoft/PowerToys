/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../../nls.js';
import { Emitter } from '../../../base/common/event.js';
import { LanguageIdentifier } from '../modes.js';
import { LanguageConfigurationRegistry } from './languageConfigurationRegistry.js';
import { Registry } from '../../../platform/registry/common/platform.js';
// Define extension point ids
export const Extensions = {
    ModesRegistry: 'editor.modesRegistry'
};
export class EditorModesRegistry {
    constructor() {
        this._onDidChangeLanguages = new Emitter();
        this.onDidChangeLanguages = this._onDidChangeLanguages.event;
        this._languages = [];
        this._dynamicLanguages = [];
    }
    // --- languages
    registerLanguage(def) {
        this._languages.push(def);
        this._onDidChangeLanguages.fire(undefined);
        return {
            dispose: () => {
                for (let i = 0, len = this._languages.length; i < len; i++) {
                    if (this._languages[i] === def) {
                        this._languages.splice(i, 1);
                        return;
                    }
                }
            }
        };
    }
    getLanguages() {
        return [].concat(this._languages).concat(this._dynamicLanguages);
    }
}
export const ModesRegistry = new EditorModesRegistry();
Registry.add(Extensions.ModesRegistry, ModesRegistry);
export const PLAINTEXT_MODE_ID = 'plaintext';
export const PLAINTEXT_LANGUAGE_IDENTIFIER = new LanguageIdentifier(PLAINTEXT_MODE_ID, 1 /* PlainText */);
ModesRegistry.registerLanguage({
    id: PLAINTEXT_MODE_ID,
    extensions: ['.txt'],
    aliases: [nls.localize('plainText.alias', "Plain Text"), 'text'],
    mimetypes: ['text/plain']
});
LanguageConfigurationRegistry.register(PLAINTEXT_LANGUAGE_IDENTIFIER, {
    brackets: [
        ['(', ')'],
        ['[', ']'],
        ['{', '}'],
    ],
    surroundingPairs: [
        { open: '{', close: '}' },
        { open: '[', close: ']' },
        { open: '(', close: ')' },
        { open: '<', close: '>' },
        { open: '\"', close: '\"' },
        { open: '\'', close: '\'' },
        { open: '`', close: '`' },
    ],
    folding: {
        offSide: true
    }
});
