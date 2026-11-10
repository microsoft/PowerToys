# WUP-to-CmdPal API reference

Read [Understanding WUP and the CmdPal bridge](wup-to-cmdpal-patterns.md) first if WUP is new to you.
Use this reference to check exact query fields, adapter behavior, and translation gaps.
It describes implementation details, not an introduction to the WUP model.

## 1. Purpose and scope

This guide describes how the bridge changes Windows UI Palettes (WUP) items into Command Palette (CmdPal) items.
WUP uses the `Windows.UI.Palettes` namespace.
The CmdPal extension API uses the `Microsoft.CommandPalette.Extensions` namespace.
Use this guide when you develop a WUP source or a CmdPal provider.
Each mapping describes the current code, not a future design.

The guide also describes the opposite mapping in the CmdPal toolkit.
This comparison shows which features survive a CmdPal-to-WUP-to-CmdPal round trip.
The two adapters do not make the two APIs equivalent.

Source baseline: 9 November 2026.

- Bridge source revision: `1af5295`.
- WUP contract: version 2.0 in the [local IDL][wup-idl].
- Writing reference: [ASD-STE100 Simplified Technical English, Issue 9][ste].

API names and project terms are technical names in this guide.
The guide uses short sentences, active statements, and one term for each concept.
It does not reproduce the STE standard or claim certified STE compliance.

### Terms

| Term | Meaning |
| --- | --- |
| Source | A WUP `IPaletteItemSourceExtension`. |
| Provider | A CmdPal `ICommandProvider`. |
| Bridge | The WUP-to-CmdPal extension in this repository. |
| Toolkit adapter | The CmdPal-to-WUP `PaletteExtension` in the CmdPal extension toolkit. |
| Item | A WUP `PaletteItemView`, unless the text specifies CmdPal. |
| Primary command | The CmdPal `ICommandItem.Command` action. |
| Context command | An additional action in CmdPal `MoreCommands`. |
| Entity | A WUP item used as a query argument, not as text. |
| Snapshot | The complete item array shown for one source at one time. |
| Gap | A feature that the current mapping does not preserve. |
| Round trip | CmdPal provider -> toolkit adapter -> WUP source -> bridge -> CmdPal. |

## 2. Source discovery and initial items

The bridge exposes one top-level CmdPal command: **Windows Palettes**.
Its command is a `DynamicListPage`, which implements `IDynamicListPage`.
WUP results appear inside this page, not on the CmdPal root page.

The [command provider][provider] opens `PaletteCatalog` with these source kind specifications:

```text
ZeroState
ZeroState.CommandPalette
QueryResult
QueryResult.CommandPalette
QueryResult.StartMenu
```

Catalog matching works in both directions.
A source with a bare role or a matching wildcard can participate.
The Start menu specification includes sources such as Windows Settings.

| WUP pattern | CmdPal mapping | Limit |
| --- | --- | --- |
| Eligible catalog definition | A private `WupSourcePage` inside the aggregate page | No separate top-level command for each source. |
| Empty search and `ZeroState.*` source | `source.GetItems()` -> CmdPal list items | No zero-state query object is passed. |
| Nonempty search and `QueryResult.*` source | `source.ExecuteQueryAsync()` -> CmdPal list items | Other source roles do not receive normal text searches. |
| `PaletteCatalog.Changed` | Reconcile sources by provider ID and refresh the current search | Retained pages keep their active extensions. Removed sources are released. |
| Missing or incompatible WUP API | Do not register the provider | The provider is absent from settings and command lists. |
| Other catalog failure | Log the failure and do not register the provider | Command Palette continues without this provider. |

**Gap:** The bridge does not use `ContainedKinds` or `AcceptedKindSpecs` to select sources for normal searches.
It does not use `PaletteCatalog.Parse` to resolve entities.
Sources with only assistant, web-search, or app-store roles need a matching catalog selection to participate.

See the [aggregate page][aggregate] and [source page][source-page].

The provider ID contains the package family name, application ID, and extension ID.
Each catalog refresh updates the definition, display name, and icon of a retained page.
The bridge retains its active extension even if the catalog supplies a new definition.
Future activation uses the latest definition after the extension unloads.

## 3. Query patterns

### 3.1 Text search

The [source page][source-page] makes this query for a normal search:

```csharp
new PaletteQuery
{
    Arguments = [searchText],
    DisplayLanguageTag = CultureInfo.CurrentUICulture.Name,
    SystemLanguageTag = CultureInfo.InstalledUICulture.Name,
    ItemCount = 10,
    Mode = PaletteQueryMode.Search,
};
```

