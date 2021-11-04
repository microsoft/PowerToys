/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createDecorator } from '../../instantiation/common/instantiation.js';
import { toDisposable, Disposable } from '../../../base/common/lifecycle.js';
import * as platform from '../../registry/common/platform.js';
import { Emitter } from '../../../base/common/event.js';
import { ColorScheme } from './theme.js';
import { CSSIcon } from '../../../base/common/codicons.js';
export const IThemeService = createDecorator('themeService');
export var ThemeColor;
(function (ThemeColor) {
    function isThemeColor(obj) {
        return obj && typeof obj === 'object' && typeof obj.id === 'string';
    }
    ThemeColor.isThemeColor = isThemeColor;
})(ThemeColor || (ThemeColor = {}));
export function themeColorFromId(id) {
    return { id };
}
export var ThemeIcon;
(function (ThemeIcon) {
    function isThemeIcon(obj) {
        return obj && typeof obj === 'object' && typeof obj.id === 'string' && (typeof obj.color === 'undefined' || ThemeColor.isThemeColor(obj.color));
    }
    ThemeIcon.isThemeIcon = isThemeIcon;
    const _regexFromString = /^\$\(([a-z.]+\/)?([a-z-~]+)\)$/i;
    function fromString(str) {
        const match = _regexFromString.exec(str);
        if (!match) {
            return undefined;
        }
        let [, owner, name] = match;
        if (!owner || owner === 'codicon/') {
            return { id: name };
        }
        return { id: owner + name };
    }
    ThemeIcon.fromString = fromString;
    function modify(icon, modifier) {
        let id = icon.id;
        const tildeIndex = id.lastIndexOf('~');
        if (tildeIndex !== -1) {
            id = id.substring(0, tildeIndex);
        }
        if (modifier) {
            id = `${id}~${modifier}`;
        }
        return { id };
    }
    ThemeIcon.modify = modify;
    function isEqual(ti1, ti2) {
        var _a, _b;
        return ti1.id === ti2.id && ((_a = ti1.color) === null || _a === void 0 ? void 0 : _a.id) === ((_b = ti2.color) === null || _b === void 0 ? void 0 : _b.id);
    }
    ThemeIcon.isEqual = isEqual;
    ThemeIcon.asClassNameArray = CSSIcon.asClassNameArray;
    ThemeIcon.asClassName = CSSIcon.asClassName;
    ThemeIcon.asCSSSelector = CSSIcon.asCSSSelector;
})(ThemeIcon || (ThemeIcon = {}));
export function getThemeTypeSelector(type) {
    switch (type) {
        case ColorScheme.DARK: return 'vs-dark';
        case ColorScheme.HIGH_CONTRAST: return 'hc-black';
        default: return 'vs';
    }
}
// static theming participant
export const Extensions = {
    ThemingContribution: 'base.contributions.theming'
};
class ThemingRegistry {
    constructor() {
        this.themingParticipants = [];
        this.themingParticipants = [];
        this.onThemingParticipantAddedEmitter = new Emitter();
    }
    onColorThemeChange(participant) {
        this.themingParticipants.push(participant);
        this.onThemingParticipantAddedEmitter.fire(participant);
        return toDisposable(() => {
            const idx = this.themingParticipants.indexOf(participant);
            this.themingParticipants.splice(idx, 1);
        });
    }
    getThemingParticipants() {
        return this.themingParticipants;
    }
}
let themingRegistry = new ThemingRegistry();
platform.Registry.add(Extensions.ThemingContribution, themingRegistry);
export function registerThemingParticipant(participant) {
    return themingRegistry.onColorThemeChange(participant);
}
/**
 * Utility base class for all themable components.
 */
export class Themable extends Disposable {
    constructor(themeService) {
        super();
        this.themeService = themeService;
        this.theme = themeService.getColorTheme();
        // Hook up to theme changes
        this._register(this.themeService.onDidColorThemeChange(theme => this.onThemeChange(theme)));
    }
    onThemeChange(theme) {
        this.theme = theme;
        this.updateStyles();
    }
    updateStyles() {
        // Subclasses to override
    }
}
