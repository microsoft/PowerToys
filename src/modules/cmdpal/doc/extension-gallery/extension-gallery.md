# Command Palette Extension Gallery

This document describes the JSON feed format consumed by the Command Palette extension gallery.

The live gallery content and extension submission workflow are maintained in the external repository:

- `https://github.com/microsoft/CmdPal-Extensions`

The current gallery implementation lives in:

- `Microsoft.CmdPal.Common/ExtensionGallery/Services/ExtensionGalleryService.cs`
- `Microsoft.CmdPal.Common/ExtensionGallery/Models/`

The local sample gallery content in this repo lives in:

- `doc/extension-gallery/extensionGallery/extensions.json`
- `doc/extension-gallery/extensionGallery/extensions/<extension-id>/manifest.json`

## Feed layout

The local sample feed is stored alongside these docs:

```text
doc/extension-gallery/
  extension-gallery.md
  gallery-index.schema.json
  gallery-manifest.schema.json
  extensionGallery/
    extensions.json
    schema.json
    Validate-GalleryJson.ps1
    extensions/
      sample-extension/
        manifest.json
        icon.png
        README.md
```

At runtime, the gallery service:

1. Loads the configured feed endpoint, typically `extensions.json`
2. Parses the wrapped `extensions` payload when full extension data is inline
3. Normalizes icon URIs for local file feeds
4. Caches the resulting entries for offline fallback

## `extensions.json`

The preferred feed format is a wrapped compound document with inline extension entries.

Example:

```json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/main/.github/schemas/gallery.schema.json",
  "version": "1.0",
  "generatedAt": "2026-04-01T13:40:15Z",
  "extensionCount": 1,
  "extensions": [
    {
      "id": "sample-extension",
      "title": "Sample Extension",
      "description": "A sample extension demonstrating the gallery feed format.",
      "author": {
        "name": "Microsoft",
        "url": "https://github.com/microsoft"
      },
      "homepage": "https://github.com/microsoft/CmdPal-Extensions",
      "tags": [
        "sample",
        "reference",
        "template"
      ],
      "installSources": [
        {
          "type": "url",
          "uri": "https://github.com/microsoft/CmdPal-Extensions/releases/latest"
        }
      ],
      "iconUrl": "extensions/sample-extension/icon.png"
    }
  ]
}
```

### Compound feed notes

- The production feed typically uses absolute HTTP `iconUrl` values.
- The checked-in local sample feed can use relative `iconUrl` values.
- `ExtensionGalleryService` resolves local relative icon paths against the feed location.

## `manifest.json`

Each local sample extension folder can still contain a `manifest.json` file with source metadata for that entry.
Those files are useful for authoring and validation, even though the runtime now prefers the wrapped `extensions.json` feed.

Example:

```json
{
  "$schema": "../../schema.json",
  "id": "sample-extension",
  "title": "Sample Extension",
  "description": "A sample extension demonstrating the gallery manifest format.",
  "author": {
    "name": "Microsoft",
    "url": "https://github.com/microsoft"
  },
  "homepage": "https://github.com/microsoft/CmdPal-Extensions",
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
      "uri": "https://github.com/microsoft/CmdPal-Extensions/releases/latest"
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

## Icons and companion files

- icon filenames are resolved relative to the extension folder
- readme filenames are stored as metadata relative to the extension folder

## Validation

This folder contains JSON schema files for authoring and validation:

- `gallery-index.schema.json`
- `gallery-manifest.schema.json`

The local sample feed under `doc/extension-gallery/extensionGallery/` also includes:

- `extensionGallery/schema.json`
- `extensionGallery/Validate-GalleryJson.ps1`

If you are editing the sample gallery feed in this repo, keeping the manifest `$schema` field pointed at `../../schema.json` gives editor IntelliSense and validation without changing the existing sample layout.

## Practical guidance

- Prefer the wrapped `extensions.json` format
- Keep `id` values stable once published
- Include a `winget` source for extensions that can be installed through App Installer
- Add `packageFamilyName` when you know the packaged extension identity
- Use tags sparingly and consistently so search remains useful
