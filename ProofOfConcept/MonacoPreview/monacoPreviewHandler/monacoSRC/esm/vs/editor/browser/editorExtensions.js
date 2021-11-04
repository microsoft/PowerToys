/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from '../../nls.js';
import { URI } from '../../base/common/uri.js';
import { ICodeEditorService } from './services/codeEditorService.js';
import { Position } from '../common/core/position.js';
import { IModelService } from '../common/services/modelService.js';
import { ITextModelService } from '../common/services/resolverService.js';
import { MenuId, MenuRegistry } from '../../platform/actions/common/actions.js';
import { CommandsRegistry } from '../../platform/commands/common/commands.js';
import { ContextKeyExpr, IContextKeyService } from '../../platform/contextkey/common/contextkey.js';
import { KeybindingsRegistry } from '../../platform/keybinding/common/keybindingsRegistry.js';
import { Registry } from '../../platform/registry/common/platform.js';
import { ITelemetryService } from '../../platform/telemetry/common/telemetry.js';
import { withNullAsUndefined, assertType } from '../../base/common/types.js';
export class Command {
    constructor(opts) {
        this.id = opts.id;
        this.precondition = opts.precondition;
        this._kbOpts = opts.kbOpts;
        this._menuOpts = opts.menuOpts;
        this._description = opts.description;
    }
    register() {
        if (Array.isArray(this._menuOpts)) {
            this._menuOpts.forEach(this._registerMenuItem, this);
        }
        else if (this._menuOpts) {
            this._registerMenuItem(this._menuOpts);
        }
        if (this._kbOpts) {
            let kbWhen = this._kbOpts.kbExpr;
            if (this.precondition) {
                if (kbWhen) {
                    kbWhen = ContextKeyExpr.and(kbWhen, this.precondition);
                }
                else {
                    kbWhen = this.precondition;
                }
            }
            KeybindingsRegistry.registerCommandAndKeybindingRule({
                id: this.id,
                handler: (accessor, args) => this.runCommand(accessor, args),
                weight: this._kbOpts.weight,
                args: this._kbOpts.args,
                when: kbWhen,
                primary: this._kbOpts.primary,
                secondary: this._kbOpts.secondary,
                win: this._kbOpts.win,
                linux: this._kbOpts.linux,
                mac: this._kbOpts.mac,
                description: this._description
            });
        }
        else {
            CommandsRegistry.registerCommand({
                id: this.id,
                handler: (accessor, args) => this.runCommand(accessor, args),
                description: this._description
            });
        }
    }
    _registerMenuItem(item) {
        MenuRegistry.appendMenuItem(item.menuId, {
            group: item.group,
            command: {
                id: this.id,
                title: item.title,
                icon: item.icon,
                precondition: this.precondition
            },
            when: item.when,
            order: item.order
        });
    }
}
export class MultiCommand extends Command {
    constructor() {
        super(...arguments);
        this._implementations = [];
    }
    /**
     * A higher priority gets to be looked at first
     */
    addImplementation(priority, implementation) {
        this._implementations.push([priority, implementation]);
        this._implementations.sort((a, b) => b[0] - a[0]);
        return {
            dispose: () => {
                for (let i = 0; i < this._implementations.length; i++) {
                    if (this._implementations[i][1] === implementation) {
                        this._implementations.splice(i, 1);
                        return;
                    }
                }
            }
        };
    }
    runCommand(accessor, args) {
        for (const impl of this._implementations) {
            const result = impl[1](accessor, args);
            if (result) {
                if (typeof result === 'boolean') {
                    return;
                }
                return result;
            }
        }
    }
}
//#endregion
/**
 * A command that delegates to another command's implementation.
 *
 * This lets different commands be registered but share the same implementation
 */
