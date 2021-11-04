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
import { CancellationToken } from '../../../base/common/cancellation.js';
import { Lazy } from '../../../base/common/lazy.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { escapeRegExpCharacters } from '../../../base/common/strings.js';
import { EditorAction, EditorCommand } from '../../browser/editorExtensions.js';
import { IBulkEditService, ResourceEdit } from '../../browser/services/bulkEditService.js';
import { EditorContextKeys } from '../../common/editorContextKeys.js';
import { codeActionCommandId, fixAllCommandId, organizeImportsCommandId, refactorCommandId, sourceActionCommandId } from './codeAction.js';
import { CodeActionUi } from './codeActionUi.js';
import { MessageController } from '../message/messageController.js';
import * as nls from '../../../nls.js';
import { ICommandService } from '../../../platform/commands/common/commands.js';
import { ContextKeyExpr, IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { IInstantiationService } from '../../../platform/instantiation/common/instantiation.js';
import { IMarkerService } from '../../../platform/markers/common/markers.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { IEditorProgressService } from '../../../platform/progress/common/progress.js';
import { ITelemetryService } from '../../../platform/telemetry/common/telemetry.js';
import { CodeActionModel, SUPPORTED_CODE_ACTIONS } from './codeActionModel.js';
import { CodeActionCommandArgs, CodeActionKind } from './types.js';
function contextKeyForSupportedActions(kind) {
    return ContextKeyExpr.regex(SUPPORTED_CODE_ACTIONS.keys()[0], new RegExp('(\\s|^)' + escapeRegExpCharacters(kind.value) + '\\b'));
}
const argsSchema = {
    type: 'object',
    defaultSnippets: [{ body: { kind: '' } }],
    properties: {
        'kind': {
            type: 'string',
            description: nls.localize('args.schema.kind', "Kind of the code action to run."),
        },
        'apply': {
            type: 'string',
            description: nls.localize('args.schema.apply', "Controls when the returned actions are applied."),
            default: "ifSingle" /* IfSingle */,
            enum: ["first" /* First */, "ifSingle" /* IfSingle */, "never" /* Never */],
            enumDescriptions: [
                nls.localize('args.schema.apply.first', "Always apply the first returned code action."),
                nls.localize('args.schema.apply.ifSingle', "Apply the first returned code action if it is the only one."),
                nls.localize('args.schema.apply.never', "Do not apply the returned code actions."),
            ]
        },
        'preferred': {
            type: 'boolean',
            default: false,
            description: nls.localize('args.schema.preferred', "Controls if only preferred code actions should be returned."),
        }
    }
};
let QuickFixController = class QuickFixController extends Disposable {
    constructor(editor, markerService, contextKeyService, progressService, _instantiationService) {
        super();
        this._instantiationService = _instantiationService;
        this._editor = editor;
        this._model = this._register(new CodeActionModel(this._editor, markerService, contextKeyService, progressService));
        this._register(this._model.onDidChangeState(newState => this.update(newState)));
        this._ui = new Lazy(() => this._register(new CodeActionUi(editor, QuickFixAction.Id, AutoFixAction.Id, {
            applyCodeAction: (action, retrigger) => __awaiter(this, void 0, void 0, function* () {
                try {
                    yield this._applyCodeAction(action);
                }
                finally {
                    if (retrigger) {
                        this._trigger({ type: 1 /* Auto */, filter: {} });
                    }
                }
            })
        }, this._instantiationService)));
    }
    static get(editor) {
        return editor.getContribution(QuickFixController.ID);
    }
    update(newState) {
        this._ui.getValue().update(newState);
    }
    showCodeActions(trigger, actions, at) {
        return this._ui.getValue().showCodeActionList(trigger, actions, at, { includeDisabledActions: false });
    }
    manualTriggerAtCurrentPosition(notAvailableMessage, filter, autoApply) {
        if (!this._editor.hasModel()) {
            return;
        }
        MessageController.get(this._editor).closeMessage();
        const triggerPosition = this._editor.getPosition();
        this._trigger({ type: 2 /* Manual */, filter, autoApply, context: { notAvailableMessage, position: triggerPosition } });
    }
    _trigger(trigger) {
        return this._model.trigger(trigger);
    }
    _applyCodeAction(action) {
        return this._instantiationService.invokeFunction(applyCodeAction, action, this._editor);
    }
};
QuickFixController.ID = 'editor.contrib.quickFixController';
QuickFixController = __decorate([
    __param(1, IMarkerService),
    __param(2, IContextKeyService),
    __param(3, IEditorProgressService),
    __param(4, IInstantiationService)
], QuickFixController);
export { QuickFixController };
export function applyCodeAction(accessor, item, editor) {
    return __awaiter(this, void 0, void 0, function* () {
        const bulkEditService = accessor.get(IBulkEditService);
        const commandService = accessor.get(ICommandService);
        const telemetryService = accessor.get(ITelemetryService);
        const notificationService = accessor.get(INotificationService);
        telemetryService.publicLog2('codeAction.applyCodeAction', {
            codeActionTitle: item.action.title,
            codeActionKind: item.action.kind,
            codeActionIsPreferred: !!item.action.isPreferred,
        });
        yield item.resolve(CancellationToken.None);
        if (item.action.edit) {
            yield bulkEditService.apply(ResourceEdit.convert(item.action.edit), { editor, label: item.action.title });
        }
        if (item.action.command) {
            try {
                yield commandService.executeCommand(item.action.command.id, ...(item.action.command.arguments || []));
            }
            catch (err) {
                const message = asMessage(err);
                notificationService.error(typeof message === 'string'
                    ? message
                    : nls.localize('applyCodeActionFailed', "An unknown error occurred while applying the code action"));
            }
        }
    });
}
function asMessage(err) {
    if (typeof err === 'string') {
        return err;
    }
    else if (err instanceof Error && typeof err.message === 'string') {
        return err.message;
    }
    else {
        return undefined;
    }
}
function triggerCodeActionsForEditorSelection(editor, notAvailableMessage, filter, autoApply) {
    if (editor.hasModel()) {
        const controller = QuickFixController.get(editor);
        if (controller) {
            controller.manualTriggerAtCurrentPosition(notAvailableMessage, filter, autoApply);
        }
    }
}
export class QuickFixAction extends EditorAction {
    constructor() {
        super({
            id: QuickFixAction.Id,
            label: nls.localize('quickfix.trigger.label', "Quick Fix..."),
            alias: 'Quick Fix...',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasCodeActionsProvider),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 84 /* US_DOT */,
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(_accessor, editor) {
        return triggerCodeActionsForEditorSelection(editor, nls.localize('editor.action.quickFix.noneMessage', "No code actions available"), undefined, undefined);
    }
}
QuickFixAction.Id = 'editor.action.quickFix';
export class CodeActionCommand extends EditorCommand {
    constructor() {
        super({
            id: codeActionCommandId,
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasCodeActionsProvider),
            description: {
                description: 'Trigger a code action',
                args: [{ name: 'args', schema: argsSchema, }]
            }
        });
    }
    runEditorCommand(_accessor, editor, userArgs) {
        const args = CodeActionCommandArgs.fromUser(userArgs, {
            kind: CodeActionKind.Empty,
            apply: "ifSingle" /* IfSingle */,
        });
        return triggerCodeActionsForEditorSelection(editor, typeof (userArgs === null || userArgs === void 0 ? void 0 : userArgs.kind) === 'string'
            ? args.preferred
                ? nls.localize('editor.action.codeAction.noneMessage.preferred.kind', "No preferred code actions for '{0}' available", userArgs.kind)
                : nls.localize('editor.action.codeAction.noneMessage.kind', "No code actions for '{0}' available", userArgs.kind)
            : args.preferred
                ? nls.localize('editor.action.codeAction.noneMessage.preferred', "No preferred code actions available")
                : nls.localize('editor.action.codeAction.noneMessage', "No code actions available"), {
            include: args.kind,
            includeSourceActions: true,
            onlyIncludePreferredActions: args.preferred,
        }, args.apply);
    }
}
export class RefactorAction extends EditorAction {
    constructor() {
        super({
            id: refactorCommandId,
            label: nls.localize('refactor.label', "Refactor..."),
            alias: 'Refactor...',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasCodeActionsProvider),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 48 /* KEY_R */,
                mac: {
                    primary: 256 /* WinCtrl */ | 1024 /* Shift */ | 48 /* KEY_R */
                },
                weight: 100 /* EditorContrib */
            },
            contextMenuOpts: {
                group: '1_modification',
                order: 2,
                when: ContextKeyExpr.and(EditorContextKeys.writable, contextKeyForSupportedActions(CodeActionKind.Refactor)),
            },
            description: {
                description: 'Refactor...',
                args: [{ name: 'args', schema: argsSchema }]
            }
        });
    }
    run(_accessor, editor, userArgs) {
        const args = CodeActionCommandArgs.fromUser(userArgs, {
            kind: CodeActionKind.Refactor,
            apply: "never" /* Never */
        });
        return triggerCodeActionsForEditorSelection(editor, typeof (userArgs === null || userArgs === void 0 ? void 0 : userArgs.kind) === 'string'
            ? args.preferred
                ? nls.localize('editor.action.refactor.noneMessage.preferred.kind', "No preferred refactorings for '{0}' available", userArgs.kind)
                : nls.localize('editor.action.refactor.noneMessage.kind', "No refactorings for '{0}' available", userArgs.kind)
            : args.preferred
                ? nls.localize('editor.action.refactor.noneMessage.preferred', "No preferred refactorings available")
                : nls.localize('editor.action.refactor.noneMessage', "No refactorings available"), {
            include: CodeActionKind.Refactor.contains(args.kind) ? args.kind : CodeActionKind.None,
            onlyIncludePreferredActions: args.preferred,
        }, args.apply);
    }
}
export class SourceAction extends EditorAction {
    constructor() {
        super({
            id: sourceActionCommandId,
            label: nls.localize('source.label', "Source Action..."),
            alias: 'Source Action...',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, EditorContextKeys.hasCodeActionsProvider),
            contextMenuOpts: {
                group: '1_modification',
                order: 2.1,
                when: ContextKeyExpr.and(EditorContextKeys.writable, contextKeyForSupportedActions(CodeActionKind.Source)),
            },
            description: {
                description: 'Source Action...',
                args: [{ name: 'args', schema: argsSchema }]
            }
        });
    }
    run(_accessor, editor, userArgs) {
        const args = CodeActionCommandArgs.fromUser(userArgs, {
            kind: CodeActionKind.Source,
            apply: "never" /* Never */
        });
        return triggerCodeActionsForEditorSelection(editor, typeof (userArgs === null || userArgs === void 0 ? void 0 : userArgs.kind) === 'string'
            ? args.preferred
                ? nls.localize('editor.action.source.noneMessage.preferred.kind', "No preferred source actions for '{0}' available", userArgs.kind)
                : nls.localize('editor.action.source.noneMessage.kind', "No source actions for '{0}' available", userArgs.kind)
            : args.preferred
                ? nls.localize('editor.action.source.noneMessage.preferred', "No preferred source actions available")
                : nls.localize('editor.action.source.noneMessage', "No source actions available"), {
            include: CodeActionKind.Source.contains(args.kind) ? args.kind : CodeActionKind.None,
            includeSourceActions: true,
            onlyIncludePreferredActions: args.preferred,
        }, args.apply);
    }
}
export class OrganizeImportsAction extends EditorAction {
    constructor() {
        super({
            id: organizeImportsCommandId,
            label: nls.localize('organizeImports.label', "Organize Imports"),
            alias: 'Organize Imports',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, contextKeyForSupportedActions(CodeActionKind.SourceOrganizeImports)),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 1024 /* Shift */ | 512 /* Alt */ | 45 /* KEY_O */,
                weight: 100 /* EditorContrib */
            },
        });
    }
    run(_accessor, editor) {
        return triggerCodeActionsForEditorSelection(editor, nls.localize('editor.action.organize.noneMessage', "No organize imports action available"), { include: CodeActionKind.SourceOrganizeImports, includeSourceActions: true }, "ifSingle" /* IfSingle */);
    }
}
export class FixAllAction extends EditorAction {
    constructor() {
        super({
            id: fixAllCommandId,
            label: nls.localize('fixAll.label', "Fix All"),
            alias: 'Fix All',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, contextKeyForSupportedActions(CodeActionKind.SourceFixAll))
        });
    }
    run(_accessor, editor) {
        return triggerCodeActionsForEditorSelection(editor, nls.localize('fixAll.noneMessage', "No fix all action available"), { include: CodeActionKind.SourceFixAll, includeSourceActions: true }, "ifSingle" /* IfSingle */);
    }
}
export class AutoFixAction extends EditorAction {
    constructor() {
        super({
            id: AutoFixAction.Id,
            label: nls.localize('autoFix.label', "Auto Fix..."),
            alias: 'Auto Fix...',
            precondition: ContextKeyExpr.and(EditorContextKeys.writable, contextKeyForSupportedActions(CodeActionKind.QuickFix)),
            kbOpts: {
                kbExpr: EditorContextKeys.editorTextFocus,
                primary: 512 /* Alt */ | 1024 /* Shift */ | 84 /* US_DOT */,
                mac: {
                    primary: 2048 /* CtrlCmd */ | 512 /* Alt */ | 84 /* US_DOT */
                },
                weight: 100 /* EditorContrib */
            }
        });
    }
    run(_accessor, editor) {
        return triggerCodeActionsForEditorSelection(editor, nls.localize('editor.action.autoFix.noneMessage', "No auto fixes available"), {
            include: CodeActionKind.QuickFix,
            onlyIncludePreferredActions: true
        }, "ifSingle" /* IfSingle */);
    }
}
AutoFixAction.Id = 'editor.action.autoFix';
