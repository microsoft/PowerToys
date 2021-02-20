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
var _a, _b, _c, _d, _e, _f, _g, _h;
import { alert } from '../../../base/browser/ui/aria/aria.js';
import { createCancelablePromise, raceCancellation } from '../../../base/common/async.js';
import { KeyChord } from '../../../base/common/keyCodes.js';
import { isWeb } from '../../../base/common/platform.js';
import { isCodeEditor } from '../../browser/editorBrowser.js';
import { EditorAction, registerEditorAction } from '../../browser/editorExtensions.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import * as corePosition from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { isLocationLink } from '../../common/modes.js';
import { MessageController } from '../message/messageController.js';
import { PeekContext } from '../peekView/peekView.js';
import { ReferencesController } from './peek/referencesController.js';
import { ReferencesModel } from './referencesModel.js';
import * as nls from '../../../nls.js';
import { MenuId, MenuRegistry } from '../../../platform/actions/common/actions.js';
import { ContextKeyExpr } from '../../../platform/contextkey/common/contextkey.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { IEditorProgressService } from '../../../platform/progress/common/progress.js';
import { getDefinitionsAtPosition, getImplementationsAtPosition, getTypeDefinitionsAtPosition, getDeclarationsAtPosition, getReferencesAtPosition } from './goToSymbol.js';
import { CommandsRegistry, ICommandService } from '../../../platform/commands/common/commands.js';
import { EditorStateCancellationTokenSource } from '../../browser/core/editorState.js';
import { ISymbolNavigationService } from './symbolNavigation.js';
import { isStandalone } from '../../../base/browser/browser.js';
import { URI } from '../../../base/common/uri.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { assertType } from '../../../base/common/types.js';
import { EmbeddedCodeEditorWidget } from '../../browser/widget/embeddedCodeEditorWidget.js';
MenuRegistry.appendMenuItem(MenuId.EditorContext, {
    submenu: MenuId.EditorContextPeek,
    title: nls.localize('peek.submenu', "Peek"),
    group: 'navigation',
    order: 100
});
class SymbolNavigationAction extends EditorAction {
    constructor(configuration, opts) {
        super(opts);
        this._configuration = configuration;
    }
    run(accessor, editor) {
        if (!editor.hasModel()) {
            return Promise.resolve(undefined);
        }
        const notificationService = accessor.get(INotificationService);
        const editorService = accessor.get(ICodeEditorService);
        const progressService = accessor.get(IEditorProgressService);
        const symbolNavService = accessor.get(ISymbolNavigationService);
        const model = editor.getModel();
        const pos = editor.getPosition();
        const cts = new EditorStateCancellationTokenSource(editor, 1 /* Value */ | 4 /* Position */);
        const promise = raceCancellation(this._getLocationModel(model, pos, cts.token), cts.token).then((references) => __awaiter(this, void 0, void 0, function* () {
            if (!references || cts.token.isCancellationRequested) {
                return;
            }
            alert(references.ariaMessage);
            let altAction;
            if (references.referenceAt(model.uri, pos)) {
                const altActionId = this._getAlternativeCommand(editor);
                if (altActionId !== this.id) {
                    altAction = editor.getAction(altActionId);
                }
            }
            const referenceCount = references.references.length;
            if (referenceCount === 0) {
                // no result -> show message
                if (!this._configuration.muteMessage) {
                    const info = model.getWordAtPosition(pos);
                    MessageController.get(editor).showMessage(this._getNoResultFoundMessage(info), pos);
                }
            }
            else if (referenceCount === 1 && altAction) {
                // already at the only result, run alternative
                altAction.run();
            }
            else {
                // normal results handling
                return this._onResult(editorService, symbolNavService, editor, references);
            }
        }), (err) => {
            // report an error
            notificationService.error(err);
        }).finally(() => {
            cts.dispose();
        });
        progressService.showWhile(promise, 250);
        return promise;
    }
    _onResult(editorService, symbolNavService, editor, model) {
        return __awaiter(this, void 0, void 0, function* () {
            const gotoLocation = this._getGoToPreference(editor);
            if (!(editor instanceof EmbeddedCodeEditorWidget) && (this._configuration.openInPeek || (gotoLocation === 'peek' && model.references.length > 1))) {
                this._openInPeek(editor, model);
            }
            else {
                const next = model.firstReference();
                const peek = model.references.length > 1 && gotoLocation === 'gotoAndPeek';
                const targetEditor = yield this._openReference(editor, editorService, next, this._configuration.openToSide, !peek);
                if (peek && targetEditor) {
                    this._openInPeek(targetEditor, model);
                }
                else {
                    model.dispose();
                }
                // keep remaining locations around when using
                // 'goto'-mode
                if (gotoLocation === 'goto') {
                    symbolNavService.put(next);
                }
            }
        });
    }
    _openReference(editor, editorService, reference, sideBySide, highlight) {
        return __awaiter(this, void 0, void 0, function* () {
            // range is the target-selection-range when we have one
            // and the fallback is the 'full' range
            let range = undefined;
            if (isLocationLink(reference)) {
                range = reference.targetSelectionRange;
            }
            if (!range) {
                range = reference.range;
            }
            if (!range) {
                return undefined;
            }
            const targetEditor = yield editorService.openCodeEditor({
                resource: reference.uri,
                options: {
                    selection: Range.collapseToStart(range),
                    selectionRevealType: 3 /* NearTopIfOutsideViewport */
                }
            }, editor, sideBySide);
            if (!targetEditor) {
                return undefined;
            }
            if (highlight) {
                const modelNow = targetEditor.getModel();
                const ids = targetEditor.deltaDecorations([], [{ range, options: { className: 'symbolHighlight' } }]);
                setTimeout(() => {
                    if (targetEditor.getModel() === modelNow) {
                        targetEditor.deltaDecorations(ids, []);
                    }
                }, 350);
            }
            return targetEditor;
        });
    }
    _openInPeek(target, model) {
        let controller = ReferencesController.get(target);
        if (controller && target.hasModel()) {
            controller.toggleWidget(target.getSelection(), createCancelablePromise(_ => Promise.resolve(model)), this._configuration.openInPeek);
        }
        else {
            model.dispose();
        }
    }
}
//#region --- DEFINITION
export class DefinitionAction extends SymbolNavigationAction {
    _getLocationModel(model, position, token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(yield getDefinitionsAtPosition(model, position, token), nls.localize('def.title', 'Definitions'));
        });
    }
    _getNoResultFoundMessage(info) {
        return info && info.word
            ? nls.localize('noResultWord', "No definition found for '{0}'", info.word)
            : nls.localize('generic.noResults', "No definition found");
    }
    _getAlternativeCommand(editor) {
        return editor.getOption(45 /* gotoLocation */).alternativeDefinitionCommand;
    }
    _getGoToPreference(editor) {
        return editor.getOption(45 /* gotoLocation */).multipleDefinitions;
    }
}
const goToDefinitionKb = isWeb && !isStandalone
    ? 2048 /* CtrlCmd */ | 70 /* F12 */
    : 70 /* F12 */;
