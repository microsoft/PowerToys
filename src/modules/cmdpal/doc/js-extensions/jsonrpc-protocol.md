# Command Palette JSONRPC Extension Protocol

This document defines the JSONRPC 2.0 protocol used between the Command Palette host (C#) and the Node.js extension host process.

## Transport

- **Transport:** stdio (stdin/stdout)
- **Framing:** Header-delimited (Content-Length), matching the LSP/DAP convention
- **Direction:** Bidirectional — both host and extensions can send requests and notifications

## Lifecycle

1. CmdPal spawns the Node host process
2. Host sends `initialize` request with configuration
3. Node host responds with capabilities
4. Host sends `extensions/load` with list of extensions to activate
5. Node host loads each extension, responds with success/failure per extension
6. Normal operation: host sends requests, extensions respond; extensions send notifications

## Conventions

- All method names use `/` as namespace separator
- Request IDs are integers, auto-incremented by the sender
- Extension-specific requests include an `extensionId` field to route to the correct extension
- All property values use camelCase JSON naming

---

## Host → Node Requests

### `initialize`

Sent once after process spawn to configure the Node host.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "hostVersion": "0.100.0",
    "extensionsDirectory": "C:\\Users\\...\\extensions",
    "logsDirectory": "C:\\Users\\...\\logs"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "1.0.0",
    "hostReady": true
  }
}
```

### `extensions/load`

Instructs the Node host to load a set of extensions.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "extensions/load",
  "params": {
    "extensions": [
      {
        "id": "cooldev.weather",
        "packageName": "@cooldev/weather-extension",
        "entryPoint": "dist/index.js",
        "directory": "C:\\Users\\...\\extensions\\@cooldev-weather-extension"
      }
    ]
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "loaded": [
      { "id": "cooldev.weather", "success": true }
    ],
    "failed": []
  }
}
```

### `extensions/unload`

Instructs the Node host to unload a specific extension.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "extensions/unload",
  "params": {
    "extensionId": "cooldev.weather"
  }
}
```

### `provider/getTopLevelCommands`

Maps to `ICommandProvider.TopLevelCommands()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "provider/getTopLevelCommands",
  "params": {
    "extensionId": "cooldev.weather"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "items": [
      {
        "command": {
          "id": "weather-now",
          "name": "Current Weather",
          "icon": { "light": { "icon": "\uE9CA" }, "dark": { "icon": "\uE9CA" } }
        },
        "title": "Current Weather",
        "subtitle": "Get current weather for your location",
        "icon": { "light": { "icon": "\uE9CA" }, "dark": { "icon": "\uE9CA" } }
      }
    ]
  }
}
```

### `provider/getFallbackCommands`

Maps to `ICommandProvider.FallbackCommands()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "provider/getFallbackCommands",
  "params": {
    "extensionId": "cooldev.weather"
  }
}
```

**Response:** Same shape as `provider/getTopLevelCommands` but items include `fallbackHandler` field.

### `provider/getSettings`

Maps to `ICommandProvider.Settings`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "provider/getSettings",
  "params": {
    "extensionId": "cooldev.weather"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "settingsPage": {
      "id": "weather-settings",
      "name": "Weather Settings",
      "title": "Weather Settings",
      "content": [...]
    }
  }
}
```

### `command/invoke`

Maps to `IInvokableCommand.Invoke()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "method": "command/invoke",
  "params": {
    "extensionId": "cooldev.weather",
    "commandId": "weather-now"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": {
    "kind": "goToPage",
    "args": {
      "pageId": "weather-detail",
      "navigationMode": "push"
    }
  }
}
```

### `page/getItems`

Maps to `IListPage.GetItems()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "page/getItems",
  "params": {
    "extensionId": "cooldev.weather",
    "pageId": "weather-list"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": {
    "items": [
      {
        "command": { "id": "city-nyc", "name": "New York" },
        "title": "New York",
        "subtitle": "72°F, Sunny",
        "tags": [{ "text": "Favorite", "icon": null }],
        "section": "Favorites"
      }
    ]
  }
}
```

### `page/setSearchText`

Maps to `IDynamicListPage.SearchText { set }`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 9,
  "method": "page/setSearchText",
  "params": {
    "extensionId": "cooldev.weather",
    "pageId": "weather-list",
    "searchText": "new york"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 9,
  "result": { "accepted": true }
}
```

### `page/loadMore`

Maps to `IListPage.LoadMore()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 10,
  "method": "page/loadMore",
  "params": {
    "extensionId": "cooldev.weather",
    "pageId": "weather-list"
  }
}
```

### `page/getContent`

Maps to `IContentPage.GetContent()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 11,
  "method": "page/getContent",
  "params": {
    "extensionId": "cooldev.weather",
    "pageId": "weather-detail"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 11,
  "result": {
    "content": [
      { "type": "markdown", "body": "## Current Weather\n\n72°F and sunny" },
      { "type": "image", "image": { "light": { "icon": "https://..." } }, "maxWidth": 200, "maxHeight": 200 }
    ]
  }
}
```

### `page/getProperties`

Gets page-level properties (title, isLoading, placeholderText, showDetails, filters, gridProperties, etc.).

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 12,
  "method": "page/getProperties",
  "params": {
    "extensionId": "cooldev.weather",
    "pageId": "weather-list"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 12,
  "result": {
    "title": "Weather",
    "isLoading": false,
    "placeholderText": "Search cities...",
    "showDetails": true,
    "hasMoreItems": false,
    "filters": {
      "currentFilterId": "all",
      "filters": [
        { "id": "all", "name": "All" },
        { "id": "favorites", "name": "Favorites" }
      ]
    }
  }
}
```

### `form/submit`

