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
import * as nls from '../../../../nls.js';
import { onUnexpectedError } from '../../../../base/common/errors.js';
import { DisposableStore } from '../../../../base/common/lifecycle.js';
import { ICodeEditorService } from '../../../browser/services/codeEditorService.js';
import { IInstantiationService } from '../../../../platform/instantiation/common/instantiation.js';
import { IContextKeyService, RawContextKey, ContextKeyExpr } from '../../../../platform/contextkey/common/contextkey.js';
import { IConfigurationService } from '../../../../platform/configuration/common/configuration.js';
import { IStorageService } from '../../../../platform/storage/common/storage.js';
import { OneReference } from '../referencesModel.js';
import { ReferenceWidget, LayoutData } from './referencesWidget.js';
import { Range } from '../../../common/core/range.js';
import { Position } from '../../../common/core/position.js';
import { INotificationService } from '../../../../platform/notification/common/notification.js';
import { createCancelablePromise } from '../../../../base/common/async.js';
import { getOuterEditor, PeekContext } from '../../peekView/peekView.js';
import { IListService, WorkbenchListFocusContextKey } from '../../../../platform/list/browser/listService.js';
import { KeybindingsRegistry } from '../../../../platform/keybinding/common/keybindingsRegistry.js';
import { KeyChord } from '../../../../base/common/keyCodes.js';
import { CommandsRegistry } from '../../../../platform/commands/common/commands.js';
export const ctxReferenceSearchVisible = new RawContextKey('referenceSearchVisible', false);
let ReferencesController = class ReferencesController {
    constructor(_defaultTreeKeyboardSupport, _editor, contextKeyService, _editorService, _notificationService, _instantiationService, _storageService, _configurationService) {
        this._defaultTreeKeyboardSupport = _defaultTreeKeyboardSupport;
        this._editor = _editor;
        this._editorService = _editorService;
        this._notificationService = _notificationService;
        this._instantiationService = _instantiationService;
        this._storageService = _storageService;
        this._configurationService = _configurationService;
        this._disposables = new DisposableStore();
        this._requestIdPool = 0;
        this._ignoreModelChangeEvent = false;
        this._referenceSearchVisible = ctxReferenceSearchVisible.bindTo(contextKeyService);
    }
    static get(editor) {
        return editor.getContribution(ReferencesController.ID);
    }
    dispose() {
        var _a, _b;
        this._referenceSearchVisible.reset();
        this._disposables.dispose();
        (_a = this._widget) === null || _a === void 0 ? void 0 : _a.dispose();
        (_b = this._model) === null || _b === void 0 ? void 0 : _b.dispose();
        this._widget = undefined;
        this._model = undefined;
    }
    toggleWidget(range, modelPromise, peekMode) {
        // close current widget and return early is position didn't change
        let widgetPosition;
        if (this._widget) {
            widgetPosition = this._widget.position;
        }
        this.closeWidget();
        if (!!widgetPosition && range.containsPosition(widgetPosition)) {
            return;
        }
        this._peekMode = peekMode;
        this._referenceSearchVisible.set(true);
        // close the widget on model/mode changes
        this._disposables.add(this._editor.onDidChangeModelLanguage(() => { this.closeWidget(); }));
        this._disposables.add(this._editor.onDidChangeModel(() => {
            if (!this._ignoreModelChangeEvent) {
                this.closeWidget();
            }
        }));
        const storageKey = 'peekViewLayout';
        const data = LayoutData.fromJSON(this._storageService.get(storageKey, 0 /* GLOBAL */, '{}'));
        this._widget = this._instantiationService.createInstance(ReferenceWidget, this._editor, this._defaultTreeKeyboardSupport, data);
        this._widget.setTitle(nls.localize('labelLoading', "Loading..."));
        this._widget.show(range);
        this._disposables.add(this._widget.onDidClose(() => {
            modelPromise.cancel();
            if (this._widget) {
                this._storageService.store(storageKey, JSON.stringify(this._widget.layoutData), 0 /* GLOBAL */, 1 /* MACHINE */);
                this._widget = undefined;
            }
            this.closeWidget();
        }));
        this._disposables.add(this._widget.onDidSelectReference(event => {
            let { element, kind } = event;
            if (!element) {
                return;
            }
            switch (kind) {
                case 'open':
                    if (event.source !== 'editor' || !this._configurationService.getValue('editor.stablePeek')) {
                        // when stable peek is configured we don't close
                        // the peek window on selecting the editor
                        this.openReference(element, false, false);
                    }
                    break;
                case 'side':
                    this.openReference(element, true, false);
                    break;
                case 'goto':
                    if (peekMode) {
                        this._gotoReference(element);
                    }
                    else {
                        this.openReference(element, false, true);
                    }
                    break;
            }
        }));
        const requestId = ++this._requestIdPool;
        modelPromise.then(model => {
            // still current request? widget still open?
            if (requestId !== this._requestIdPool || !this._widget) {
                return undefined;
            }
            if (this._model) {
                this._model.dispose();
            }
            this._model = model;
            // show widget
            return this._widget.setModel(this._model).then(() => {
                if (this._widget && this._model && this._editor.hasModel()) { // might have been closed
                    // set title
                    if (!this._model.isEmpty) {
                        this._widget.setMetaTitle(nls.localize('metaTitle.N', "{0} ({1})", this._model.title, this._model.references.length));
                    }
                    else {
                        this._widget.setMetaTitle('');
                    }
                    // set 'best' selection
                    let uri = this._editor.getModel().uri;
                    let pos = new Position(range.startLineNumber, range.startColumn);
                    let selection = this._model.nearestReference(uri, pos);
                    if (selection) {
                        return this._widget.setSelection(selection).then(() => {
                            if (this._widget && this._editor.getOption(71 /* peekWidgetDefaultFocus */) === 'editor') {
                                this._widget.focusOnPreviewEditor();
                            }
                        });
                    }
                }
                return undefined;
            });
        }, error => {
            this._notificationService.error(error);
        });
    }
    changeFocusBetweenPreviewAndReferences() {
        if (!this._widget) {
            // can be called while still resolving...
            return;
        }
        if (this._widget.isPreviewEditorFocused()) {
            this._widget.focusOnReferenceTree();
        }
        else {
            this._widget.focusOnPreviewEditor();
        }
    }
    goToNextOrPreviousReference(fwd) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._editor.hasModel() || !this._model || !this._widget) {
                // can be called while still resolving...
                return;
            }
            const currentPosition = this._widget.position;
            if (!currentPosition) {
                return;
            }
            const source = this._model.nearestReference(this._editor.getModel().uri, currentPosition);
            if (!source) {
                return;
            }
            const target = this._model.nextOrPreviousReference(source, fwd);
            const editorFocus = this._editor.hasTextFocus();
            const previewEditorFocus = this._widget.isPreviewEditorFocused();
            yield this._widget.setSelection(target);
            yield this._gotoReference(target);
            if (editorFocus) {
                this._editor.focus();
            }
            else if (this._widget && previewEditorFocus) {
                this._widget.focusOnPreviewEditor();
            }
        });
    }
    revealReference(reference) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this._editor.hasModel() || !this._model || !this._widget) {
                // can be called while still resolving...
                return;
            }
            yield this._widget.revealReference(reference);
        });
    }
    closeWidget(focusEditor = true) {
        var _a, _b;
        (_a = this._widget) === null || _a === void 0 ? void 0 : _a.dispose();
        (_b = this._model) === null || _b === void 0 ? void 0 : _b.dispose();
        this._referenceSearchVisible.reset();
        this._disposables.clear();
        this._widget = undefined;
        this._model = undefined;
        if (focusEditor) {
            this._editor.focus();
        }
        this._requestIdPool += 1; // Cancel pending requests
    }
    _gotoReference(ref) {
        if (this._widget) {
            this._widget.hide();
        }
        this._ignoreModelChangeEvent = true;
        const range = Range.lift(ref.range).collapseToStart();
        return this._editorService.openCodeEditor({
            resource: ref.uri,
            options: { selection: range }
        }, this._editor).then(openedEditor => {
            var _a;
            this._ignoreModelChangeEvent = false;
            if (!openedEditor || !this._widget) {
                // something went wrong...
                this.closeWidget();
                return;
            }
            if (this._editor === openedEditor) {
                //
                this._widget.show(range);
                this._widget.focusOnReferenceTree();
            }
            else {
                // we opened a different editor instance which means a different controller instance.
                // therefore we stop with this controller and continue with the other
                const other = ReferencesController.get(openedEditor);
                const model = this._model.clone();
                this.closeWidget();
                openedEditor.focus();
                other.toggleWidget(range, createCancelablePromise(_ => Promise.resolve(model)), (_a = this._peekMode) !== null && _a !== void 0 ? _a : false);
            }
        }, (err) => {
            this._ignoreModelChangeEvent = false;
            onUnexpectedError(err);
        });
    }
    openReference(ref, sideBySide, pinned) {
        // clear stage
        if (!sideBySide) {
            this.closeWidget();
        }
        const { uri, range } = ref;
        this._editorService.openCodeEditor({
            resource: uri,
            options: { selection: range, pinned }
        }, this._editor, sideBySide);
    }
};
ReferencesController.ID = 'editor.contrib.referencesController';
ReferencesController = __decorate([
    __param(2, IContextKeyService),
    __param(3, ICodeEditorService),
    __param(4, INotificationService),
    __param(5, IInstantiationService),
    __param(6, IStorageService),
    __param(7, IConfigurationService)
], ReferencesController);
export { ReferencesController };
function withController(accessor, fn) {
    const outerEditor = getOuterEditor(accessor);
    if (!outerEditor) {
        return;
    }
    let controller = ReferencesController.get(outerEditor);
    if (controller) {
        fn(controller);
    }
}
KeybindingsRegistry.registerCommandAndKeybindingRule({
    id: 'togglePeekWidgetFocus',
    weight: 100 /* EditorContrib */,
    primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, 60 /* F2 */),
    when: ContextKeyExpr.or(ctxReferenceSearchVisible, PeekContext.inPeekEditor),
    handler(accessor) {
        withController(accessor, controller => {
            controller.changeFocusBetweenPreviewAndReferences();
        });
    }
});
KeybindingsRegistry.registerCommandAndKeybindingRule({
    id: 'goToNextReference',
    weight: 100 /* EditorContrib */ - 10,
    primary: 62 /* F4 */,
    secondary: [70 /* F12 */],
    when: ContextKeyExpr.or(ctxReferenceSearchVisible, PeekContext.inPeekEditor),
    handler(accessor) {
        withController(accessor, controller => {
            controller.goToNextOrPreviousReference(true);
        });
    }
});
KeybindingsRegistry.registerCommandAndKeybindingRule({
    id: 'goToPreviousReference',
    weight: 100 /* EditorContrib */ - 10,
    primary: 1024 /* Shift */ | 62 /* F4 */,
    secondary: [1024 /* Shift */ | 70 /* F12 */],
    when: ContextKeyExpr.or(ctxReferenceSearchVisible, PeekContext.inPeekEditor),
    handler(accessor) {
        withController(accessor, controller => {
            controller.goToNextOrPreviousReference(false);
        });
    }
});
// commands that aren't needed anymore because there is now ContextKeyExpr.OR
CommandsRegistry.registerCommandAlias('goToNextReferenceFromEmbeddedEditor', 'goToNextReference');
CommandsRegistry.registerCommandAlias('goToPreviousReferenceFromEmbeddedEditor', 'goToPreviousReference');
// close
CommandsRegistry.registerCommandAlias('closeReferenceSearchEditor', 'closeReferenceSearch');
CommandsRegistry.registerCommand('closeReferenceSearch', accessor => withController(accessor, controller => controller.closeWidget()));
KeybindingsRegistry.registerKeybindingRule({
    id: 'closeReferenceSearch',
    weight: 100 /* EditorContrib */ - 101,
    primary: 9 /* Escape */,
    secondary: [1024 /* Shift */ | 9 /* Escape */],
    when: ContextKeyExpr.and(PeekContext.inPeekEditor, ContextKeyExpr.not('config.editor.stablePeek'))
});
KeybindingsRegistry.registerKeybindingRule({
    id: 'closeReferenceSearch',
    weight: 200 /* WorkbenchContrib */ + 50,
    primary: 9 /* Escape */,
    secondary: [1024 /* Shift */ | 9 /* Escape */],
    when: ContextKeyExpr.and(ctxReferenceSearchVisible, ContextKeyExpr.not('config.editor.stablePeek'))
});
KeybindingsRegistry.registerCommandAndKeybindingRule({
    id: 'revealReference',
    weight: 200 /* WorkbenchContrib */,
    primary: 3 /* Enter */,
    mac: {
        primary: 3 /* Enter */,
        secondary: [2048 /* CtrlCmd */ | 18 /* DownArrow */]
    },
    when: ContextKeyExpr.and(ctxReferenceSearchVisible, WorkbenchListFocusContextKey),
    handler(accessor) {
        var _a;
        const listService = accessor.get(IListService);
        const focus = (_a = listService.lastFocusedList) === null || _a === void 0 ? void 0 : _a.getFocus();
        if (Array.isArray(focus) && focus[0] instanceof OneReference) {
            withController(accessor, controller => controller.revealReference(focus[0]));
        }
    }
});
KeybindingsRegistry.registerCommandAndKeybindingRule({
    id: 'openReferenceToSide',
    weight: 100 /* EditorContrib */,
    primary: 2048 /* CtrlCmd */ | 3 /* Enter */,
    mac: {
        primary: 256 /* WinCtrl */ | 3 /* Enter */
    },
    when: ContextKeyExpr.and(ctxReferenceSearchVisible, WorkbenchListFocusContextKey),
    handler(accessor) {
        var _a;
        const listService = accessor.get(IListService);
        const focus = (_a = listService.lastFocusedList) === null || _a === void 0 ? void 0 : _a.getFocus();
        if (Array.isArray(focus) && focus[0] instanceof OneReference) {
            withController(accessor, controller => controller.openReference(focus[0], true, true));
        }
    }
});
CommandsRegistry.registerCommand('openReference', (accessor) => {
    var _a;
    const listService = accessor.get(IListService);
    const focus = (_a = listService.lastFocusedList) === null || _a === void 0 ? void 0 : _a.getFocus();
    if (Array.isArray(focus) && focus[0] instanceof OneReference) {
        withController(accessor, controller => controller.openReference(focus[0], false, true));
    }
});
