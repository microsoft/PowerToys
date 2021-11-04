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
import { CancellationTokenSource } from '../../../base/common/cancellation.js';
import { DisposableStore, Disposable, MutableDisposable } from '../../../base/common/lifecycle.js';
import { timeout } from '../../../base/common/async.js';
export var TriggerAction;
(function (TriggerAction) {
    /**
     * Do nothing after the button was clicked.
     */
    TriggerAction[TriggerAction["NO_ACTION"] = 0] = "NO_ACTION";
    /**
     * Close the picker.
     */
    TriggerAction[TriggerAction["CLOSE_PICKER"] = 1] = "CLOSE_PICKER";
    /**
     * Update the results of the picker.
     */
    TriggerAction[TriggerAction["REFRESH_PICKER"] = 2] = "REFRESH_PICKER";
    /**
     * Remove the item from the picker.
     */
    TriggerAction[TriggerAction["REMOVE_ITEM"] = 3] = "REMOVE_ITEM";
})(TriggerAction || (TriggerAction = {}));
function isPicksWithActive(obj) {
    const candidate = obj;
    return Array.isArray(candidate.items);
}
function isFastAndSlowPicks(obj) {
    const candidate = obj;
    return !!candidate.picks && candidate.additionalPicks instanceof Promise;
}
export class PickerQuickAccessProvider extends Disposable {
    constructor(prefix, options) {
        super();
        this.prefix = prefix;
        this.options = options;
    }
    provide(picker, token) {
        var _a;
        const disposables = new DisposableStore();
        // Apply options if any
        picker.canAcceptInBackground = !!((_a = this.options) === null || _a === void 0 ? void 0 : _a.canAcceptInBackground);
        // Disable filtering & sorting, we control the results
        picker.matchOnLabel = picker.matchOnDescription = picker.matchOnDetail = picker.sortByLabel = false;
        // Set initial picks and update on type
        let picksCts = undefined;
        const picksDisposable = disposables.add(new MutableDisposable());
        const updatePickerItems = () => __awaiter(this, void 0, void 0, function* () {
            const picksDisposables = picksDisposable.value = new DisposableStore();
            // Cancel any previous ask for picks and busy
            picksCts === null || picksCts === void 0 ? void 0 : picksCts.dispose(true);
            picker.busy = false;
            // Create new cancellation source for this run
            picksCts = new CancellationTokenSource(token);
            // Collect picks and support both long running and short or combined
            const picksToken = picksCts.token;
            const picksFilter = picker.value.substr(this.prefix.length).trim();
            const providedPicks = this.getPicks(picksFilter, picksDisposables, picksToken);
            const applyPicks = (picks, skipEmpty) => {
                var _a;
                let items;
                let activeItem = undefined;
                if (isPicksWithActive(picks)) {
                    items = picks.items;
                    activeItem = picks.active;
                }
                else {
                    items = picks;
                }
                if (items.length === 0) {
                    if (skipEmpty) {
                        return false;
                    }
                    if (picksFilter.length > 0 && ((_a = this.options) === null || _a === void 0 ? void 0 : _a.noResultsPick)) {
                        items = [this.options.noResultsPick];
                    }
                }
                picker.items = items;
                if (activeItem) {
                    picker.activeItems = [activeItem];
                }
                return true;
            };
            // No Picks
            if (providedPicks === null) {
                // Ignore
            }
            // Fast and Slow Picks
            else if (isFastAndSlowPicks(providedPicks)) {
                let fastPicksApplied = false;
                let slowPicksApplied = false;
                yield Promise.all([
                    // Fast Picks: to reduce amount of flicker, we race against
                    // the slow picks over 500ms and then set the fast picks.
                    // If the slow picks are faster, we reduce the flicker by
                    // only setting the items once.
                    (() => __awaiter(this, void 0, void 0, function* () {
                        yield timeout(PickerQuickAccessProvider.FAST_PICKS_RACE_DELAY);
                        if (picksToken.isCancellationRequested) {
                            return;
                        }
                        if (!slowPicksApplied) {
                            fastPicksApplied = applyPicks(providedPicks.picks, true /* skip over empty to reduce flicker */);
                        }
                    }))(),
                    // Slow Picks: we await the slow picks and then set them at
                    // once together with the fast picks, but only if we actually
                    // have additional results.
                    (() => __awaiter(this, void 0, void 0, function* () {
                        picker.busy = true;
                        try {
                            const awaitedAdditionalPicks = yield providedPicks.additionalPicks;
                            if (picksToken.isCancellationRequested) {
                                return;
                            }
                            let picks;
                            let activePick = undefined;
                            if (isPicksWithActive(providedPicks.picks)) {
                                picks = providedPicks.picks.items;
                                activePick = providedPicks.picks.active;
                            }
                            else {
                                picks = providedPicks.picks;
                            }
                            let additionalPicks;
                            let additionalActivePick = undefined;
                            if (isPicksWithActive(awaitedAdditionalPicks)) {
                                additionalPicks = awaitedAdditionalPicks.items;
                                additionalActivePick = awaitedAdditionalPicks.active;
                            }
                            else {
                                additionalPicks = awaitedAdditionalPicks;
                            }
                            if (additionalPicks.length > 0 || !fastPicksApplied) {
                                // If we do not have any activePick or additionalActivePick
                                // we try to preserve the currently active pick from the
                                // fast results. This fixes an issue where the user might
                                // have made a pick active before the additional results
                                // kick in.
                                // See https://github.com/microsoft/vscode/issues/102480
                                let fallbackActivePick = undefined;
                                if (!activePick && !additionalActivePick) {
                                    const fallbackActivePickCandidate = picker.activeItems[0];
                                    if (fallbackActivePickCandidate && picks.indexOf(fallbackActivePickCandidate) !== -1) {
                                        fallbackActivePick = fallbackActivePickCandidate;
                                    }
                                }
                                applyPicks({
                                    items: [...picks, ...additionalPicks],
                                    active: activePick || additionalActivePick || fallbackActivePick
                                });
                            }
                        }
                        finally {
                            if (!picksToken.isCancellationRequested) {
                                picker.busy = false;
                            }
                            slowPicksApplied = true;
                        }
                    }))()
                ]);
            }
            // Fast Picks
            else if (!(providedPicks instanceof Promise)) {
                applyPicks(providedPicks);
            }
            // Slow Picks
            else {
                picker.busy = true;
                try {
                    const awaitedPicks = yield providedPicks;
                    if (picksToken.isCancellationRequested) {
                        return;
                    }
                    applyPicks(awaitedPicks);
                }
                finally {
                    if (!picksToken.isCancellationRequested) {
                        picker.busy = false;
                    }
                }
            }
        });
        disposables.add(picker.onDidChangeValue(() => updatePickerItems()));
        updatePickerItems();
        // Accept the pick on accept and hide picker
        disposables.add(picker.onDidAccept(event => {
            const [item] = picker.selectedItems;
            if (typeof (item === null || item === void 0 ? void 0 : item.accept) === 'function') {
                if (!event.inBackground) {
                    picker.hide(); // hide picker unless we accept in background
                }
                item.accept(picker.keyMods, event);
            }
        }));
        // Trigger the pick with button index if button triggered
        disposables.add(picker.onDidTriggerItemButton(({ button, item }) => __awaiter(this, void 0, void 0, function* () {
            var _b, _c;
            if (typeof item.trigger === 'function') {
                const buttonIndex = (_c = (_b = item.buttons) === null || _b === void 0 ? void 0 : _b.indexOf(button)) !== null && _c !== void 0 ? _c : -1;
                if (buttonIndex >= 0) {
                    const result = item.trigger(buttonIndex, picker.keyMods);
                    const action = (typeof result === 'number') ? result : yield result;
                    if (token.isCancellationRequested) {
                        return;
                    }
                    switch (action) {
                        case TriggerAction.NO_ACTION:
                            break;
                        case TriggerAction.CLOSE_PICKER:
                            picker.hide();
                            break;
                        case TriggerAction.REFRESH_PICKER:
                            updatePickerItems();
                            break;
                        case TriggerAction.REMOVE_ITEM:
                            const index = picker.items.indexOf(item);
                            if (index !== -1) {
                                const items = picker.items.slice();
                                items.splice(index, 1);
                                picker.items = items;
                            }
                            break;
                    }
                }
            }
        })));
        return disposables;
    }
}
PickerQuickAccessProvider.FAST_PICKS_RACE_DELAY = 200; // timeout before we accept fast results before slow results are present
