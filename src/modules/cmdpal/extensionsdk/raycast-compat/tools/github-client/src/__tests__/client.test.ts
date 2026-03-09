// Copyright (c) Microsoft Corporation
// Licensed under the MIT License.

import { RaycastExtensionsClient, RateLimitError, GitHubApiError } from '../client';
import { RaycastManifest } from '../types';

// ---------------------------------------------------------------------------
// Mock fetch globally
// ---------------------------------------------------------------------------

const mockFetch = jest.fn();
(global as any).fetch = mockFetch;

function jsonResponse(body: unknown, status = 200, headers: Record<string, string> = {}): Response {
  const defaultHeaders: Record<string, string> = {
    'x-ratelimit-limit': '5000',
    'x-ratelimit-remaining': '4999',
    'x-ratelimit-reset': String(Math.floor(Date.now() / 1000) + 3600),
    'x-ratelimit-used': '1',
    ...headers,
  };
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    headers: new Headers(defaultHeaders),
    json: async () => body,
    text: async () => JSON.stringify(body),
  } as unknown as Response;
}

function base64Encode(str: string): string {
  return Buffer.from(str, 'utf-8').toString('base64');
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('RaycastExtensionsClient', () => {
  let client: RaycastExtensionsClient;

  beforeEach(() => {
    mockFetch.mockReset();
    client = new RaycastExtensionsClient({ token: 'test-token' });
  });

  // ---- listExtensions (Contents API path) ----

  describe('listExtensions', () => {
    it('returns directory entries from Contents API', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse([
          { name: 'raycast-wallpaper', path: 'extensions/raycast-wallpaper', type: 'dir', sha: 'abc', size: 0, url: '', download_url: null },
          { name: 'spotify-player', path: 'extensions/spotify-player', type: 'dir', sha: 'def', size: 0, url: '', download_url: null },
          { name: '.gitkeep', path: 'extensions/.gitkeep', type: 'file', sha: 'ghi', size: 0, url: '', download_url: null },
        ]),
      );

      const entries = await client.listExtensions();
      expect(entries).toHaveLength(2);
      expect(entries[0].name).toBe('raycast-wallpaper');
      expect(entries[1].name).toBe('spotify-player');
      // Verify files are filtered out
      expect(entries.every((e) => e.type === 'dir')).toBe(true);
    });

    it('falls back to Tree API when Contents API fails', async () => {
      // Contents API fails
      mockFetch.mockResolvedValueOnce(jsonResponse({ message: 'too large' }, 403));

      // Tree API: get ref
      mockFetch.mockResolvedValueOnce(jsonResponse({ object: { sha: 'commit-sha' } }));
      // Tree API: get root tree
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          tree: [
            { path: 'extensions', type: 'tree', sha: 'ext-tree-sha', mode: '040000', url: '' },
            { path: 'README.md', type: 'blob', sha: 'rm-sha', mode: '100644', url: '' },
          ],
        }),
      );
      // Tree API: get extensions subtree
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          tree: [
            { path: 'ext-a', type: 'tree', sha: 'a-sha', mode: '040000', url: '' },
            { path: 'ext-b', type: 'tree', sha: 'b-sha', mode: '040000', url: '' },
          ],
          truncated: false,
        }),
      );

      const entries = await client.listExtensions();
      expect(entries).toHaveLength(2);
      expect(entries[0].name).toBe('ext-a');
      expect(entries[0].path).toBe('extensions/ext-a');
    });

    it('caches results on second call', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse([
          { name: 'ext-a', path: 'extensions/ext-a', type: 'dir', sha: 'a', size: 0, url: '', download_url: null },
        ]),
      );

      await client.listExtensions();
      await client.listExtensions(); // Should hit cache
      expect(mockFetch).toHaveBeenCalledTimes(1);
    });
  });

  // ---- getManifest ----

  describe('getManifest', () => {
    it('fetches and decodes a base64 package.json', async () => {
      const manifest: RaycastManifest = {
        name: 'test-ext',
        title: 'Test Extension',
        description: 'A test extension',
        platforms: ['macOS', 'Windows'],
      };
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          name: 'package.json',
          path: 'extensions/test-ext/package.json',
          sha: 'abc',
          size: 100,
          content: base64Encode(JSON.stringify(manifest)),
          encoding: 'base64',
          download_url: null,
        }),
      );

      const result = await client.getManifest('test-ext');
      expect(result).not.toBeNull();
      expect(result!.title).toBe('Test Extension');
      expect(result!.platforms).toEqual(['macOS', 'Windows']);
    });

    it('returns null for 404', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse({ message: 'Not Found' }, 404),
      );

      const result = await client.getManifest('nonexistent');
      expect(result).toBeNull();
    });

    it('caches null results for 404s', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse({ message: 'Not Found' }, 404),
      );

      await client.getManifest('nonexistent');
      await client.getManifest('nonexistent');
      expect(mockFetch).toHaveBeenCalledTimes(1);
    });
  });

  // ---- isWindowsCompatible ----

  describe('isWindowsCompatible', () => {
    it('returns true when platforms is absent', () => {
      expect(client.isWindowsCompatible({ name: 'ext' })).toBe(true);
    });

    it('returns true when platforms is empty', () => {
      expect(client.isWindowsCompatible({ name: 'ext', platforms: [] })).toBe(true);
    });

    it('returns true when platforms includes Windows', () => {
      expect(
        client.isWindowsCompatible({ name: 'ext', platforms: ['macOS', 'Windows'] }),
      ).toBe(true);
    });

    it('returns true case-insensitively', () => {
      expect(
        client.isWindowsCompatible({ name: 'ext', platforms: ['windows'] }),
      ).toBe(true);
    });

    it('returns false when platforms excludes Windows', () => {
      expect(
        client.isWindowsCompatible({ name: 'ext', platforms: ['macOS', 'Linux'] }),
      ).toBe(false);
    });
  });

  // ---- searchExtensions ----

  describe('searchExtensions', () => {
    it('parses search results into extension entries', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          total_count: 2,
          incomplete_results: false,
          items: [
            {
              name: 'package.json',
              path: 'extensions/spotify-player/package.json',
              sha: 'a',
              repository: { full_name: 'raycast/extensions' },
              html_url: '',
            },
            {
              name: 'package.json',
              path: 'extensions/apple-music/package.json',
              sha: 'b',
              repository: { full_name: 'raycast/extensions' },
              html_url: '',
            },
          ],
        }),
      );

      const results = await client.searchExtensions('music');
      expect(results).toHaveLength(2);
      expect(results[0].name).toBe('spotify-player');
      expect(results[1].name).toBe('apple-music');
    });

    it('deduplicates results', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          total_count: 2,
          incomplete_results: false,
          items: [
            { name: 'package.json', path: 'extensions/ext-a/package.json', sha: 'a', repository: { full_name: 'raycast/extensions' }, html_url: '' },
            { name: 'package.json', path: 'extensions/ext-a/package.json', sha: 'a', repository: { full_name: 'raycast/extensions' }, html_url: '' },
          ],
        }),
      );

      const results = await client.searchExtensions('test');
      expect(results).toHaveLength(1);
    });
  });

  // ---- getReadme ----

  describe('getReadme', () => {
    it('decodes README content from base64', async () => {
      const readme = '# Test Extension\n\nThis is a test.';
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          name: 'README.md',
          path: 'extensions/test-ext/README.md',
          sha: 'abc',
          size: readme.length,
          content: base64Encode(readme),
          encoding: 'base64',
          download_url: null,
        }),
      );

      const result = await client.getReadme('test-ext');
      expect(result).toBe(readme);
    });

    it('returns null for missing README', async () => {
      mockFetch.mockResolvedValueOnce(jsonResponse({ message: 'Not Found' }, 404));
      const result = await client.getReadme('no-readme');
      expect(result).toBeNull();
    });
  });

  // ---- downloadExtension ----

  describe('downloadExtension', () => {
    it('recursively downloads extension files', async () => {
      // Resolve ref
      mockFetch.mockResolvedValueOnce(jsonResponse({ object: { sha: 'commit-sha' } }));
      // Resolve commit → root tree
      mockFetch.mockResolvedValueOnce(jsonResponse({ tree: { sha: 'root-tree-sha' } }));
      // Walk: root tree
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          tree: [{ path: 'extensions', type: 'tree', sha: 'ext-sha', mode: '040000', url: '' }],
        }),
      );
      // Walk: extensions tree
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          tree: [{ path: 'my-ext', type: 'tree', sha: 'my-ext-sha', mode: '040000', url: '' }],
        }),
      );
      // Recursive tree of the extension
      mockFetch.mockResolvedValueOnce(
        jsonResponse({
          tree: [
            { path: 'package.json', type: 'blob', sha: 'pkg-sha', mode: '100644', url: '' },
            { path: 'src/index.ts', type: 'blob', sha: 'idx-sha', mode: '100644', url: '' },
            { path: 'src', type: 'tree', sha: 'src-sha', mode: '040000', url: '' },
          ],
          truncated: false,
        }),
      );
      // Blob downloads
      mockFetch.mockResolvedValueOnce(
        jsonResponse({ content: base64Encode('{"name":"my-ext"}'), encoding: 'base64' }),
      );
      mockFetch.mockResolvedValueOnce(
        jsonResponse({ content: base64Encode('console.log("hello")'), encoding: 'base64' }),
      );

      const files = await client.downloadExtension('my-ext');
      expect(files).toHaveLength(2);
      expect(files[0].path).toBe('package.json');
      expect(files[0].content).toBe('{"name":"my-ext"}');
      expect(files[1].path).toBe('src/index.ts');
    });
  });

  // ---- Rate limit handling ----

  describe('rate limiting', () => {
    it('throws RateLimitError on 403 with zero remaining', async () => {
      const rateLimitResponse = jsonResponse(
        { message: 'API rate limit exceeded' },
        403,
        {
          'x-ratelimit-limit': '60',
          'x-ratelimit-remaining': '0',
          'x-ratelimit-reset': String(Math.floor(Date.now() / 1000) + 3600),
          'x-ratelimit-used': '60',
        },
      );
      // Both Contents API and Tree API fallback hit rate limit
      mockFetch.mockResolvedValue(rateLimitResponse);

      await expect(client.listExtensions()).rejects.toThrow(RateLimitError);
    });

    it('exposes rate limit info after requests', async () => {
      mockFetch.mockResolvedValueOnce(
        jsonResponse(
          [{ name: 'ext', path: 'extensions/ext', type: 'dir', sha: 'a', size: 0, url: '', download_url: null }],
          200,
          {
            'x-ratelimit-limit': '5000',
            'x-ratelimit-remaining': '4990',
            'x-ratelimit-reset': String(Math.floor(Date.now() / 1000) + 3600),
            'x-ratelimit-used': '10',
          },
        ),
      );

      await client.listExtensions();
      expect(client.rateLimit).toBeDefined();
      expect(client.rateLimit!.remaining).toBe(4990);
      expect(client.rateLimit!.limit).toBe(5000);
    });
  });

  // ---- Error handling ----

  describe('error handling', () => {
    it('throws GitHubApiError for non-rate-limit errors', async () => {
      // Use mockResolvedValue so both Contents and Tree fallback hit the same error
      mockFetch.mockResolvedValue(
        jsonResponse({ message: 'Internal Server Error' }, 500),
      );

      await expect(client.listExtensions()).rejects.toThrow(GitHubApiError);
    });

    it('includes status and body in error', async () => {
      mockFetch.mockResolvedValue(
        jsonResponse({ message: 'Bad Request' }, 400),
      );

      try {
        await client.listExtensions();
        fail('Should have thrown');
      } catch (err) {
        expect(err).toBeInstanceOf(GitHubApiError);
        const apiErr = err as GitHubApiError;
        expect(apiErr.status).toBe(400);
      }
    });
  });

  // ---- Constructor / options ----

  describe('options', () => {
    it('uses custom base URL', () => {
      const custom = new RaycastExtensionsClient({
        baseUrl: 'https://custom.api.com/',
        token: 'tok',
      });
      expect(custom).toBeDefined();
    });

    it('clearCache works', async () => {
      mockFetch.mockResolvedValue(
        jsonResponse([
          { name: 'ext', path: 'extensions/ext', type: 'dir', sha: 'a', size: 0, url: '', download_url: null },
        ]),
      );

      await client.listExtensions();
      client.clearCache();
      await client.listExtensions();
      expect(mockFetch).toHaveBeenCalledTimes(2);
    });
  });
});