registerEditorAction((_a = class GoToDefinitionAction extends DefinitionAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: false,
                muteMessage: false
            }, {
                id: GoToDefinitionAction.id,
                label: nls.localize('actions.goToDecl.label', "Go to Definition"),
                alias: 'Go to Definition',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasDefinitionProvider, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                kbOpts: {
                    kbExpr: EditorContextKeys.editorTextFocus,
                    primary: goToDefinitionKb,
                    weight: 100 /* EditorContrib */
                },
                contextMenuOpts: {
                    group: 'navigation',
                    order: 1.1
                },
                menuOpts: {
                    menuId: MenuId.MenubarGoMenu,
                    group: '4_symbol_nav',
                    order: 2,
                    title: nls.localize({ key: 'miGotoDefinition', comment: ['&& denotes a mnemonic'] }, "Go to &&Definition")
                }
            });
            CommandsRegistry.registerCommandAlias('editor.action.goToDeclaration', GoToDefinitionAction.id);
        }
    },
    _a.id = 'editor.action.revealDefinition',
    _a));
registerEditorAction((_b = class OpenDefinitionToSideAction extends DefinitionAction {
        constructor() {
            super({
                openToSide: true,
                openInPeek: false,
                muteMessage: false
            }, {
                id: OpenDefinitionToSideAction.id,
                label: nls.localize('actions.goToDeclToSide.label', "Open Definition to the Side"),
                alias: 'Open Definition to the Side',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasDefinitionProvider, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                kbOpts: {
                    kbExpr: EditorContextKeys.editorTextFocus,
                    primary: KeyChord(2048 /* CtrlCmd */ | 41 /* KEY_K */, goToDefinitionKb),
                    weight: 100 /* EditorContrib */
                }
            });
            CommandsRegistry.registerCommandAlias('editor.action.openDeclarationToTheSide', OpenDefinitionToSideAction.id);
        }
    },
    _b.id = 'editor.action.revealDefinitionAside',
    _b));
