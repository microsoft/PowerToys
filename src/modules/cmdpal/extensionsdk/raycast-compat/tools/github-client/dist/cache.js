"use strict";
// Copyright (c) Microsoft Corporation
// Licensed under the MIT License.
Object.defineProperty(exports, "__esModule", { value: true });
exports.Cache = void 0;
/**
 * Simple in-memory TTL cache for GitHub API responses.
 */
class Cache {
    store = new Map();
    ttlMs;
    constructor(ttlMs = 5 * 60 * 1000) {
        this.ttlMs = ttlMs;
    }
    get(key) {
        const entry = this.store.get(key);
        if (!entry)
            return undefined;
        if (Date.now() > entry.expiresAt) {
            this.store.delete(key);
            return undefined;
        }
        return entry.data;
    }
    set(key, data, ttlMs) {
        const expiry = ttlMs ?? this.ttlMs;
        this.store.set(key, { data, expiresAt: Date.now() + expiry });
    }
    has(key) {
        return this.get(key) !== undefined;
    }
    delete(key) {
        this.store.delete(key);
    }
    clear() {
        this.store.clear();
    }
    /** Remove all expired entries. */
    prune() {
        const now = Date.now();
        for (const [key, entry] of this.store) {
            if (now > entry.expiresAt) {
                this.store.delete(key);
            }
        }
    }
    get size() {
        this.prune();
        return this.store.size;
    }
}
exports.Cache = Cache;
//# sourceMappingURL=cache.js.map