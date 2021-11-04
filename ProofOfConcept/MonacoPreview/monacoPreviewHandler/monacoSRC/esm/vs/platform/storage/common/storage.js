/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { createDecorator } from '../../instantiation/common/instantiation.js';
import { Emitter, PauseableEmitter } from '../../../base/common/event.js';
import { Disposable } from '../../../base/common/lifecycle.js';
import { isUndefinedOrNull } from '../../../base/common/types.js';
const TARGET_KEY = '__$__targetStorageMarker';
export const IStorageService = createDecorator('storageService');
export var WillSaveStateReason;
(function (WillSaveStateReason) {
    /**
     * No specific reason to save state.
     */
    WillSaveStateReason[WillSaveStateReason["NONE"] = 0] = "NONE";
    /**
     * A hint that the workbench is about to shutdown.
     */
    WillSaveStateReason[WillSaveStateReason["SHUTDOWN"] = 1] = "SHUTDOWN";
})(WillSaveStateReason || (WillSaveStateReason = {}));
export class AbstractStorageService extends Disposable {
    constructor() {
        super(...arguments);
        this._onDidChangeValue = this._register(new PauseableEmitter());
        this._onDidChangeTarget = this._register(new PauseableEmitter());
        this._onWillSaveState = this._register(new Emitter());
        this.onWillSaveState = this._onWillSaveState.event;
        this._workspaceKeyTargets = undefined;
        this._globalKeyTargets = undefined;
    }
    emitDidChangeValue(scope, key) {
        // Specially handle `TARGET_KEY`
        if (key === TARGET_KEY) {
            // Clear our cached version which is now out of date
            if (scope === 0 /* GLOBAL */) {
                this._globalKeyTargets = undefined;
            }
            else if (scope === 1 /* WORKSPACE */) {
                this._workspaceKeyTargets = undefined;
            }
            // Emit as `didChangeTarget` event
            this._onDidChangeTarget.fire({ scope });
        }
        // Emit any other key to outside
        else {
            this._onDidChangeValue.fire({ scope, key, target: this.getKeyTargets(scope)[key] });
        }
    }
    store(key, value, scope, target) {
        // We remove the key for undefined/null values
        if (isUndefinedOrNull(value)) {
            this.remove(key, scope);
            return;
        }
        // Update our datastructures but send events only after
        this.withPausedEmitters(() => {
            // Update key-target map
            this.updateKeyTarget(key, scope, target);
            // Store actual value
            this.doStore(key, value, scope);
        });
    }
    remove(key, scope) {
        // Update our datastructures but send events only after
        this.withPausedEmitters(() => {
            // Update key-target map
            this.updateKeyTarget(key, scope, undefined);
            // Remove actual key
            this.doRemove(key, scope);
        });
    }
    withPausedEmitters(fn) {
        // Pause emitters
        this._onDidChangeValue.pause();
        this._onDidChangeTarget.pause();
        try {
            fn();
        }
        finally {
            // Resume emitters
            this._onDidChangeValue.resume();
            this._onDidChangeTarget.resume();
        }
    }
    updateKeyTarget(key, scope, target) {
        // Add
        const keyTargets = this.getKeyTargets(scope);
        if (typeof target === 'number') {
            if (keyTargets[key] !== target) {
                keyTargets[key] = target;
                this.doStore(TARGET_KEY, JSON.stringify(keyTargets), scope);
            }
        }
        // Remove
        else {
            if (typeof keyTargets[key] === 'number') {
                delete keyTargets[key];
                this.doStore(TARGET_KEY, JSON.stringify(keyTargets), scope);
            }
        }
    }
    get workspaceKeyTargets() {
        if (!this._workspaceKeyTargets) {
            this._workspaceKeyTargets = this.loadKeyTargets(1 /* WORKSPACE */);
        }
        return this._workspaceKeyTargets;
    }
    get globalKeyTargets() {
        if (!this._globalKeyTargets) {
            this._globalKeyTargets = this.loadKeyTargets(0 /* GLOBAL */);
        }
        return this._globalKeyTargets;
    }
    getKeyTargets(scope) {
        return scope === 0 /* GLOBAL */ ? this.globalKeyTargets : this.workspaceKeyTargets;
    }
    loadKeyTargets(scope) {
        const keysRaw = this.get(TARGET_KEY, scope);
        if (keysRaw) {
            try {
                return JSON.parse(keysRaw);
            }
            catch (error) {
                // Fail gracefully
            }
        }
        return Object.create(null);
    }
}
export class InMemoryStorageService extends AbstractStorageService {
    constructor() {
        super(...arguments);
        this.globalCache = new Map();
        this.workspaceCache = new Map();
    }
    getCache(scope) {
        return scope === 0 /* GLOBAL */ ? this.globalCache : this.workspaceCache;
    }
    get(key, scope, fallbackValue) {
        const value = this.getCache(scope).get(key);
        if (isUndefinedOrNull(value)) {
            return fallbackValue;
        }
        return value;
    }
    getBoolean(key, scope, fallbackValue) {
        const value = this.getCache(scope).get(key);
        if (isUndefinedOrNull(value)) {
            return fallbackValue;
        }
        return value === 'true';
    }
    getNumber(key, scope, fallbackValue) {
        const value = this.getCache(scope).get(key);
        if (isUndefinedOrNull(value)) {
            return fallbackValue;
        }
        return parseInt(value, 10);
    }
    doStore(key, value, scope) {
        // Otherwise, convert to String and store
        const valueStr = String(value);
        // Return early if value already set
        const currentValue = this.getCache(scope).get(key);
        if (currentValue === valueStr) {
            return;
        }
        // Update in cache
        this.getCache(scope).set(key, valueStr);
        // Events
        this.emitDidChangeValue(scope, key);
    }
    doRemove(key, scope) {
        const wasDeleted = this.getCache(scope).delete(key);
        if (!wasDeleted) {
            return; // Return early if value already deleted
        }
        // Events
        this.emitDidChangeValue(scope, key);
    }
}