The complete search text is one string argument.
The bridge does not split the text into tokens.
It does not set `Scopes`, `RequestedProperties`, or query context entries for this search.

**Gap:** The request asks for ten items, but the bridge does not request the next batch.
A source that obeys `ItemCount` can therefore expose only its first batch.
The bridge does not impose a separate ten-item limit on returned arrays.

### 3.2 Query modes and scopes

| WUP operation | Query shape from the bridge | CmdPal surface |
| --- | --- | --- |
| Normal search | `Search`; `Arguments = [searchText]`; no explicit scopes | Aggregate `IDynamicListPage`. |
| Open a container | `Children`; `Scopes = [parent]`; empty arguments | Child `IDynamicListPage`. |
| Search inside a container | `Children`; `Scopes = [parent]`; one text argument | The same child page. |
| Read context commands | `Commands`; empty arguments; item is the implicit scope | `ICommandItem.MoreCommands`. |
| Context-command fallback | `Commands`; empty arguments; `Scopes = [item]` on the source | The same `MoreCommands`. |
| Run a Query system invoker | Query restored with `PaletteQuery.FromJson()` | A separate `IListPage`. |

The bridge does not set language tags or an item count for child and context-command queries.
The Query system invoker keeps the fields supplied in its serialized query.

**Gap:** Normal CmdPal input cannot construct ordered WUP entity arguments.
The bridge does not provide a `RichTitle` editor or fill `Placeholder` items.
The toolkit adapter rejects non-string arguments with `NotSupportedException`.

### 3.3 Progress, cancellation, and paging

For source searches, each WUP progress update replaces that source's visible item snapshot.
The bridge does not append progress items to the previous snapshot.
Final results replace the snapshot again.
The aggregate page merges the current snapshots.

New searches cancel earlier queries.
Query generations prevent older completions from replacing current results.
The bridge uses `IsLoading` as a CmdPal loading indication.

**Gaps:**

- The bridge does not map `PaletteQueryProgress.PaletteProgressState` to a numeric page progress value.
- Child queries and context-command queries do not publish intermediate progress items.
- Every bridge page reports no additional items.
- `LoadMore()` does nothing.
- The bridge never calls `PaletteQueryResultsView.GetMoreItemsAsync()`.

See the [source page][source-page], [child page][children], and [item adapter][list-item].

## 4. Primary command patterns

The [command factory][command-factory] selects the primary command in this order:

1. If the item has the `Container` kind, create a child page.
2. Otherwise, call `TryGetInvoker()`.
3. For a system invoker, select the supported host action.
4. For another invoker, create an invokable command.
5. If no invoker is available, create a `NoOpCommand`.

The container test has priority over every invoker.
A container's primary command therefore opens children, even if the item also has an invocation action.

| WUP pattern | CmdPal pattern | Behavior or gap |
| --- | --- | --- |
| `KnownPaletteKinds.Container` | `WupChildrenPage : DynamicListPage` | Query the owning source for children. |
| `PaletteSystemInvoker(Navigate)` | The same child page | Use `Scopes = [item]`; no separate system navigation action. |
| `PaletteDelegateInvoker` | `WupInvokerCommand : InvokableCommand` | Call the WUP invoker. |
| `PaletteUriInvoker` | The same invokable command | WUP performs the URI action; the bridge does not rebuild launcher options. |
| `PaletteToggleInvoker` | The same invokable command | No CmdPal checked-state mapping from `IsToggled`. |
| No invoker | `NoOpCommand` | The item can still expose details and context commands. |
| `TryGetInvoker()` failure during adaptation | `NoOpCommand` | Log the failure. |

The bridge passes the query that produced the item to `invoker.Invoke(query)`.
Zero-state items and restored commands receive no originating query.
Context commands receive their parent result's originating query, not the Commands query that found the verb.

After a successful invocation, the bridge normally returns `CommandResult.Dismiss()`.
For `InlineCommand` or `KnownPaletteKinds.Toggle`, it returns `CommandResult.KeepOpen()`.
The kind controls this policy; `PaletteToggleInvoker` alone does not select KeepOpen.

**Gap:** WUP invocation has no CmdPal `ICommandResult` return channel.
The bridge cannot recover a provider's original Hide, KeepOpen, navigation, or confirmation result from WUP invocation.
An invocation failure produces a toast and keeps CmdPal open.

