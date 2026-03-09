/** Internal: set the storage directory (called by environment setup). */
export declare function _setStoragePath(dir: string): void;
export declare const LocalStorage: {
    /**
     * Retrieve a value by key. Returns `undefined` if not found.
     */
    getItem(key: string): Promise<string | undefined>;
    /**
     * Store a value. Values are JSON-stringified if not already strings.
     */
    setItem(key: string, value: unknown): Promise<void>;
    /**
     * Remove a key from storage.
     */
    removeItem(key: string): Promise<void>;
    /**
     * Return all key-value pairs.
     */
    allItems(): Promise<Record<string, string>>;
    /**
     * Clear all stored data.
     */
    clear(): Promise<void>;
};
//# sourceMappingURL=local-storage.d.ts.map