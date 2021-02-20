/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { KeyCodeUtils } from '../../../base/common/keyCodes.js';
import { BaseResolvedKeybinding } from './baseResolvedKeybinding.js';
/**
 * Do not instantiate. Use KeybindingService to get a ResolvedKeybinding seeded with information about the current kb layout.
 */
export class USLayoutResolvedKeybinding extends BaseResolvedKeybinding {
    constructor(actual, os) {
        super(os, actual.parts);
    }
    _keyCodeToUILabel(keyCode) {
        if (this._os === 2 /* Macintosh */) {
            switch (keyCode) {
                case 15 /* LeftArrow */:
                    return '←';
                case 16 /* UpArrow */:
                    return '↑';
                case 17 /* RightArrow */:
                    return '→';
                case 18 /* DownArrow */:
                    return '↓';
            }
        }
        return KeyCodeUtils.toString(keyCode);
    }
    _getLabel(keybinding) {
        if (keybinding.isDuplicateModifierCase()) {
            return '';
        }
        return this._keyCodeToUILabel(keybinding.keyCode);
    }
    _getAriaLabel(keybinding) {
        if (keybinding.isDuplicateModifierCase()) {
            return '';
        }
        return KeyCodeUtils.toString(keybinding.keyCode);
    }
    _getDispatchPart(keybinding) {
        return USLayoutResolvedKeybinding.getDispatchStr(keybinding);
    }
    static getDispatchStr(keybinding) {
        if (keybinding.isModifierKey()) {
            return null;
        }
        let result = '';
        if (keybinding.ctrlKey) {
            result += 'ctrl+';
        }
        if (keybinding.shiftKey) {
            result += 'shift+';
        }
        if (keybinding.altKey) {
            result += 'alt+';
        }
        if (keybinding.metaKey) {
            result += 'meta+';
        }
        result += KeyCodeUtils.toString(keybinding.keyCode);
        return result;
    }
}