See the [invocation commands][invokers].

### System invokers

| System invoker | CmdPal translation | Gap |
| --- | --- | --- |
| `Navigate` | Child list page | Navigation context is not interpreted. |
| `Query` | `WupSystemQueryPage : ListPage` | The page runs the fixed query; it has no dynamic text-search translation. |
| `PickFiles` | Unsupported-action toast; KeepOpen | No picker or entity substitution; `FileTypeFilter` has no effect. |
| `PickFolders` | Unsupported-action toast; KeepOpen | No folder picker. |
| `PickAgent` | Unsupported-action toast; KeepOpen | No agent picker. |
| `TakeScreenshot` | Unsupported-action toast; KeepOpen | No screenshot action. |
| Another system kind | Unsupported-action toast; KeepOpen | No generic system-action execution. |

For `Query`, the bridge reads `SourceKinds` and `Query` from the invoker context.
`SourceKinds` can be a string or a collection of strings.
If no usable source kinds exist, the bridge uses its normal catalog specifications.
It opens a catalog and runs a separate restored query against each selected source.

**Gap:** Missing or invalid query JSON produces **Query failed** empty content.
Individual source failures are logged and clear that source's results.
Paging remains disabled, including for this fixed-query page.

See the [system query page][system-query].

## 5. Children and context commands

### 5.1 Child navigation

A child page uses the owning source, not `parent.ExecuteQueryAsync()`.
The source must handle `Mode = Children` with `Scopes = [parent]`.
This route also applies to a non-container item with a Navigate system invoker.

The first CmdPal `GetItems()` call waits for the initial child snapshot.
This wait has a 30-second limit.
Concurrent initial reads share the query.
Later searches run asynchronously and raise `ItemsChanged`.

This behavior prevents a fast initial update from arriving before CmdPal subscribes to `ItemsChanged`.
Initial failures and timeouts reach normal CmdPal page-error handling.
Later failures are logged and clear the child results.

**Gap:** The bridge does not use `ParentItemId` to make breadcrumbs or find a parent.
It relies on CmdPal page navigation.
Child results keep source order; the aggregate ranking and duplicate-removal step does not run here.

### 5.2 Context commands

The first read of `MoreCommands` starts an asynchronous Commands query.
The bridge first calls `item.ExecuteQueryAsync(query)`.
If this call fails or returns no items, the bridge queries the owning source with `Scopes = [item]`.

Each returned item becomes a `CommandContextItem`.
Its command uses the same command factory as a normal list item.
A container can therefore expose context commands as well as children.

**Gaps:**

- The bridge does not query unscoped source Commands to expose source-level settings or actions.
- The bridge requests context commands once per list-item adapter.
- Item property changes do not repeat the Commands query for that adapter.
- No context-menu separators, nested menu metadata, shortcuts, or other CmdPal context-item attributes are reconstructed.
- A query failure is logged; the context-command list can remain empty.

**Important:** Return children only for Children queries.
Return actions only for Commands queries.
If a source returns children for Commands, CmdPal shows those children as context commands.

See the [item adapter][list-item] and [child page][children].

## 6. Text, icons, tags, and kinds

### Item presentation

| WUP field | CmdPal field | Limit |
| --- | --- | --- |
| `Title` | `ListItem.Title` | Null becomes an empty string. |
| `Subtitle` string elements | `ListItem.Subtitle` | Join nonblank strings with spaces. |
| `Subtitle` item elements | The same subtitle | Use each nonblank item title; discard entity identity. |
| Other subtitle elements | No output | Unsupported element types are omitted. |
| `Icon` | `IconInfo` | See the icon table below. |
| `Tags` | No current list-tag mapping | The tag-conversion helper exists but is not called. |
| `Status.Message` and `Status.Severity` | One `Tag` with a severity glyph | No tag if the message is blank. |
| `Status.Progress` | No item-tag progress | No progress indicator from this field. |
| `Kinds` | Command selection and invocation policy | No general CmdPal item-type metadata mapping. |

File, application, setting, picture, suggestion, and custom kinds remain generic CmdPal list items.
These kinds alone do not create a file launcher, application launcher, preview, or specialized action.
The item must supply an invoker or context commands.

The adapter subscribes to item `PropertyChanged` and refreshes list presentation.
If the command shape changes, the adapter replaces the primary command.
If the shape stays the same, the existing command retains its constructor-time name and icon.

See the [item adapter][list-item] and [text adapter][text].

