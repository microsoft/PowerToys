# Command Palette Extension Gallery

This document describes the JSON feed format consumed by the Command Palette extension gallery.

The current gallery implementation lives in:

- `Microsoft.CmdPal.Common/ExtensionGallery/Services/ExtensionGalleryService.cs`
- `Microsoft.CmdPal.Common/ExtensionGallery/Models/`

The sample gallery content in this repo lives in:

- `extensionGallery/index.json`
- `extensionGallery/extensions/<extension-id>/manifest.json`

## Feed layout

The gallery feed is a folder with one index file and one subfolder per extension:

```text
extensionGallery/
  index.json
  extensions/
    sample-extension/
      manifest.json
      manifest.cs-cz.json
      icon.png
      README.md
```

At runtime, the gallery service:

1. Loads `index.json`
2. Resolves each extension folder from the index entry `id`
3. Loads `extensions/<id>/manifest.json`
4. Tries a localized manifest such as `manifest.cs-cz.json` before falling back to `manifest.json`
5. Merges and caches the resulting entries for offline fallback

## `index.json`

Two index formats are supported:

### Preferred format

Use an array of objects. This is the format to use for new gallery feeds.

```json
[
  {
    "id": "sample-extension",
    "tags": ["sample", "reference", "template"]
  },
  {
    "id": "github-extension",
    "tags": ["github", "repositories", "developer tools"]
  }
]
```

### Legacy format

The gallery still accepts a plain string array for back-compat:

```json
[
  "sample-extension",
  "github-extension"
]
```

### Index rules

- `id` must match the extension folder name under `extensions/`
- `tags` are optional
- tags from the index and manifest are merged and de-duplicated case-insensitively
- blank ids and duplicate ids are ignored by the loader

## `manifest.json`

Each extension folder contains a `manifest.json` file with the extension metadata.

Example:

```json
{
  "$schema": "../../schema.json",
  "id": "sample-extension",
  "title": "Sample Extension",
  "description": "A sample extension demonstrating the gallery manifest format.",
  "author": {
    "name": "Microsoft",
    "url": "https://github.com/microsoft/PowerToys"
  },
  "homepage": "https://github.com/microsoft/PowerToys",
  "readme": "README.md",
  "icon": "icon.png",
  "tags": [
    "sample",
    "reference",
    "template"
  ],
  "installSources": [
    {
      "type": "url",
      "uri": "https://github.com/microsoft/PowerToys/releases"
    }
  ],
  "detection": {
    "packageFamilyName": "Contoso.SampleExtension_1234567890abc"
  }
}
```

### Manifest fields

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `id` | Yes | `string` | Lowercase identifier. Should match the folder name and the index entry id. |
| `title` | Yes | `string` | Display name shown in the gallery UI. |
| `description` | Yes | `string` | Short summary shown in the list and detail page. |
| `author` | Yes | `object` | Currently supports `name` and optional `url`. |
| `homepage` | No | `string` | Absolute URL for the extension home page or repo. |
| `readme` | No | `string` | Relative filename for extension documentation metadata. |
| `icon` | No | `string` | Relative filename for the icon asset. |
| `iconDark` | No | `string` | Optional dark-theme icon variant kept in the manifest model. |
| `installSources` | Yes | `array` | One or more install sources such as WinGet, Microsoft Store, or a URL. |
| `detection` | No | `object` | Optional install-state hints. Currently supports `packageFamilyName`. |
| `tags` | No | `array` | Optional discoverability tags. |

## Install sources

The gallery currently understands these source types:

| `type` | Required field | Used for |
| --- | --- | --- |
| `winget` | `id` | Install or update via the shared WinGet service. |
| `msstore` | `id` | Open the Microsoft Store product page. |
| `url` | `uri` | Open a website or repository link. |

Notes:

- `url` sources are surfaced as GitHub or Website links based on the URI host
- a manifest can contain more than one install source
- the WinGet id is also how the gallery joins shared install progress and package status updates

## Detection metadata

`detection.packageFamilyName` is optional metadata used by the gallery to recognize an already-installed extension package, even before WinGet metadata is queried.

Example:

```json
"detection": {
  "packageFamilyName": "Contoso.MyExtension_8wekyb3d8bbwe"
}
```

## Localization

The preferred localization model is a full localized manifest per culture:

- `manifest.json` is the default manifest, typically English
- `manifest.<locale>.json` is a full document copy for a specific locale
- the loader tries `CurrentUICulture`, then parent cultures, then `manifest.json`

Example:

- `manifest.json`
- `manifest.cs-cz.json`
- `manifest.fr-fr.json`

The loader also still accepts a legacy per-field localization object for `title`, `description`, and `readme`, for example:

```json
{
  "title": {
    "en": "Sample Extension",
    "cs-cz": "Ukazkove rozsireni"
  }
}
```

That legacy object form is supported for compatibility, but new feeds should prefer localized manifest files.

## Icons and companion files

- icon filenames are resolved relative to the extension folder
- readme filenames are stored as metadata relative to the extension folder
- keep filenames stable across localized manifests unless a locale truly needs a different asset

## Validation

This folder contains JSON schema files for authoring and validation:

- `gallery-index.schema.json`
- `gallery-manifest.schema.json`

The sample feed under `extensionGallery/` also includes:

- `extensionGallery/schema.json`
- `extensionGallery/Validate-GalleryJson.ps1`

If you are editing the sample gallery feed in this repo, keeping the manifest `$schema` field pointed at `../../schema.json` gives editor IntelliSense and validation without changing the existing sample layout.

## Practical guidance

- Prefer the object-based `index.json` format
- Keep `id` values stable once published
- Include a `winget` source for extensions that can be installed through App Installer
- Add `packageFamilyName` when you know the packaged extension identity
- Treat `manifest.<locale>.json` as a full copy of the base manifest, not a partial override
- Use tags sparingly and consistently so search remains useful
