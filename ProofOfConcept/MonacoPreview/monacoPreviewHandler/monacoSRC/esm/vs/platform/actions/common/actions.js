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
import { Separator, SubmenuAction } from '../../../base/common/actions.js';
import { createDecorator } from '../../instantiation/common/instantiation.js';
import { IContextKeyService } from '../../contextkey/common/contextkey.js';
import { ICommandService } from '../../commands/common/commands.js';
import { toDisposable } from '../../../base/common/lifecycle.js';
import { Emitter } from '../../../base/common/event.js';
import { ThemeIcon } from '../../theme/common/themeService.js';
import { Iterable } from '../../../base/common/iterator.js';
import { LinkedList } from '../../../base/common/linkedList.js';
import { CSSIcon } from '../../../base/common/codicons.js';
export function isIMenuItem(item) {
    return item.command !== undefined;
}
export class MenuId {
    constructor(debugName) {
        this.id = MenuId._idPool++;
        this._debugName = debugName;
    }
}
MenuId._idPool = 0;
MenuId.CommandPalette = new MenuId('CommandPalette');
MenuId.EditorContext = new MenuId('EditorContext');
MenuId.EditorContextPeek = new MenuId('EditorContextPeek');
MenuId.MenubarEditMenu = new MenuId('MenubarEditMenu');
MenuId.MenubarGoMenu = new MenuId('MenubarGoMenu');
MenuId.MenubarSelectionMenu = new MenuId('MenubarSelectionMenu');
export const IMenuService = createDecorator('menuService');
export const MenuRegistry = new class {
    constructor() {
        this._commands = new Map();
        this._menuItems = new Map();
        this._onDidChangeMenu = new Emitter();
        this.onDidChangeMenu = this._onDidChangeMenu.event;
        this._commandPaletteChangeEvent = {
            has: id => id === MenuId.CommandPalette
        };
    }
    addCommand(command) {
        return this.addCommands(Iterable.single(command));
    }
    addCommands(commands) {
        for (const command of commands) {
            this._commands.set(command.id, command);
        }
        this._onDidChangeMenu.fire(this._commandPaletteChangeEvent);
        return toDisposable(() => {
            let didChange = false;
            for (const command of commands) {
                didChange = this._commands.delete(command.id) || didChange;
            }
            if (didChange) {
                this._onDidChangeMenu.fire(this._commandPaletteChangeEvent);
            }
        });
    }
    getCommand(id) {
        return this._commands.get(id);
    }
    getCommands() {
        const map = new Map();
        this._commands.forEach((value, key) => map.set(key, value));
        return map;
    }
    appendMenuItem(id, item) {
        return this.appendMenuItems(Iterable.single({ id, item }));
    }
    appendMenuItems(items) {
        const changedIds = new Set();
        const toRemove = new LinkedList();
        for (const { id, item } of items) {
            let list = this._menuItems.get(id);
            if (!list) {
                list = new LinkedList();
                this._menuItems.set(id, list);
            }
            toRemove.push(list.push(item));
            changedIds.add(id);
        }
        this._onDidChangeMenu.fire(changedIds);
        return toDisposable(() => {
            if (toRemove.size > 0) {
                for (let fn of toRemove) {
                    fn();
                }
                this._onDidChangeMenu.fire(changedIds);
                toRemove.clear();
            }
        });
    }
    getMenuItems(id) {
        let result;
        if (this._menuItems.has(id)) {
            result = [...this._menuItems.get(id)];
        }
        else {
            result = [];
        }
        if (id === MenuId.CommandPalette) {
            // CommandPalette is special because it shows
            // all commands by default
            this._appendImplicitItems(result);
        }
        return result;
    }
    _appendImplicitItems(result) {
        const set = new Set();
        for (const item of result) {
            if (isIMenuItem(item)) {
                set.add(item.command.id);
                if (item.alt) {
                    set.add(item.alt.id);
                }
            }
        }
        this._commands.forEach((command, id) => {
            if (!set.has(id)) {
                result.push({ command });
            }
        });
    }
};
export class SubmenuItemAction extends SubmenuAction {
    constructor(item, menuService, contextKeyService, options) {
        const result = [];
        const menu = menuService.createMenu(item.submenu, contextKeyService);
        const groups = menu.getActions(options);
        menu.dispose();
        for (let group of groups) {
            const [, actions] = group;
            if (actions.length > 0) {
                result.push(...actions);
                result.push(new Separator());
            }
        }
        if (result.length) {
            result.pop(); // remove last separator
        }
        super(`submenuitem.${item.submenu.id}`, typeof item.title === 'string' ? item.title : item.title.value, result, 'submenu');
        this.item = item;
    }
}
// implements IAction, does NOT extend Action, so that no one
// subscribes to events of Action or modified properties
let MenuItemAction = class MenuItemAction {
    constructor(item, alt, options, contextKeyService, _commandService) {
        var _a;
        this._commandService = _commandService;
        this.id = item.id;
        this.label = typeof item.title === 'string' ? item.title : item.title.value;
        this.tooltip = (_a = item.tooltip) !== null && _a !== void 0 ? _a : '';
        this.enabled = !item.precondition || contextKeyService.contextMatchesRules(item.precondition);
        this.checked = false;
        if (item.toggled) {
            const toggled = (item.toggled.condition ? item.toggled : { condition: item.toggled });
            this.checked = contextKeyService.contextMatchesRules(toggled.condition);
            if (this.checked && toggled.tooltip) {
                this.tooltip = typeof toggled.tooltip === 'string' ? toggled.tooltip : toggled.tooltip.value;
            }
        }
        this.item = item;
        this.alt = alt ? new MenuItemAction(alt, undefined, options, contextKeyService, _commandService) : undefined;
        this._options = options;
        if (ThemeIcon.isThemeIcon(item.icon)) {
            this.class = CSSIcon.asClassName(item.icon);
        }
    }
    dispose() {
        // there is NOTHING to dispose and the MenuItemAction should
        // never have anything to dispose as it is a convenience type
        // to bridge into the rendering world.
    }
    run(...args) {
        var _a, _b;
        let runArgs = [];
        if ((_a = this._options) === null || _a === void 0 ? void 0 : _a.arg) {
            runArgs = [...runArgs, this._options.arg];
        }
        if ((_b = this._options) === null || _b === void 0 ? void 0 : _b.shouldForwardArgs) {
            runArgs = [...runArgs, ...args];
        }
        return this._commandService.executeCommand(this.id, ...runArgs);
    }
};
MenuItemAction = __decorate([
    __param(3, IContextKeyService),
    __param(4, ICommandService)
], MenuItemAction);
export { MenuItemAction };
//#endregion