### Icons

The [icon adapter][icons] uses the first available representation in this order:

| WUP representation | CmdPal translation |
| --- | --- |
| `IconGlyph` | Glyph `IconInfo`. |
| `IconUri` | URI string `IconInfo`. |
| `ImageStreamReference` | `IconData` with the stream reference. |
| `SoftwareBitmap` | Encode PNG and expose a stream reference. |
| No usable representation | Empty `IconInfo`. |

**Gaps:**

- The bridge does not pass `FontFamily`; glyphs use the CmdPal default icon font.
- The bridge ignores `IconOverlay`.
- It does not call `GetBitmapAsync(qualifierValues)` for resource-qualified icons.
- It does not construct separate light-theme and dark-theme icons.
- Bitmap encoding waits at most two seconds; a timeout produces an empty icon.
- Bitmap access and conversion failures are logged and produce an empty icon.

## 7. Content and form actions

An item with `IsContentAvailable` receives a CmdPal `Details` adapter with the `IDetails2` contract.
The bridge calls `GetContentAsync()` only after CmdPal first reads the detail content.
Content does not become a primary `IContentPage`.

| WUP content pattern | CmdPal pattern | Limit |
| --- | --- | --- |
| `PaletteTemplateContentView` | `FormContent` inside `Details.Content` | Not a full CmdPal content page. |
| `TemplateJson` | `IFormContent.TemplateJson` | Pass the JSON without schema conversion. |
| `DataJson` | `IFormContent.DataJson` | Pass the JSON without data conversion. |
| No WUP form-state field | `StateJson = "{}"` | Original CmdPal form state cannot survive the round trip. |
| Template `PropertyChanged` | Refresh template and data JSON | State remains `{}`. |
| Item `ContentChanged` | Reload content after the first content request | No eager load for every list item. |
| Other content-view type | No adapted content | No generic content fallback or explicit unsupported-content message. |

For an action, CmdPal calls `SubmitAction(actionId, inputs, data)`.
The bridge calls WUP `InvokeAction(actionId, data, inputs)`.
The order of data and inputs changes to match the WUP API.
After the action, the bridge returns KeepOpen.

**Gaps:**

- The bridge does not make `IDetails.Metadata` from WUP item properties.
- Form actions have no translated navigation or dismissal result.
- The bridge does not catch form-action exceptions in `SubmitAction`.
- Content-load failures are logged and clear detail content.

The local WUP contract specifies Adaptive Cards version 1.6 and earlier.
JSON forwarding does not prove that every card feature works in the CmdPal renderer.

See the [details adapter][details] and [template adapter][template].

## 8. Ranking, duplicate removal, and item properties

The aggregate page and Query system page use the [result merger][merger].

1. Put items with a finite `double` Score before unscored items.
2. Sort scored items by descending Score.
3. Keep source order when scores are equal or absent.
4. Remove duplicates after sorting.

The duplicate key contains the primary kind and a nonempty string `KindSpecificId`.
Key comparison is case-insensitive.
The first item for a key survives.

**Gap:** The documented WUP Score range is 0.0 to 1.0.
The bridge accepts any finite `double`; it does not check that range.
Other numeric types do not supply a bridge score.

| WUP property | Current use in the bridge |
| --- | --- |
| `Score` | Aggregate sorting. |
| `KindSpecificId` | Aggregate duplicate removal with the primary kind. |
| `SourceKinds`, `Query` | Query system-invoker context only. |
| `Path`, `System.AppUserModel.ID` | No automatic launcher or detail metadata. |
| `CommandGlyph` | No separate action-glyph mapping. |
| `UndoItemId` | No undo relationship or undo command. |
| `ParentItemId` | No parent lookup or breadcrumb mapping. |
| `MatchType` | No match classification in CmdPal. |
| `LastAccessedDate`, `LastModifiedDate`, `CreatedDate` | No automatic detail fields or date-based sorting. |
| `ExcludeFromHistory` | No CmdPal history-exclusion mapping. |
| `RichTitle` | No template display or entity-selection mapping. |

**Important gap:** WUP requires hosts to honor `ExcludeFromHistory`.
The bridge does not translate this property into a CmdPal history restriction.
Do not assume that sensitive or session-only items are excluded from CmdPal history.

The bridge does not populate `ExpectedKinds` or `AdditionalQueries` for normal text searches.
A Query system invoker can carry these fields in serialized query context.
The selected source must implement them; the bridge does not apply a separate result-kind filter or combined-query expansion.

