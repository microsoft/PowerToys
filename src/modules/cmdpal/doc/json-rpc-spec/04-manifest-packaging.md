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
| `icon` | `string` | ❌ | Icon glyph character (e.g., `"\uE943"`) or relative path to an icon file (PNG recommended). |
| `publisher` | `string` | ❌ | Author or publisher name. When omitted, the top-level npm `author` name is used as a fallback. |
| `debug` | `boolean` | ❌ | When `true`, starts Node.js with `--inspect` for debugger attachment. Default: `false`. |
| `debugPort` | `integer` | ❌ | Inspector port when `debug` is `true`. If not specified, auto-assigned starting at 9229. |
| `main` | `string` | ❌ | Optional override of the top-level `main` field (for packages where the CmdPal entry point differs from the npm main). |

### Validation Rules

A `package.json` is recognized as a CmdPal extension if:
1. It contains a `cmdpal` object (even if empty: `"cmdpal": {}`)
2. `name` is present and non-empty
3. Either `cmdpal.main` or top-level `main` resolves to an existing built file. For TypeScript extensions, run `tsc` before discovery so `dist/index.js` exists.

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
- **New directory with valid `package.json`** → extension is loaded automatically
- **Directory removed** → extension is unloaded, Node.js process is terminated
- **`*.js` file changed** within an extension → hot-reload (500ms debounce)

This means:
- Installing an extension = copying its directory to `JSExtensions/`
- Uninstalling = deleting the directory
- Updating = replacing files (hot-reload handles `*.js` changes)

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

### npm Package Structure

Extensions are distributed as standard npm packages. The recommended `package.json`:

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
    "build": "tsc",
    "prepublishOnly": "npm run build"
  },
  "files": [
    "dist/",
    "icon.png"
  ],
  "dependencies": {
    "@microsoft/cmdpal-sdk": "file:../../ts-sdk"
  },
  "devDependencies": {
    "typescript": "^5.8.0"
  },
  "keywords": ["cmdpal", "powertoys", "command-palette"],
  "engines": {
    "node": ">=22.0.0"
  }
}
```

The `@microsoft/cmdpal-sdk` package is not yet published to a public npm
registry, so extensions reference it through a relative `file:` dependency that
points at the SDK inside this repository (adjust the path to match where your
extension lives). Once the SDK is published, replace that reference with a
semantic version range such as `^0.1.0`.

### Naming Convention

Recommended npm package naming: `@publisher/cmdpal-<name>` or `cmdpal-<name>`.

The `cmdpal-` prefix helps with discoverability and could be used for future npm-based discovery.

---

## Extension Gallery Integration


### Gallery Manifest Entry

The existing CmdPal extension gallery pulls from a manifest that lists available extensions. For JavaScript extensions, the manifest entry includes npm package details:

```json
{
  "name": "my-extension",
  "displayName": "My Extension",
  "description": "Does amazing things",
  "publisher": "your-name",
  "version": "1.0.0",
  "icon": "https://example.com/icon.png",
  "type": "jsonrpc",
  "npm": {
    "package": "@publisher/cmdpal-my-extension",
    "registry": "https://registry.npmjs.org"
  }
}
```

The `type: "jsonrpc"` field distinguishes JavaScript extensions from COM-based extensions.

### Installation Flow

When a user clicks "Install" for a JavaScript extension in the gallery:

1. CmdPal creates `JSExtensions\<name>` and runs `npm install <pkg>` with that directory as the working directory.
2. npm initially places the package under `JSExtensions\<name>\node_modules\<pkg>`.
3. After npm exits successfully, `NpmCommandRunner` calls `JsExtensionPackageLayout.Materialize(targetDirectory)`.
4. `Materialize` finds the installed package whose `package.json` contains a valid `cmdpal` section and hoists that package to `JSExtensions\<name>`.
5. The discoverable root then contains `package.json`, `dist\`, and `node_modules\`.
6. `FileSystemWatcher` detects the prepared directory, the manifest is parsed, and the extension loads automatically.
7. The extension appears in the main CmdPal list

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
