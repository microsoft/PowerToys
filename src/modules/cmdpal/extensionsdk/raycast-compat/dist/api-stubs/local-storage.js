"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.LocalStorage = void 0;
exports._setStoragePath = _setStoragePath;
/**
 * Raycast LocalStorage compatibility stub.
 *
 * File-backed key-value store using a JSON file in the extension's data
 * directory. The `supportPath` from the environment module determines where
 * the storage file lives.
 *
 * Thread-safety note: Node.js is single-threaded, so no locking needed.
 * The file is read lazily on first access and written on every mutation
 * to ensure persistence across process restarts.
 */
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
let storePath = null;
let cache = null;
/** Internal: set the storage directory (called by environment setup). */
function _setStoragePath(dir) {
    storePath = path.join(dir, 'local-storage.json');
    cache = null; // Force reload on next access
}
function getStoreFile() {
    if (!storePath) {
        // Fallback to temp directory if environment hasn't been configured
        const fallback = path.join(process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.', 'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions', '_raycast-compat');
        storePath = path.join(fallback, 'local-storage.json');
    }
    return storePath;
}
function ensureDir(filePath) {
    const dir = path.dirname(filePath);
    if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
    }
}
function load() {
    if (cache !== null)
        return cache;
    const file = getStoreFile();
    try {
        if (fs.existsSync(file)) {
            const raw = fs.readFileSync(file, 'utf-8');
            cache = JSON.parse(raw);
        }
        else {
            cache = {};
        }
    }
    catch {
        console.warn('[LocalStorage] Failed to read storage file, starting empty');
        cache = {};
    }
    return cache;
}
function save() {
    const file = getStoreFile();
    try {
        ensureDir(file);
        fs.writeFileSync(file, JSON.stringify(cache, null, 2), 'utf-8');
    }
    catch (err) {
        console.warn('[LocalStorage] Failed to persist storage:', err);
    }
}
exports.LocalStorage = {
    /**
     * Retrieve a value by key. Returns `undefined` if not found.
     */
    async getItem(key) {
        const store = load();
        return store[key];
    },
    /**
     * Store a value. Values are JSON-stringified if not already strings.
     */
    async setItem(key, value) {
        const store = load();
        store[key] = typeof value === 'string' ? value : JSON.stringify(value);
        save();
    },
    /**
     * Remove a key from storage.
     */
    async removeItem(key) {
        const store = load();
        delete store[key];
        save();
    },
    /**
     * Return all key-value pairs.
     */
    async allItems() {
        return { ...load() };
    },
    /**
     * Clear all stored data.
     */
    async clear() {
        cache = {};
        save();
    },
};
//# sourceMappingURL=local-storage.js.map