/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { CharacterClassifier } from '../core/characterClassifier.js';
export class WordCharacterClassifier extends CharacterClassifier {
    constructor(wordSeparators) {
        super(0 /* Regular */);
        for (let i = 0, len = wordSeparators.length; i < len; i++) {
            this.set(wordSeparators.charCodeAt(i), 2 /* WordSeparator */);
        }
        this.set(32 /* Space */, 1 /* Whitespace */);
        this.set(9 /* Tab */, 1 /* Whitespace */);
    }
}
function once(computeFn) {
    let cache = {}; // TODO@Alex unbounded cache
    return (input) => {
        if (!cache.hasOwnProperty(input)) {
            cache[input] = computeFn(input);
        }
        return cache[input];
    };
}
export const getMapForWordSeparators = once((input) => new WordCharacterClassifier(input));
