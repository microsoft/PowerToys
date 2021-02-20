/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Token, TokenizationResult, TokenizationResult2 } from '../core/token.js';
import { LanguageIdentifier } from '../modes.js';
class NullStateImpl {
    clone() {
        return this;
    }
    equals(other) {
        return (this === other);
    }
}
export const NULL_STATE = new NullStateImpl();
export const NULL_MODE_ID = 'vs.editor.nullMode';
export const NULL_LANGUAGE_IDENTIFIER = new LanguageIdentifier(NULL_MODE_ID, 0 /* Null */);
export function nullTokenize(modeId, buffer, state, deltaOffset) {
    return new TokenizationResult([new Token(deltaOffset, '', modeId)], state);
}
export function nullTokenize2(languageId, buffer, state, deltaOffset) {
    let tokens = new Uint32Array(2);
    tokens[0] = deltaOffset;
    tokens[1] = ((languageId << 0 /* LANGUAGEID_OFFSET */)
        | (0 /* Other */ << 8 /* TOKEN_TYPE_OFFSET */)
        | (0 /* None */ << 11 /* FONT_STYLE_OFFSET */)
        | (1 /* DefaultForeground */ << 14 /* FOREGROUND_OFFSET */)
        | (2 /* DefaultBackground */ << 23 /* BACKGROUND_OFFSET */)) >>> 0;
    return new TokenizationResult2(tokens, state === null ? NULL_STATE : state);
}
