// Copyright (c) Microsoft Corporation
// Licensed under the MIT License.

/**
 * GitHub API client for browsing the raycast/extensions repository.
 *
 * Uses Node.js built-in `fetch` (18+). No external HTTP dependencies.
 * Supports optional GitHub token via constructor or GITHUB_TOKEN env var.
 * Includes in-memory TTL cache and rate-limit handling.
 */

import { Cache } from './cache';
import {
  ExtensionEntry,
  ExtensionFile,
  GitHubClientOptions,
  GitHubContentEntry,
  GitHubFileContent,
  GitHubSearchCodeResponse,
  GitHubTreeEntry,
  RateLimitInfo,
  RaycastManifest,
  SearchOptions,
} from './types';

/** Thrown when the GitHub API rate limit is exceeded. */
export class RateLimitError extends Error {
  public readonly resetAt: Date;
  public readonly limit: number;
  public readonly remaining: number;

  constructor(info: RateLimitInfo) {
    const waitSec = Math.ceil((info.reset.getTime() - Date.now()) / 1000);
    super(`GitHub API rate limit exceeded. Resets in ${waitSec}s at ${info.reset.toISOString()}`);
    this.name = 'RateLimitError';
    this.resetAt = info.reset;
    this.limit = info.limit;
    this.remaining = info.remaining;
  }
}

/** Thrown for non-OK HTTP responses that aren't rate-limit errors. */
export class GitHubApiError extends Error {
  public readonly status: number;
  public readonly statusText: string;
  public readonly body: string;

  constructor(status: number, statusText: string, body: string) {
    super(`GitHub API error: ${status} ${statusText}`);
    this.name = 'GitHubApiError';
    this.status = status;
    this.statusText = statusText;
    this.body = body;
  }
}

const DEFAULT_BASE_URL = 'https://api.github.com';
const DEFAULT_OWNER = 'raycast';
const DEFAULT_REPO = 'extensions';
const DEFAULT_CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes
const EXTENSIONS_DIR = 'extensions';

export class RaycastExtensionsClient {
  private readonly token: string | undefined;
  private readonly baseUrl: string;
  private readonly owner: string;
  private readonly repo: string;
  private readonly cache: Cache<unknown>;
  private lastRateLimit: RateLimitInfo | undefined;

  constructor(options: GitHubClientOptions = {}) {
    this.token = options.token ?? process.env.GITHUB_TOKEN;
    this.baseUrl = (options.baseUrl ?? DEFAULT_BASE_URL).replace(/\/+$/, '');
    this.owner = options.owner ?? DEFAULT_OWNER;
    this.repo = options.repo ?? DEFAULT_REPO;
    this.cache = new Cache(options.cacheTtlMs ?? DEFAULT_CACHE_TTL_MS);
  }

  /** Current rate-limit info from the last API response, if available. */
  get rateLimit(): RateLimitInfo | undefined {
    return this.lastRateLimit;
  }

  /** Clear all cached data. */
  clearCache(): void {
    this.cache.clear();
  }

  // ---------------------------------------------------------------------------
  // 1. List extensions
  // ---------------------------------------------------------------------------

  /**
   * List all extension directories in the `extensions/` folder.
   *
   * The Contents API truncates at ~1000 entries, so we fall back to the
   * Git Trees API (which handles any size) when truncation is detected.
   */
  async listExtensions(): Promise<ExtensionEntry[]> {
    const cacheKey = 'extensions:list';
    const cached = this.cache.get(cacheKey) as ExtensionEntry[] | undefined;
    if (cached) return cached;

    // Try Contents API first (simpler), fall back to Trees API for large dirs
    let entries: ExtensionEntry[];
    try {
      entries = await this.listExtensionsViaContents();
    } catch {
      entries = await this.listExtensionsViaTree();
    }

    this.cache.set(cacheKey, entries);
    return entries;
  }

