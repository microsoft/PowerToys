---
description: 'Monaco editor bundle built from the pinned monaco-editor npm package. Read before upgrading Monaco or changing its languages, editor features, or build.'
applyTo: 'src/Monaco/**, src/Monaco.props'
---

# Monaco Editor Bundle

PowerToys builds Monaco from the `monaco-editor` npm package pinned in `src/Monaco/package.json`. `src/Monaco/build.mjs` bundles it with esbuild into `src/Monaco/dist`, and `src/Monaco.props` runs it during the normal build. The full guide is `doc/devdocs/common/monaco-editor.md`.

## Rules

- Don't check in Monaco source or edit files under `node_modules` or `dist`. Put PowerToys changes in `src/Monaco/src`, and import only public `monaco-editor` entry points.
- Keep `src/Monaco/src/features.js` an allow-list. Don't import the root `monaco-editor` entry or the `languages/features/*` language services; both pull in every editor feature.
- Register languages only in `src/Monaco/src/languages.js`, then run `npm run gen:languages`.
- Pin exact versions and commit `package.json` and `package-lock.json` together.

## Upgrade Monaco

Follow the update process in `doc/devdocs/common/monaco-editor.md` step by step:

1. In `src/Monaco`, run `npm install monaco-editor@<version> --save-exact`.
2. Point lockfile `resolved` URLs at `https://registry.npmjs.org/` and replace `sha1-` integrity values with `sha512-`.
3. Run `npm run gen:languages` and review the `monaco_languages.json` diff.
4. Run `npm run build`. It fails with a pointer to the guide when the lockfile, `NOTICE.md`, the temporary DOMPurify pin, or the installer file list needs an update. Fix the cause; don't weaken the check.
5. Build PowerToys and check the Preview Pane, Peek, and Registry Preview.

## Validation

- `npm run build` in `src/Monaco` exits 0.
- `npm run check:languages` in `src/Monaco` exits 0.
