# 03 - JSON-RPC Protocol Specification

## Overview

Communication between the CmdPal host and JavaScript extensions uses **JSON-RPC 2.0** over **stdio** with **LSP-style `Content-Length` framing**.

### Framing Format

Every message (request, response, and notification) is preceded by a header:

```http
Content-Length: <byte-count>\r\n
\r\n
<UTF-8 JSON body>
```

Where `<byte-count>` is the byte length (not character length) of the JSON body.

### Connection Properties

| Property | Value |
|----------|-------|
| Transport | stdin (host→extension), stdout (extension→host) |
| Encoding | UTF-8 |
| Protocol | JSON-RPC 2.0 |
| Request timeout | 10 seconds |
| Concurrency | Requests are serialized (one at a time); notifications can interleave |

### Message Types

| Type | Has `id` | Has `method` | Direction |
|------|----------|-------------|-----------|
| Request | ✅ | ✅ | Host → Extension |
| Response | ✅ | ❌ | Extension → Host |
| Notification | ❌ | ✅ | Either direction |

---

## Host → Extension Requests

### `initialize`

Called once after the Node.js process starts. The extension should initialize its provider and return capabilities.

**Parameters:**
```json
{
  "extensionId": "my-extension"
}
```

**Response:**
```json
{
  "capabilities": ["commands"]
}
```

---

### `provider/getTopLevelCommands`

Fetches the extension's top-level command items (shown in the main CmdPal list).

**Parameters:** `null`

**Response:** Array of command items:
```json
[
  {
    "id": "cmd-1",
    "title": "My Command",
    "displayName": "My Command",
    "subtitle": "Does something useful",
    "command": {
      "id": "cmd-1",
      "name": "My Command",
      "icon": { "light": { "icon": "\uE8A5" } },
      "pageType": "dynamicListPage"
    },
    "icon": { "light": { "icon": "\uE8A5" } },
    "moreCommands": []
  }
]
```

The `command` object includes a `pageType` field when the command represents a page. Values are:
- `"listPage"`: static list page
- `"dynamicListPage"`: search-enabled list page
- `"contentPage"`: rich content page
- Absent: invokable command (no page)

---

### `provider/getFallbackCommands`

Fetches commands that receive the user's search query when no other results match.

**Parameters:** `null`

**Response:** Array of fallback command items, or `null`:
```json
[
  {
    "id": "search-web",
    "title": "Search the web",
    "displayName": "Search the web",
    "command": { "id": "search-web", "name": "Search the web" }
  }
]
```

---

### `provider/getCommand`

Fetches a specific command/page by ID. Used when navigating to a page.

**Parameters:**
```json
{
  "commandId": "my-page-id"
}
```

**Response:** Command object or `null`:
```json
{
  "id": "my-page-id",
  "name": "My Page",
  "pageType": "listPage",
  "icon": { "light": { "icon": "\uE8A5" } }
}
```

---

### `provider/getSettings`

Fetches the extension's settings page ID.

**Parameters:** `null`

**Response:**
```json
{
  "id": "settings-page-id"
}
```

Or `null` if the extension has no settings.

---

### `command/invoke`

Invokes a command by ID.

**Parameters:**
```json
{
  "commandId": "my-command-id"
}
```

**Response:** Command result. The TypeScript SDK returns `{ kind, args? }`, and the runtime serializes that object to the numeric wire shape shown here.
```json
{
  "Kind": 6,
  "Args": {
    "Message": "Operation complete!"
  }
}
```

`Kind` values:
| Value | Name | Description |
|-------|------|-------------|
| 0 | Dismiss | Close CmdPal |
| 1 | GoHome | Navigate to home |
| 2 | GoBack | Navigate back |
| 3 | Hide | Hide CmdPal (keep state) |
| 4 | KeepOpen | Stay on current page |
| 5 | GoToPage | Navigate to page (requires `PageId` in args) |
| 6 | ShowToast | Show toast notification (requires `Message` in args) |
| 7 | Confirm | Show confirmation dialog |

---

### `listPage/getItems`

Fetches items for a list page.

**Parameters:**
```json
{
  "pageId": "my-list-page"
}
```

**Response:**
```json
{
  "items": [
    {
      "id": "item-1",
      "title": "Item One",
      "subtitle": "Description",
      "section": "Group A",
      "command": { "id": "item-1-cmd", "name": "Item One" },
      "icon": { "light": { "icon": "\uE8A5" } },
      "tags": [{ "text": "New", "foreground": { "hasValue": true, "color": { "r": 255, "g": 255, "b": 255, "a": 255 } } }],
      "details": {
        "title": "Item One Details",
        "body": "**Rich** markdown description",
        "metadata": [
          { "key": "Author", "data": { "type": "tags", "tags": [{ "text": "mjolley" }] } },
          { "key": "Link", "data": { "type": "link", "link": "https://github.com", "text": "GitHub" } }
        ]
      },
      "moreCommands": [
        {
          "command": { "id": "copy-cmd", "name": "Copy" },
          "title": "Copy to clipboard",
          "icon": { "light": { "icon": "\uE8C8" } }
        }
      ]
    }
  ]
}
```

Items with `_isSeparator: true` are rendered as visual separators:
```json
{
  "title": "Section Header",
  "section": "Section Header",
  "_isSeparator": true,
  "command": null
}
```

---

### `listPage/setSearchText`

Updates the search text for a dynamic list page.

**Parameters:**
```json
{
  "pageId": "my-dynamic-page",
  "searchText": "user query"
}
```

**Response:** `null`

The extension should update its internal state and send a `listPage/itemsChanged` notification when items are ready.

---

### `listPage/setFilter`

Updates the active filter for a list page.

**Parameters:**
```json
{
  "pageId": "my-filtered-page",
  "filterId": "recent"
}
```