  private async listExtensionsViaContents(): Promise<ExtensionEntry[]> {
    const data = await this.request<GitHubContentEntry[]>(
      `/repos/${this.owner}/${this.repo}/contents/${EXTENSIONS_DIR}`,
    );

    // The Contents API returns a flat array. If it's not an array the dir is
    // too large and GitHub may return a partial result or a "too large" error.
    if (!Array.isArray(data)) {
      throw new Error('Contents API did not return an array — dir too large');
    }

    return data
      .filter((e) => e.type === 'dir')
      .map((e) => ({ name: e.name, path: e.path, type: e.type }));
  }

  private async listExtensionsViaTree(): Promise<ExtensionEntry[]> {
    // Get the default branch SHA, then walk the tree
    const refData = await this.request<{ object: { sha: string } }>(
      `/repos/${this.owner}/${this.repo}/git/ref/heads/main`,
    );
    const commitSha = refData.object.sha;

    // Get the root tree
    const rootTree = await this.request<{ tree: GitHubTreeEntry[] }>(
      `/repos/${this.owner}/${this.repo}/git/trees/${commitSha}`,
    );

    const extensionsTree = rootTree.tree.find(
      (e) => e.path === EXTENSIONS_DIR && e.type === 'tree',
    );
    if (!extensionsTree) {
      throw new Error(`"${EXTENSIONS_DIR}" directory not found in repository tree`);
    }

    // Get the extensions subtree (non-recursive — just top-level)
    const subTree = await this.request<{ tree: GitHubTreeEntry[]; truncated: boolean }>(
      `/repos/${this.owner}/${this.repo}/git/trees/${extensionsTree.sha}`,
    );

    return subTree.tree
      .filter((e) => e.type === 'tree')
      .map((e) => ({
        name: e.path,
        path: `${EXTENSIONS_DIR}/${e.path}`,
        type: 'dir' as const,
      }));
  }

  // ---------------------------------------------------------------------------
  // 2. Get extension manifest
  // ---------------------------------------------------------------------------

  /**
   * Fetch and parse a specific extension's package.json.
   * Returns `null` if the extension or manifest does not exist.
   */
  async getManifest(extensionName: string): Promise<RaycastManifest | null> {
    const cacheKey = `manifest:${extensionName}`;
    const cached = this.cache.get(cacheKey) as RaycastManifest | null | undefined;
    if (cached !== undefined) return cached;

    try {
      const file = await this.request<GitHubFileContent>(
        `/repos/${this.owner}/${this.repo}/contents/${EXTENSIONS_DIR}/${extensionName}/package.json`,
      );
      const json = Buffer.from(file.content, 'base64').toString('utf-8');
      const manifest: RaycastManifest = JSON.parse(json);
      this.cache.set(cacheKey, manifest);
      return manifest;
    } catch (err) {
      if (err instanceof GitHubApiError && err.status === 404) {
        this.cache.set(cacheKey, null);
        return null;
      }
      throw err;
    }
  }

  /**
   * Check whether an extension's package.json declares Windows support.
   *
   * Rules:
   * - If `platforms` is absent or empty → assume all platforms → Windows OK
   * - If `platforms` is present → must include "windows" (case-insensitive)
   */
  isWindowsCompatible(manifest: RaycastManifest): boolean {
    if (!manifest.platforms || manifest.platforms.length === 0) {
      return true; // No restriction means all platforms
    }
    return manifest.platforms.some((p) => p.toLowerCase() === 'windows');
  }

  // ---------------------------------------------------------------------------
  // 3. Search extensions
  // ---------------------------------------------------------------------------

