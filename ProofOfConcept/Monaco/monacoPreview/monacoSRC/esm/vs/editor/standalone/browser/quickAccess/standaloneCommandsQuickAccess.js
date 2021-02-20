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
import { Registry } from '../../../../platform/registry/common/platform.js';
import { Extensions } from '../../../../platform/quickinput/common/quickAccess.js';
import { QuickCommandNLS } from '../../../common/standaloneStrings.js';
import { ICodeEditorService } from '../../../browser/services/codeEditorService.js';
import { AbstractEditorCommandsQuickAccessProvider } from '../../../contrib/quickAccess/commandsQuickAccess.js';
import { withNullAsUndefined } from '../../../../base/common/types.js';
import { IInstantiationService } from '../../../../platform/instantiation/common/instantiation.js';
import { IKeybindingService } from '../../../../platform/keybinding/common/keybinding.js';
import { ICommandService } from '../../../../platform/commands/common/commands.js';
import { ITelemetryService } from '../../../../platform/telemetry/common/telemetry.js';
import { INotificationService } from '../../../../platform/notification/common/notification.js';
import { EditorAction, registerEditorAction } from '../../../browser/editorExtensions.js';
import { EditorContextKeys } from '../../../common/editorContextKeys.js';
import { IQuickInputService } from '../../../../platform/quickinput/common/quickInput.js';
let StandaloneCommandsQuickAccessProvider = class StandaloneCommandsQuickAccessProvider extends AbstractEditorCommandsQuickAccessProvider {
    constructor(instantiationService, codeEditorService, keybindingService, commandService, telemetryService, notificationService) {
        super({ showAlias: false }, instantiationService, keybindingService, commandService, telemetryService, notificationService);
        this.codeEditorService = codeEditorService;
    }
    get activeTextEditorControl() { return withNullAsUndefined(this.codeEditorService.getFocusedCodeEditor()); }
    getCommandPicks() {
        return __awaiter(this, void 0, void 0, function* () {
            return this.getCodeEditorCommandPicks();
        });
    }
};
StandaloneCommandsQuickAccessProvider = __decorate([
    __param(0, IInstantiationService),
    __param(1, ICodeEditorService),
    __param(2, IKeybindingService),
    __param(3, ICommandService),
    __param(4, ITelemetryService),
    __param(5, INotificationService)
], StandaloneCommandsQuickAccessProvider);
export { StandaloneCommandsQuickAccessProvider };
Registry.as(Extensions.Quickaccess).registerQuickAccessProvider({
    ctor: StandaloneCommandsQuickAccessProvider,
    prefix: StandaloneCommandsQuickAccessProvider.PREFIX,
    helpEntries: [{ description: QuickCommandNLS.quickCommandHelp, needsEditor: true }]
});
export class GotoLineAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.quickCommand',
            label: QuickCommandNLS.quickCommandActionLabel,
            alias: 'Command Palette',
            precondition: undefined,
            kbOpts: {
                kbExpr: EditorContextKeys.focus,
                primary: 59 /* F1 */,
                weight: 100 /* EditorContrib */
            },
            contextMenuOpts: {
                group: 'z_commands',
                order: 1
            }
        });
    }
    run(accessor) {
        accessor.get(IQuickInputService).quickAccess.show(StandaloneCommandsQuickAccessProvider.PREFIX);
    }
}
registerEditorAction(GotoLineAction);
