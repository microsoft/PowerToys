# Command Palette Extension Gallery

This document describes how Command Palette (CmdPal) discovers extensions for
the in-app **Extension gallery** page.

## At a glance

- The gallery loads a single JSON feed called `extensions.json` from a remote
  HTTPS URL, parses it, and renders the entries.
- The default feed lives in the external repo
  **`microsoft/CmdPal-Extensions`** at
  `https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/refs/heads/main/extensions.json`.
- Feed content + icon images are cached on disk so the page works offline and
  survives short network hiccups.
- There is no WinGet discovery, no per-extension `manifest.json` fetch, and no
  other network call for rendering the list.

## Implementation pointers

| Concern | File |
| --- | --- |
| Fetching, parsing, caching, pruning | `Microsoft.CmdPal.Common/ExtensionGallery/Services/ExtensionGalleryService.cs` |
| Resolving which URL to fetch | `Microsoft.CmdPal.Common/ExtensionGallery/Services/GalleryFeedUrlProvider.cs` + `Microsoft.CmdPal.UI/Helpers/GalleryServiceRegistration.cs` |
| HTTP + on-disk cache | `Microsoft.CmdPal.Common/ExtensionGallery/Services/ExtensionGalleryHttpClient.cs` (wraps `Microsoft.CmdPal.Common/Services/HttpCaching/HttpCachingClient`) |
| Feed + entry models | `Microsoft.CmdPal.Common/ExtensionGallery/Models/` |

## Feed URL resolution

`ExtensionGalleryService.GetFeedUrl()` returns, in order:

1. The user-configured URL from CmdPal settings (`SettingsModel.GalleryFeedUrl`,
   exposed via the hidden `InternalPage` settings page). Any non-empty value
   wins. Mostly used for local testing against a custom feed.
2. Otherwise, the built-in default
   `https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/refs/heads/main/extensions.json`.

Local `file://` URIs are allowed too — `FetchFeedDocumentAsync` reads the file
directly and bypasses the HTTP cache.

## Feed format

The feed is a single wrapped JSON document with inline entries:

```json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/CmdPal-Extensions/main/.github/schemas/gallery.schema.json",
  "extensions": [
    {
      "id": "sample-extension",
      "title": "Sample Extension",
      "description": "A sample extension demonstrating the gallery feed format.",
      "author": { "name": "Microsoft", "url": "https://github.com/microsoft" },
      "homepage": "https://github.com/microsoft/CmdPal-Extensions",
      "iconUrl": "https://.../icon.png",
      "screenshotUrls": ["https://.../screenshot-1.png"],
      "tags": ["sample"],
      "installSources": [
        { "type": "winget",  "id":  "Contoso.SampleExtension" },
        { "type": "msstore", "id":  "9P…" },
        { "type": "url",     "uri": "https://github.com/contoso/sample/releases/latest" }
      ],
      "detection": { "packageFamilyName": "Contoso.SampleExtension_1234567890abc" }
    }
  ]
}
```

