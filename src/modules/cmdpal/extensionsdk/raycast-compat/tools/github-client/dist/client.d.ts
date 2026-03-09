import { ExtensionEntry, ExtensionFile, GitHubClientOptions, RateLimitInfo, RaycastManifest, SearchOptions } from './types';
/** Thrown when the GitHub API rate limit is exceeded. */
export declare class RateLimitError extends Error {
    readonly resetAt: Date;
    readonly limit: number;
    readonly remaining: number;
    constructor(info: RateLimitInfo);
}
/** Thrown for non-OK HTTP responses that aren't rate-limit errors. */
export declare class GitHubApiError extends Error {
    readonly status: number;
    readonly statusText: string;
    readonly body: string;
    constructor(status: number, statusText: string, body: string);
}
export declare class RaycastExtensionsClient {
    private readonly token;
    private readonly baseUrl;
    private readonly owner;
    private readonly repo;
    private readonly cache;
    private lastRateLimit;
    constructor(options?: GitHubClientOptions);
    /** Current rate-limit info from the last API response, if available. */
    get rateLimit(): RateLimitInfo | undefined;
    /** Clear all cached data. */
    clearCache(): void;
    /**
     * List all extension directories in the `extensions/` folder.
     *
     * The Contents API truncates at ~1000 entries, so we fall back to the
     * Git Trees API (which handles any size) when truncation is detected.
     */
    listExtensions(): Promise<ExtensionEntry[]>;
    private listExtensionsViaContents;
    private listExtensionsViaTree;
    /**
     * Fetch and parse a specific extension's package.json.
     * Returns `null` if the extension or manifest does not exist.
     */
    getManifest(extensionName: string): Promise<RaycastManifest | null>;
    /**
     * Check whether an extension's package.json declares Windows support.
     *
     * Rules:
     * - If `platforms` is absent or empty → assume all platforms → Windows OK
     * - If `platforms` is present → must include "windows" (case-insensitive)
     */
    isWindowsCompatible(manifest: RaycastManifest): boolean;
    /**
     * Search for extensions by keyword using the GitHub Code Search API.
     * Searches within `package.json` files in the extensions directory.
     */
    searchExtensions(query: string, options?: SearchOptions): Promise<ExtensionEntry[]>;
    /**
     * Fetch an extension's README.md content.
     * Returns `null` if the README does not exist.
     */
    getReadme(extensionName: string): Promise<string | null>;
    /**
     * Download all files for a specific extension.
     * Uses the Git Trees API to recursively list files, then fetches each blob.
     */
    downloadExtension(extensionName: string): Promise<ExtensionFile[]>;
    /**
     * Resolve the tree SHA for a given path in the repo by walking the tree.
     */
    private resolveTreeSha;
    /**
     * List only Windows-compatible extensions.
     *
     * Strategy: fetch the full extension list, then batch-check manifests.
     * Results are cached aggressively since this is expensive.
     */
    listWindowsExtensions(): Promise<ExtensionEntry[]>;
    /**
     * Filter a list of extensions to only those compatible with Windows.
     * Fetches each manifest in parallel batches.
     */
    private filterWindowsCompatible;
    /**
     * Make an authenticated GET request to the GitHub API.
     * Handles caching, rate limits, and error responses.
     */
    private request;
    private parseRateLimitHeaders;
}
//# sourceMappingURL=client.d.ts.map