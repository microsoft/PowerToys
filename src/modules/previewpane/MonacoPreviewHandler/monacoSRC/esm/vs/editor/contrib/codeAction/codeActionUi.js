/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { onUnexpectedError } from '../../../base/common/errors.js';
import { Lazy } from '../../../base/common/lazy.js';
import { Disposable, MutableDisposable } from '../../../base/common/lifecycle.js';
import { MessageController } from '../message/messageController.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { CodeActionMenu } from './codeActionMenu.js';
import { LightBulbWidget } from './lightBulbWidget.js';
let CodeActionUi = class CodeActionUi extends Disposable {
    constructor(_editor, quickFixActionId, preferredFixActionId, delegate, instantiationService) {
        super();
        this._editor = _editor;
        this.delegate = delegate;
        this._activeCodeActions = this._register(new MutableDisposable());
        this._codeActionWidget = new Lazy(() => {
            return this._register(instantiationService.createInstance(CodeActionMenu, this._editor, {
                onSelectCodeAction: (action) => __awaiter(this, void 0, void 0, function* () {
                    this.delegate.applyCodeAction(action, /* retrigger */ true);
                })
            }));
        });
        this._lightBulbWidget = new Lazy(() => {
            const widget = this._register(instantiationService.createInstance(LightBulbWidget, this._editor, quickFixActionId, preferredFixActionId));
            this._register(widget.onClick(e => this.showCodeActionList(e.trigger, e.actions, e, { includeDisabledActions: false })));
            return widget;
        });
    }
    update(newState) {
        var _a, _b, _c;
        return __awaiter(this, void 0, void 0, function* () {
            if (newState.type !== 1 /* Triggered */) {
                (_a = this._lightBulbWidget.rawValue) === null || _a === void 0 ? void 0 : _a.hide();
                return;
            }
            let actions;
            try {
                actions = yield newState.actions;
            }
            catch (e) {
                onUnexpectedError(e);
                return;
            }
            this._lightBulbWidget.getValue().update(actions, newState.trigger, newState.position);
            if (newState.trigger.type === 2 /* Manual */) {
                if ((_b = newState.trigger.filter) === null || _b === void 0 ? void 0 : _b.include) { // Triggered for specific scope
                    // Check to see if we want to auto apply.
                    const validActionToApply = this.tryGetValidActionToApply(newState.trigger, actions);
                    if (validActionToApply) {
                        try {
                            yield this.delegate.applyCodeAction(validActionToApply, false);
                        }
                        finally {
                            actions.dispose();
                        }
                        return;
                    }
                    // Check to see if there is an action that we would have applied were it not invalid
                    if (newState.trigger.context) {
                        const invalidAction = this.getInvalidActionThatWouldHaveBeenApplied(newState.trigger, actions);
                        if (invalidAction && invalidAction.action.disabled) {
                            MessageController.get(this._editor).showMessage(invalidAction.action.disabled, newState.trigger.context.position);
                            actions.dispose();
                            return;
                        }
                    }
                }
                const includeDisabledActions = !!((_c = newState.trigger.filter) === null || _c === void 0 ? void 0 : _c.include);
                if (newState.trigger.context) {
                    if (!actions.allActions.length || !includeDisabledActions && !actions.validActions.length) {
                        MessageController.get(this._editor).showMessage(newState.trigger.context.notAvailableMessage, newState.trigger.context.position);
                        this._activeCodeActions.value = actions;
                        actions.dispose();
                        return;
                    }
                }
                this._activeCodeActions.value = actions;
                this._codeActionWidget.getValue().show(newState.trigger, actions, newState.position, { includeDisabledActions });
            }
            else {
                // auto magically triggered
                if (this._codeActionWidget.getValue().isVisible) {
                    // TODO: Figure out if we should update the showing menu?
                    actions.dispose();
                }
                else {
                    this._activeCodeActions.value = actions;
                }
            }
        });
    }
    getInvalidActionThatWouldHaveBeenApplied(trigger, actions) {
        if (!actions.allActions.length) {
            return undefined;
        }
        if ((trigger.autoApply === "first" /* First */ && actions.validActions.length === 0)
            || (trigger.autoApply === "ifSingle" /* IfSingle */ && actions.allActions.length === 1)) {
            return actions.allActions.find(({ action }) => action.disabled);
        }
        return undefined;
    }
    tryGetValidActionToApply(trigger, actions) {
        if (!actions.validActions.length) {
            return undefined;
        }
        if ((trigger.autoApply === "first" /* First */ && actions.validActions.length > 0)
            || (trigger.autoApply === "ifSingle" /* IfSingle */ && actions.validActions.length === 1)) {
            return actions.validActions[0];
        }
        return undefined;
    }
    showCodeActionList(trigger, actions, at, options) {
        return __awaiter(this, void 0, void 0, function* () {
            this._codeActionWidget.getValue().show(trigger, actions, at, options);
        });
    }
};
CodeActionUi = __decorate([
    __param(4, IInstantiationService)
], CodeActionUi);
export { CodeActionUi };
