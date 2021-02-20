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
import * as strings from '../../../base/common/strings.js';
import * as dom from '../../../base/browser/dom.js';
import { StandardKeyboardEvent } from '../../../base/browser/keyboardEvent.js';
import { Emitter, Event } from '../../../base/common/event.js';
import { SimpleKeybinding, createKeybinding } from '../../../base/common/keyCodes.js';
import { ImmortalReference, toDisposable, DisposableStore, Disposable } from '../../../base/common/lifecycle.js';
import { OS, isLinux, isMacintosh } from '../../../base/common/platform.js';
import Severity from '../../../base/common/severity.js';
import { URI } from '../../../base/common/uri.js';
import { isCodeEditor } from '../../browser/editorBrowser.js';
import { ResourceTextEdit } from '../../browser/services/bulkEditService.js';
import { isDiffEditorConfigurationKey, isEditorConfigurationKey } from '../../common/config/commonEditorConfig.js';
import { EditOperation } from '../../common/core/editOperation.js';
import { Position as Pos } from '../../common/core/position.js';
import { Range } from '../../common/core/range.js';
import { IModelService } from '../../common/services/modelService.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { IConfigurationService } from '../../../platform/configuration/common/configuration.js';
import { Configuration, ConfigurationModel, DefaultConfigurationModel, ConfigurationChangeEvent } from '../../../platform/configuration/common/configurationModels.js';
import { AbstractKeybindingService } from '../../../platform/keybinding/common/abstractKeybindingService.js';
import { KeybindingResolver } from '../../../platform/keybinding/common/keybindingResolver.js';
import { KeybindingsRegistry } from '../../../platform/keybinding/common/keybindingsRegistry.js';
import { ResolvedKeybindingItem } from '../../../platform/keybinding/common/resolvedKeybindingItem.js';
import { USLayoutResolvedKeybinding } from '../../../platform/keybinding/common/usLayoutResolvedKeybinding.js';
import { NoOpNotification } from '../../../platform/notification/common/notification.js';
import { WorkspaceFolder } from '../../../platform/workspace/common/workspace.js';
import { SimpleServicesNLS } from '../../common/standaloneStrings.js';
export class SimpleModel {
    constructor(model) {
        this.disposed = false;
        this.model = model;
        this._onDispose = new Emitter();
    }
    get textEditorModel() {
        return this.model;
    }
    dispose() {
        this.disposed = true;
        this._onDispose.fire();
    }
}
function withTypedEditor(widget, codeEditorCallback, diffEditorCallback) {
    if (isCodeEditor(widget)) {
        // Single Editor
        return codeEditorCallback(widget);
    }
    else {
        // Diff Editor
        return diffEditorCallback(widget);
    }
}
let SimpleEditorModelResolverService = class SimpleEditorModelResolverService {
    constructor(modelService) {
        this.modelService = modelService;
    }
    setEditor(editor) {
        this.editor = editor;
    }
    createModelReference(resource) {
        let model = null;
        if (this.editor) {
            model = withTypedEditor(this.editor, (editor) => this.findModel(editor, resource), (diffEditor) => this.findModel(diffEditor.getOriginalEditor(), resource) || this.findModel(diffEditor.getModifiedEditor(), resource));
        }
        if (!model) {
            return Promise.reject(new Error(`Model not found`));
        }
        return Promise.resolve(new ImmortalReference(new SimpleModel(model)));
    }
    findModel(editor, resource) {
        let model = this.modelService.getModel(resource);
        if (model && model.uri.toString() !== resource.toString()) {
            return null;
        }
        return model;
    }
};
SimpleEditorModelResolverService = __decorate([
    __param(0, IModelService)
], SimpleEditorModelResolverService);
export { SimpleEditorModelResolverService };
export class SimpleEditorProgressService {
    show() {
        return SimpleEditorProgressService.NULL_PROGRESS_RUNNER;
    }
    showWhile(promise, delay) {
        return __awaiter(this, void 0, void 0, function* () {
            yield promise;
        });
    }
}
SimpleEditorProgressService.NULL_PROGRESS_RUNNER = {
    done: () => { },
    total: () => { },
    worked: () => { }
};
export class SimpleDialogService {
    confirm(confirmation) {
        return this.doConfirm(confirmation).then(confirmed => {
            return {
                confirmed,
                checkboxChecked: false // unsupported
            };
        });
    }
    doConfirm(confirmation) {
        let messageText = confirmation.message;
        if (confirmation.detail) {
            messageText = messageText + '\n\n' + confirmation.detail;
        }
        return Promise.resolve(window.confirm(messageText));
    }
    show(severity, message, buttons, options) {
        return Promise.resolve({ choice: 0 });
    }
}
export class SimpleNotificationService {
    info(message) {
        return this.notify({ severity: Severity.Info, message });
    }
    warn(message) {
        return this.notify({ severity: Severity.Warning, message });
    }
    error(error) {
        return this.notify({ severity: Severity.Error, message: error });
    }
    notify(notification) {
        switch (notification.severity) {
            case Severity.Error:
                console.error(notification.message);
                break;
            case Severity.Warning:
                console.warn(notification.message);
                break;
            default:
                console.log(notification.message);
                break;
        }
        return SimpleNotificationService.NO_OP;
    }
    status(message, options) {
        return Disposable.None;
    }
}
SimpleNotificationService.NO_OP = new NoOpNotification();
export class StandaloneCommandService {
    constructor(instantiationService) {
        this._onWillExecuteCommand = new Emitter();
        this._onDidExecuteCommand = new Emitter();
        this._instantiationService = instantiationService;
    }
    executeCommand(id, ...args) {
        const command = CommandsRegistry.getCommand(id);
        if (!command) {
            return Promise.reject(new Error(`command '${id}' not found`));
        }
        try {
            this._onWillExecuteCommand.fire({ commandId: id, args });
            const result = this._instantiationService.invokeFunction.apply(this._instantiationService, [command.handler, ...args]);
            this._onDidExecuteCommand.fire({ commandId: id, args });
            return Promise.resolve(result);
        }
        catch (err) {
            return Promise.reject(err);
        }
    }
}
export class StandaloneKeybindingService extends AbstractKeybindingService {
    constructor(contextKeyService, commandService, telemetryService, notificationService, logService, domNode) {
        super(contextKeyService, commandService, telemetryService, notificationService, logService);
        this._cachedResolver = null;
        this._dynamicKeybindings = [];
        this._register(dom.addDisposableListener(domNode, dom.EventType.KEY_DOWN, (e) => {
            let keyEvent = new StandardKeyboardEvent(e);
            let shouldPreventDefault = this._dispatch(keyEvent, keyEvent.target);
            if (shouldPreventDefault) {
                keyEvent.preventDefault();
                keyEvent.stopPropagation();
            }
        }));
    }
    addDynamicKeybinding(commandId, _keybinding, handler, when) {
        const keybinding = createKeybinding(_keybinding, OS);
        const toDispose = new DisposableStore();
        if (keybinding) {
            this._dynamicKeybindings.push({
                keybinding: keybinding,
                command: commandId,
                when: when,
                weight1: 1000,
                weight2: 0,
                extensionId: null,
                isBuiltinExtension: false
            });
            toDispose.add(toDisposable(() => {
                for (let i = 0; i < this._dynamicKeybindings.length; i++) {
                    let kb = this._dynamicKeybindings[i];
                    if (kb.command === commandId) {
                        this._dynamicKeybindings.splice(i, 1);
                        this.updateResolver({ source: 1 /* Default */ });
                        return;
                    }
                }
            }));
        }
        toDispose.add(CommandsRegistry.registerCommand(commandId, handler));
        this.updateResolver({ source: 1 /* Default */ });
        return toDispose;
    }
    updateResolver(event) {
        this._cachedResolver = null;
        this._onDidUpdateKeybindings.fire(event);
    }
    _getResolver() {
        if (!this._cachedResolver) {
            const defaults = this._toNormalizedKeybindingItems(KeybindingsRegistry.getDefaultKeybindings(), true);
            const overrides = this._toNormalizedKeybindingItems(this._dynamicKeybindings, false);
            this._cachedResolver = new KeybindingResolver(defaults, overrides, (str) => this._log(str));
        }
        return this._cachedResolver;
    }
    _documentHasFocus() {
        return document.hasFocus();
    }
    _toNormalizedKeybindingItems(items, isDefault) {
        let result = [], resultLen = 0;
        for (const item of items) {
            const when = item.when || undefined;
            const keybinding = item.keybinding;
            if (!keybinding) {
                // This might be a removal keybinding item in user settings => accept it
                result[resultLen++] = new ResolvedKeybindingItem(undefined, item.command, item.commandArgs, when, isDefault, null, false);
            }
            else {
                const resolvedKeybindings = this.resolveKeybinding(keybinding);
                for (const resolvedKeybinding of resolvedKeybindings) {
                    result[resultLen++] = new ResolvedKeybindingItem(resolvedKeybinding, item.command, item.commandArgs, when, isDefault, null, false);
                }
            }
        }
        return result;
    }
    resolveKeybinding(keybinding) {
        return [new USLayoutResolvedKeybinding(keybinding, OS)];
    }
    resolveKeyboardEvent(keyboardEvent) {
        let keybinding = new SimpleKeybinding(keyboardEvent.ctrlKey, keyboardEvent.shiftKey, keyboardEvent.altKey, keyboardEvent.metaKey, keyboardEvent.keyCode).toChord();
        return new USLayoutResolvedKeybinding(keybinding, OS);
    }
}
function isConfigurationOverrides(thing) {
    return thing
        && typeof thing === 'object'
        && (!thing.overrideIdentifier || typeof thing.overrideIdentifier === 'string')
        && (!thing.resource || thing.resource instanceof URI);
}
export class SimpleConfigurationService {
    constructor() {
        this._onDidChangeConfiguration = new Emitter();
        this.onDidChangeConfiguration = this._onDidChangeConfiguration.event;
        this._configuration = new Configuration(new DefaultConfigurationModel(), new ConfigurationModel());
    }
    getValue(arg1, arg2) {
        const section = typeof arg1 === 'string' ? arg1 : undefined;
        const overrides = isConfigurationOverrides(arg1) ? arg1 : isConfigurationOverrides(arg2) ? arg2 : {};
        return this._configuration.getValue(section, overrides, undefined);
    }
    updateValues(values) {
        const previous = { data: this._configuration.toData() };
        let changedKeys = [];
        for (const entry of values) {
            const [key, value] = entry;
            if (this.getValue(key) === value) {
                continue;
            }
            this._configuration.updateValue(key, value);
            changedKeys.push(key);
        }
        if (changedKeys.length > 0) {
            const configurationChangeEvent = new ConfigurationChangeEvent({ keys: changedKeys, overrides: [] }, previous, this._configuration);
            configurationChangeEvent.source = 7 /* MEMORY */;
            configurationChangeEvent.sourceConfig = null;
            this._onDidChangeConfiguration.fire(configurationChangeEvent);
        }
        return Promise.resolve();
    }
}
export class SimpleResourceConfigurationService {
    constructor(configurationService) {
        this.configurationService = configurationService;
        this._onDidChangeConfiguration = new Emitter();
        this.configurationService.onDidChangeConfiguration((e) => {
            this._onDidChangeConfiguration.fire({ affectedKeys: e.affectedKeys, affectsConfiguration: (resource, configuration) => e.affectsConfiguration(configuration) });
        });
    }
    getValue(resource, arg2, arg3) {
        const position = Pos.isIPosition(arg2) ? arg2 : null;
        const section = position ? (typeof arg3 === 'string' ? arg3 : undefined) : (typeof arg2 === 'string' ? arg2 : undefined);
        if (typeof section === 'undefined') {
            return this.configurationService.getValue();
        }
        return this.configurationService.getValue(section);
    }
}
let SimpleResourcePropertiesService = class SimpleResourcePropertiesService {
    constructor(configurationService) {
        this.configurationService = configurationService;
    }
    getEOL(resource, language) {
        const eol = this.configurationService.getValue('files.eol', { overrideIdentifier: language, resource });
        if (eol && eol !== 'auto') {
            return eol;
        }
        return (isLinux || isMacintosh) ? '\n' : '\r\n';
    }
};
SimpleResourcePropertiesService = __decorate([
    __param(0, IConfigurationService)
], SimpleResourcePropertiesService);
export { SimpleResourcePropertiesService };
export class StandaloneTelemetryService {
    publicLog(eventName, data) {
        return Promise.resolve(undefined);
    }
    publicLog2(eventName, data) {
        return this.publicLog(eventName, data);
    }
}
export class SimpleWorkspaceContextService {
    constructor() {
        const resource = URI.from({ scheme: SimpleWorkspaceContextService.SCHEME, authority: 'model', path: '/' });
        this.workspace = { id: '4064f6ec-cb38-4ad0-af64-ee6467e63c82', folders: [new WorkspaceFolder({ uri: resource, name: '', index: 0 })] };
    }
    getWorkspace() {
        return this.workspace;
    }
}
SimpleWorkspaceContextService.SCHEME = 'inmemory';
export function updateConfigurationService(configurationService, source, isDiffEditor) {
    if (!source) {
        return;
    }
    if (!(configurationService instanceof SimpleConfigurationService)) {
        return;
    }
    let toUpdate = [];
    Object.keys(source).forEach((key) => {
        if (isEditorConfigurationKey(key)) {
            toUpdate.push([`editor.${key}`, source[key]]);
        }
        if (isDiffEditor && isDiffEditorConfigurationKey(key)) {
            toUpdate.push([`diffEditor.${key}`, source[key]]);
        }
    });
    if (toUpdate.length > 0) {
        configurationService.updateValues(toUpdate);
    }
}
export class SimpleBulkEditService {
    constructor(_modelService) {
        this._modelService = _modelService;
        //
    }
    hasPreviewHandler() {
        return false;
    }
    apply(edits, _options) {
        return __awaiter(this, void 0, void 0, function* () {
            const textEdits = new Map();
            for (let edit of edits) {
                if (!(edit instanceof ResourceTextEdit)) {
                    throw new Error('bad edit - only text edits are supported');
                }
                const model = this._modelService.getModel(edit.resource);
                if (!model) {
                    throw new Error('bad edit - model not found');
                }
                if (typeof edit.versionId === 'number' && model.getVersionId() !== edit.versionId) {
                    throw new Error('bad state - model changed in the meantime');
                }
                let array = textEdits.get(model);
                if (!array) {
                    array = [];
                    textEdits.set(model, array);
                }
                array.push(EditOperation.replaceMove(Range.lift(edit.textEdit.range), edit.textEdit.text));
            }
            let totalEdits = 0;
            let totalFiles = 0;
            for (const [model, edits] of textEdits) {
                model.pushStackElement();
                model.pushEditOperations([], edits, () => []);
                model.pushStackElement();
                totalFiles += 1;
                totalEdits += edits.length;
            }
            return {
                ariaSummary: strings.format(SimpleServicesNLS.bulkEditServiceSummary, totalEdits, totalFiles)
            };
        });
    }
}
export class SimpleUriLabelService {
    getUriLabel(resource, options) {
        if (resource.scheme === 'file') {
            return resource.fsPath;
        }
        return resource.path;
    }
}
export class SimpleLayoutService {
    constructor(_codeEditorService, _container) {
        this._codeEditorService = _codeEditorService;
        this._container = _container;
        this.onLayout = Event.None;
    }
    get dimension() {
        if (!this._dimension) {
            this._dimension = dom.getClientArea(window.document.body);
        }
        return this._dimension;
    }
    get container() {
        return this._container;
    }
    focus() {
        var _a;
        (_a = this._codeEditorService.getFocusedCodeEditor()) === null || _a === void 0 ? void 0 : _a.focus();
    }
}
