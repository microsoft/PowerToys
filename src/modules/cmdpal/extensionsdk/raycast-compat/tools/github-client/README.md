# @cmdpal/raycast-github-client

GitHub API client for browsing and downloading Raycast extensions from the [`raycast/extensions`](https://github.com/raycast/extensions) repository.

## Features

- **List extensions** — enumerate all extensions in the repo (handles 1000+ via Git Trees API fallback)
- **Get manifest** — fetch and parse any extension's `package.json`
- **Search** — keyword search via GitHub Code Search API
- **Get README** — fetch extension documentation
- **Download source** — recursively download all files for an extension
- **Windows filtering** — built-in filter rejecting non-Windows extensions based on `platforms` field

## Quick start

```bash
npm install
npm run build

# List first 20 extensions and check Windows compatibility
GITHUB_TOKEN=ghp_xxx node dist/list-windows.js

# Search for "calculator" extensions
GITHUB_TOKEN=ghp_xxx node dist/list-windows.js --search calculator --limit 10
```

## API

```typescript
import { RaycastExtensionsClient } from '@cmdpal/raycast-github-client';

const client = new RaycastExtensionsClient({
  token: process.env.GITHUB_TOKEN,  // optional, 60 req/hr without
  cacheTtlMs: 5 * 60 * 1000,       // 5 min default
});

// List all extension directories
const extensions = await client.listExtensions();

// Get a specific extension's manifest
const manifest = await client.getManifest('raycast-wallpaper');

// Check Windows compatibility
const isWindows = client.isWindowsCompatible(manifest!);

// Search by keyword
const results = await client.searchExtensions('calculator', { windowsOnly: true });

// Get README
const readme = await client.getReadme('raycast-wallpaper');

// Download all source files
const files = await client.downloadExtension('raycast-wallpaper');

// List only Windows-compatible extensions (expensive — fetches all manifests)
const windowsExts = await client.listWindowsExtensions();
```

## Rate limits

| Mode | Limit |
|------|-------|
| Unauthenticated | 60 req/hour |
| With `GITHUB_TOKEN` | 5,000 req/hour |

The client exposes `client.rateLimit` after each request and throws `RateLimitError` when the limit is hit.

## Caching

All API responses are cached in memory with a configurable TTL (default 5 minutes). The Windows-compatible extension list uses a longer 10-minute TTL since it requires many API calls.

Call `client.clearCache()` to force fresh data.

## Testing

```bash
npm test          # run all tests (mocked, no network)
npm run test:ci   # CI mode
```
