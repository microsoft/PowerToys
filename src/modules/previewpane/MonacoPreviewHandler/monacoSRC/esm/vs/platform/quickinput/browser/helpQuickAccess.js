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
import { IQuickInputService } from '../common/quickInput.js';
import { Extensions } from '../common/quickAccess.js';
import { Registry } from '../../registry/common/platform.js';
import { localize } from '../../../nls.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
let HelpQuickAccessProvider = class HelpQuickAccessProvider {
    constructor(quickInputService) {
        this.quickInputService = quickInputService;
        this.registry = Registry.as(Extensions.Quickaccess);
    }
    provide(picker) {
        const disposables = new DisposableStore();
        // Open a picker with the selected value if picked
        disposables.add(picker.onDidAccept(() => {
            const [item] = picker.selectedItems;
            if (item) {
                this.quickInputService.quickAccess.show(item.prefix, { preserveValue: true });
            }
        }));
        // Also open a picker when we detect the user typed the exact
        // name of a provider (e.g. `?term` for terminals)
        disposables.add(picker.onDidChangeValue(value => {
            const providerDescriptor = this.registry.getQuickAccessProvider(value.substr(HelpQuickAccessProvider.PREFIX.length));
            if (providerDescriptor && providerDescriptor.prefix && providerDescriptor.prefix !== HelpQuickAccessProvider.PREFIX) {
                this.quickInputService.quickAccess.show(providerDescriptor.prefix, { preserveValue: true });
            }
        }));
        // Fill in all providers separated by editor/global scope
        const { editorProviders, globalProviders } = this.getQuickAccessProviders();
        picker.items = editorProviders.length === 0 || globalProviders.length === 0 ?
            // Without groups
            [
                ...(editorProviders.length === 0 ? globalProviders : editorProviders)
            ] :
            // With groups
            [
                { label: localize('globalCommands', "global commands"), type: 'separator' },
                ...globalProviders,
                { label: localize('editorCommands', "editor commands"), type: 'separator' },
                ...editorProviders
            ];
        return disposables;
    }
    getQuickAccessProviders() {
        const globalProviders = [];
        const editorProviders = [];
        for (const provider of this.registry.getQuickAccessProviders().sort((providerA, providerB) => providerA.prefix.localeCompare(providerB.prefix))) {
            if (provider.prefix === HelpQuickAccessProvider.PREFIX) {
                continue; // exclude help which is already active
            }
            for (const helpEntry of provider.helpEntries) {
                const prefix = helpEntry.prefix || provider.prefix;
                const label = prefix || '\u2026' /* ... */;
                (helpEntry.needsEditor ? editorProviders : globalProviders).push({
                    prefix,
                    label,
                    ariaLabel: localize('helpPickAriaLabel', "{0}, {1}", label, helpEntry.description),
                    description: helpEntry.description
                });
            }
        }
        return { editorProviders, globalProviders };
    }
};
HelpQuickAccessProvider.PREFIX = '?';
HelpQuickAccessProvider = __decorate([
    __param(0, IQuickInputService)
], HelpQuickAccessProvider);
export { HelpQuickAccessProvider };
