# DevDocs Plugin
The DevDocs plugin searches programming documentation hosted on [DevDocs](https://devdocs.io/) and opens the selected entry in the default browser.

### Query format
The plugin is not global and is activated with the `;;` action keyword:

```
;; <documentation> <search term>
```

 - `<documentation>` selects the documentation set, either by name (`python`), by slug (`python~3.11`) or by one of the aliases listed below.
 - `<search term>` is optional. When omitted, the first 50 entries of the documentation set are returned.
 - When no version is given, the newest release is used. Versions are compared with `System.Version`, so `3.11` correctly ranks above `3.9`.

### [`DevDocsConfig`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/DevDocsConfig.cs)
 - Holds the two DevDocs endpoints: `DevDocsApiUrl` for the list of documentation sets and `DocumentsBaseUrl` for the per-set entry index.
 - Holds `AliasMap`, a case-insensitive map of shorthands to slugs (`js` to `javascript`, `py` to `python`, `pg` to `postgresql`, ...).

### [`DevDocsService`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/DevDocsService.cs)
 - Fetches and caches the documentation data. Both caches are in-memory only and are rebuilt on the next start.
 - `LoadLanguagesAsync` reads the list of documentation sets from `https://devdocs.io/docs.json`.
 - `GetDocPathCached` reads the entry index of a single set from `https://documents.devdocs.io/<slug>/index.json` and caches it per slug. These indexes are large, so the first query for a set is noticeably slower than the following ones.
 - Filtering uses `StringMatcher.FuzzyMatch`, the same scorer as the rest of PT Run, and returns at most 50 entries ordered by score.

### [`Main`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/Main.cs)
 - `Init` starts loading the documentation set list on a background thread so that the first query does not block the UI thread.
 - Until that list is available, the query returns a single placeholder result.
 - Network failures are swallowed and surface as an empty result list.

### Models
[`Language`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/Models/Language.cs) and [`Links`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/Models/Links.cs) map one documentation set from `docs.json`. [`DocIndex`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/Models/DocIndex.cs) and [`Entry`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.DevDocs/Models/Entry.cs) map the per-set entry index.