**Response:** `null`

---

### `listPage/loadMore`

Requests additional items for infinite scroll.

**Parameters:**
```json
{
  "pageId": "my-page"
}
```

**Response:** `null`

The extension should append items and send a `listPage/itemsChanged` notification.

---

### `fallback/updateQuery`

Updates the search query for a fallback command. The shipped C# host sends this method as a notification; the SDK also accepts it as a request and returns `null`.

**Parameters:**
```json
{
  "commandId": "search-fallback",
  "query": "user typed text"
}
```

**Response when sent as a request:** `null`

The extension should update its internal state and send a `command/propChanged` notification to update the display title.

---

### `contentPage/getContent`

Fetches content for a content page.

**Parameters:**
```json
{
  "pageId": "my-content-page"
}
```

**Response:** Array of content items:
```json
[
  {
    "type": "markdown",
    "body": "# Hello\n\nMarkdown content"
  },
  {
    "type": "image",
    "image": { "light": { "data": "iVBORw0KGgo..." } },
    "maxWidth": 600,
    "maxHeight": 400
  },
  {
    "type": "form",
    "templateJson": "{...adaptive card JSON...}",
    "dataJson": "{...data values...}"
  }
]
```

---

### `form/submit`

Submits form data from a content page or form content.

**Parameters:**
```json
{
  "pageId": "my-form-page",
  "inputs": "{\"name\":\"John\"}",
  "data": "{}"
}
```

**Response:** Command result (same format as `command/invoke`).

---

### `dispose`

Notification sent before the host kills the extension process.

**Parameters:** `null`

**Note:** This is a notification (no `id`), not a request. The extension should clean up resources.

---

## Extension → Host Notifications

### `provider/itemsChanged`

Tells the host to re-fetch the provider's top-level or fallback command items.

```json
{
  "jsonrpc": "2.0",
  "method": "provider/itemsChanged",
  "params": {
    "totalItems": 10
  }
}
```

---

### `listPage/itemsChanged`

Tells the host to re-fetch items for a list page.

```json
{
  "jsonrpc": "2.0",
  "method": "listPage/itemsChanged",
  "params": {
    "pageId": "my-list-page"
  }
}
```

---

### `command/propChanged`

Tells the host that a command's properties have changed (e.g., fallback display title).

```json
{
  "jsonrpc": "2.0",
  "method": "command/propChanged",
  "params": {
    "commandId": "my-fallback",
    "properties": {
      "displayTitle": "Search: new query"
    }
  }
}
```

---

### `host/logMessage`

Sends a log message to the host's logging system.

```json
{
  "jsonrpc": "2.0",
  "method": "host/logMessage",
  "params": {
    "message": "Extension initialized successfully",
    "state": 0
  }
}
```

State values: 0 = Info, 1 = Success, 2 = Warning, 3 = Error

---

### `host/showStatus`

Shows a status message in the CmdPal status bar. The extension mints a stable
`statusId` and includes it so the same status can later be updated or hidden by id.
Re-sending `host/showStatus` with an existing `statusId` updates that status in place
(this is what `ExtensionHost.updateStatus` does). The `message` object is retained for
compatibility with a host that still matches on message text.

```json
{
  "jsonrpc": "2.0",
  "method": "host/showStatus",
  "params": {
    "statusId": "status-1",
    "message": {
      "Message": "Loading data...",
      "State": 0
    },
    "progress": { "isIndeterminate": true },
    "context": "extension"
  }
}
```

---

### `host/hideStatus`

Hides a previously shown status message, identified by the `statusId` returned when it
was shown. The `message` object is included for compatibility with a host that still
matches on message text.

```json
{
  "jsonrpc": "2.0",
  "method": "host/hideStatus",
  "params": {
    "statusId": "status-1",
    "message": {
      "Message": "Loading data...",
      "State": 0
    }
  }
}
```

---

### `host/copyText`

Copies text to the system clipboard (since Node.js doesn't have clipboard access).

```json
{
  "jsonrpc": "2.0",
  "method": "host/copyText",
  "params": {
    "text": "Text to copy to clipboard"
  }
}
```

---

## Protocol Summary Table

| Method | Direction | Type | Purpose |
|--------|-----------|------|---------|
| `initialize` | Host → Ext | Request | Initialize extension |
| `provider/getTopLevelCommands` | Host → Ext | Request | Get top-level commands |
| `provider/getFallbackCommands` | Host → Ext | Request | Get fallback commands |
| `provider/getCommand` | Host → Ext | Request | Get command by ID |
| `provider/getSettings` | Host → Ext | Request | Get settings page |
| `command/invoke` | Host → Ext | Request | Invoke a command |
| `listPage/getItems` | Host → Ext | Request | Get list page items |
| `listPage/setSearchText` | Host → Ext | Request | Update search query |
| `listPage/setFilter` | Host → Ext | Request | Update active filter |
| `listPage/loadMore` | Host → Ext | Request | Load more items |
| `fallback/updateQuery` | Host → Ext | Notification or request | Update fallback query |
| `contentPage/getContent` | Host → Ext | Request | Get content page content |
| `form/submit` | Host → Ext | Request | Submit form data |
| `dispose` | Host → Ext | Notification | Clean up before exit |
| `provider/itemsChanged` | Ext → Host | Notification | Provider items have changed |
| `listPage/itemsChanged` | Ext → Host | Notification | Items have changed |
| `command/propChanged` | Ext → Host | Notification | Command props changed |
| `host/logMessage` | Ext → Host | Notification | Log message |
| `host/showStatus` | Ext → Host | Notification | Show status bar message |
| `host/hideStatus` | Ext → Host | Notification | Hide status bar message |
| `host/copyText` | Ext → Host | Notification | Copy text to clipboard |