registerEditorAction((_c = class PeekDefinitionAction extends DefinitionAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: true,
                muteMessage: false
            }, {
                id: PeekDefinitionAction.id,
                label: nls.localize('actions.previewDecl.label', "Peek Definition"),
                alias: 'Peek Definition',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasDefinitionProvider, PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                kbOpts: {
                    kbExpr: EditorContextKeys.editorTextFocus,
                    primary: 512 /* Alt */ | 70 /* F12 */,
                    linux: { primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 68 /* F10 */ },
                    weight: 100 /* EditorContrib */
                },
                contextMenuOpts: {
                    menuId: MenuId.EditorContextPeek,
                    group: 'peek',
                    order: 2
                }
            });
            CommandsRegistry.registerCommandAlias('editor.action.previewDeclaration', PeekDefinitionAction.id);
        }
    },
    _c.id = 'editor.action.peekDefinition',
    _c));
//#endregion
//#region --- DECLARATION
class DeclarationAction extends SymbolNavigationAction {
    _getLocationModel(model, position, token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(yield getDeclarationsAtPosition(model, position, token), nls.localize('decl.title', 'Declarations'));
        });
    }
    _getNoResultFoundMessage(info) {
        return info && info.word
            ? nls.localize('decl.noResultWord', "No declaration found for '{0}'", info.word)
            : nls.localize('decl.generic.noResults', "No declaration found");
    }
    _getAlternativeCommand(editor) {
        return editor.getOption(45 /* gotoLocation */).alternativeDeclarationCommand;
    }
    _getGoToPreference(editor) {
        return editor.getOption(45 /* gotoLocation */).multipleDeclarations;
    }
}
registerEditorAction((_d = class GoToDeclarationAction extends DeclarationAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: false,
                muteMessage: false
            }, {
                id: GoToDeclarationAction.id,
                label: nls.localize('actions.goToDeclaration.label', "Go to Declaration"),
                alias: 'Go to Declaration',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasDeclarationProvider, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                contextMenuOpts: {
                    group: 'navigation',
                    order: 1.3
                },
                menuOpts: {
                    menuId: MenuId.MenubarGoMenu,
                    group: '4_symbol_nav',
                    order: 3,
                    title: nls.localize({ key: 'miGotoDeclaration', comment: ['&& denotes a mnemonic'] }, "Go to &&Declaration")
                },
            });
        }
        _getNoResultFoundMessage(info) {
            return info && info.word
                ? nls.localize('decl.noResultWord', "No declaration found for '{0}'", info.word)
                : nls.localize('decl.generic.noResults', "No declaration found");
        }
    },
    _d.id = 'editor.action.revealDeclaration',
    _d));
