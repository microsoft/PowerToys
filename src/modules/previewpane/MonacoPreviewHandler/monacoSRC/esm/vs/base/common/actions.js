/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import * as nls from '../../nls.js';
import { Disposable } from './lifecycle.js';
import { Emitter } from './event.js';
export class Action extends Disposable {
    constructor(id, label = '', cssClass = '', enabled = true, actionCallback) {
        super();
        this._onDidChange = this._register(new Emitter());
        this.onDidChange = this._onDidChange.event;
        this._enabled = true;
        this._checked = false;
        this._id = id;
        this._label = label;
        this._cssClass = cssClass;
        this._enabled = enabled;
        this._actionCallback = actionCallback;
    }
    get id() {
        return this._id;
    }
    get label() {
        return this._label;
    }
    set label(value) {
        this._setLabel(value);
    }
    _setLabel(value) {
        if (this._label !== value) {
            this._label = value;
            this._onDidChange.fire({ label: value });
        }
    }
    get tooltip() {
        return this._tooltip || '';
    }
    set tooltip(value) {
        this._setTooltip(value);
    }
    _setTooltip(value) {
        if (this._tooltip !== value) {
            this._tooltip = value;
            this._onDidChange.fire({ tooltip: value });
        }
    }
    get class() {
        return this._cssClass;
    }
    set class(value) {
        this._setClass(value);
    }
    _setClass(value) {
        if (this._cssClass !== value) {
            this._cssClass = value;
            this._onDidChange.fire({ class: value });
        }
    }
    get enabled() {
        return this._enabled;
    }
    set enabled(value) {
        this._setEnabled(value);
    }
    _setEnabled(value) {
        if (this._enabled !== value) {
            this._enabled = value;
            this._onDidChange.fire({ enabled: value });
        }
    }
    get checked() {
        return this._checked;
    }
    set checked(value) {
        this._setChecked(value);
    }
    _setChecked(value) {
        if (this._checked !== value) {
            this._checked = value;
            this._onDidChange.fire({ checked: value });
        }
    }
    run(event, _data) {
        if (this._actionCallback) {
            return this._actionCallback(event);
        }
        return Promise.resolve(true);
    }
}
export class ActionRunner extends Disposable {
    constructor() {
        super(...arguments);
        this._onBeforeRun = this._register(new Emitter());
        this.onBeforeRun = this._onBeforeRun.event;
        this._onDidRun = this._register(new Emitter());
        this.onDidRun = this._onDidRun.event;
    }
    run(action, context) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!action.enabled) {
                return Promise.resolve(null);
            }
            this._onBeforeRun.fire({ action: action });
            try {
                const result = yield this.runAction(action, context);
                this._onDidRun.fire({ action: action, result: result });
            }
            catch (error) {
                this._onDidRun.fire({ action: action, error: error });
            }
        });
    }
    runAction(action, context) {
        const res = context ? action.run(context) : action.run();
        return Promise.resolve(res);
    }
}
export class Separator extends Action {
    constructor(label) {
        super(Separator.ID, label, label ? 'separator text' : 'separator');
        this.checked = false;
        this.enabled = false;
    }
}
Separator.ID = 'vs.actions.separator';
export class SubmenuAction {
    constructor(id, label, actions, cssClass) {
        this.tooltip = '';
        this.enabled = true;
        this.checked = false;
        this.id = id;
        this.label = label;
        this.class = cssClass;
        this._actions = actions;
    }
    dispose() {
        // there is NOTHING to dispose and the SubmenuAction should
        // never have anything to dispose as it is a convenience type
        // to bridge into the rendering world.
    }
    get actions() {
        return this._actions;
    }
    run() {
        return __awaiter(this, void 0, void 0, function* () { });
    }
}
export class EmptySubmenuAction extends Action {
    constructor() {
        super(EmptySubmenuAction.ID, nls.localize('submenu.empty', '(empty)'), undefined, false);
    }
}
EmptySubmenuAction.ID = 'vs.actions.empty';
