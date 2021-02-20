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
import { IQuickInputService, ItemActivation } from '../common/quickInput.js';
import { Disposable, DisposableStore, toDisposable } from '../../../base/common/lifecycle.js';
import { Extensions, DefaultQuickAccessFilterValue } from '../common/quickAccess.js';
import { Registry } from '../../registry/common/platform.js';
import { CancellationTokenSource } from '../../../base/common/cancellation.js';
import { IInstantiationService } from '../../instantiation/common/instantiation.js';
import { once } from '../../../base/common/functional.js';
let QuickAccessController = class QuickAccessController extends Disposable {
    constructor(quickInputService, instantiationService) {
        super();
        this.quickInputService = quickInputService;
        this.instantiationService = instantiationService;
        this.registry = Registry.as(Extensions.Quickaccess);
        this.mapProviderToDescriptor = new Map();
        this.lastAcceptedPickerValues = new Map();
        this.visibleQuickAccess = undefined;
    }
    show(value = '', options) {
        var _a;
        // Find provider for the value to show
        const [provider, descriptor] = this.getOrInstantiateProvider(value);
        // Return early if quick access is already showing on that same prefix
        const visibleQuickAccess = this.visibleQuickAccess;
        const visibleDescriptor = visibleQuickAccess === null || visibleQuickAccess === void 0 ? void 0 : visibleQuickAccess.descriptor;
        if (visibleQuickAccess && descriptor && visibleDescriptor === descriptor) {
            // Apply value only if it is more specific than the prefix
            // from the provider and we are not instructed to preserve
            if (value !== descriptor.prefix && !(options === null || options === void 0 ? void 0 : options.preserveValue)) {
                visibleQuickAccess.picker.value = value;
            }
            // Always adjust selection
            this.adjustValueSelection(visibleQuickAccess.picker, descriptor, options);
            return;
        }
        // Rewrite the filter value based on certain rules unless disabled
        if (descriptor && !(options === null || options === void 0 ? void 0 : options.preserveValue)) {
            let newValue = undefined;
            // If we have a visible provider with a value, take it's filter value but
            // rewrite to new provider prefix in case they differ
            if (visibleQuickAccess && visibleDescriptor && visibleDescriptor !== descriptor) {
                const newValueCandidateWithoutPrefix = visibleQuickAccess.value.substr(visibleDescriptor.prefix.length);
                if (newValueCandidateWithoutPrefix) {
                    newValue = `${descriptor.prefix}${newValueCandidateWithoutPrefix}`;
                }
            }
            // Otherwise, take a default value as instructed
            if (!newValue) {
                const defaultFilterValue = provider === null || provider === void 0 ? void 0 : provider.defaultFilterValue;
                if (defaultFilterValue === DefaultQuickAccessFilterValue.LAST) {
                    newValue = this.lastAcceptedPickerValues.get(descriptor);
                }
                else if (typeof defaultFilterValue === 'string') {
                    newValue = `${descriptor.prefix}${defaultFilterValue}`;
                }
            }
            if (typeof newValue === 'string') {
                value = newValue;
            }
        }
        // Create a picker for the provider to use with the initial value
        // and adjust the filtering to exclude the prefix from filtering
        const disposables = new DisposableStore();
        const picker = disposables.add(this.quickInputService.createQuickPick());
        picker.value = value;
        this.adjustValueSelection(picker, descriptor, options);
        picker.placeholder = descriptor === null || descriptor === void 0 ? void 0 : descriptor.placeholder;
        picker.quickNavigate = options === null || options === void 0 ? void 0 : options.quickNavigateConfiguration;
        picker.hideInput = !!picker.quickNavigate && !visibleQuickAccess; // only hide input if there was no picker opened already
        if (typeof (options === null || options === void 0 ? void 0 : options.itemActivation) === 'number' || (options === null || options === void 0 ? void 0 : options.quickNavigateConfiguration)) {
            picker.itemActivation = (_a = options === null || options === void 0 ? void 0 : options.itemActivation) !== null && _a !== void 0 ? _a : ItemActivation.SECOND /* quick nav is always second */;
        }
        picker.contextKey = descriptor === null || descriptor === void 0 ? void 0 : descriptor.contextKey;
        picker.filterValue = (value) => value.substring(descriptor ? descriptor.prefix.length : 0);
        if (descriptor === null || descriptor === void 0 ? void 0 : descriptor.placeholder) {
            picker.ariaLabel = descriptor === null || descriptor === void 0 ? void 0 : descriptor.placeholder;
        }
        // Register listeners
        const cancellationToken = this.registerPickerListeners(picker, provider, descriptor, value, disposables);
        // Ask provider to fill the picker as needed if we have one
        if (provider) {
            disposables.add(provider.provide(picker, cancellationToken));
        }
        // Finally, show the picker. This is important because a provider
        // may not call this and then our disposables would leak that rely
        // on the onDidHide event.
        picker.show();
    }
    adjustValueSelection(picker, descriptor, options) {
        var _a;
        let valueSelection;
        // Preserve: just always put the cursor at the end
        if (options === null || options === void 0 ? void 0 : options.preserveValue) {
            valueSelection = [picker.value.length, picker.value.length];
        }
        // Otherwise: select the value up until the prefix
        else {
            valueSelection = [(_a = descriptor === null || descriptor === void 0 ? void 0 : descriptor.prefix.length) !== null && _a !== void 0 ? _a : 0, picker.value.length];
        }
        picker.valueSelection = valueSelection;
    }
    registerPickerListeners(picker, provider, descriptor, value, disposables) {
        // Remember as last visible picker and clean up once picker get's disposed
        const visibleQuickAccess = this.visibleQuickAccess = { picker, descriptor, value };
        disposables.add(toDisposable(() => {
            if (visibleQuickAccess === this.visibleQuickAccess) {
                this.visibleQuickAccess = undefined;
            }
        }));
        // Whenever the value changes, check if the provider has
        // changed and if so - re-create the picker from the beginning
        disposables.add(picker.onDidChangeValue(value => {
            const [providerForValue] = this.getOrInstantiateProvider(value);
            if (providerForValue !== provider) {
                this.show(value, { preserveValue: true } /* do not rewrite value from user typing! */);
            }
            else {
                visibleQuickAccess.value = value; // remember the value in our visible one
            }
        }));
        // Remember picker input for future use when accepting
        if (descriptor) {
            disposables.add(picker.onDidAccept(() => {
                this.lastAcceptedPickerValues.set(descriptor, picker.value);
            }));
        }
        // Create a cancellation token source that is valid as long as the
        // picker has not been closed without picking an item
        const cts = disposables.add(new CancellationTokenSource());
        once(picker.onDidHide)(() => {
            if (picker.selectedItems.length === 0) {
                cts.cancel();
            }
            // Start to dispose once picker hides
            disposables.dispose();
        });
        return cts.token;
    }
    getOrInstantiateProvider(value) {
        const providerDescriptor = this.registry.getQuickAccessProvider(value);
        if (!providerDescriptor) {
            return [undefined, undefined];
        }
        let provider = this.mapProviderToDescriptor.get(providerDescriptor);
        if (!provider) {
            provider = this.instantiationService.createInstance(providerDescriptor.ctor);
            this.mapProviderToDescriptor.set(providerDescriptor, provider);
        }
        return [provider, providerDescriptor];
    }
};
QuickAccessController = __decorate([
    __param(0, IQuickInputService),
    __param(1, IInstantiationService)
], QuickAccessController);
export { QuickAccessController };