## 9. Identity, status, and source lifetime

### 9.1 Command identity

The [command ID helper][command-id] builds IDs with this format:

```text
wup-item:v1:<kind>:<encoded-extension-id>:<encoded-item-id>
```

The kind token is `i` for invocation and `c` for a list page.
Each identity component uses UTF-8 and Base64url encoding without padding.
The bridge also reads the older `wup:<extension-id>:<item-id>` format.

CmdPal `GetCommandItem()` restores an item through `source.GetItem(itemId)`.
The bridge checks that the restored command has the expected command kind.
Source activation and item lookup each have a five-second wait limit.
Failure is logged and returns no restored command.

**Gaps:** Restoration does not recover the originating query.
Stable restoration requires stable extension and item IDs.
Session-only toolkit IDs cannot provide durable restoration after the toolkit clears its item cache.

### 9.2 Host status

| WUP host call | CmdPal translation |
| --- | --- |
| `ReportStatusAsync` message | `IExtensionHost.ShowStatus`, with extension context. |
| Informational, Success, Warning, Error | Info, Success, Warning, Error. |
| Fractional progress | Multiply by 100, clamp to 0-100, and convert to an unsigned integer. |
| Indeterminate progress | `ProgressState.IsIndeterminate`. |
| Repeated message and severity | Update the existing status object's progress. |
| `GetSystemInvoker` | Create a WUP system invoker, with context if supplied. |

**Gaps:** The bridge does not preserve `PaletteStatus.Item` as an item-specific CmdPal status.
It does not subscribe to later changes in the WUP progress object.
The shared status cache has no translated hide or session-clear operation.
If the CmdPal host is unavailable, the status call completes without display.

See the [extension host][host].

### 9.3 Events, dismissal, and analytics

Sources activate on first use.
Source `ItemsChanged` makes the bridge repeat that source's last search or fixed query.
`RequestUnload()` cancels queries, clears results, releases the source, and raises the host's Dismissed event.
The next query can activate the source again.

The bridge also raises Dismissed after a dismissing invocation and during source replacement or disposal.
Disposal waits up to five seconds per source for the Dismissed callback.
Normal invocation dismissal runs the callback asynchronously.

The [analytics adapter][analytics] reports each final-result identity once per session.
It reports Selected, OtherSelected, or Dismissed when the session ends.

**Gaps:**

- CmdPal does not notify the bridge when the user only closes the window.
- The bridge cannot report that close as an immediate WUP dismissal.
- Final-result analytics run before aggregate duplicate removal and do not measure actual on-screen visibility.
- Progress-only results and child-page results do not receive equivalent visibility reports.
- Child-page adapters have no analytics session.
- Selection tracking does not cover page navigation or form actions.

## 10. What the toolkit produces in the opposite direction

The toolkit `PaletteExtension` type wraps an existing CmdPal provider as a WUP source.
Use a separate provider instance for each host.
The toolkit adapter owns and disposes that provider.

### 10.1 Provider and item patterns

| Original CmdPal pattern | WUP pattern from the toolkit | Result after the bridge |
| --- | --- | --- |
| `TopLevelCommands()` | Source `GetItems()` | Items inside Windows Palettes, if the source supports zero state. |
| Root text search | Filter top-level commands; update fallback handlers for nonblank text | Search results inside Windows Palettes. |
| `IListPage` command | Container kind and Navigate system invoker | A bridge child page, not the original page object. |
| `IDynamicListPage` | Set `SearchText`; read page results | Child search uses the provider's dynamic results. |
| Static `IListPage` | Toolkit text filtering of page items | Child search returns filtered items. |
| `IInvokableCommand` | Command kind and delegate invoker | A bridge invokable command. |
| `IContentPage` | Command kind and content delegate | CmdPal details content; no primary content page. |
| `ICommandItem.MoreCommands` | Item Commands query | New CmdPal `CommandContextItem` objects. |
| `IContentPage.Commands` | Additional item Commands results | Context commands on the bridge list item. |
| Provider settings page | Unscoped source Commands result | Not requested by the bridge's normal page. |
| Stable `ICommand.Id` | The WUP item ID | Encoded bridge command ID. |
| Missing command ID | `cmdpal-session:<guid>` and `ExcludeFromHistory = true` | Session identity; the bridge does not enforce history exclusion. |
| `IListItem.Tags` | WUP tags, with a fallback glyph for text-only tags | Original tags are omitted. |
| Page `IsLoading` | Informational item status with indeterminate progress | A status tag, not the original page's loading UI. |