  /**
   * Search for extensions by keyword using the GitHub Code Search API.
   * Searches within `package.json` files in the extensions directory.
   */
  async searchExtensions(
    query: string,
    options: SearchOptions = {},
  ): Promise<ExtensionEntry[]> {
    const { limit = 30, page = 1 } = options;
    const cacheKey = `search:${query}:${page}:${limit}:${options.windowsOnly ?? false}`;
    const cached = this.cache.get(cacheKey) as ExtensionEntry[] | undefined;
    if (cached) return cached;

    const q = encodeURIComponent(
      `${query} repo:${this.owner}/${this.repo} path:${EXTENSIONS_DIR} filename:package.json`,
    );

    const data = await this.request<GitHubSearchCodeResponse>(
      `/search/code?q=${q}&per_page=${limit}&page=${page}`,
    );

    // Extract extension names from matched paths: "extensions/<name>/package.json"
    const seen = new Set<string>();
    const entries: ExtensionEntry[] = [];
    for (const item of data.items) {
      const match = item.path.match(/^extensions\/([^/]+)\/package\.json$/);
      if (match && !seen.has(match[1])) {
        seen.add(match[1]);
        entries.push({
          name: match[1],
          path: `extensions/${match[1]}`,
          type: 'dir',
        });
      }
    }

    let result = entries;
    if (options.windowsOnly) {
      result = await this.filterWindowsCompatible(entries);
    }

    this.cache.set(cacheKey, result);
    return result;
  }

  // ---------------------------------------------------------------------------
  // 4. Get extension README
  // ---------------------------------------------------------------------------

  /**
   * Fetch an extension's README.md content.
   * Returns `null` if the README does not exist.
   */
  async getReadme(extensionName: string): Promise<string | null> {
    const cacheKey = `readme:${extensionName}`;
    const cached = this.cache.get(cacheKey) as string | null | undefined;
    if (cached !== undefined) return cached;

    try {
      const file = await this.request<GitHubFileContent>(
        `/repos/${this.owner}/${this.repo}/contents/${EXTENSIONS_DIR}/${extensionName}/README.md`,
      );
      const content = Buffer.from(file.content, 'base64').toString('utf-8');
      this.cache.set(cacheKey, content);
      return content;
    } catch (err) {
      if (err instanceof GitHubApiError && err.status === 404) {
        this.cache.set(cacheKey, null);
        return null;
      }
      throw err;
    }
  }

  // ---------------------------------------------------------------------------
  // 5. Download extension source
  // ---------------------------------------------------------------------------

  /**
   * Download all files for a specific extension.
   * Uses the Git Trees API to recursively list files, then fetches each blob.
   */
  async downloadExtension(extensionName: string): Promise<ExtensionFile[]> {
    const cacheKey = `download:${extensionName}`;
    const cached = this.cache.get(cacheKey) as ExtensionFile[] | undefined;
    if (cached) return cached;

    // Get the tree for the extension directory recursively
    const extPath = `${EXTENSIONS_DIR}/${extensionName}`;

    // Step 1: resolve the tree SHA for the extension directory
    const treeSha = await this.resolveTreeSha(extPath);
    if (!treeSha) {
      throw new GitHubApiError(404, 'Not Found', `Extension "${extensionName}" not found`);
    }

    // Step 2: get recursive tree
    const tree = await this.request<{ tree: GitHubTreeEntry[]; truncated: boolean }>(
      `/repos/${this.owner}/${this.repo}/git/trees/${treeSha}?recursive=1`,
    );

    // Step 3: fetch each blob (only files, skip subdirectory trees)
    const blobs = tree.tree.filter((e) => e.type === 'blob');
    const files: ExtensionFile[] = [];

    // Batch downloads in groups of 10 to avoid flooding
    const batchSize = 10;
    for (let i = 0; i < blobs.length; i += batchSize) {
      const batch = blobs.slice(i, i + batchSize);
      const results = await Promise.all(
        batch.map(async (blob) => {
          try {
            const data = await this.request<{ content: string; encoding: string }>(
              `/repos/${this.owner}/${this.repo}/git/blobs/${blob.sha}`,
            );
            const content =
              data.encoding === 'base64'
                ? Buffer.from(data.content, 'base64').toString('utf-8')
                : data.content;
            return { path: blob.path, content };
          } catch {
            // Skip files that fail to download (binary, too large, etc.)
            return null;
          }
        }),
      );
      for (const r of results) {
        if (r) files.push(r);
      }
    }

    this.cache.set(cacheKey, files);
    return files;
  }