registerEditorAction(class PeekDeclarationAction extends DeclarationAction {
    constructor() {
        super({
            openToSide: false,
            openInPeek: true,
            muteMessage: false
        }, {
            id: 'editor.action.peekDeclaration',
            label: nls.localize('actions.peekDecl.label', "Peek Declaration"),
            alias: 'Peek Declaration',
            precondition: ContextKeyExpr.and(EditorContextKeys.hasDeclarationProvider, PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
            contextMenuOpts: {
                menuId: MenuId.EditorContextPeek,
                group: 'peek',
                order: 3
            }
        });
    }
});
//#endregion
//#region --- TYPE DEFINITION
class TypeDefinitionAction extends SymbolNavigationAction {
    _getLocationModel(model, position, token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(yield getTypeDefinitionsAtPosition(model, position, token), nls.localize('typedef.title', 'Type Definitions'));
        });
    }
    _getNoResultFoundMessage(info) {
        return info && info.word
            ? nls.localize('goToTypeDefinition.noResultWord', "No type definition found for '{0}'", info.word)
            : nls.localize('goToTypeDefinition.generic.noResults', "No type definition found");
    }
    _getAlternativeCommand(editor) {
        return editor.getOption(45 /* gotoLocation */).alternativeTypeDefinitionCommand;
    }
    _getGoToPreference(editor) {
        return editor.getOption(45 /* gotoLocation */).multipleTypeDefinitions;
    }
}
registerEditorAction((_e = class GoToTypeDefinitionAction extends TypeDefinitionAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: false,
                muteMessage: false
            }, {
                id: GoToTypeDefinitionAction.ID,
                label: nls.localize('actions.goToTypeDefinition.label', "Go to Type Definition"),
                alias: 'Go to Type Definition',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasTypeDefinitionProvider, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                kbOpts: {
                    kbExpr: EditorContextKeys.editorTextFocus,
                    primary: 0,
                    weight: 100 /* EditorContrib */
                },
                contextMenuOpts: {
                    group: 'navigation',
                    order: 1.4
                },
                menuOpts: {
                    menuId: MenuId.MenubarGoMenu,
                    group: '4_symbol_nav',
                    order: 3,
                    title: nls.localize({ key: 'miGotoTypeDefinition', comment: ['&& denotes a mnemonic'] }, "Go to &&Type Definition")
                }
            });
        }
    },
    _e.ID = 'editor.action.goToTypeDefinition',
    _e));
registerEditorAction((_f = class PeekTypeDefinitionAction extends TypeDefinitionAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: true,
                muteMessage: false
            }, {
                id: PeekTypeDefinitionAction.ID,
                label: nls.localize('actions.peekTypeDefinition.label', "Peek Type Definition"),
                alias: 'Peek Type Definition',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasTypeDefinitionProvider, PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                contextMenuOpts: {
                    menuId: MenuId.EditorContextPeek,
                    group: 'peek',
                    order: 4
                }
            });
        }
    },
    _f.ID = 'editor.action.peekTypeDefinition',
    _f));
//#endregion
//#region --- IMPLEMENTATION
class ImplementationAction extends SymbolNavigationAction {
    _getLocationModel(model, position, token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(yield getImplementationsAtPosition(model, position, token), nls.localize('impl.title', 'Implementations'));
        });
    }
    _getNoResultFoundMessage(info) {
        return info && info.word
            ? nls.localize('goToImplementation.noResultWord', "No implementation found for '{0}'", info.word)
            : nls.localize('goToImplementation.generic.noResults', "No implementation found");
    }
    _getAlternativeCommand(editor) {
        return editor.getOption(45 /* gotoLocation */).alternativeImplementationCommand;
    }
    _getGoToPreference(editor) {
        return editor.getOption(45 /* gotoLocation */).multipleImplementations;
    }
}
registerEditorAction((_g = class GoToImplementationAction extends ImplementationAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: false,
                muteMessage: false
            }, {
                id: GoToImplementationAction.ID,
                label: nls.localize('actions.goToImplementation.label', "Go to Implementations"),
                alias: 'Go to Implementations',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasImplementationProvider, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                kbOpts: {
                    kbExpr: EditorContextKeys.editorTextFocus,
                    primary: 2048 /* CtrlCmd */ | 70 /* F12 */,
                    weight: 100 /* EditorContrib */
                },
                menuOpts: {
                    menuId: MenuId.MenubarGoMenu,
                    group: '4_symbol_nav',
                    order: 4,
                    title: nls.localize({ key: 'miGotoImplementation', comment: ['&& denotes a mnemonic'] }, "Go to &&Implementations")
                },
                contextMenuOpts: {
                    group: 'navigation',
                    order: 1.45
                }
            });
        }
    },
    _g.ID = 'editor.action.goToImplementation',
    _g));
