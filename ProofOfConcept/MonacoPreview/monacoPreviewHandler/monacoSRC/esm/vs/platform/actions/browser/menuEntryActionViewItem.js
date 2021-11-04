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
import './menuEntryActionViewItem.css';
import { asCSSUrl, ModifierKeyEmitter } from '../../../base/browser/dom.js';
import { domEvent } from '../../../base/browser/event.js';
import { Separator } from '../../../base/common/actions.js';
import { toDisposable, MutableDisposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { localize } from '../../../nls.js';
import { MenuItemAction, SubmenuItemAction } from '../common/actions.js';
import { IContextMenuService } from '../../contextview/browser/contextView.js';
import { IKeybindingService } from '../../keybinding/common/keybinding.js';
import { INotificationService } from '../../notification/common/notification.js';
import { ThemeIcon } from '../../theme/common/themeService.js';
import { ActionViewItem } from '../../../base/browser/ui/actionbar/actionViewItems.js';
import { DropdownMenuActionViewItem } from '../../../base/browser/ui/dropdown/dropdownActionViewItem.js';
import { isWindows, isLinux } from '../../../base/common/platform.js';
export function createAndFillInActionBarActions(menu, options, target, isPrimaryGroup) {
    const groups = menu.getActions(options);
    // Action bars handle alternative actions on their own so the alternative actions should be ignored
    fillInActions(groups, target, false, isPrimaryGroup);
    return asDisposable(groups);
}
function asDisposable(groups) {
    const disposables = new DisposableStore();
    for (const [, actions] of groups) {
        for (const action of actions) {
            disposables.add(action);
        }
    }
    return disposables;
}
function fillInActions(groups, target, useAlternativeActions, isPrimaryGroup = group => group === 'navigation') {
    for (let [group, actions] of groups) {
        if (useAlternativeActions) {
            actions = actions.map(a => (a instanceof MenuItemAction) && !!a.alt ? a.alt : a);
        }
        if (isPrimaryGroup(group)) {
            const to = Array.isArray(target) ? target : target.primary;
            to.unshift(...actions);
        }
        else {
            const to = Array.isArray(target) ? target : target.secondary;
            if (to.length > 0) {
                to.push(new Separator());
            }
            to.push(...actions);
        }
    }
}
let MenuEntryActionViewItem = class MenuEntryActionViewItem extends ActionViewItem {
    constructor(_action, _keybindingService, _notificationService) {
        super(undefined, _action, { icon: !!(_action.class || _action.item.icon), label: !_action.class && !_action.item.icon });
        this._action = _action;
        this._keybindingService = _keybindingService;
        this._notificationService = _notificationService;
        this._wantsAltCommand = false;
        this._itemClassDispose = this._register(new MutableDisposable());
        this._altKey = ModifierKeyEmitter.getInstance();
    }
    get _commandAction() {
        return this._wantsAltCommand && this._action.alt || this._action;
    }
    onClick(event) {
        event.preventDefault();
        event.stopPropagation();
        this.actionRunner
            .run(this._commandAction, this._context)
            .catch(err => this._notificationService.error(err));
    }
    render(container) {
        super.render(container);
        container.classList.add('menu-entry');
        this._updateItemClass(this._action.item);
        let mouseOver = false;
        let alternativeKeyDown = this._altKey.keyStatus.altKey || ((isWindows || isLinux) && this._altKey.keyStatus.shiftKey);
        const updateAltState = () => {
            const wantsAltCommand = mouseOver && alternativeKeyDown;
            if (wantsAltCommand !== this._wantsAltCommand) {
                this._wantsAltCommand = wantsAltCommand;
                this.updateLabel();
                this.updateTooltip();
                this.updateClass();
            }
        };
        if (this._action.alt) {
            this._register(this._altKey.event(value => {
                alternativeKeyDown = value.altKey || ((isWindows || isLinux) && value.shiftKey);
                updateAltState();
            }));
        }
        this._register(domEvent(container, 'mouseleave')(_ => {
            mouseOver = false;
            updateAltState();
        }));
        this._register(domEvent(container, 'mouseenter')(e => {
            mouseOver = true;
            updateAltState();
        }));
    }
    updateLabel() {
        if (this.options.label && this.label) {
            this.label.textContent = this._commandAction.label;
        }
    }
    updateTooltip() {
        if (this.label) {
            const keybinding = this._keybindingService.lookupKeybinding(this._commandAction.id);
            const keybindingLabel = keybinding && keybinding.getLabel();
            const tooltip = this._commandAction.tooltip || this._commandAction.label;
            this.label.title = keybindingLabel
                ? localize('titleAndKb', "{0} ({1})", tooltip, keybindingLabel)
                : tooltip;
        }
    }
    updateClass() {
        if (this.options.icon) {
            if (this._commandAction !== this._action) {
                if (this._action.alt) {
                    this._updateItemClass(this._action.alt.item);
                }
            }
            else if (this._action.alt) {
                this._updateItemClass(this._action.item);
            }
        }
    }
    _updateItemClass(item) {
        var _a;
        this._itemClassDispose.value = undefined;
        const { element, label } = this;
        if (!element || !label) {
            return;
        }
        const icon = this._commandAction.checked && ((_a = item.toggled) === null || _a === void 0 ? void 0 : _a.icon) ? item.toggled.icon : item.icon;
        if (!icon) {
            return;
        }
        if (ThemeIcon.isThemeIcon(icon)) {
            // theme icons
            const iconClass = ThemeIcon.asClassName(icon);
            label.classList.add(...iconClass.split(' '));
            this._itemClassDispose.value = toDisposable(() => {
                label.classList.remove(...iconClass.split(' '));
            });
        }
        else {
            // icon path/url
            if (icon.light) {
                label.style.setProperty('--menu-entry-icon-light', asCSSUrl(icon.light));
            }
            if (icon.dark) {
                label.style.setProperty('--menu-entry-icon-dark', asCSSUrl(icon.dark));
            }
            label.classList.add('icon');
            this._itemClassDispose.value = toDisposable(() => {
                label.classList.remove('icon');
                label.style.removeProperty('--menu-entry-icon-light');
                label.style.removeProperty('--menu-entry-icon-dark');
            });
        }
    }
};
MenuEntryActionViewItem = __decorate([
    __param(1, IKeybindingService),
    __param(2, INotificationService)
], MenuEntryActionViewItem);
export { MenuEntryActionViewItem };
let SubmenuEntryActionViewItem = class SubmenuEntryActionViewItem extends DropdownMenuActionViewItem {
    constructor(action, contextMenuService) {
        super(action, { getActions: () => action.actions }, contextMenuService, {
            menuAsChild: true,
            classNames: ThemeIcon.isThemeIcon(action.item.icon) ? ThemeIcon.asClassName(action.item.icon) : undefined,
        });
    }
    render(container) {
        super.render(container);
        if (this.element) {
            container.classList.add('menu-entry');
            const { icon } = this._action.item;
            if (icon && !ThemeIcon.isThemeIcon(icon)) {
                this.element.classList.add('icon');
                if (icon.light) {
                    this.element.style.setProperty('--menu-entry-icon-light', asCSSUrl(icon.light));
                }
                if (icon.dark) {
                    this.element.style.setProperty('--menu-entry-icon-dark', asCSSUrl(icon.dark));
                }
            }
        }
    }
};
SubmenuEntryActionViewItem = __decorate([
    __param(1, IContextMenuService)
], SubmenuEntryActionViewItem);
export { SubmenuEntryActionViewItem };
/**
 * Creates action view items for menu actions or submenu actions.
 */
export function createActionViewItem(instaService, action) {
    if (action instanceof MenuItemAction) {
        return instaService.createInstance(MenuEntryActionViewItem, action);
    }
    else if (action instanceof SubmenuItemAction) {
        return instaService.createInstance(SubmenuEntryActionViewItem, action);
    }
    else {
        return undefined;
    }
}