  /**
   * Resolve the tree SHA for a given path in the repo by walking the tree.
   */
  private async resolveTreeSha(targetPath: string): Promise<string | null> {
    const refData = await this.request<{ object: { sha: string } }>(
      `/repos/${this.owner}/${this.repo}/git/ref/heads/main`,
    );

    const parts = targetPath.split('/');
    let currentSha = refData.object.sha;

    // Walk from root commit → each path segment
    // First get the root tree from the commit
    const commit = await this.request<{ tree: { sha: string } }>(
      `/repos/${this.owner}/${this.repo}/git/commits/${currentSha}`,
    );
    currentSha = commit.tree.sha;

    for (const part of parts) {
      const tree = await this.request<{ tree: GitHubTreeEntry[] }>(
        `/repos/${this.owner}/${this.repo}/git/trees/${currentSha}`,
      );
      const entry = tree.tree.find((e) => e.path === part && e.type === 'tree');
      if (!entry) return null;
      currentSha = entry.sha;
    }

    return currentSha;
  }

  // ---------------------------------------------------------------------------
  // 6. Windows-only filtering
  // ---------------------------------------------------------------------------

  /**
   * List only Windows-compatible extensions.
   *
   * Strategy: fetch the full extension list, then batch-check manifests.
   * Results are cached aggressively since this is expensive.
   */
  async listWindowsExtensions(): Promise<ExtensionEntry[]> {
    const cacheKey = 'extensions:windows';
    const cached = this.cache.get(cacheKey) as ExtensionEntry[] | undefined;
    if (cached) return cached;

    const all = await this.listExtensions();
    const result = await this.filterWindowsCompatible(all);
    // Cache Windows list for longer (10 minutes) since it's expensive
    this.cache.set(cacheKey, result, 10 * 60 * 1000);
    return result;
  }

  /**
   * Filter a list of extensions to only those compatible with Windows.
   * Fetches each manifest in parallel batches.
   */
  private async filterWindowsCompatible(
    entries: ExtensionEntry[],
  ): Promise<ExtensionEntry[]> {
    const result: ExtensionEntry[] = [];
    const batchSize = 10;

    for (let i = 0; i < entries.length; i += batchSize) {
      const batch = entries.slice(i, i + batchSize);
      const manifests = await Promise.all(
        batch.map((e) => this.getManifest(e.name)),
      );
      for (let j = 0; j < batch.length; j++) {
        const manifest = manifests[j];
        if (manifest && this.isWindowsCompatible(manifest)) {
          result.push(batch[j]);
        }
      }
    }

    return result;
  }

  // ---------------------------------------------------------------------------
  // HTTP plumbing
  // ---------------------------------------------------------------------------

  /**
   * Make an authenticated GET request to the GitHub API.
   * Handles caching, rate limits, and error responses.
   */
  private async request<T>(path: string): Promise<T> {
    const url = path.startsWith('http') ? path : `${this.baseUrl}${path}`;

    const headers: Record<string, string> = {
      Accept: 'application/vnd.github.v3+json',
      'User-Agent': 'CmdPal-RaycastGitHubClient/1.0',
    };
    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    const response = await fetch(url, { headers });

    // Parse rate-limit headers
    this.lastRateLimit = this.parseRateLimitHeaders(response.headers);

    if (response.status === 403 && this.lastRateLimit && this.lastRateLimit.remaining === 0) {
      throw new RateLimitError(this.lastRateLimit);
    }

    if (!response.ok) {
      const body = await response.text();
      throw new GitHubApiError(response.status, response.statusText, body);
    }

    return (await response.json()) as T;
  }

  private parseRateLimitHeaders(headers: Headers): RateLimitInfo | undefined {
    const limit = headers.get('x-ratelimit-limit');
    const remaining = headers.get('x-ratelimit-remaining');
    const reset = headers.get('x-ratelimit-reset');
    const used = headers.get('x-ratelimit-used');

    if (!limit || !remaining || !reset) return undefined;

    return {
      limit: parseInt(limit, 10),
      remaining: parseInt(remaining, 10),
      reset: new Date(parseInt(reset, 10) * 1000),
      used: used ? parseInt(used, 10) : 0,
    };
  }
}
