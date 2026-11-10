# Understanding WUP and the CmdPal bridge

This guide introduces the Windows.UI.Palettes (WUP) API.
It explains how WUP patterns describe application behavior.
It also explains how the WUP-to-CmdPal bridge changes these patterns into Command Palette (CmdPal) equivalents.

The [API reference](wup-to-cmdpal-reference.md) contains exact fields,
implementation details, and a complete list of gaps between the two APIs.

- [Understanding WUP and the CmdPal bridge](#understanding-wup-and-the-cmdpal-bridge)
  - [1. What WUP is trying to do](#1-what-wup-is-trying-to-do)
  - [2. The WUP building blocks](#2-the-wup-building-blocks)
    - [Sources and the catalog: who supplies the items?](#sources-and-the-catalog-who-supplies-the-items)
    - [Items: what can the user see?](#items-what-can-the-user-see)
    - [Queries: what is the host asking for?](#queries-what-is-the-host-asking-for)
    - [Invokers: what happens when the user activates an item?](#invokers-what-happens-when-the-user-activates-an-item)
    - [Content: what can the user inspect or enter?](#content-what-can-the-user-inspect-or-enter)
  - [3. One example from start to finish](#3-one-example-from-start-to-finish)
  - [4. Common patterns](#4-common-patterns)
    - [Pattern A: perform one action](#pattern-a-perform-one-action)
    - [Pattern B: browse a group](#pattern-b-browse-a-group)
    - [Pattern C: offer additional actions](#pattern-c-offer-additional-actions)
    - [Pattern D: search across sources](#pattern-d-search-across-sources)
    - [Pattern E: keep an action inside the palette](#pattern-e-keep-an-action-inside-the-palette)
    - [Pattern F: show a summary or form](#pattern-f-show-a-summary-or-form)
    - [Pattern G: ask the host to perform a task](#pattern-g-ask-the-host-to-perform-a-task)
    - [Pattern H: build a request with selected objects](#pattern-h-build-a-request-with-selected-objects)
  - [5. How presentation and updates work](#5-how-presentation-and-updates-work)
  - [6. What happens to an existing CmdPal provider?](#6-what-happens-to-an-existing-cmdpal-provider)
  - [7. Choose a pattern](#7-choose-a-pattern)
  - [8. Read the implementation details when needed](#8-read-the-implementation-details-when-needed)


## 1. What WUP is trying to do

An application can expose things that a user can find, inspect, or act on.
For example, a project application can expose projects, documents, and commands.
WUP gives applications a common way to describe these things and their behavior.
The API is in the `Windows.UI.Palettes` namespace.

A WUP **item** is a description of one thing.
It has a title and can have an icon, additional information, and behavior.
The item can represent a document, a group of documents, or an action such as **Copy path**.
An item does not have to be an executable command.

A WUP **source** supplies these items.
A **host** is the application that requests items and presents them to the user.
The source describes the available information and behavior.
The host decides how to display them.

For example, a source can describe **Projects** as a group that contains other items.
WUP does not require the source to build a CmdPal page.
The bridge turns that description into a page that CmdPal can display.

The `Microsoft.CommandPalette.Extensions` namespace contains structurally
similar abstractions. However, the mapping between WUP items and CmdPal
structures is not one-to-one. This bridge handles the translation and decides
how to represent each WUP item within CmdPal.

**Main rule:** A WUP item describes what is available.
The bridge decides which CmdPal structure represents it.
Not every WUP feature has a CmdPal translation.

## 2. The WUP building blocks

### Sources and the catalog: who supplies the items?

The **catalog** lets a host find WUP sources.
A source declares its roles and the hosts in which it can participate.
For example, a source can supply items before input or results after a search.

The API names are `PaletteCatalog` and `IPaletteItemSourceExtension`.
Before use, a source receives an item factory and a host object.
The factory creates items associated with that source.
The host object supports status reports and requests for host actions.

The bridge finds eligible sources through the catalog.
It exposes their items inside one CmdPal page named **Windows Palettes**.
It does not put each WUP result on the CmdPal root page.

### Items: what can the user see?

A source builds a `PaletteItem` with an ID, title, and other fields.
It returns a `PaletteItemView` to the host.
The view lets the host read the item and request its behavior.
The source can later change the item and notify the host.

An item's **kinds** describe what it represents.
For example, `Container` means that the item represents a group.
`File` means that it represents a file.
Kinds are semantic classifications, not visible labels or CmdPal page objects.

The bridge creates a CmdPal `ListItem` for a returned item.
It copies supported presentation fields and assigns a primary command.
Later sections explain how it selects that command.

### Queries: what is the host asking for?

A query is a request for items.
WUP uses one query type, `PaletteQuery`, for three different questions:

- **Search:** Which items match this input?
- **Children:** Which items are inside this group?
- **Commands:** Which actions are available for this item?

The query's `Mode` selects the question.
Its `Arguments` contain input, such as search text.
Its `Scopes` identify the items to which the question applies.
A scope is a target item, not necessarily a filesystem folder.

For example, a Children query scoped to **Projects** asks for the items inside **Projects**.
A Commands query scoped to **Roadmap** asks for actions available for **Roadmap**.
The same source must return different results for these two questions.

As another example: An item representing the "Documents" folder responds to a
* `Commands` query with items like "Copy path", "Show in explorer". _Verbs_ on that item.
* `Children` query with file items that are in that folder.

### Invokers: what happens when the user activates an item?

An **invoker** is the behavior attached to an item.
For example, an invoker can open a URI or call source code.
The host obtains it through `TryGetInvoker()`.
It does not have to infer an action from the item's title.

WUP also has **system invokers**.
These ask the host to perform an action, such as navigation or file selection.
A system invoker is a request, not proof that the host supports the action.

### Content: what can the user inspect or enter?

An item can supply more content than its title and subtitle.
For example, it can supply a project summary or a form.
The host requests that content only when it needs it.

In the current WUP contract, template content uses Adaptive Card JSON.
The template describes the controls.
Data supplies values for those controls.
A form button can call an action supplied by the source.

The bridge puts supported content in the CmdPal item's details area.
It does not turn content into a separate primary page.

## 3. One example from start to finish

Suppose the fictional source supplies a **Projects** item.
That item contains **Roadmap** and **Release checklist**.
The **Roadmap** item opens a website and also offers **Copy link**.

The user sees this structure in CmdPal:

```text
CmdPal root
  Windows Palettes                 bridge's top-level command
    Projects                       opens a child page
      Roadmap                      opens a website
        More commands: Copy link   performs an additional action
      Release checklist
```

These labels describe navigation, not a WUP object hierarchy stored in CmdPal.
The bridge requests each set of items when CmdPal needs it.

1. The user opens **Windows Palettes**.
   The bridge requests initial items from eligible sources.
2. The source returns **Projects** with the Container kind.
   The bridge makes a list item whose primary command opens a child page.
3. The user opens **Projects**.
   The bridge asks its source for Children with **Projects** as the scope.
4. The source returns **Roadmap** and **Release checklist**.
   The bridge makes list items for both results.
5. The user activates **Roadmap**.
   The bridge calls its invoker, which opens the website.

Reading additional commands takes a different path.
When CmdPal first reads Roadmap's `MoreCommands`, the bridge sends a Commands query for **Roadmap**.
The source returns the **Copy link** action as another WUP item.
The bridge turns it into a CmdPal context command.
CmdPal calls that action's invoker when the user selects **Copy link**.

**Important:** **Roadmap** is a child of **Projects**.
**Copy link** is an action on **Roadmap**.
Children and Commands are different relationships, even though both queries return items.

## 4. Common patterns

### Pattern A: perform one action

**User goal:** Open the project website.

**In WUP:** Return an item with a title and an invoker.
For a website, use `PaletteUriInvoker`.
For behavior implemented by the source, use `PaletteDelegateInvoker`.

This C# excerpt uses the factory supplied when the source is initialized:

```csharp
var item = factory.Create(
    "project-website",
    new PaletteKinds([KnownPaletteKinds.Command]));
item.Title = "Open project website";
item.Invoker = new PaletteUriInvoker(new Uri("https://example.com/projects"));
return item.GetView();
```

**In CmdPal:** The bridge creates a `ListItem` with an `InvokableCommand`.
When the user activates it, the bridge calls the WUP invoker.
A successful invocation normally closes CmdPal.

> [!WARNING]
> **Gap:** A File or Application kind alone does not create an open action.
For example, a file path in `Properties` does not make the bridge launch that file.
Supply an invoker when the item must perform an action.
Without an invoker, the bridge uses `NoOpCommand`, which performs no primary action.

### Pattern B: browse a group

**User goal:** Open **Projects** and see its contents.

**In WUP:** Return an item with `KnownPaletteKinds.Container`.
Handle Children queries with that item in `Scopes`.
Return the group's contents as query results.

**In CmdPal:** The bridge assigns an `IDynamicListPage` as the item's primary command.
This is a list page that can send new requests when the search text changes.
Opening the item requests children; typing inside the page requests filtered children.
Both requests use Children mode and the same parent scope.

The bridge also uses this page pattern for a Navigate system invoker.
For child navigation, it queries the owning source with explicit scopes.
It does not call the parent item's query method.

> [!WARNING]
> **Gap:** A Container item opens children even if it also has an invoker.
Put a separate action in its Commands results if the container also needs an executable action.
The bridge does not use `ParentItemId` to construct navigation breadcrumbs.

### Pattern C: offer additional actions

**User goal:** Copy a document link without opening the document.

**In WUP:** Return a **Copy link** item for a Commands query on the document.
Give that action item its own invoker.
Do not return the document's children for this request.

**In CmdPal:** Each action becomes a `CommandContextItem` in `MoreCommands`.
This is an additional command attached to the visible list item.
The bridge first queries the item itself.
If that request fails or returns no actions, it queries the source with the item in `Scopes`.

> [!WARNING]
> **Gap:** The bridge loads these actions once per list-item adapter.
It does not query source-level actions without an item scope.
It does not reconstruct menu separators, keyboard shortcuts, or other context-menu metadata.

### Pattern D: search across sources

**User goal:** Type **roadmap** and find matching items.

**In WUP:** A Search query asks a source for matching results.
A source can also supply initial items before the user types.
Those initial items are called **zero-state items**.

**In CmdPal:** The bridge's Windows Palettes page combines results from eligible sources.
For empty input, it calls `GetItems()` on sources that supply zero-state items.
For nonempty input, it sends Search queries to sources that supply query results.
The whole search text becomes one string argument.

> [!WARNING]
> **Gap:** Normal search requests ask for ten items.
The bridge does not request later result batches.
This can hide additional matches from a source that returns results in batches.
The bridge does not independently truncate a larger returned array to ten items.

### Pattern E: keep an action inside the palette

**User goal:** Change a setting and continue using CmdPal.

**In WUP:** An item can use `PaletteToggleInvoker` to represent toggle behavior.
Its `IsToggled` field describes the current toggle state.
The `KnownPaletteKinds.Toggle` kind identifies a toggle item.

**In CmdPal:** The bridge calls the invoker.
It keeps CmdPal open for items with the Toggle or `InlineCommand` kind.

> [!WARNING]
> **Gap:** It does not display a checked state from `IsToggled`.
A toggle invoker without the Toggle kind does not, by itself, select the keep-open behavior.
The bridge selects window behavior from the item's kinds, not a returned WUP command result.

### Pattern F: show a summary or form

**User goal:** Inspect project information or edit a value.

**In WUP:** Attach a content delegate to the item.
It supplies `PaletteTemplateContent` with template JSON, data JSON, and an action callback.
The item view exposes this through `IsContentAvailable` and `GetContentAsync()`.

**In CmdPal:** The bridge assigns `Details` to the list item.
It adapts the template as `FormContent` inside the details area.
It loads the content only when CmdPal first reads it.
A submitted action goes back to the WUP content callback.
The bridge keeps CmdPal open after the action.

An item can have content and an invoker at the same time.
For example, **Roadmap** can show a summary in details and open a website when activated.
Content, child navigation, and additional actions are independent capabilities.

> [!WARNING]
> **Gap:** Content does not become a primary `IContentPage`.
Other content-view types have no adapted output.
The bridge does not preserve CmdPal form state or translate form-action navigation results.
The WUP contract allows Adaptive Cards version 1.6 and earlier; rendering still depends on CmdPal support.

### Pattern G: ask the host to perform a task

**User goal:** Start another search or select a file.

**In WUP:** Attach a `PaletteSystemInvoker`.
Its kind identifies the requested host action.
Its context contains action parameters.
For example, a Query invoker contains a serialized query and optional source-selection kinds.

**In CmdPal:** Navigate opens a child page.
Query opens a separate list page that runs the supplied query against selected sources.
The Query page runs that fixed request; it is not a new dynamic text-search page.

> [!WARNING]
> **Gap:** PickFiles, PickFolders, PickAgent, and TakeScreenshot are not implemented by the bridge.
They show an unsupported-action toast and keep CmdPal open.
The bridge does not perform the task or insert its result into another request.

### Pattern H: build a request with selected objects

**User goal:** Choose a document and an agent for **Send [document] to [agent]**.

**In WUP:** A `RichTitle` can describe text with item-selection slots.
Items with the Placeholder kind identify slots that still need a selection.
After selection, the query's Arguments can contain the chosen item views along with text.
Here, an **entity** means an item used as an argument, not just its title.

**In CmdPal:** The bridge has no equivalent input flow.
Normal search input becomes a string, not a sequence of selected entities.

> [!WARNING]
> **Gap:** The bridge does not fill placeholders or construct entity arguments.
The opposite toolkit adapter also rejects entity arguments.
Replacing a document entity with its title loses identity and is not an equivalent translation.

## 5. How presentation and updates work

The bridge copies titles and supported icons to CmdPal list items.
It reduces a WUP subtitle to one string.
Subtitle items contribute their titles, not their identity or behavior.
This is a display conversion, not entity-input support.

WUP visible tags and semantic kinds serve different purposes.
A tag is a label for display.
A kind describes what the item represents.
The bridge currently omits ordinary WUP tags.
An item status message becomes a tag with a severity icon.

A source can report a host status, such as **Still indexing**.
The bridge turns this into a CmdPal extension status message.
This differs from the status tag on an individual item.

Sources can notify the host when their results change.
The bridge repeats the source's last query after `ItemsChanged`.
Item property changes update supported list presentation.
Content notifications update content after its first load.

**Limits to remember:**

- Custom glyph fonts, icon overlays, and separate theme icons are not preserved.
- Source search progress can replace visible results; it does not append to the previous result array.
- Numeric query progress does not become numeric page progress.
- The bridge can sort combined results by Score and remove duplicates by primary kind and `KindSpecificId`.
- File paths, timestamps, and other properties do not automatically become CmdPal detail fields.
- The bridge does not construct undo actions from `UndoItemId`.

**History gap:** WUP's `ExcludeFromHistory` asks hosts not to save an item in user history.
The bridge does not translate this into a CmdPal history restriction.
Do not assume that sensitive or session-only items stay out of CmdPal history.

**Lifetime gap:** The bridge can notify sources after an action closes CmdPal.
It cannot observe a user who only closes the CmdPal window.
Dismissal notifications and analytics therefore do not cover every actual window session.

## 6. What happens to an existing CmdPal provider?

The toolkit adapter works in the opposite direction.
It exposes an existing CmdPal provider as a WUP source.
The WUP-to-CmdPal bridge can then consume that source.
This complete path is called a **round trip**.

```text
Existing CmdPal provider
        |
        v
Toolkit adapter: CmdPal -> WUP
        |
        v
WUP source interface
        |
        v
Bridge: WUP -> CmdPal
        |
        v
New CmdPal structures
```

The final structures are not the original provider objects.
Each adapter preserves only the features that it supports.

| Original CmdPal feature | WUP representation | What returns through the bridge |
| --- | --- | --- |
| List page | Container item and Children results | New child list page. |
| Invokable command | Item with a delegate invoker | New invokable command. |
| Additional commands | Commands query results | New `MoreCommands` entries. |
| Content page | Item with template content | Details content, not a primary content page. |
| Text or Markdown content | Adaptive Card text blocks | Template content in details. |
| List-item tags | WUP tags | Original tags are omitted. |
| Provider settings page | Source-level Commands result | Not requested by the bridge's normal page. |
| More result batches | WUP paging | Only the batches already returned; no load-more request. |

Window behavior is another important difference.
The toolkit leaves Dismiss, Hide, and KeepOpen results to the WUP host.
The bridge cannot recover the original result.
It normally closes CmdPal after successful invocation, except for the keep-open kinds described earlier.

The toolkit rejects unsupported command results, including confirmation and page navigation.
It does not accept a confirmation or execute its primary command automatically.
It also rejects unsupported content, such as an image content item or a mixed form/text array.

**Practical consequence:** A working direct CmdPal extension can behave differently after a round trip.
Test the translated interaction, not only the original extension.
Use stable command IDs for items that users must restore later.
Restoration through the bridge does not recover the query that originally produced the item.

## 7. Choose a pattern

Start with the user goal, then choose the WUP representation.

| User goal | WUP representation | CmdPal structure from the bridge |
| --- | --- | --- |
| Perform an action | Item with an invoker | List item with an invokable primary command. |
| Browse a group | Container item and Children query handling | List item that opens a child page. |
| Offer another action | Action items from a Commands query | Context commands in `MoreCommands`. |
| Find matching items | Search query handling | Results in the Windows Palettes page. |
| Inspect information or fill a form | Item with template content | Content in the list item's details area. |
| Request another search | Query system invoker | Separate fixed-query list page. |
| Select entities for a request | RichTitle, placeholders, and entity arguments | Not supported by the bridge. |

For a container with actions, implement Children and Commands separately.
For a file that must open, supply an invoker as well as file metadata.
For editable content, test the form in the details area.
For large result sets, account for the lack of paging.

## 8. Read the implementation details when needed

The [API reference](wup-to-cmdpal-reference.md) preserves the detailed mappings and gaps.
Use these sections for specific questions:

- [Source selection](wup-to-cmdpal-reference.md#2-source-discovery-and-initial-items): catalog filters and initial items.
- [Queries](wup-to-cmdpal-reference.md#3-query-patterns): exact fields, scopes, progress, and paging.
- [Primary commands](wup-to-cmdpal-reference.md#4-primary-command-patterns): selection order and system actions.
- [Children and actions](wup-to-cmdpal-reference.md#5-children-and-context-commands): loading, routing, and failures.
- [Content](wup-to-cmdpal-reference.md#7-content-and-form-actions): form fields, callbacks, and limits.
- [Properties](wup-to-cmdpal-reference.md#8-ranking-duplicate-removal-and-item-properties): ranking and unsupported metadata.
- [Identity and lifetime](wup-to-cmdpal-reference.md#9-identity-status-and-source-lifetime): restoration, status, dismissal, and analytics.
- [Toolkit translation](wup-to-cmdpal-reference.md#10-what-the-toolkit-produces-in-the-opposite-direction): the other side of the round trip.

For code, start with the [command factory](../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupCommandFactory.cs).
It selects the CmdPal primary command for each WUP item.
Then read the [item adapter](../../ext/Microsoft.CmdPal.Ext.WindowsPalettes/WupListItemAdapter.cs) for presentation and additional commands.
The [local WUP contract](../../Windows.UI.Palettes/Windows.UI.Palettes.idl) defines the API types used here.
