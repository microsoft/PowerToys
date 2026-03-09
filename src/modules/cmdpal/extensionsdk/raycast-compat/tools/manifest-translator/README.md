# Raycast → CmdPal Manifest Translator

Converts a Raycast extension `package.json` into a CmdPal `cmdpal.json` manifest that the JavaScript Extension Service can discover and load.

## Quick Start

```bash
# Install dependencies & build
npm install
npm run build

# Translate a Raycast extension manifest
node dist/translate-manifest.js <path-to-raycast-package.json>

# Specify custom output path
node dist/translate-manifest.js ./package.json --output ./dist/cmdpal.json
```

## What It Does

| Raycast field | CmdPal field | Notes |
|---|---|---|
| `name` | `name` | Prefixed with `raycast-` to avoid collisions |
| `title` | `displayName` | |
| `description` | `description` | |
| `author` / `owner` | `publisher` | |
| `icon` | `icon` | Mapped to `assets/<filename>` if bare filename |
| `version` | `version` | Defaults to `1.0.0` if absent |
| `commands[0].name` | _(used for entry point)_ | `main` is set to `dist/index.js` |
| `commands[]` | `capabilities` | `"commands"` always included; `"listPages"` added for `mode: "view"` |

### Platform Check (Critical)

Extensions where `platforms` does **not** include `"Windows"` are **rejected**.
If the `platforms` field is missing entirely, Raycast defaults to `["macOS"]` — which means rejection.

### Output Files

The translator produces two files:

1. **`cmdpal.json`** — The CmdPal manifest (consumed by `JavaScriptExtensionService`)
2. **`raycast-compat.json`** — Companion metadata preserving Raycast-specific fields (commands, preferences, platforms) for the runtime compatibility layer

## Samples

Test files in `samples/`:

| File | Expected Result |
|---|---|
| `raycast-package.json` | ✓ Translates successfully (has `"Windows"` in platforms) |
| `raycast-macos-only.json` | ✗ Rejected (no platforms field → defaults to macOS) |
| `raycast-no-platforms.json` | ✗ Rejected (platforms is `["macOS"]` only) |

## Development

```bash
npm install
npm run build    # Compile TypeScript → dist/
npm run translate -- ./samples/raycast-package.json
```
