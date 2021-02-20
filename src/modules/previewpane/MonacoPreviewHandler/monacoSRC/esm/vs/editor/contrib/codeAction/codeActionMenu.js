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
import { getDomNodePagePosition } from '../../../base/browser/dom.js';
import { Action, Separator } from '../../../base/common/actions.js';
import { canceled } from '../../../base/common/errors.js';
import { Lazy } from '../../../base/common/lazy.js';
import { Disposable, MutableDisposable } from '../../../base/common/lifecycle.js';
import { Position } from '../../common/core/position.js';
import { CodeActionProviderRegistry } from '../../common/modes.js';
import { codeActionCommandId, CodeActionItem, fixAllCommandId, organizeImportsCommandId, refactorCommandId, sourceActionCommandId } from './codeAction.js';
import { CodeActionCommandArgs, CodeActionKind } from './types.js';
import { IContextMenuService } from '../../../platform/contextview/browser/contextView.js';
import { IKeybindingService } from '../../../platform/keybinding/common/keybinding.js';
class CodeActionAction extends Action {
    constructor(action, callback) {
        super(action.command ? action.command.id : action.title, stripNewlines(action.title), undefined, !action.disabled, callback);
        this.action = action;
    }
}
function stripNewlines(str) {
    return str.replace(/\r\n|\r|\n/g, ' ');
}
let CodeActionMenu = class CodeActionMenu extends Disposable {
    constructor(_editor, _delegate, _contextMenuService, keybindingService) {
        super();
        this._editor = _editor;
        this._delegate = _delegate;
        this._contextMenuService = _contextMenuService;
        this._visible = false;
        this._showingActions = this._register(new MutableDisposable());
        this._keybindingResolver = new CodeActionKeybindingResolver({
            getKeybindings: () => keybindingService.getKeybindings()
        });
    }
    get isVisible() {
        return this._visible;
    }
    show(trigger, codeActions, at, options) {
        return __awaiter(this, void 0, void 0, function* () {
            const actionsToShow = options.includeDisabledActions ? codeActions.allActions : codeActions.validActions;
            if (!actionsToShow.length) {
                this._visible = false;
                return;
            }
            if (!this._editor.getDomNode()) {
                // cancel when editor went off-dom
                this._visible = false;
                throw canceled();
            }
            this._visible = true;
            this._showingActions.value = codeActions;
            const menuActions = this.getMenuActions(trigger, actionsToShow, codeActions.documentation);
            const anchor = Position.isIPosition(at) ? this._toCoords(at) : at || { x: 0, y: 0 };
            const resolver = this._keybindingResolver.getResolver();
            this._contextMenuService.showContextMenu({
                domForShadowRoot: this._editor.getDomNode(),
                getAnchor: () => anchor,
                getActions: () => menuActions,
                onHide: () => {
                    this._visible = false;
                    this._editor.focus();
                },
                autoSelectFirstItem: true,
                getKeyBinding: action => action instanceof CodeActionAction ? resolver(action.action) : undefined,
            });
        });
    }
    getMenuActions(trigger, actionsToShow, documentation) {
        var _a, _b;
        const toCodeActionAction = (item) => new CodeActionAction(item.action, () => this._delegate.onSelectCodeAction(item));
        const result = actionsToShow
            .map(toCodeActionAction);
        const allDocumentation = [...documentation];
        const model = this._editor.getModel();
        if (model && result.length) {
            for (const provider of CodeActionProviderRegistry.all(model)) {
                if (provider._getAdditionalMenuItems) {
                    allDocumentation.push(...provider._getAdditionalMenuItems({ trigger: trigger.type, only: (_b = (_a = trigger.filter) === null || _a === void 0 ? void 0 : _a.include) === null || _b === void 0 ? void 0 : _b.value }, actionsToShow.map(item => item.action)));
                }
            }
        }
        if (allDocumentation.length) {
            result.push(new Separator(), ...allDocumentation.map(command => toCodeActionAction(new CodeActionItem({
                title: command.title,
                command: command,
            }, undefined))));
        }
        return result;
    }
    _toCoords(position) {
        if (!this._editor.hasModel()) {
            return { x: 0, y: 0 };
        }
        this._editor.revealPosition(position, 1 /* Immediate */);
        this._editor.render();
        // Translate to absolute editor position
        const cursorCoords = this._editor.getScrolledVisiblePosition(position);
        const editorCoords = getDomNodePagePosition(this._editor.getDomNode());
        const x = editorCoords.left + cursorCoords.left;
        const y = editorCoords.top + cursorCoords.top + cursorCoords.height;
        return { x, y };
    }
};
CodeActionMenu = __decorate([
    __param(2, IContextMenuService),
    __param(3, IKeybindingService)
], CodeActionMenu);
export { CodeActionMenu };
export class CodeActionKeybindingResolver {
    constructor(_keybindingProvider) {
        this._keybindingProvider = _keybindingProvider;
    }
    getResolver() {
        // Lazy since we may not actually ever read the value
        const allCodeActionBindings = new Lazy(() => this._keybindingProvider.getKeybindings()
            .filter(item => CodeActionKeybindingResolver.codeActionCommands.indexOf(item.command) >= 0)
            .filter(item => item.resolvedKeybinding)
            .map((item) => {
            // Special case these commands since they come built-in with VS Code and don't use 'commandArgs'
            let commandArgs = item.commandArgs;
            if (item.command === organizeImportsCommandId) {
                commandArgs = { kind: CodeActionKind.SourceOrganizeImports.value };
            }
            else if (item.command === fixAllCommandId) {
                commandArgs = { kind: CodeActionKind.SourceFixAll.value };
            }
            return Object.assign({ resolvedKeybinding: item.resolvedKeybinding }, CodeActionCommandArgs.fromUser(commandArgs, {
                kind: CodeActionKind.None,
                apply: "never" /* Never */
            }));
        }));
        return (action) => {
            if (action.kind) {
                const binding = this.bestKeybindingForCodeAction(action, allCodeActionBindings.getValue());
                return binding === null || binding === void 0 ? void 0 : binding.resolvedKeybinding;
            }
            return undefined;
        };
    }
    bestKeybindingForCodeAction(action, candidates) {
        if (!action.kind) {
            return undefined;
        }
        const kind = new CodeActionKind(action.kind);
        return candidates
            .filter(candidate => candidate.kind.contains(kind))
            .filter(candidate => {
            if (candidate.preferred) {
                // If the candidate keybinding only applies to preferred actions, the this action must also be preferred
                return action.isPreferred;
            }
            return true;
        })
            .reduceRight((currentBest, candidate) => {
            if (!currentBest) {
                return candidate;
            }
            // Select the more specific binding
            return currentBest.kind.contains(candidate.kind) ? candidate : currentBest;
        }, undefined);
    }
}
CodeActionKeybindingResolver.codeActionCommands = [
    refactorCommandId,
    codeActionCommandId,
    sourceActionCommandId,
    organizeImportsCommandId,
    fixAllCommandId
];
