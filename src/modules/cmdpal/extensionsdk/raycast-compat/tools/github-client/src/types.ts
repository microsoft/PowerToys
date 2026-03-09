// Copyright (c) Microsoft Corporation
// Licensed under the MIT License.

/**
 * Types for the GitHub API client for browsing raycast/extensions.
 */

/** A directory entry returned by the GitHub Contents API. */
export interface GitHubContentEntry {
  name: string;
  path: string;
  type: 'file' | 'dir' | 'symlink' | 'submodule';
  sha: string;
  size: number;
  url: string;
  download_url: string | null;
}

/** A file entry with content from the GitHub Contents API. */
export interface GitHubFileContent {
  name: string;
  path: string;
  sha: string;
  size: number;
  content: string;
  encoding: 'base64' | string;
  download_url: string | null;
}

/** A tree entry from the GitHub Git Trees API. */
export interface GitHubTreeEntry {
  path: string;
  mode: string;
  type: 'blob' | 'tree';
  sha: string;
  size?: number;
  url: string;
}

/** A search result item from the GitHub Code Search API. */
export interface GitHubSearchCodeItem {
  name: string;
  path: string;
  sha: string;
  repository: { full_name: string };
  html_url: string;
}

/** GitHub Code Search API response. */
export interface GitHubSearchCodeResponse {
  total_count: number;
  incomplete_results: boolean;
  items: GitHubSearchCodeItem[];
}

/** Simplified extension directory entry. */
export interface ExtensionEntry {
  name: string;
  path: string;
  type: string;
}

/** A Raycast extension's package.json (relevant fields). */
export interface RaycastManifest {
  name?: string;
  title?: string;
  description?: string;
  icon?: string;
  author?: string;
  categories?: string[];
  license?: string;
  commands?: RaycastCommand[];
  preferences?: RaycastPreference[];
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
  /** Optional platforms field — if present, lists supported platforms. */
  platforms?: string[];
  /** Raw parsed JSON for any fields we don't model. */
  [key: string]: unknown;
}

export interface RaycastCommand {
  name: string;
  title: string;
  subtitle?: string;
  description: string;
  mode: string;
  keywords?: string[];
  [key: string]: unknown;
}

export interface RaycastPreference {
  name: string;
  type: string;
  required: boolean;
  title: string;
  description: string;
  [key: string]: unknown;
}

/** A downloaded extension file. */
export interface ExtensionFile {
  path: string;
  content: string;
}

/** Rate limit information from GitHub API response headers. */
export interface RateLimitInfo {
  limit: number;
  remaining: number;
  reset: Date;
  used: number;
}

/** Options for the GitHub client. */
export interface GitHubClientOptions {
  /** GitHub personal access token. Falls back to GITHUB_TOKEN env var. */
  token?: string;
  /** Cache TTL in milliseconds. Default: 5 minutes. */
  cacheTtlMs?: number;
  /** Base URL for GitHub API. Default: https://api.github.com */
  baseUrl?: string;
  /** Repository owner. Default: raycast */
  owner?: string;
  /** Repository name. Default: extensions */
  repo?: string;
}

/** Search options for filtering extensions. */
export interface SearchOptions {
  /** Only return Windows-compatible extensions. */
  windowsOnly?: boolean;
  /** Maximum number of results. */
  limit?: number;
  /** Page number (1-based) for search API. */
  page?: number;
}