Maps to `IFormContent.SubmitForm()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 13,
  "method": "form/submit",
  "params": {
    "extensionId": "cooldev.weather",
    "pageId": "settings-form",
    "inputs": "{ \"apiKey\": \"abc123\" }",
    "data": "{ \"location\": \"NYC\" }"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 13,
  "result": {
    "kind": "dismiss"
  }
}
```

### `fallback/updateQuery`

Maps to `IFallbackHandler.UpdateQuery()`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 14,
  "method": "fallback/updateQuery",
  "params": {
    "extensionId": "cooldev.weather",
    "commandId": "weather-fallback",
    "query": "weather in paris"
  }
}
```

### `provider/getCommand`

Maps to `ICommandProvider.GetCommand(id)`.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 15,
  "method": "provider/getCommand",
  "params": {
    "extensionId": "cooldev.weather",
    "commandId": "weather-now"
  }
}
```

---

## Node → Host Notifications

These are fire-and-forget messages from extensions to the host. They are emitted by
the SDK runtime (`ts-sdk/src/runtime/stdio-server.ts`) and handled by the host proxies
(`JSListPageProxy`, `JSCommandProviderProxy`).

### `listPage/itemsChanged`

A dynamic list page signals that its items have changed. The host re-fetches the page's items.

```json
{
  "jsonrpc": "2.0",
  "method": "listPage/itemsChanged",
  "params": {
    "pageId": "weather-list"
  }
}
```

### `command/propChanged`

A command signals that one or more of its observable properties have changed. The host
applies the supplied properties without re-fetching the whole command.

```json
{
  "jsonrpc": "2.0",
  "method": "command/propChanged",
  "params": {
    "commandId": "weather-now",
    "properties": {
      "displayTitle": "Weather - 5 cities"
    }
  }
}
```

### `host/logMessage`

Extension sends a log message to the host log. Backs the SDK `ExtensionHost.log` API.

```json
{
  "jsonrpc": "2.0",
  "method": "host/logMessage",
  "params": {
    "message": "Fetched weather data for 5 cities",
    "state": 2
  }
}
```

`state` is a numeric severity: `0` = trace, `1` = debug, `2` = info, `3` = warning, `4` = error.

### `host/showStatus`

Extension requests a status message be shown. Backs the SDK `ExtensionHost.showStatus` API.

```json
{
  "jsonrpc": "2.0",
  "method": "host/showStatus",
  "params": {
    "message": { "Message": "Loading weather data...", "State": 2 },
    "context": "extension"
  }
}
```

`State` uses the same numeric severity scale as `host/logMessage`.

### `host/hideStatus`

Extension requests a previously shown status message be hidden. Backs the SDK
`ExtensionHost.hideStatus` API.

```json
{
  "jsonrpc": "2.0",
  "method": "host/hideStatus",
  "params": {
    "message": { "Message": "Loading weather data...", "State": 2 }
  }
}
```

### `host/copyText`

Extension requests text be copied to the clipboard. Backs the SDK
`ExtensionHost.copyToClipboard` API.

```json
{
  "jsonrpc": "2.0",
  "method": "host/copyText",
  "params": {
    "text": "72F and sunny"
  }
}
```

> Note: the host additionally registers handlers for provider-level variants
> (`provider/itemsChanged`, `provider/propChanged`, `page/propChanged`,
> `content/propChanged`) in `JSCommandProviderProxy`. These are reserved for future use
> and are not currently emitted by the SDK runtime.

---

## Data Types

### IconInfo
```json
{
  "light": { "icon": "<glyph or URL>", "data": null },
  "dark": { "icon": "<glyph or URL>", "data": null }
}
```

### CommandResultKind (enum string)
`"dismiss"` | `"goHome"` | `"goBack"` | `"hide"` | `"keepOpen"` | `"goToPage"` | `"showToast"` | `"confirm"`

### NavigationMode (enum string)
`"push"` | `"goBack"` | `"goHome"`

### MessageState (enum string)
`"info"` | `"success"` | `"warning"` | `"error"`

### StatusContext (enum string)
`"page"` | `"extension"`

### ContentType (discriminator for content array items)
`"markdown"` | `"form"` | `"tree"` | `"plainText"` | `"image"`

### Tag
```json
{
  "icon": <IconInfo | null>,
  "text": "string",
  "foreground": { "hasValue": true, "color": { "r": 255, "g": 0, "b": 0, "a": 255 } },
  "background": null,
  "toolTip": "string"
}
```

### Details
```json
{
  "heroImage": <IconInfo | null>,
  "title": "string",
  "body": "string",
  "metadata": [
    { "key": "Author", "data": { "type": "tags", "tags": [...] } },
    { "key": "Link", "data": { "type": "link", "link": "https://...", "text": "Homepage" } }
  ]
}
```

### Filter
```json
{
  "id": "string",
  "name": "string",
  "icon": <IconInfo | null>
}
```

### GridProperties
```json
{
  "type": "small" | "medium" | "gallery",
  "showTitle": true,
  "showSubtitle": true
}
```

---

## Error Handling

JSONRPC errors use standard error codes:
- `-32700`: Parse error
- `-32600`: Invalid request
- `-32601`: Method not found
- `-32602`: Invalid params
- `-32603`: Internal error

Custom error codes (extension-specific):
- `-32000`: Extension not found
- `-32001`: Extension not loaded
- `-32002`: Command not found
- `-32003`: Page not found
- `-32004`: Extension threw an exception (details in `data` field)

```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "error": {
    "code": -32004,
    "message": "Extension threw an exception",
    "data": {
      "extensionId": "cooldev.weather",
      "stack": "Error: API key invalid\n    at ..."
    }
  }
}
```