export class ProxyCommand extends Command {
    constructor(command, opts) {
        super(opts);
        this.command = command;
    }
    runCommand(accessor, args) {
        return this.command.runCommand(accessor, args);
    }
}
export class EditorCommand extends Command {
    /**
     * Create a command class that is bound to a certain editor contribution.
     */
    static bindToContribution(controllerGetter) {
        return class EditorControllerCommandImpl extends EditorCommand {
            constructor(opts) {
                super(opts);
                this._callback = opts.handler;
            }
            runEditorCommand(accessor, editor, args) {
                const controller = controllerGetter(editor);
                if (controller) {
                    this._callback(controllerGetter(editor), args);
                }
            }
        };
    }
    runCommand(accessor, args) {
        const codeEditorService = accessor.get(ICodeEditorService);
        // Find the editor with text focus or active
        const editor = codeEditorService.getFocusedCodeEditor() || codeEditorService.getActiveCodeEditor();
        if (!editor) {
            // well, at least we tried...
            return;
        }
        return editor.invokeWithinContext((editorAccessor) => {
            const kbService = editorAccessor.get(IContextKeyService);
            if (!kbService.contextMatchesRules(withNullAsUndefined(this.precondition))) {
                // precondition does not hold
                return;
            }
            return this.runEditorCommand(editorAccessor, editor, args);
        });
    }
}
export class EditorAction extends EditorCommand {
    constructor(opts) {
        super(EditorAction.convertOptions(opts));
        this.label = opts.label;
        this.alias = opts.alias;
    }
    static convertOptions(opts) {
        let menuOpts;
        if (Array.isArray(opts.menuOpts)) {
            menuOpts = opts.menuOpts;
        }
        else if (opts.menuOpts) {
            menuOpts = [opts.menuOpts];
        }
        else {
            menuOpts = [];
        }
        function withDefaults(item) {
            if (!item.menuId) {
                item.menuId = MenuId.EditorContext;
            }
            if (!item.title) {
                item.title = opts.label;
            }
            item.when = ContextKeyExpr.and(opts.precondition, item.when);
            return item;
        }
        if (Array.isArray(opts.contextMenuOpts)) {
            menuOpts.push(...opts.contextMenuOpts.map(withDefaults));
        }
        else if (opts.contextMenuOpts) {
            menuOpts.push(withDefaults(opts.contextMenuOpts));
        }
        opts.menuOpts = menuOpts;
        return opts;
    }
    runEditorCommand(accessor, editor, args) {
        this.reportTelemetry(accessor, editor);
        return this.run(accessor, editor, args || {});
    }
    reportTelemetry(accessor, editor) {
        accessor.get(ITelemetryService).publicLog2('editorActionInvoked', { name: this.label, id: this.id });
    }
}
export class MultiEditorAction extends EditorAction {
    constructor(opts) {
        super(opts);
        this._implementations = [];
    }
    runEditorCommand(accessor, editor, args) {
        this.reportTelemetry(accessor, editor);
        for (const impl of this._implementations) {
            if (impl[1](accessor, args)) {
                return;
            }
        }
        return this.run(accessor, editor, args || {});
    }
}
//#endregion
// --- Registration of commands and actions
export function registerModelAndPositionCommand(id, handler) {
    CommandsRegistry.registerCommand(id, function (accessor, ...args) {
        const [resource, position] = args;
        assertType(URI.isUri(resource));
        assertType(Position.isIPosition(position));
        const model = accessor.get(IModelService).getModel(resource);
        if (model) {
            const editorPosition = Position.lift(position);
            return handler(model, editorPosition, ...args.slice(2));
        }
        return accessor.get(ITextModelService).createModelReference(resource).then(reference => {
            return new Promise((resolve, reject) => {
                try {
                    const result = handler(reference.object.textEditorModel, Position.lift(position), args.slice(2));
                    resolve(result);
                }
                catch (err) {
                    reject(err);
                }
            }).finally(() => {
                reference.dispose();
            });
        });
    });
}
export function registerModelCommand(id, handler) {
    CommandsRegistry.registerCommand(id, function (accessor, ...args) {
        const [resource] = args;
        assertType(URI.isUri(resource));
        const model = accessor.get(IModelService).getModel(resource);
        if (model) {
            return handler(model, ...args.slice(1));
        }
        return accessor.get(ITextModelService).createModelReference(resource).then(reference => {
            return new Promise((resolve, reject) => {
                try {
                    const result = handler(reference.object.textEditorModel, args.slice(1));
                    resolve(result);
                }
                catch (err) {
                    reject(err);
                }
            }).finally(() => {
                reference.dispose();
            });
        });
    });
}
export function registerEditorCommand(editorCommand) {
    EditorContributionRegistry.INSTANCE.registerEditorCommand(editorCommand);
    return editorCommand;
}
export function registerEditorAction(ctor) {
    const action = new ctor();
    EditorContributionRegistry.INSTANCE.registerEditorAction(action);
    return action;
}
export function registerMultiEditorAction(action) {
    EditorContributionRegistry.INSTANCE.registerEditorAction(action);
    return action;
}
export function registerInstantiatedEditorAction(editorAction) {
    EditorContributionRegistry.INSTANCE.registerEditorAction(editorAction);
}
export function registerEditorContribution(id, ctor) {
    EditorContributionRegistry.INSTANCE.registerEditorContribution(id, ctor);
}
export var EditorExtensionsRegistry;
(function (EditorExtensionsRegistry) {
    function getEditorCommand(commandId) {
        return EditorContributionRegistry.INSTANCE.getEditorCommand(commandId);
    }
    EditorExtensionsRegistry.getEditorCommand = getEditorCommand;
    function getEditorActions() {
        return EditorContributionRegistry.INSTANCE.getEditorActions();
    }
    EditorExtensionsRegistry.getEditorActions = getEditorActions;
    function getEditorContributions() {
        return EditorContributionRegistry.INSTANCE.getEditorContributions();
    }
    EditorExtensionsRegistry.getEditorContributions = getEditorContributions;
    function getSomeEditorContributions(ids) {
        return EditorContributionRegistry.INSTANCE.getEditorContributions().filter(c => ids.indexOf(c.id) >= 0);
    }
    EditorExtensionsRegistry.getSomeEditorContributions = getSomeEditorContributions;
    function getDiffEditorContributions() {
        return EditorContributionRegistry.INSTANCE.getDiffEditorContributions();
    }
    EditorExtensionsRegistry.getDiffEditorContributions = getDiffEditorContributions;
})(EditorExtensionsRegistry || (EditorExtensionsRegistry = {}));
// Editor extension points
const Extensions = {
    EditorCommonContributions: 'editor.contributions'
};
class EditorContributionRegistry {
    constructor() {
        this.editorContributions = [];
        this.diffEditorContributions = [];
        this.editorActions = [];
        this.editorCommands = Object.create(null);
    }
    registerEditorContribution(id, ctor) {
        this.editorContributions.push({ id, ctor: ctor });
    }
    getEditorContributions() {
        return this.editorContributions.slice(0);
    }
    getDiffEditorContributions() {
        return this.diffEditorContributions.slice(0);
    }
    registerEditorAction(action) {
        action.register();
        this.editorActions.push(action);
    }
    getEditorActions() {
        return this.editorActions.slice(0);
    }
    registerEditorCommand(editorCommand) {
        editorCommand.register();
        this.editorCommands[editorCommand.id] = editorCommand;
    }
    getEditorCommand(commandId) {
        return (this.editorCommands[commandId] || null);
    }
}
EditorContributionRegistry.INSTANCE = new EditorContributionRegistry();
Registry.add(Extensions.EditorCommonContributions, EditorContributionRegistry.INSTANCE);
function registerCommand(command) {
    command.register();
    return command;
}
export const UndoCommand = registerCommand(new MultiCommand({
    id: 'undo',
    precondition: undefined,
    kbOpts: {
        weight: 0 /* EditorCore */,
        primary: 2048 /* CtrlCmd */ | 56 /* KEY_Z */
    },
    menuOpts: [{
            menuId: MenuId.MenubarEditMenu,
            group: '1_do',
            title: nls.localize({ key: 'miUndo', comment: ['&& denotes a mnemonic'] }, "&&Undo"),
            order: 1
        }, {
            menuId: MenuId.CommandPalette,
            group: '',
            title: nls.localize('undo', "Undo"),
            order: 1
        }]
}));
registerCommand(new ProxyCommand(UndoCommand, { id: 'default:undo', precondition: undefined }));
export const RedoCommand = registerCommand(new MultiCommand({
    id: 'redo',
    precondition: undefined,
    kbOpts: {
        weight: 0 /* EditorCore */,
        primary: 2048 /* CtrlCmd */ | 55 /* KEY_Y */,
        secondary: [2048 /* CtrlCmd */ | 1024 /* Shift */ | 56 /* KEY_Z */],
        mac: { primary: 2048 /* CtrlCmd */ | 1024 /* Shift */ | 56 /* KEY_Z */ }
    },
    menuOpts: [{
            menuId: MenuId.MenubarEditMenu,
            group: '1_do',
            title: nls.localize({ key: 'miRedo', comment: ['&& denotes a mnemonic'] }, "&&Redo"),
            order: 2
        }, {
            menuId: MenuId.CommandPalette,
            group: '',
            title: nls.localize('redo', "Redo"),
            order: 1
        }]
}));
registerCommand(new ProxyCommand(RedoCommand, { id: 'default:redo', precondition: undefined }));
export const SelectAllCommand = registerCommand(new MultiCommand({
    id: 'editor.action.selectAll',
    precondition: undefined,
    kbOpts: {
        weight: 0 /* EditorCore */,
        kbExpr: null,
        primary: 2048 /* CtrlCmd */ | 31 /* KEY_A */
    },
    menuOpts: [{
            menuId: MenuId.MenubarSelectionMenu,
            group: '1_basic',
            title: nls.localize({ key: 'miSelectAll', comment: ['&& denotes a mnemonic'] }, "&&Select All"),
            order: 1
        }, {
            menuId: MenuId.CommandPalette,
            group: '',
            title: nls.localize('selectAll', "Select All"),
            order: 1
        }]
}));
