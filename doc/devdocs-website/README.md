# Dev Docs Website

This folder hosts the [docmd](https://docmd.io/) project that turns the PowerToys developer
documentation in [`doc/devdocs`](../devdocs) into a static website.

## Generated site

The `site/` folder is the docmd build output. It is **not committed** to the repository &mdash; it is
git-ignored and rebuilt on demand. You only need it locally when previewing your changes (see below).

Publishing is handled by the
[Publish Dev Docs Website](../../.github/workflows/regenerate-devdocs-website.yml) GitHub Action, which
runs whenever files under `doc/devdocs` (or this folder) change on the `main` branch &mdash; it can also
be triggered manually from the **Actions** tab. The action builds the site and deploys it straight to
GitHub Pages as an artifact, so nothing is written back to the repository.

> [!NOTE]
> The action requires GitHub Pages to be enabled with **Source: GitHub Actions** under the repository
> **Settings → Pages**.

## Editing the docs

To change the documentation, edit the Markdown files under [`doc/devdocs`](../devdocs). The remaining
files in this folder are maintained by hand and are safe to edit:

- `docmd.config.json` &mdash; docmd configuration (title, source, output, and so on)
- `package.json` &mdash; pins the docmd version used to build the site

## Building locally

Requires [Node.js](https://nodejs.org/).

```powershell
npm install      # install dependencies (first time only)
npm run dev      # start a local preview server at http://localhost:3000
npm run build    # generate the static site into ./site
```
