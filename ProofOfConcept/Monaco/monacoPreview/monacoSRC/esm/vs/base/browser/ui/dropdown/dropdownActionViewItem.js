/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './dropdown.css';
import { append, $ } from '../../dom.js';
import { Emitter } from '../../../common/event.js';
import { BaseActionViewItem } from '../actionbar/actionViewItems.js';
import { DropdownMenu } from './dropdown.js';
export class DropdownMenuActionViewItem extends BaseActionViewItem {
    constructor(action, menuActionsOrProvider, contextMenuProvider, options = {}) {
        super(null, action, options);
        this.options = options;
        this.actionItem = null;
        this._onDidChangeVisibility = this._register(new Emitter());
        this.menuActionsOrProvider = menuActionsOrProvider;
        this.contextMenuProvider = contextMenuProvider;
        if (this.options.actionRunner) {
            this.actionRunner = this.options.actionRunner;
        }
    }
    render(container) {
        this.actionItem = container;
        const labelRenderer = (el) => {
            this.element = append(el, $('a.action-label'));
            let classNames = [];
            if (typeof this.options.classNames === 'string') {
                classNames = this.options.classNames.split(/\s+/g).filter(s => !!s);
            }
            else if (this.options.classNames) {
                classNames = this.options.classNames;
            }
            // todo@aeschli: remove codicon, should come through `this.options.classNames`
            if (!classNames.find(c => c === 'icon')) {
                classNames.push('codicon');
            }
            this.element.classList.add(...classNames);
            this.element.tabIndex = 0;
            this.element.setAttribute('role', 'button');
            this.element.setAttribute('aria-haspopup', 'true');
            this.element.setAttribute('aria-expanded', 'false');
            this.element.title = this._action.label || '';
            return null;
        };
        const isActionsArray = Array.isArray(this.menuActionsOrProvider);
        const options = {
            contextMenuProvider: this.contextMenuProvider,
            labelRenderer: labelRenderer,
            menuAsChild: this.options.menuAsChild,
            actions: isActionsArray ? this.menuActionsOrProvider : undefined,
            actionProvider: isActionsArray ? undefined : this.menuActionsOrProvider
        };
        this.dropdownMenu = this._register(new DropdownMenu(container, options));
        this._register(this.dropdownMenu.onDidChangeVisibility(visible => {
            var _a;
            (_a = this.element) === null || _a === void 0 ? void 0 : _a.setAttribute('aria-expanded', `${visible}`);
            this._onDidChangeVisibility.fire(visible);
        }));
        this.dropdownMenu.menuOptions = {
            actionViewItemProvider: this.options.actionViewItemProvider,
            actionRunner: this.actionRunner,
            getKeyBinding: this.options.keybindingProvider,
            context: this._context
        };
        if (this.options.anchorAlignmentProvider) {
            const that = this;
            this.dropdownMenu.menuOptions = Object.assign(Object.assign({}, this.dropdownMenu.menuOptions), { get anchorAlignment() {
                    return that.options.anchorAlignmentProvider();
                } });
        }
        this.updateEnabled();
    }
    setActionContext(newContext) {
        super.setActionContext(newContext);
        if (this.dropdownMenu) {
            if (this.dropdownMenu.menuOptions) {
                this.dropdownMenu.menuOptions.context = newContext;
            }
            else {
                this.dropdownMenu.menuOptions = { context: newContext };
            }
        }
    }
    updateEnabled() {
        var _a, _b;
        const disabled = !this.getAction().enabled;
        (_a = this.actionItem) === null || _a === void 0 ? void 0 : _a.classList.toggle('disabled', disabled);
        (_b = this.element) === null || _b === void 0 ? void 0 : _b.classList.toggle('disabled', disabled);
    }
}
