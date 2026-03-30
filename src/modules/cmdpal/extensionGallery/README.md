# Command Palette Extension Gallery Feed

For the fuller authoring guide and the doc-local JSON schemas, see:

- `../doc/extension-gallery/extension-gallery.md`
- `../doc/extension-gallery/gallery-index.schema.json`
- `../doc/extension-gallery/gallery-manifest.schema.json`

## Manifest localization

- `manifest.json` is the default manifest (typically English).
- Localized manifests are full document copies named `manifest.<locale>.json`, for example:
  - `manifest.cs-cz.json`
  - `manifest.fr-fr.json`
- The gallery loads `manifest.<CurrentUICulture>.json` first (with parent culture fallback), then falls back to `manifest.json`.

## Index format

- `index.json` supports both:
  - legacy string ids: `["id-a", "id-b"]`
  - entry objects with optional tags:
    - `{ "id": "id-a", "tags": ["search", "utility"] }`

## Notes

- `schema.json` defines the manifest schema.
- `tags` are optional and are used for discoverability in the gallery UI.
