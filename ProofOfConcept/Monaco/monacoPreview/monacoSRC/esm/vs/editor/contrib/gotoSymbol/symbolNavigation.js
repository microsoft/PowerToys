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
import { RawContextKey, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { createDecorator } from '../../../platform/instantiation/common/instantiation.js';
import { registerSingleton } from '../../../platform/instantiation/common/extensions.js';
import { KeybindingsRegistry } from '../../../platform/keybinding/common/keybindingsRegistry.js';
import { registerEditorCommand, EditorCommand } from '../../browser/editorExtensions.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import { Range } from '../../common/core/range.js';
import { dispose, combinedDisposable, DisposableStore } from '../../../base/common/lifecycle.js';
import { Emitter } from '../../../base/common/event.js';
import { localize } from '../../../nls.js';
import { IKeybindingService } from '../../../platform/keybinding/common/keybinding.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { isEqual } from '../../../base/common/resources.js';
export const ctxHasSymbols = new RawContextKey('hasSymbols', false);
export const ISymbolNavigationService = createDecorator('ISymbolNavigationService');
let SymbolNavigationService = class SymbolNavigationService {
    constructor(contextKeyService, _editorService, _notificationService, _keybindingService) {
        this._editorService = _editorService;
        this._notificationService = _notificationService;
        this._keybindingService = _keybindingService;
        this._currentModel = undefined;
        this._currentIdx = -1;
        this._ignoreEditorChange = false;
        this._ctxHasSymbols = ctxHasSymbols.bindTo(contextKeyService);
    }
    reset() {
        var _a, _b;
        this._ctxHasSymbols.reset();
        (_a = this._currentState) === null || _a === void 0 ? void 0 : _a.dispose();
        (_b = this._currentMessage) === null || _b === void 0 ? void 0 : _b.dispose();
        this._currentModel = undefined;
        this._currentIdx = -1;
    }
    put(anchor) {
        const refModel = anchor.parent.parent;
        if (refModel.references.length <= 1) {
            this.reset();
            return;
        }
        this._currentModel = refModel;
        this._currentIdx = refModel.references.indexOf(anchor);
        this._ctxHasSymbols.set(true);
        this._showMessage();
        const editorState = new EditorState(this._editorService);
        const listener = editorState.onDidChange(_ => {
            if (this._ignoreEditorChange) {
                return;
            }
            const editor = this._editorService.getActiveCodeEditor();
            if (!editor) {
                return;
            }
            const model = editor.getModel();
            const position = editor.getPosition();
            if (!model || !position) {
                return;
            }
            let seenUri = false;
            let seenPosition = false;
            for (const reference of refModel.references) {
                if (isEqual(reference.uri, model.uri)) {
                    seenUri = true;
                    seenPosition = seenPosition || Range.containsPosition(reference.range, position);
                }
                else if (seenUri) {
                    break;
                }
            }
            if (!seenUri || !seenPosition) {
                this.reset();
            }
        });
        this._currentState = combinedDisposable(editorState, listener);
    }
    revealNext(source) {
        if (!this._currentModel) {
            return Promise.resolve();
        }
        // get next result and advance
        this._currentIdx += 1;
        this._currentIdx %= this._currentModel.references.length;
        const reference = this._currentModel.references[this._currentIdx];
        // status
        this._showMessage();
        // open editor, ignore events while that happens
        this._ignoreEditorChange = true;
        return this._editorService.openCodeEditor({
            resource: reference.uri,
            options: {
                selection: Range.collapseToStart(reference.range),
                selectionRevealType: 3 /* NearTopIfOutsideViewport */
            }
        }, source).finally(() => {
            this._ignoreEditorChange = false;
        });
    }
    _showMessage() {
        var _a;
        (_a = this._currentMessage) === null || _a === void 0 ? void 0 : _a.dispose();
        const kb = this._keybindingService.lookupKeybinding('editor.gotoNextSymbolFromResult');
        const message = kb
            ? localize('location.kb', "Symbol {0} of {1}, {2} for next", this._currentIdx + 1, this._currentModel.references.length, kb.getLabel())
            : localize('location', "Symbol {0} of {1}", this._currentIdx + 1, this._currentModel.references.length);
        this._currentMessage = this._notificationService.status(message);
    }
};
SymbolNavigationService = __decorate([
    __param(0, IContextKeyService),
    __param(1, ICodeEditorService),
    __param(2, INotificationService),
    __param(3, IKeybindingService)
], SymbolNavigationService);
registerSingleton(ISymbolNavigationService, SymbolNavigationService, true);
registerEditorCommand(new class extends EditorCommand {
    constructor() {
        super({
            id: 'editor.gotoNextSymbolFromResult',
            precondition: ctxHasSymbols,
            kbOpts: {
                weight: 100 /* EditorContrib */,
                primary: 70 /* F12 */
            }
        });
    }
    runEditorCommand(accessor, editor) {
        return accessor.get(ISymbolNavigationService).revealNext(editor);
    }
});
KeybindingsRegistry.registerCommandAndKeybindingRule({
    id: 'editor.gotoNextSymbolFromResult.cancel',
    weight: 100 /* EditorContrib */,
    when: ctxHasSymbols,
    primary: 9 /* Escape */,
    handler(accessor) {
        accessor.get(ISymbolNavigationService).reset();
    }
});
//
let EditorState = class EditorState {
    constructor(editorService) {
        this._listener = new Map();
        this._disposables = new DisposableStore();
        this._onDidChange = new Emitter();
        this.onDidChange = this._onDidChange.event;
        this._disposables.add(editorService.onCodeEditorRemove(this._onDidRemoveEditor, this));
        this._disposables.add(editorService.onCodeEditorAdd(this._onDidAddEditor, this));
        editorService.listCodeEditors().forEach(this._onDidAddEditor, this);
    }
    dispose() {
        this._disposables.dispose();
        this._onDidChange.dispose();
        dispose(this._listener.values());
    }
    _onDidAddEditor(editor) {
        this._listener.set(editor, combinedDisposable(editor.onDidChangeCursorPosition(_ => this._onDidChange.fire({ editor })), editor.onDidChangeModelContent(_ => this._onDidChange.fire({ editor }))));
    }
    _onDidRemoveEditor(editor) {
        var _a;
        (_a = this._listener.get(editor)) === null || _a === void 0 ? void 0 : _a.dispose();
        this._listener.delete(editor);
    }
};
EditorState = __decorate([
    __param(0, ICodeEditorService)
], EditorState);
