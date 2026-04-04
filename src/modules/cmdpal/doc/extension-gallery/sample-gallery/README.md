# Command Palette Extension Gallery Feed

The live extension gallery and submission workflow are maintained in:

- `https://github.com/microsoft/CmdPal-Extensions`

This folder is only a local sample feed used for docs, development, and tests in the PowerToys repo.

For the fuller authoring guide and the doc-local JSON schemas, see:

- `../extension-gallery.md`
- `../gallery-index.schema.json`
- `../gallery-manifest.schema.json`

## Local Compound Feed

- `extensions.json` mirrors the wrapped feed consumed by the app.
- Local sample entries can keep `iconUrl` relative, for example `extensions/sample-extension/icon.png`.
- `ExtensionGalleryService` resolves those relative icon paths against the local feed location, so checked-in sample data does not need machine-specific absolute `file:` URIs.

## Local Authoring Data

- `schema.json` defines the manifest schema.
- `extensions/<id>/manifest.json` files remain useful as local source data when editing sample entries.
- `tags` are optional and are used for discoverability in the gallery UI.
