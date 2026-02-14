---
name: winmd-api-search
description: 'Find and explore Windows desktop APIs. Use when building features that need platform capabilities — camera, file access, notifications, UI controls, AI/ML, sensors, networking, etc. Discovers the right API for a task and retrieves full type details (methods, properties, events, enumeration values).'
---

# WinMD API Search

This skill helps you find the right Windows API for any capability and get its full details. It searches a local cache of all WinMD metadata from:

- **Windows Platform SDK** — all `Windows.*` WinRT APIs (always available, no restore needed)
- **WinAppSDK / WinUI** — bundled as a baseline in the cache generator (always available, no restore needed)
- **NuGet packages** — any additional packages in restored projects that contain `.winmd` files
- **Project-output WinMD** — class libraries (C++/WinRT, C#) that produce `.winmd` as build output

Even on a fresh clone with no restore or build, you still get full Platform SDK + WinAppSDK coverage.

## When to Use

- User wants to build a feature and you need to find which API provides that capability
- User asks "how do I do X?" where X involves a platform feature (camera, files, notifications, sensors, AI, etc.)
- You need the exact methods, properties, events, or enumeration values of a type before writing code
- You're unsure which control, class, or interface to use for a UI or system task

## How to Use

Pick the path that matches the situation:

---

### Discover — "I don't know which API to use"

The user describes a capability in their own words. You need to find the right API.

**1. Translate user language → search keywords**

Map the user's daily language to programming terms. Try multiple variations:

| User says | Search keywords to try (in order) |
|-----------|-----------------------------------|
| "take a picture" | `camera`, `capture`, `photo`, `MediaCapture` |
| "load from disk" | `file open`, `picker`, `FileOpen`, `StorageFile` |
| "describe what's in it" | `image description`, `Vision`, `Recognition` |
| "show a popup" | `dialog`, `flyout`, `popup`, `ContentDialog` |
| "drag and drop" | `drag`, `drop`, `DragDrop` |
| "save settings" | `settings`, `ApplicationData`, `LocalSettings` |

Start with simple everyday words. If results are weak or irrelevant, try the more technical variation.

**2. Run searches**

```powershell
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action search -Query "<keyword>"
```

This returns ranked namespaces with top matching types and the **JSON file path**.

**3. Read the JSON to choose the right API**

Use `read_file` on the file path(s) from the top results. The JSON has all types in that namespace — full members, signatures, parameters, return types, enumeration values.

Read and decide which types and members fit the user's requirement.

**4. Use the API knowledge to answer or write code**

---

### Lookup — "I know the API, show me the details"

You already know (or suspect) the type or namespace name. Go direct:

```powershell
# Get all members of a known type
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action members -TypeName "Microsoft.UI.Xaml.Controls.NavigationView"

# Get enum values
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action enums -TypeName "Microsoft.UI.Xaml.Visibility"

# List all types in a namespace
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action types -Namespace "Microsoft.UI.Xaml.Controls"

# Browse namespaces
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action namespaces -Filter "Microsoft.UI"
```

If you need full detail beyond what `-Action members` shows, use `-Action search` to get the JSON file path, then `read_file` the JSON directly.

---

### Other Commands

```powershell
# List cached projects
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action projects

# Show stats
.\.github\skills\winmd-api-search\scripts\Invoke-WinMdQuery.ps1 -Action stats
```

> If only one project is cached, `-Project` is auto-selected.
> If multiple projects exist, add `-Project <name>`.

## Search Scoring

The search ranks type names against your query:

| Score | Match type | Example |
|-------|-----------|---------|
| 100 | Exact name | `Button` → `Button` |
| 80 | Starts with | `Navigation` → `NavigationView` |
| 60 | Contains | `Dialog` → `ContentDialog` |
| 50 | PascalCase initials | `ASB` → `AutoSuggestBox` |
| 40 | Multi-keyword AND | `navigation item` → `NavigationViewItem` |
| 20 | Fuzzy character match | `NavVw` → `NavigationView` |

Results are grouped by namespace. Higher-scored namespaces appear first.

## Cache Setup

If the cache doesn't exist, generate it:

```powershell
# Single project
.\.\.github\skills\winmd-api-search\scripts\Update-WinMdCache.ps1 -ProjectDir <project-folder>

# All projects in the repo
.\.\.github\skills\winmd-api-search\scripts\Update-WinMdCache.ps1
```

Requires:
- **.NET SDK 8.0 or later** — auto-detects the highest installed SDK (>= 8). Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download) if not available.

No project restore or build is needed for baseline coverage (Platform SDK + WinAppSDK). For additional NuGet packages, the project needs `dotnet restore` (which generates `project.assets.json`) or a `packages.config` file.

Cache is stored at `Generated Files\winmd-cache\`, deduplicated per-package+version.

### What gets indexed

| Source | When available |
|--------|----------------|
| Windows Platform SDK | Always (reads from local SDK install) |
| WinAppSDK (latest) | Always (bundled as baseline in cache generator) |
| Project NuGet packages | After `dotnet restore` or with `packages.config` |
| Project-output `.winmd` | After project build (class libraries that produce WinMD) |

> **Note:** This cache directory should be in `.gitignore` — it's generated, not source.

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "Cache not found" | Run `Update-WinMdCache.ps1` |
| "Multiple projects cached" | Add `-Project <name>` |
| "Namespace not found" | Use `-Action namespaces` to list available ones |
| "Type not found" | Use fully qualified name (e.g., `Microsoft.UI.Xaml.Controls.Button`) |
| Stale after NuGet update | Re-run `Update-WinMdCache.ps1` |
| Cache in git history | Add `Generated Files/` to `.gitignore` |