Only the `extensions` array is read at runtime. The authoritative JSON
schema for an entry lives in the upstream feed repo
([`microsoft/CmdPal-Extensions`](https://github.com/microsoft/CmdPal-Extensions));
don't duplicate it here — it drifts.

### Required + optional entry fields

| Field | Required | Notes |
| --- | --- | --- |
| `id` | yes | Lowercase stable identifier; entries with empty id are dropped. |
| `title` | yes | Display name. |
| `description` | yes | Shown in list and detail views. |
| `author.name` | yes | `author.url` optional. |
| `installSources` | yes | At least one entry; see [Install sources](#install-sources). |
| `homepage`, `iconUrl`, `screenshotUrls`, `tags`, `detection.packageFamilyName` | no | All optional. |

Relative `iconUrl` / `screenshotUrls` are resolved against the feed URL's
directory (useful only for local / `file://` feeds during development).

## Install sources

Each entry's `installSources` is consumed by
`ExtensionGalleryItemViewModel` to decide which install affordances to show.

| `type` | Required field | Behaviour |
| --- | --- | --- |
| `winget` | `id` | Enables the "Install via WinGet" button (uses the shared WinGet service), and joins in-flight install progress + installed/update status. |
| `msstore` | `id` | Opens `ms-windows-store://pdp/?ProductId={id}`. |
| `url` | `uri` | Shown as a "GitHub" or "Website" link depending on host. |

An entry can declare any combination. Sources the runtime does not recognise
are surfaced as an "unknown source" indicator.

## Fetching and caching

`ExtensionGalleryService` uses `ExtensionGalleryHttpClient`, which wraps
`HttpCachingClient` over a file-system cache. Both the feed JSON and any
cacheable icon URLs are cached.

| Setting | Value | Defined in |
| --- | --- | --- |
| Cache root | `{AppCache}\GalleryCache\` | `ExtensionGalleryHttpClient.CacheDirectoryName` |
| Feed TTL | 4 hours | `ExtensionGalleryHttpClient.DefaultTimeToLive` |
| Icon TTL | 24 hours | `ExtensionGalleryService.IconCacheTtl` |
| HTTP timeout | 30 s | `ExtensionGalleryHttpClient` |
| `User-Agent` | `PowerToys-CmdPal/1.0` | `ExtensionGalleryHttpClient` |

`{AppCache}` resolves to `ApplicationData.Current.LocalCacheFolder` when
CmdPal runs packaged, and to
`%LOCALAPPDATA%\Microsoft\PowerToys\Microsoft.CmdPal\Cache\` when unpackaged
(see `ApplicationInfoService.DetermineCacheDirectory`).

### Fetch flow

`GetExtensionsAsync` (normal load) and `RefreshAsync` (user-initiated
refresh, `forceRefresh: true`) both go through `FetchWrappedFeedAsync`:

1. Resolve the feed URL (see above).
2. If the URL is local, read it from disk. Otherwise, hand it to
   `HttpCachingClient.GetResourceAsync` which:
   - Serves a fresh cached copy if one exists and TTL has not elapsed.
   - Otherwise issues a conditional GET (ETag / `If-None-Match`). On `304
     Not Modified` it refreshes the cache metadata and returns the cached
     body.
   - On network failure it returns the last-known cached body with
     `UsedFallbackCache = true`, so the UI can show a "stale data" banner.
3. Parse the JSON with the source-generated `GallerySerializationContext`
   (strongly-typed `GalleryRemoteIndex` — no reflection, AOT-friendly).
4. Drop entries with missing `id`, normalize relative `iconUrl` and
   `screenshotUrls`, and resolve remote icon URIs through the same HTTP
   cache so the UI binds to local `file://` URIs.
5. On a successful forced refresh, `PruneCachedResources` deletes cache
   entries that are no longer referenced by the current feed (old feed URL
   and icon URLs that dropped out of the feed).

### Fetch result flags

`GetExtensionsAsync` returns a `GalleryFetchResult` that the view model uses
for UI hints:

| Flag | Meaning |
| --- | --- |
| `FromCache` | The feed came from cache without hitting the network (TTL still valid). |
| `UsedFallbackCache` | A network request was attempted and failed, and the cached copy was served as fallback. The UI shows a stale-data info bar. |
| `RateLimited` | The origin returned `429 Too Many Requests` and no fallback was available. The UI shows a rate-limit error. |

## Authoring

- Entries for the production gallery are added to the feed repo
  `microsoft/CmdPal-Extensions`.
- For editor validation of an entry, reference the schema published in the
  upstream repo via the entry's `$schema` field.
- Keep `id` stable once an extension is published — users may have it
  installed and the gallery keys install status by id.
- Prefer providing a `winget` source when the extension ships through App
  Installer; the gallery uses it both for status ("Installed" / "Update
  available") and for the in-app install button.
- `detection.packageFamilyName` lets the gallery recognise an
  already-installed packaged extension before WinGet metadata resolves.

