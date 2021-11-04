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
import * as dom from '../../../base/browser/dom.js';
import { ActionBar } from '../../../base/browser/ui/actionbar/actionbar.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import { suggestWidgetStatusbarMenu } from './suggest.js';
import { localize } from '../../../nls.js';
import { MenuEntryActionViewItem } from '../../../platform/actions/browser/menuEntryActionViewItem.js';
import { IMenuService, MenuItemAction } from '../../../platform/actions/common/actions.js';
import { IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
class StatusBarViewItem extends MenuEntryActionViewItem {
    updateLabel() {
        const kb = this._keybindingService.lookupKeybinding(this._action.id);
        if (!kb) {
            return super.updateLabel();
        }
        if (this.label) {
            this.label.textContent = localize('ddd', '{0} ({1})', this._action.label, StatusBarViewItem.symbolPrintEnter(kb));
        }
    }
    static symbolPrintEnter(kb) {
        var _a;
        return (_a = kb.getLabel()) === null || _a === void 0 ? void 0 : _a.replace(/\benter\b/gi, '\u23CE');
    }
}
let SuggestWidgetStatus = class SuggestWidgetStatus {
    constructor(container, instantiationService, _menuService, _contextKeyService) {
        this._menuService = _menuService;
        this._contextKeyService = _contextKeyService;
        this._menuDisposables = new DisposableStore();
        this.element = dom.append(container, dom.$('.suggest-status-bar'));
        const actionViewItemProvider = (action => {
            return action instanceof MenuItemAction ? instantiationService.createInstance(StatusBarViewItem, action) : undefined;
        });
        this._leftActions = new ActionBar(this.element, { actionViewItemProvider });
        this._rightActions = new ActionBar(this.element, { actionViewItemProvider });
        this._leftActions.domNode.classList.add('left');
        this._rightActions.domNode.classList.add('right');
    }
    dispose() {
        this._menuDisposables.dispose();
        this.element.remove();
    }
    show() {
        const menu = this._menuService.createMenu(suggestWidgetStatusbarMenu, this._contextKeyService);
        const renderMenu = () => {
            const left = [];
            const right = [];
            for (let [group, actions] of menu.getActions()) {
                if (group === 'left') {
                    left.push(...actions);
                }
                else {
                    right.push(...actions);
                }
            }
            this._leftActions.clear();
            this._leftActions.push(left);
            this._rightActions.clear();
            this._rightActions.push(right);
        };
        this._menuDisposables.add(menu.onDidChange(() => renderMenu()));
        this._menuDisposables.add(menu);
    }
    hide() {
        this._menuDisposables.clear();
    }
};
SuggestWidgetStatus = __decorate([
    __param(1, IInstantiationService),
    __param(2, IMenuService),
    __param(3, IContextKeyService)
], SuggestWidgetStatus);
export { SuggestWidgetStatus };
