# 04 - Manifest, Packaging, and Installation

## Extension Project Structure

A CmdPal JavaScript extension is a standard Node.js project. Extension metadata is declared in the `cmdpal` field of `package.json` (similar to how VS Code uses `contributes`):

```
my-extension/
├── package.json         # Node.js manifest + "cmdpal" section (required)
├── dist/
│   └── index.js         # Compiled entry point
├── src/
│   └── index.ts         # TypeScript source
├── tsconfig.json        # TypeScript config
├── node_modules/        # Dependencies (ideally bundled)
└── icon.png             # Extension icon (optional)
```

The key files:
- **`package.json`**: Standard Node.js package manifest with an added `cmdpal` section for CmdPal-specific metadata
- **`dist/index.js`**: The compiled JavaScript entry point that CmdPal will execute

---

## `package.json` Schema

CmdPal discovers extensions by finding directories with a `package.json` that contains a `cmdpal` object. Top-level npm fields provide identity; the `cmdpal` section provides CmdPal-specific metadata. The parsed `cmdpal` fields are `displayName`, `icon`, `publisher`, `main`, `watchPath`, `debug`, and `debugPort`.

### Full Example

```json
{
  "name": "@microsoft/cmdpal-my-extension",
  "version": "1.0.0",
  "description": "A brief description of the extension",
  "type": "module",
  "main": "dist/index.js",
  "engines": {
    "node": ">=22.0.0"
  },
  "cmdpal": {
    "displayName": "My Extension",
    "icon": "icon.png",
    "publisher": "your-name",
    "watchPath": "dist",
    "debug": false,
    "debugPort": 9230
  },
  "scripts": {
    "build": "tsc",
    "dev": "tsc --watch"
  },
  "dependencies": {
    "@microsoft/cmdpal-sdk": "file:../../ts-sdk"
  },
  "devDependencies": {
    "typescript": "^5.8.0"
  },
  "keywords": ["cmdpal", "powertoys", "command-palette"]
}
```

### Field Reference

#### Top-level fields (standard npm)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | `string` | ✅ | Package identifier. Must be unique across installed extensions. Used as the extension ID. |
| `version` | `string` | ❌ | Semantic version string (e.g., `"1.0.0"`). |
| `description` | `string` | ❌ | Brief description shown in the extension gallery and settings. |
| `author` | `string` or `object` | ❌ | npm author. Used as the publisher name only when `cmdpal.publisher` is absent. Accepts the string form `"Name <email> (url)"` or an object with a `name` property; only the name is used. |
| `main` | `string` | Conditional | Relative path to the entry point JavaScript file. Required when `cmdpal.main` is not specified. This is what `node` executes. |
| `engines.node` | `string` | ❌ | Node.js version requirement (expected value: `">=22.0.0"`). |

