/**
 * Simple in-memory TTL cache for GitHub API responses.
 */
export declare class Cache<T> {
    private store;
    private readonly ttlMs;
    constructor(ttlMs?: number);
    get(key: string): T | undefined;
    set(key: string, data: T, ttlMs?: number): void;
    has(key: string): boolean;
    delete(key: string): void;
    clear(): void;
    /** Remove all expired entries. */
    prune(): void;
    get size(): number;
}
//# sourceMappingURL=cache.d.ts.map