// docmd plugin: github-source-links
//
// The dev docs link to source files with repo-root-relative paths such as
// "/src/modules/.../Foo.cpp". VS Code resolves those against the workspace root,
// so they stay clickable while editing locally. On the published static site,
// however, a "/src/..." link resolves against the site origin and 404s.
//
// This plugin rewrites those links to absolute GitHub blob URLs so they work on
// the published site, while the Markdown sources stay untouched (keeping local
// VS Code navigation intact).
//
// It hooks markdownSetup at the Markdown token level, so it only rewrites links
// written in the docs' content. docmd's own generated links (sidebar, breadcrumbs,
// canonical tags) are never seen here, which matters because an internal doc route
// like "/tools/build-tools" is otherwise indistinguishable from a repo path like
// "/tools/BugReportTool" once rendered to HTML.
//
// docmd appends a trailing slash to the rewritten links (".../Foo.cpp/"); GitHub
// resolves that to the file anyway, so it is left as-is for simplicity.

const REPO_BLOB_BASE = 'https://github.com/microsoft/PowerToys/blob/main';

export default {
  plugin: {
    name: 'github-source-links',
    version: '1.0.0',
    capabilities: ['markdown'],
  },

  markdownSetup(md) {
    const defaultRender =
      md.renderer.rules.link_open ||
      ((tokens, idx, options, env, self) => self.renderToken(tokens, idx, options));

    md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
      const token = tokens[idx];
      const hrefIndex = token.attrIndex('href');

      if (hrefIndex >= 0) {
        const href = token.attrs[hrefIndex][1];

        // Only repo-root-relative links ("/src/..."). Leave protocol-relative
        // ("//host"), absolute ("https://..."), relative and anchor links alone.
        if (href.length > 1 && href[0] === '/' && href[1] !== '/') {
          token.attrs[hrefIndex][1] = REPO_BLOB_BASE + href;
        }
      }

      return defaultRender(tokens, idx, options, env, self);
    };
  },
};