registerEditorAction((_h = class PeekImplementationAction extends ImplementationAction {
        constructor() {
            super({
                openToSide: false,
                openInPeek: true,
                muteMessage: false
            }, {
                id: PeekImplementationAction.ID,
                label: nls.localize('actions.peekImplementation.label', "Peek Implementations"),
                alias: 'Peek Implementations',
                precondition: ContextKeyExpr.and(EditorContextKeys.hasImplementationProvider, PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
                kbOpts: {
                    kbExpr: EditorContextKeys.editorTextFocus,
                    primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 70 /* F12 */,
                    weight: 100 /* EditorContrib */
                },
                contextMenuOpts: {
                    menuId: MenuId.EditorContextPeek,
                    group: 'peek',
                    order: 5
                }
            });
        }
    },
    _h.ID = 'editor.action.peekImplementation',
    _h));
//#endregion
//#region --- REFERENCES
class ReferencesAction extends SymbolNavigationAction {
    _getNoResultFoundMessage(info) {
        return info
            ? nls.localize('references.no', "No references found for '{0}'", info.word)
            : nls.localize('references.noGeneric', "No references found");
    }
    _getAlternativeCommand(editor) {
        return editor.getOption(45 /* gotoLocation */).alternativeReferenceCommand;
    }
    _getGoToPreference(editor) {
        return editor.getOption(45 /* gotoLocation */).multipleReferences;
    }
}
registerEditorAction(class GoToReferencesAction extends ReferencesAction {
    constructor() {
        super({
            openToSide: false,
            openInPeek: false,
            muteMessage: false
        }, {
            id: 'editor.action.goToReferences',
            label: nls.localize('goToReferences.label', "Go to References"),
            alias: 'Go to References',
            precondition: ContextKeyExpr.and(EditorContextKeys.hasReferenceProvider, PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 70 /* F12 */,
                weight: 100 /* EditorContrib */
            },
            contextMenuOpts: {
                group: 'navigation',
                order: 1.45
            },
            menuOpts: {
                menuId: MenuId.MenubarGoMenu,
                group: '4_symbol_nav',
                order: 5,
                title: nls.localize({ key: 'miGotoReference', comment: ['&& denotes a mnemonic'] }, "Go to &&References")
            },
        });
    }
    _getLocationModel(model, position, token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(yield getReferencesAtPosition(model, position, true, token), nls.localize('ref.title', 'References'));
        });
    }
});
registerEditorAction(class PeekReferencesAction extends ReferencesAction {
    constructor() {
        super({
            openToSide: false,
            openInPeek: true,
            muteMessage: false
        }, {
            id: 'editor.action.referenceSearch.trigger',
            label: nls.localize('references.action.label', "Peek References"),
            alias: 'Peek References',
            precondition: ContextKeyExpr.and(EditorContextKeys.hasReferenceProvider, PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
            contextMenuOpts: {
                menuId: MenuId.EditorContextPeek,
                group: 'peek',
                order: 6
            }
        });
    }
    _getLocationModel(model, position, token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(yield getReferencesAtPosition(model, position, false, token), nls.localize('ref.title', 'References'));
        });
    }
});
//#endregion
//#region --- GENERIC goto symbols command
class GenericGoToLocationAction extends SymbolNavigationAction {
    constructor(config, _references, _gotoMultipleBehaviour) {
        super(config, {
            id: 'editor.action.goToLocation',
            label: nls.localize('label.generic', "Go To Any Symbol"),
            alias: 'Go To Any Symbol',
            precondition: ContextKeyExpr.and(PeekContext.notInPeekEditor, EditorContextKeys.isInWalkThroughSnippet.toNegated()),
        });
        this._references = _references;
        this._gotoMultipleBehaviour = _gotoMultipleBehaviour;
    }
    _getLocationModel(_model, _position, _token) {
        return __awaiter(this, void 0, void 0, function* () {
            return new ReferencesModel(this._references, nls.localize('generic.title', 'Locations'));
        });
    }
    _getNoResultFoundMessage(info) {
        return info && nls.localize('generic.noResult', "No results for '{0}'", info.word) || '';
    }
    _getGoToPreference(editor) {
        var _a;
        return (_a = this._gotoMultipleBehaviour) !== null && _a !== void 0 ? _a : editor.getOption(45 /* gotoLocation */).multipleReferences;
    }
    _getAlternativeCommand() { return ''; }
}
CommandsRegistry.registerCommand({
    id: 'editor.action.goToLocations',
    description: {
        description: 'Go to locations from a position in a file',
        args: [
            { name: 'uri', description: 'The text document in which to start', constraint: URI },
            { name: 'position', description: 'The position at which to start', constraint: corePosition.Position.isIPosition },
            { name: 'locations', description: 'An array of locations.', constraint: Array },
            { name: 'multiple', description: 'Define what to do when having multiple results, either `peek`, `gotoAndPeek`, or `goto' },
            { name: 'noResultsMessage', description: 'Human readable message that shows when locations is empty.' },
        ]
    },
    handler: (accessor, resource, position, references, multiple, noResultsMessage, openInPeek) => __awaiter(void 0, void 0, void 0, function* () {
        assertType(URI.isUri(resource));
        assertType(corePosition.Position.isIPosition(position));
        assertType(Array.isArray(references));
        assertType(typeof multiple === 'undefined' || typeof multiple === 'string');
        assertType(typeof openInPeek === 'undefined' || typeof openInPeek === 'boolean');
        const editorService = accessor.get(ICodeEditorService);
        const editor = yield editorService.openCodeEditor({ resource }, editorService.getFocusedCodeEditor());
        if (isCodeEditor(editor)) {
            editor.setPosition(position);
            editor.revealPositionInCenterIfOutsideViewport(position, 0 /* Smooth */);
            return editor.invokeWithinContext(accessor => {
                const command = new class extends GenericGoToLocationAction {
                    _getNoResultFoundMessage(info) {
                        return noResultsMessage || super._getNoResultFoundMessage(info);
                    }
                }({
                    muteMessage: !Boolean(noResultsMessage),
                    openInPeek: Boolean(openInPeek),
                    openToSide: false
                }, references, multiple);
                accessor.get(IInstantiationService).invokeFunction(command.run.bind(command), editor);
            });
        }
    })
});
CommandsRegistry.registerCommand({
    id: 'editor.action.peekLocations',
    description: {
        description: 'Peek locations from a position in a file',
        args: [
            { name: 'uri', description: 'The text document in which to start', constraint: URI },
            { name: 'position', description: 'The position at which to start', constraint: corePosition.Position.isIPosition },
            { name: 'locations', description: 'An array of locations.', constraint: Array },
            { name: 'multiple', description: 'Define what to do when having multiple results, either `peek`, `gotoAndPeek`, or `goto' },
        ]
    },
    handler: (accessor, resource, position, references, multiple) => __awaiter(void 0, void 0, void 0, function* () {
        accessor.get(ICommandService).executeCommand('editor.action.goToLocations', resource, position, references, multiple, undefined, true);
    })
});
//#endregion
//#region --- REFERENCE search special commands
CommandsRegistry.registerCommand({
    id: 'editor.action.findReferences',
    handler: (accessor, resource, position) => {
        assertType(URI.isUri(resource));
        assertType(corePosition.Position.isIPosition(position));
        const codeEditorService = accessor.get(ICodeEditorService);
        return codeEditorService.openCodeEditor({ resource }, codeEditorService.getFocusedCodeEditor()).then(control => {
            if (!isCodeEditor(control) || !control.hasModel()) {
                return undefined;
            }
            const controller = ReferencesController.get(control);
            if (!controller) {
                return undefined;
            }
            const references = createCancelablePromise(token => getReferencesAtPosition(control.getModel(), corePosition.Position.lift(position), false, token).then(references => new ReferencesModel(references, nls.localize('ref.title', 'References'))));
            const range = new Range(position.lineNumber, position.column, position.lineNumber, position.column);
            return Promise.resolve(controller.toggleWidget(range, references, false));
        });
    }
});
// use NEW command
CommandsRegistry.registerCommandAlias('editor.action.showReferences', 'editor.action.peekLocations');
//#endregion