The toolkit keeps only context items that implement `ICommandContextItem`.
Other context-item types do not enter the WUP Commands result.
The toolkit does not copy `IListItem.Details` into WUP content.
It creates content from an `IContentPage` command instead.

The toolkit uses the light icon if available, then the dark icon.
It can read a font-family extended attribute and make a WUP glyph icon.
The bridge does not restore that font-family attribute.

The toolkit item adapter and query adapter implement this translation.

### 10.2 Content and command results

| Original CmdPal pattern | Toolkit translation | Round-trip gap |
| --- | --- | --- |
| One `IFormContent` | WUP template and data JSON | Form state is not copied; content returns as details. |
| `IFormContent2.SubmitAction` | WUP action delegate with the action ID | The bridge returns KeepOpen after the WUP action. |
| Plain text or Markdown content | Adaptive Card 1.6 TextBlock elements | Original content types are not restored. |
| Image content or a mixed form/text array | `NotSupportedException` | No equivalent content output. |
| Dismiss, Hide, KeepOpen result | Leave window policy to the WUP host | The bridge normally dismisses; original policy is lost. |
| ShowToast result | WUP informational host status; handle any nested result | Returns as a CmdPal status, not the original toast. |
| Confirmation, page navigation, or another result kind | `NotSupportedException` | No equivalent result; confirmation is not accepted automatically. |

The toolkit does not invoke a confirmation's primary command without user consent.
This is a deliberate safety limit, not an implicit confirmation.
For invocation, unsupported results reach the bridge's failure toast.
For form actions, the bridge has no local exception handler.

The toolkit content adapter and result handler implement this translation.

### 10.3 Query and host limits in the toolkit

- The toolkit joins string arguments with spaces to make CmdPal search text.
- It does not interpret query language tags or `RequestedProperties`.
- It rejects entity arguments and foreign-extension scopes.
- It rejects `AdditionalQueries`.
- It applies `ExpectedKinds` when that value is a string array.
- It supports WUP result batches and CmdPal `LoadMore()`.
- It rejects a paginated query with multiple scopes.
- It rejects LoadMore for a superseded page search.
- It does not emit WUP query progress from its query operation.
- Its analytics methods complete without processing reports.

**Round-trip gap:** Toolkit paging exists, but bridge paging does not.
A provider that works with a paging WUP host can lose results through this bridge.

The toolkit maps CmdPal status and log messages to WUP host status.
Its HideStatus call sends an empty informational status.
The bridge does not interpret that call as removal of the earlier CmdPal status.

The toolkit query adapter, extension, and host adapter implement this translation.

## 11. Developer checks

Use these checks for a source that must work through the bridge.

1. Declare source kinds that match the intended catalog.
2. Supply stable IDs for items that users can restore.
3. Handle Children queries with explicit parent scopes.
4. Keep Children results separate from Commands results.
5. Support item-scoped Commands or the source-scoped fallback.
6. Supply an invoker for leaf items that must perform an action.
7. Add the Toggle or InlineCommand kind when invocation must keep CmdPal open.
8. Use default-font glyphs, URI icons, or stream icons when appearance is important.
9. Test form actions in the details area, not only in a content page.
10. Check first-batch results because the bridge cannot load additional batches.
11. Do not depend on rich entity input, undo relationships, or history exclusion.
12. Test failures as well as successful queries and actions.

Focused tests must cover child loading, cancellation, errors, and result reuse.
These tests do not prove complete round-trip behavior or identical UI rendering.

## Source references

[wup-idl]: ../../Windows.UI.Palettes/Windows.UI.Palettes.idl
[provider]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WindowsPalettesCommandsProvider.cs
[aggregate]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WindowsPalettesPage.cs
[source-page]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupSourcePage.cs
[command-factory]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupCommandFactory.cs
[children]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupChildrenPage.cs
[list-item]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupListItemAdapter.cs
[invokers]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupInvokerCommand.cs
[system-query]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupSystemQueryPage.cs
[text]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupTextAdapter.cs
[icons]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupIconAdapter.cs
[details]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupDetailsAdapter.cs
[template]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupTemplateContentAdapter.cs
[merger]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupResultMerger.cs
[command-id]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupCommandId.cs
[host]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupExtensionHost.cs
[analytics]: ../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupAnalyticsSession.cs
[ste]: https://www.asd-ste100.org/about_STE.html