#### `cmdpal` section fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `displayName` | `string` | ❌ | Human-readable name shown in CmdPal UI. Falls back to `name` if not provided. |
| `icon` | `string` | ❌ | Icon glyph character (e.g., `"\uE943"`), non-file URL, or a relative path to an icon file (PNG recommended) inside the package. Relative files are resolved against the package directory and must stay inside it; rooted filesystem paths and file URLs are rejected. See [Icon resolution](#icon-resolution). |
| `publisher` | `string` | ❌ | Author or publisher name. When omitted, the top-level npm `author` name is used as a fallback. |
| `debug` | `boolean` | ❌ | When `true`, starts Node.js with `--inspect` for debugger attachment. Default: `false`. |
| `debugPort` | `integer` | ❌ | Inspector port when `debug` is `true`. If not specified, auto-assigned starting at 9229. |
| `main` | `string` | ❌ | Optional override of the top-level `main` field (for packages where the CmdPal entry point differs from the npm main). |
| `watchPath` | `string` | ❌ | Relative directory watched recursively for `.js`, `.mjs`, and `.cjs` changes. When omitted, CmdPal watches the entry point's directory. |

### Validation Rules

CmdPal parses each `package.json` and only loads the directory as an extension when every rule below passes. A failure is fatal for that extension: the directory is skipped (during discovery) or the install is rejected (from the gallery), and the reason is logged. The rules are enforced in `JSExtensionManifest.TryParse`.

1. **`cmdpal` object present.** The manifest must contain a `cmdpal` object (even if empty: `"cmdpal": {}`).
2. **`name` present.** The top-level `name` must be present and non-empty.
3. **Entry point declared.** `cmdpal.main` (preferred) or the top-level `main` must specify an entry point. If both are missing or blank, the extension is rejected.
4. **Entry point stays inside the extension.** The entry point must be a **relative** path (a rooted or absolute path is rejected) that resolves to a location **inside** the extension directory. A path that escapes the directory with `..` is rejected so an extension cannot point its entry point at a file outside its own folder.
5. **Entry point is a JavaScript module.** The resolved entry point must end in `.js`, `.mjs`, or `.cjs`. Any other extension (for example an uncompiled `.ts` source) is rejected, because the host runs the file directly with `node`.
6. **Entry point exists.** The resolved entry point must be an existing file on disk. A `main` that points at a file that was never built or shipped is rejected.
7. **No symlink or junction escape.** After confirming the file exists, the resolved entry point is re-checked against the real filesystem: a symbolic link, junction, or other reparse point that redirects the entry point outside the extension directory is rejected, even when the lexical path (rule 4) stayed inside it.
8. **Watch path stays inside the extension.** When present, `cmdpal.watchPath` must be a relative path to an existing directory inside the extension. Rooted paths, `..` escapes, and paths that traverse a symbolic link or junction are rejected. When omitted, the source watcher uses the entry point's directory.

A resolved relative icon (`cmdpal.icon`) is subject to the same containment rules; see [Icon resolution](#icon-resolution).

### Icon resolution

The `cmdpal.icon` value is interpreted as follows:

- A **glyph** (for example, `"\uE943"`) or a **non-file URL** (for example, an
  `https://` value) is trimmed and then used as written.
- A **relative file path** (for example, `"icon.png"` or `"assets/icon.png"`) is
  resolved against the extension's own installed directory, which is the folder that
  contains its `package.json`.
- A **rooted filesystem path** or **file URL** is rejected.

A resolved relative file icon must stay **inside** the package directory. The path is
rejected (and the extension shows no icon rather than loading an out-of-package file)
when:

- it escapes the package with `..`,
- it is redirected outside the package by a symbolic link, junction, or other reparse
  point, or
- the target file does not exist.

Keep icon files inside your package (and list them in `files`) so they are present in
the installed directory. There is a single `icon` value; separate light and dark
variants are not currently expressed in the manifest.

---

## Installation Directory

Extensions are installed to:

```
%LOCALAPPDATA%\Microsoft\PowerToys\CmdPal\JSExtensions\
```

Each extension occupies its own subdirectory:

```
JSExtensions/
├── my-extension/
│   ├── package.json       ← contains "cmdpal" section
│   ├── dist/
│   │   └── index.js
│   └── node_modules/
├── another-extension/
│   ├── package.json
│   └── ...
```

### Discovery

The `JsonRpcExtensionService` watches this directory for extension installs and removals:
- **New directory with valid `package.json`** (for example, a sideloaded extension copied in) is loaded automatically
- **Directory removed** unloads the extension and terminates its Node.js process

Each loaded extension also gets a recursive source watcher. It uses `cmdpal.watchPath`
when declared, or the entry point's directory otherwise. Changes to `.js`, `.mjs`, and
`.cjs` files trigger hot-reload after a 500ms debounce. Changes under `node_modules` are
ignored.

This means for sideloaded development:
- Installing an extension = copying a fully prepared directory into `JSExtensions/`
- Uninstalling = deleting the directory
- Updating = replacing files (hot-reload handles `.js`, `.mjs`, and `.cjs` changes)

Gallery installs do not rely on the watcher observing a half-written directory. The
installer prepares the extension in a staging location outside `JSExtensions/`,
verifies it, and then moves the finished directory into place in a single atomic
step. See [Installation Flow](#installation-flow) for the full sequence. Because the
directory only ever appears complete, the watcher never sees a partially copied
extension.

---

## Development Setup

### Creating a New Extension

1. **Create the project directory:**
   ```bash
   mkdir my-extension && cd my-extension
   npm init -y
   ```

2. **Install the SDK:**
   ```bash
   npm install ..\..\ts-sdk
   ```

3. **Add the `cmdpal` section to `package.json`:**
   ```json
   {
     "name": "my-extension",
     "version": "1.0.0",
     "description": "My awesome CmdPal extension",
     "type": "module",
     "main": "dist/index.js",
     "cmdpal": {
       "displayName": "My Extension",
       "debug": true
     },
     "engines": {
       "node": ">=22.0.0"
     },
     "dependencies": {
       "@microsoft/cmdpal-sdk": "file:../../ts-sdk"
     }
   }
   ```

4. **Create `tsconfig.json`:**
   ```json
   {
     "compilerOptions": {
       "target": "ES2022",
       "module": "NodeNext",
       "moduleResolution": "NodeNext",
       "outDir": "./dist",
       "rootDir": "./src",
       "strict": true,
       "esModuleInterop": true,
       "skipLibCheck": true
     },
     "include": ["src/**/*"]
   }
   ```

5. **Write your extension** in `src/index.ts` (see [05-getting-started.md](./05-getting-started.md))

6. **Build:**
   ```bash
   npx tsc
   ```

### Development Installation

For development, symlink or copy your extension to the JSExtensions directory:

```powershell
# Option 1: Copy
Copy-Item -Recurse ./my-extension "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\JSExtensions\my-extension"

# Option 2: Junction link (recommended for development)
New-Item -ItemType Junction -Path "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\JSExtensions\my-extension" -Target (Resolve-Path ./my-extension)
```

With a junction link, changes to built `.js`, `.mjs`, or `.cjs` files under the source watch root trigger hot-reload automatically.

### Debugging

1. Set `"debug": true` in the `cmdpal` section of `package.json`
2. Optionally set `"debugPort": 9230` (or any available port)
3. Open Chrome DevTools: `chrome://inspect` or attach VS Code's debugger
4. The Node.js process starts with `--inspect=<port>`, ready for debugger attachment

---

## Production Packaging

### SDK distribution status

Registry distribution of `@microsoft/cmdpal-sdk` is **not yet supported**: the SDK
is not published to a public npm registry, so a published package cannot depend on
it by version. The way you reference the SDK differs between local development and
gallery submission, and getting this wrong is the most common reason a package fails
to install from the gallery.

**Local development.** Reference the in-repo SDK directly with
`"@microsoft/cmdpal-sdk": "file:../../ts-sdk"` (or `npm link`). This is convenient
while iterating inside this repository, but it is **not** submittable to the gallery:
a `file:` dependency resolves to a local path, not a registry URL, so it carries no
integrity (SRI) hash and cannot appear in an `npm-shrinkwrap.json` as a verifiable,
trusted entry. The gallery installer rejects any package whose dependency closure
contains such an untrusted entry.

**Gallery submission.** The published package must not carry a `file:` dependency on
the SDK. Two paths satisfy the gallery's trusted-lockfile rules:

1. **Bundle the SDK into `dist/`** (required today, recommended). Use a bundler (for
   example, `esbuild` or `rollup`) so the SDK is inlined into the files you ship under
   `dist/`. The published package then has **no runtime dependency** on
   `@microsoft/cmdpal-sdk` at all, so nothing about the SDK needs to appear in the
   lockfile, and `npm install <your-package>` needs no access to this repository. This
   is the only submittable path until the SDK is published to a registry.
2. **Depend on the published SDK by version** (available once the SDK ships to a
   trusted registry). Declare `"@microsoft/cmdpal-sdk": "<exact-version>"` under
   `dependencies` so it resolves to a registry URL with an integrity hash and is frozen
   in the embedded `npm-shrinkwrap.json` (see [Freezing the dependency
   closure](#freezing-the-dependency-closure-npm-shrinkwrapjson)). Do **not** use a
   vendored `file:` tarball (`file:./microsoft-cmdpal-sdk-<version>.tgz`) for
   submission: like `file:../../ts-sdk`, it lacks a trusted registry URL and SRI and is
   rejected.

Do not ship a gallery package whose path to the SDK is any `file:` reference
(`file:../../ts-sdk` or a vendored `.tgz`). Those forms are for **local development
only** (see [Development Setup](#development-setup)); bundle the SDK instead.

### npm Package Structure

Extensions are distributed as standard npm packages. The recommended `package.json`
for a bundled production build:

```json
{
  "name": "@publisher/cmdpal-my-extension",
  "version": "1.0.0",
  "description": "My CmdPal extension",
  "type": "module",
  "main": "dist/index.js",
  "cmdpal": {
    "displayName": "My Extension",
    "icon": "icon.png",
    "publisher": "your-name"
  },
  "scripts": {
    "build": "tsc && esbuild dist/index.js --bundle --platform=node --format=esm --outfile=dist/index.js --allow-overwrite",
    "prepack": "npm run build"
  },
  "files": [
    "dist/",
    "icon.png"
  ],
  "devDependencies": {
    "@microsoft/cmdpal-sdk": "file:../../ts-sdk",
    "esbuild": "^0.23.0",
    "typescript": "^5.8.0"
  },
  "keywords": ["cmdpal", "powertoys", "command-palette"],
  "engines": {
    "node": ">=22.0.0"
  }
}
```

The SDK appears only under `devDependencies` because the build step inlines it into
`dist/`. The shipped package therefore lists no runtime dependency on
`@microsoft/cmdpal-sdk`.

The build is wired to the **`prepack`** lifecycle hook, not `prepublishOnly`, on
purpose. `npm pack` (which produces the tarball the gallery ultimately installs, and
the tarball you validate below) runs `prepack` but does **not** run `prepublishOnly`;
only `npm publish` runs `prepublishOnly`. Using `prepack` guarantees `dist/` is
rebuilt whenever the tarball is assembled, whether you are validating locally or
publishing, so a clean checkout can never pack a stale or missing `dist/`. The SDK
itself takes the equivalent belt-and-suspenders approach: its `verify:pack` script
runs `npm run build` explicitly before `npm pack` rather than trusting a publish-only
hook.

### Validating a clean install

To confirm your package installs without the PowerToys repository present, pack it
and install it into a throwaway directory:

```powershell
npm pack                     # runs the prepack build, then produces publisher-cmdpal-my-extension-1.0.0.tgz with dist/ inside
$temp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ("cmdpal-smoke-" + [guid]::NewGuid()))
Copy-Item .\publisher-cmdpal-my-extension-1.0.0.tgz $temp
Push-Location $temp
npm init -y | Out-Null
npm install .\publisher-cmdpal-my-extension-1.0.0.tgz
node -e "import('@publisher/cmdpal-my-extension').then(() => console.log('loaded'))"
Pop-Location
```

A bundled package resolves with no reference back to `ts-sdk`. The `ts-sdk` package
ships its own equivalent check (`npm run verify:pack`) that packs the SDK, installs
the tarball into a temporary project, and type-checks against it.

### Naming Convention

Recommended npm package naming: `@publisher/cmdpal-<name>` or `cmdpal-<name>`.

The `cmdpal-` prefix helps with discoverability and could be used for future npm-based discovery.

---

## Extension Gallery Integration


### Gallery Manifest Entry

The existing CmdPal extension gallery pulls from a feed that lists available extensions. The feed is a wrapped document with an `extensions` array; each entry describes one extension and how to install it. For a JavaScript/TypeScript extension, the install information lives in an `installSources` entry whose `type` is `"jsonrpc"`:

```json
{
  "extensions": [
    {
      "id": "publisher.cmdpal-my-extension",
      "title": "My Extension",
      "description": "Does amazing things from the Command Palette.",
      "shortDescription": "Does amazing things.",
      "author": {
        "name": "Your Name",
        "url": "https://example.com"
      },
      "homepage": "https://example.com/my-extension",
      "tags": ["cmdpal", "productivity"],
      "installSources": [
        {
          "type": "jsonrpc",
          "npm": {
            "package": "@publisher/cmdpal-my-extension",
            "version": "1.0.0",
            "integrity": "sha512-3sxT2b3Ea2u2vLXA7Yl0dOZH3Rm9j1p3T0i8b9m2wJ0kZ8t2K1cQ0f8p7L6r5S4d3F2a1B0c9D8e7F6g5H4i3J2k1L0m9N8o7P6q5R4s3T2u1V0w==",
            "registry": "https://registry.npmjs.org"
          }
        }
      ]
    }
  ]
}
```

Fields on the `jsonrpc` install source's `npm` object:

| Field | Required | Notes |
|-------|----------|-------|
| `package` | Yes | npm package identifier to install. |
| `version` | Yes | Exact version to install. Ranges and dist-tags (such as `latest`) are rejected so the installed bytes always match the approved artifact. |
| `integrity` | Yes | Subresource Integrity (`sha512-...`) of the approved tarball. The installer verifies the resolved package against this value before promoting it. |
| `registry` | No | Absolute HTTPS registry URL. When present it must be on the approved allowlist. When omitted, the machine's default registry is used. |

An install source that does not pin both `version` and `integrity` is **not installable**: the installer fails closed rather than fetching an unverified package. Optional presentation fields (`shortDescription`, `homepage`, `iconUrl`, `screenshotUrls`, `readme`, `tags`) and the COM `detection` block are documented with the gallery models and are not required for a `jsonrpc` extension.

The `type: "jsonrpc"` install source distinguishes JavaScript extensions from COM-based extensions.

### Freezing the dependency closure (`npm-shrinkwrap.json`)

Gallery submission requires the published package to embed an `npm-shrinkwrap.json`.
The gallery installer rejects a package that does not ship one, because without it the
transitive dependency closure is not frozen: a later publish of one of your
dependencies could change the bytes that land on a user's machine, and there would be
no lockfile pinning each dependency to an exact version, resolved registry URL, and
integrity (SRI) hash. `npm-shrinkwrap.json` is npm's publishable lockfile (unlike
`package-lock.json`, npm includes it in the tarball), so it travels with the package
and lets the installer verify the whole closure against trusted registry entries.

If you **bundle** the SDK into `dist/` (the path required today) your extension may
have no runtime dependencies at all, but you must still ship an `npm-shrinkwrap.json`
so the closure is explicitly frozen (an empty or dependency-free closure is still a
verified one).

To create and maintain it:

```powershell
npm install          # resolve the exact dependency tree into package-lock.json
npm shrinkwrap       # rename/convert it to a publishable npm-shrinkwrap.json
```

Then commit `npm-shrinkwrap.json` and publish as usual (`npm publish`, or `npm pack`
for the tarball the gallery approves). npm includes `npm-shrinkwrap.json` in the
tarball automatically, so no `files` entry is needed for it. Regenerate it whenever
your dependencies change, which in practice means **once per release**: bump the
version, run `npm install` and `npm shrinkwrap` again, and commit the refreshed
lockfile alongside the new version.

### Installation Flow

When a user clicks "Install" for a JavaScript extension in the gallery, the installer prepares the extension out of sight of the watcher and only reveals it once it is verified and complete:

1. The `version` and `integrity` fields are validated, and any `registry` is checked against the HTTPS allowlist. A source that omits `version` or `integrity` is rejected before anything is downloaded.
2. `npm pack <package>@<version>` downloads the exact tarball (with lifecycle scripts disabled) into a fresh GUID-named staging directory that lives **outside** the watched `JSExtensions/` root, on the same volume so the later move is atomic. The package is never installed as a dependency, so npm cannot re-resolve a range or nest it under a parent `node_modules`.
3. The integrity that npm reports for the packed tarball is compared against the `integrity` value from the feed. A mismatch aborts the install.
4. The tarball is extracted so the published package becomes the staged root (npm roots every tarball entry under `package/`). The installer then requires a publisher-provided `npm-shrinkwrap.json` in that root and rejects the package when it is missing.
5. `npm ci` runs inside the extracted root, installing the frozen dependency closure into the package's own `node_modules` without re-resolving any version. The resolved lockfile is verified so the whole closure matches trusted registry entries.
6. The installer parses the root `package.json` and confirms the manifest identity (package name) and `version` match what the feed approved before anything is promoted.
7. The finished directory is promoted into `JSExtensions\<id>` with a single atomic `Directory.Move`, so the directory only ever appears complete.
8. The host is asked to refresh and **awaits provider registration** (`OnProviderAdded`) before the install is reported as successful. The staging directory is cleaned up regardless of outcome.

Because promotion is atomic and registration is awaited, a completed gallery install is guaranteed to be loadable when the install call returns; the `FileSystemWatcher` is not relied upon to catch a partially written directory.

### Uninstallation Flow

When a user clicks "Uninstall":

1. CmdPal terminates the extension's Node.js process
2. The extension directory is deleted from `JSExtensions/`
3. `FileSystemWatcher` detects the removal → extension is unloaded

---

## Security Considerations

### Process Isolation

Each JavaScript extension runs in its own Node.js process:
- Separate memory space
- Separate event loop
- No direct access to other extensions or CmdPal internals
- Communication only through the JSON-RPC protocol

This process separation is a fault and crash boundary, not a security boundary. An extension is not sandboxed: it runs with the user's full privileges (see [Permissions](#permissions) below), so a separate process does not contain what the extension's own code is allowed to do.

### Permissions

Currently, JavaScript extensions have the same permissions as the Node.js process:
- File system access
- Network access
- Process spawning

Future considerations:
- Extension permission declarations in `package.json` `cmdpal` section
- User consent prompts for sensitive permissions
- Sandboxing via Node.js `--experimental-policy` or similar mechanisms

### Trust Model

- Extensions installed from the gallery are implicitly trusted by the user
- Sideloaded extensions (copied to JSExtensions/) have no verification
