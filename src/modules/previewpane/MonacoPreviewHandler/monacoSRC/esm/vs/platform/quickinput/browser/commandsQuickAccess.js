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
import { localize } from '../../../nls.js';
import { PickerQuickAccessProvider } from './pickerQuickAccess.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { or, matchesPrefix, matchesWords, matchesContiguousSubString } from '../../../base/common/filters.js';
import { withNullAsUndefined } from '../../../base/common/types.js';
import { LRUCache } from '../../../base/common/map.js';
import { IStorageService } from '../../storage/common/storage.js';
import { IConfigurationService } from '../../configuration/common/configuration.js';
import { IInstantiationService } from '../../instantiation/common/instantiation.js';
import { IKeybindingService } from '../../keybinding/common/keybinding.js';
import { ICommandService } from '../../commands/common/commands.js';
import { ITelemetryService } from '../../telemetry/common/telemetry.js';
import { isPromiseCanceledError } from '../../../base/common/errors.js';
import { INotificationService } from '../../notification/common/notification.js';
import { toErrorMessage } from '../../../base/common/errorMessage.js';
let AbstractCommandsQuickAccessProvider = class AbstractCommandsQuickAccessProvider extends PickerQuickAccessProvider {
    constructor(options, instantiationService, keybindingService, commandService, telemetryService, notificationService) {
        super(AbstractCommandsQuickAccessProvider.PREFIX, options);
        this.options = options;
        this.instantiationService = instantiationService;
        this.keybindingService = keybindingService;
        this.commandService = commandService;
        this.telemetryService = telemetryService;
        this.notificationService = notificationService;
        this.commandsHistory = this._register(this.instantiationService.createInstance(CommandsHistory));
    }
    getPicks(filter, disposables, token) {
        return __awaiter(this, void 0, void 0, function* () {
            // Ask subclass for all command picks
            const allCommandPicks = yield this.getCommandPicks(disposables, token);
            if (token.isCancellationRequested) {
                return [];
            }
            // Filter
            const filteredCommandPicks = [];
            for (const commandPick of allCommandPicks) {
                const labelHighlights = withNullAsUndefined(AbstractCommandsQuickAccessProvider.WORD_FILTER(filter, commandPick.label));
                const aliasHighlights = commandPick.commandAlias ? withNullAsUndefined(AbstractCommandsQuickAccessProvider.WORD_FILTER(filter, commandPick.commandAlias)) : undefined;
                // Add if matching in label or alias
                if (labelHighlights || aliasHighlights) {
                    commandPick.highlights = {
                        label: labelHighlights,
                        detail: this.options.showAlias ? aliasHighlights : undefined
                    };
                    filteredCommandPicks.push(commandPick);
                }
                // Also add if we have a 100% command ID match
                else if (filter === commandPick.commandId) {
                    filteredCommandPicks.push(commandPick);
                }
            }
            // Add description to commands that have duplicate labels
            const mapLabelToCommand = new Map();
            for (const commandPick of filteredCommandPicks) {
                const existingCommandForLabel = mapLabelToCommand.get(commandPick.label);
                if (existingCommandForLabel) {
                    commandPick.description = commandPick.commandId;
                    existingCommandForLabel.description = existingCommandForLabel.commandId;
                }
                else {
                    mapLabelToCommand.set(commandPick.label, commandPick);
                }
            }
            // Sort by MRU order and fallback to name otherwise
            filteredCommandPicks.sort((commandPickA, commandPickB) => {
                const commandACounter = this.commandsHistory.peek(commandPickA.commandId);
                const commandBCounter = this.commandsHistory.peek(commandPickB.commandId);
                if (commandACounter && commandBCounter) {
                    return commandACounter > commandBCounter ? -1 : 1; // use more recently used command before older
                }
                if (commandACounter) {
                    return -1; // first command was used, so it wins over the non used one
                }
                if (commandBCounter) {
                    return 1; // other command was used so it wins over the command
                }
                // both commands were never used, so we sort by name
                return commandPickA.label.localeCompare(commandPickB.label);
            });
            const commandPicks = [];
            let addSeparator = false;
            for (let i = 0; i < filteredCommandPicks.length; i++) {
                const commandPick = filteredCommandPicks[i];
                const keybinding = this.keybindingService.lookupKeybinding(commandPick.commandId);
                const ariaLabel = keybinding ?
                    localize('commandPickAriaLabelWithKeybinding', "{0}, {1}", commandPick.label, keybinding.getAriaLabel()) :
                    commandPick.label;
                // Separator: recently used
                if (i === 0 && this.commandsHistory.peek(commandPick.commandId)) {
                    commandPicks.push({ type: 'separator', label: localize('recentlyUsed', "recently used") });
                    addSeparator = true;
                }
                // Separator: other commands
                if (i !== 0 && addSeparator && !this.commandsHistory.peek(commandPick.commandId)) {
                    commandPicks.push({ type: 'separator', label: localize('morecCommands', "other commands") });
                    addSeparator = false; // only once
                }
                // Command
                commandPicks.push(Object.assign(Object.assign({}, commandPick), { ariaLabel, detail: this.options.showAlias && commandPick.commandAlias !== commandPick.label ? commandPick.commandAlias : undefined, keybinding, accept: () => __awaiter(this, void 0, void 0, function* () {
                        // Add to history
                        this.commandsHistory.push(commandPick.commandId);
                        // Telementry
                        this.telemetryService.publicLog2('workbenchActionExecuted', {
                            id: commandPick.commandId,
                            from: 'quick open'
                        });
                        // Run
                        try {
                            yield this.commandService.executeCommand(commandPick.commandId);
                        }
                        catch (error) {
                            if (!isPromiseCanceledError(error)) {
                                this.notificationService.error(localize('canNotRun', "Command '{0}' resulted in an error ({1})", commandPick.label, toErrorMessage(error)));
                            }
                        }
                    }) }));
            }
            return commandPicks;
        });
    }
};
AbstractCommandsQuickAccessProvider.PREFIX = '>';
AbstractCommandsQuickAccessProvider.WORD_FILTER = or(matchesPrefix, matchesWords, matchesContiguousSubString);
AbstractCommandsQuickAccessProvider = __decorate([
    __param(1, IInstantiationService),
    __param(2, IKeybindingService),
    __param(3, ICommandService),
    __param(4, ITelemetryService),
    __param(5, INotificationService)
], AbstractCommandsQuickAccessProvider);
export { AbstractCommandsQuickAccessProvider };
let CommandsHistory = class CommandsHistory extends Disposable {
    constructor(storageService, configurationService) {
        super();
        this.storageService = storageService;
        this.configurationService = configurationService;
        this.configuredCommandsHistoryLength = 0;
        this.updateConfiguration();
        this.load();
        this.registerListeners();
    }
    registerListeners() {
        this._register(this.configurationService.onDidChangeConfiguration(() => this.updateConfiguration()));
    }
    updateConfiguration() {
        this.configuredCommandsHistoryLength = CommandsHistory.getConfiguredCommandHistoryLength(this.configurationService);
        if (CommandsHistory.cache && CommandsHistory.cache.limit !== this.configuredCommandsHistoryLength) {
            CommandsHistory.cache.limit = this.configuredCommandsHistoryLength;
            CommandsHistory.saveState(this.storageService);
        }
    }
    load() {
        const raw = this.storageService.get(CommandsHistory.PREF_KEY_CACHE, 0 /* GLOBAL */);
        let serializedCache;
        if (raw) {
            try {
                serializedCache = JSON.parse(raw);
            }
            catch (error) {
                // invalid data
            }
        }
        const cache = CommandsHistory.cache = new LRUCache(this.configuredCommandsHistoryLength, 1);
        if (serializedCache) {
            let entries;
            if (serializedCache.usesLRU) {
                entries = serializedCache.entries;
            }
            else {
                entries = serializedCache.entries.sort((a, b) => a.value - b.value);
            }
            entries.forEach(entry => cache.set(entry.key, entry.value));
        }
        CommandsHistory.counter = this.storageService.getNumber(CommandsHistory.PREF_KEY_COUNTER, 0 /* GLOBAL */, CommandsHistory.counter);
    }
    push(commandId) {
        if (!CommandsHistory.cache) {
            return;
        }
        CommandsHistory.cache.set(commandId, CommandsHistory.counter++); // set counter to command
        CommandsHistory.saveState(this.storageService);
    }
    peek(commandId) {
        var _a;
        return (_a = CommandsHistory.cache) === null || _a === void 0 ? void 0 : _a.peek(commandId);
    }
    static saveState(storageService) {
        if (!CommandsHistory.cache) {
            return;
        }
        const serializedCache = { usesLRU: true, entries: [] };
        CommandsHistory.cache.forEach((value, key) => serializedCache.entries.push({ key, value }));
        storageService.store(CommandsHistory.PREF_KEY_CACHE, JSON.stringify(serializedCache), 0 /* GLOBAL */, 0 /* USER */);
        storageService.store(CommandsHistory.PREF_KEY_COUNTER, CommandsHistory.counter, 0 /* GLOBAL */, 0 /* USER */);
    }
    static getConfiguredCommandHistoryLength(configurationService) {
        var _a, _b;
        const config = configurationService.getValue();
        const configuredCommandHistoryLength = (_b = (_a = config.workbench) === null || _a === void 0 ? void 0 : _a.commandPalette) === null || _b === void 0 ? void 0 : _b.history;
        if (typeof configuredCommandHistoryLength === 'number') {
            return configuredCommandHistoryLength;
        }
        return CommandsHistory.DEFAULT_COMMANDS_HISTORY_LENGTH;
    }
};
CommandsHistory.DEFAULT_COMMANDS_HISTORY_LENGTH = 50;
CommandsHistory.PREF_KEY_CACHE = 'commandPalette.mru.cache';
CommandsHistory.PREF_KEY_COUNTER = 'commandPalette.mru.counter';
CommandsHistory.counter = 1;
CommandsHistory = __decorate([
    __param(0, IStorageService),
    __param(1, IConfigurationService)
], CommandsHistory);
export { CommandsHistory };
