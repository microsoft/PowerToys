### 2025-07: GitHub API Client for Raycast Extension Browsing
**By:** Kane
**Status:** Implemented
**What:** Created a TypeScript GitHub API client at `tools/github-client/` that can browse the `raycast/extensions` repository — list extensions, fetch manifests, search by keyword, download source, and filter for Windows compatibility.
**Key decisions:**
1. **Git Trees API fallback** — The Contents API truncates at ~1000 entries, so `listExtensions()` auto-falls back to the Trees API for complete listings
2. **Windows compatibility heuristic** — If `platforms` is absent or empty in package.json, we assume all platforms including Windows. If present, must include "windows" (case-insensitive)
3. **No external HTTP deps** — Uses Node.js 18+ built-in `fetch` to keep the tool dependency-free
4. **In-memory TTL cache** — 5 minute default, 10 minutes for expensive Windows-only filtering (which batch-fetches manifests in groups of 10)
5. **Standalone package** — Follows the `manifest-translator` sibling pattern with own package.json/tsconfig/jest rather than embedding in the parent raycast-compat project
**Why:** The C# extension will need a build-pipeline tool to pre-index which Raycast extensions are Windows-compatible. Doing this in TypeScript/Node.js keeps the GitHub API integration simple and testable, and outputs can be consumed by the C# extension as cached JSON data.
**Impact:** Enables the Raycast Extension Store feature to discover and filter 1000+ extensions efficiently.
