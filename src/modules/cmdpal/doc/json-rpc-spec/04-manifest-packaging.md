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

CmdPal discovers extensions by finding directories with a `package.json` that contains a `cmdpal` object. Top-level npm fields provide identity; the `cmdpal` section provides CmdPal-specific metadata. The parsed `cmdpal` fields are `displayName`, `icon`, `publisher`, `main`, `debug`, and `debugPort`.

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
| `icon` | `string` | ❌ | Icon glyph character (e.g., `"\uE943"`) or a relative path to an icon file (PNG recommended) inside the package. A relative path is resolved against the package's own directory and must stay inside it; see [Icon resolution](#icon-resolution). |
| `publisher` | `string` | ❌ | Author or publisher name. When omitted, the top-level npm `author` name is used as a fallback. |
| `debug` | `boolean` | ❌ | When `true`, starts Node.js with `--inspect` for debugger attachment. Default: `false`. |
| `debugPort` | `integer` | ❌ | Inspector port when `debug` is `true`. If not specified, auto-assigned starting at 9229. |
| `main` | `string` | ❌ | Optional override of the top-level `main` field (for packages where the CmdPal entry point differs from the npm main). |

### Validation Rules

A `package.json` is recognized as a CmdPal extension if:
1. It contains a `cmdpal` object (even if empty: `"cmdpal": {}`)
2. `name` is present and non-empty
### Icon resolution

The `cmdpal.icon` value is interpreted as follows:

- A **glyph** (for example, `"\uE943"`) or an **absolute URI** (for example, an
  `https://` or `ms-appx://` value) is used exactly as written.
- A **relative file path** (for example, `"icon.png"` or `"assets/icon.png"`) is
  resolved against the extension's own installed directory, which is the folder that
  contains its `package.json`.

A resolved relative icon must stay **inside** the package directory. The path is
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

The `JsonRpcExtensionService` watches this directory with a `FileSystemWatcher`:
- **New directory with valid `package.json`** (for example, a sideloaded extension copied in) is loaded automatically
- **Directory removed** unloads the extension and terminates its Node.js process
- **`*.js` file changed** within an extension triggers hot-reload (500ms debounce)

This means for sideloaded development:
- Installing an extension = copying a fully prepared directory into `JSExtensions/`
- Uninstalling = deleting the directory
- Updating = replacing files (hot-reload handles `*.js` changes)

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

With a junction link, changes to your source files are reflected immediately (after build). The `*.js` file watcher triggers hot-reload automatically.

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
it by version. Until the SDK is published, a production extension must **bundle** the
SDK into its own output rather than declaring it as an installed dependency.

Two supported approaches:

1. **Bundle the SDK into `dist/`** (recommended). Use a bundler (for example,
   `esbuild` or `rollup`) so the SDK is inlined into the files you ship under
   `dist/`. The published package then has no runtime dependency on
   `@microsoft/cmdpal-sdk`, and `npm install <your-package>` needs no access to this
   repository.
2. **Vendor a packed SDK tarball.** Run `npm pack` in `ts-sdk`, commit the resulting
   `microsoft-cmdpal-sdk-<version>.tgz` alongside your extension, and reference it as
   `"@microsoft/cmdpal-sdk": "file:./microsoft-cmdpal-sdk-<version>.tgz"`. This
   installs on a machine that does not have the PowerToys repository.

Do not ship a production package whose only path to the SDK is
`"file:../../ts-sdk"`: that relative path exists only inside this repository, so the
package cannot install anywhere else. That form is for **local development only** (see
[Development Setup](#development-setup)).

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
    "prepublishOnly": "npm run build"
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
`@microsoft/cmdpal-sdk`. If you vendor a packed tarball instead of bundling, move the
tarball reference into `dependencies` and include the `.tgz` in `files`.

### Validating a clean install

To confirm your package installs without the PowerToys repository present, pack it
and install it into a throwaway directory:

```powershell
npm pack                     # produces publisher-cmdpal-my-extension-1.0.0.tgz
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

### Installation Flow

When a user clicks "Install" for a JavaScript extension in the gallery, the installer prepares the extension out of sight of the watcher and only reveals it once it is verified and complete:

1. The `version` and `integrity` fields are validated, and any `registry` is checked against the HTTPS allowlist. A source that omits `version` or `integrity` is rejected before anything is downloaded.
2. `npm install <package>@<version>` runs in a staging directory (a fresh GUID-named folder) that lives **outside** the watched `JSExtensions/` root, on the same volume so the later move is atomic.
3. The resolved package's integrity is compared against the `integrity` value from the feed. A mismatch aborts the install.
4. The installer resolves the exact requested package under the staged `node_modules`, parses its `package.json`, and confirms the manifest identity (package name) and `version` match what the feed approved. An ambiguous layout (more than one valid CmdPal package) is rejected.
5. The staged package's dependencies are assembled into a self-contained discovery layout.
6. The finished directory is promoted into `JSExtensions\<id>` with a single atomic `Directory.Move`, so the directory only ever appears complete.
7. The host is asked to refresh and **awaits provider registration** (`OnProviderAdded`) before the install is reported as successful. The staging directory is cleaned up regardless of outcome.

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
