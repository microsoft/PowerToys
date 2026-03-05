# JSON-RPC 2.0 Protocol Specification

## JavaScript Extension Service Protocol

This document defines the JSON-RPC 2.0 communication protocol between the C# host (CmdPal) and Node.js extension processes. The protocol uses JSON-RPC 2.0 with LSP-style length-prefixed framing over stdin/stdout.

---

## 1. Transport Layer

Communication uses length-prefixed JSON over standard input/output streams, following the Language Server Protocol (LSP) convention.

### Message Format

Each message consists of a header followed by a blank line and the JSON payload:

```
Content-Length: {byte_count}\r\n
\r\n
{json_payload}
```

**Requirements:**
- `byte_count` is the number of UTF-8 bytes in the JSON payload
- Headers are case-insensitive (e.g., `content-length` is valid)
- A blank line (`\r\n`) separates headers from payload
- JSON payload is not followed by a newline

### Example

```
Content-Length: 145\r\n
\r\n
{"jsonrpc":"2.0","method":"initialize","params":{"hostVersion":"1.0.0","capabilities":{}},"id":1}
```

---

## 2. Lifecycle Methods

### 2.1 `initialize`

**Direction:** Host → Extension (Request)

Initializes the extension. The host sends version information and capabilities; the extension responds with its capabilities.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "initialize",
  "params": {
    "hostVersion": "1.0.0",
    "capabilities": {
      "supportsImages": true,
      "supportsDetails": true
    }
  },
  "id": 1
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "capabilities": {
      "providesTopLevelCommands": true,
      "providesFallbackCommands": true,
      "providesCommandDetails": true,
      "supportsDynamicPages": true,
      "supportsContentPages": true,
      "supportsForms": true
    },
    "version": "1.0.0"
  },
  "id": 1
}
```

### 2.2 `dispose`

**Direction:** Host → Extension (Notification or Request)

Gracefully shuts down the extension. The host sends this before terminating the process.

**Request/Notification:**
```json
{
  "jsonrpc": "2.0",
  "method": "dispose",
  "id": 2
}
```

**Response (if request):**
```json
{
  "jsonrpc": "2.0",
  "result": {},
  "id": 2
}
```

---

## 3. Provider Methods

Provider methods allow the extension to expose commands and metadata. All are host → extension (request/response).

### 3.1 `provider/getTopLevelCommands`

Returns the top-level commands provided by this extension.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "provider/getTopLevelCommands",
  "id": 10
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": [
    {
      "id": "cmd-1",
      "displayName": "Search Files",
      "description": "Search for files in workspace",
      "icon": {
        "light": { "icon": "search.png" },
        "dark": { "icon": "search-dark.png" }
      }
    }
  ],
  "id": 10
}
```

### 3.2 `provider/getFallbackCommands`

Returns fallback commands that execute when no other command matches the user's input.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "provider/getFallbackCommands",
  "id": 11
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": [
    {
      "id": "fallback-1",
      "displayName": "Open with App",
      "description": "Opens the query text with an external application",
      "icon": null
    }
  ],
  "id": 11
}
```

### 3.3 `provider/getCommand`

Retrieves a specific command by ID. Includes full details and may reference a page.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "provider/getCommand",
  "params": {
    "commandId": "cmd-1"
  },
  "id": 12
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "id": "cmd-1",
    "displayName": "Search Files",
    "description": "Search for files in workspace",
    "icon": {
      "light": { "icon": "search.png" },
      "dark": { "icon": "search-dark.png" }
    },
    "pageId": "page-search-results",
    "tags": [
      {
        "text": "workspace",
        "foreground": "#FF0000",
        "background": "#00FF00"
      }
    ],
    "details": {
      "heroImage": {
        "light": { "data": "base64..." }
      },
      "title": "File Search",
      "body": "Search through all workspace files"
    }
  },
  "id": 12
}
```

### 3.4 `provider/getProperties`

Returns metadata about the provider (display name, icon, settings, etc.).

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "provider/getProperties",
  "id": 13
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "id": "my-extension",
    "displayName": "My Extension",
    "icon": {
      "light": { "icon": "logo.png" },
      "dark": { "icon": "logo-dark.png" }
    },
    "settings": [
      {
        "key": "enableFeature",
        "displayName": "Enable Advanced Features",
        "description": "Enable experimental features",
        "kind": 0
      }
    ],
    "frozen": false
  },
  "id": 13
}
```

---

## 4. Command Methods

### 4.1 `command/invoke`

Invokes a command and returns its result.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "command/invoke",
  "params": {
    "commandId": "cmd-1"
  },
  "id": 20
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "kind": 0,
    "args": null
  },
  "id": 20
}
```

Where `kind` is a CommandResultKind:
- `0` = Success
- `1` = GoToPage (includes `pageId`)
- `2` = Toast (includes `message`, `icon`, `dismissAfterMs`)
- `3` = Confirmation (includes dialog details)
- `4` = GoToUrl (includes `url`)

**Response with GoToPage:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "kind": 1,
    "args": {
      "pageId": "page-search-results"
    }
  },
  "id": 20
}
```

**Response with Toast:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "kind": 2,
    "args": {
      "message": "File copied to clipboard",
      "icon": null,
      "dismissAfterMs": 2000
    }
  },
  "id": 20
}
```

---

## 5. Page Methods

Pages provide dynamic lists, filters, and content. Pages are stateful on the extension side.

### 5.1 `listPage/getItems`

Retrieves items for a list page at a specific offset.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "listPage/getItems",
  "params": {
    "pageId": "page-search-results",
    "offset": 0,
    "limit": 50
  },
  "id": 30
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "items": [
      {
        "id": "item-1",
        "displayName": "SearchResults.ts",
        "description": "Search results component",
        "icon": {
          "light": { "icon": "file.png" }
        },
        "tags": [
          {
            "text": "TypeScript",
            "foreground": "#3178C6"
          }
        ],
        "details": null,
        "section": "Recent",
        "textToSuggest": "SearchResults"
      }
    ],
    "totalItems": 145
  },
  "id": 30
}
```

### 5.2 `listPage/setSearchText`

Sets the search text on a dynamic list page.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "listPage/setSearchText",
  "params": {
    "pageId": "page-search-results",
    "searchText": "search"
  },
  "id": 31
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "updatedItemCount": 42
  },
  "id": 31
}
```

### 5.3 `listPage/loadMore`

Triggers LoadMore on a page that supports pagination.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "listPage/loadMore",
  "params": {
    "pageId": "page-search-results"
  },
  "id": 32
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "newItemCount": 25
  },
  "id": 32
}
```

### 5.4 `listPage/setFilter`

Sets the active filter on a list page.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "listPage/setFilter",
  "params": {
    "pageId": "page-search-results",
    "filterId": "filter-by-type"
  },
  "id": 33
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "updatedItemCount": 60
  },
  "id": 33
}
```

### 5.5 `contentPage/getContent`

Retrieves content for a content page.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "contentPage/getContent",
  "params": {
    "pageId": "page-details"
  },
  "id": 34
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "content": [
      {
        "kind": 0,
        "args": {
          "title": "File Details",
          "body": "This is the file content"
        }
      }
    ]
  },
  "id": 34
}
```

### 5.6 `form/submit`

Submits form data and returns a command result.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "form/submit",
  "params": {
    "pageId": "page-form",
    "data": {
      "name": "John Doe",
      "email": "john@example.com"
    }
  },
  "id": 35
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": {
    "kind": 2,
    "args": {
      "message": "Form submitted successfully",
      "icon": null,
      "dismissAfterMs": 2000
    }
  },
  "id": 35
}
```

---

## 6. Host Callbacks

Host callbacks are notifications sent from the extension to the host (ext → host).

### 6.1 `host/logMessage`

Logs a message to the host's logging system.

**Notification:**
```json
{
  "jsonrpc": "2.0",
  "method": "host/logMessage",
  "params": {
    "state": 1,
    "message": "Processing completed"
  }
}
```

Where `state` is a MessageState:
- `0` = Trace
- `1` = Debug
- `2` = Info
- `3` = Warning
- `4` = Error

### 6.2 `host/showStatus`

Shows a status message to the user.

**Notification:**
```json
{
  "jsonrpc": "2.0",
  "method": "host/showStatus",
  "params": {
    "messageId": "status-1",
    "state": 1,
    "message": "Loading...",
    "progress": 45,
    "context": "search"
  }
}
```

### 6.3 `host/hideStatus`

Hides a previously shown status message.

**Notification:**
```json
{
  "jsonrpc": "2.0",
  "method": "host/hideStatus",
  "params": {
    "messageId": "status-1"
  }
}
```

### 6.4 `provider/itemsChanged`

Notifies the host that the items in a provider or page have changed.

**Notification:**
```json
{
  "jsonrpc": "2.0",
  "method": "provider/itemsChanged",
  "params": {
    "pageId": "page-search-results",
    "totalItems": 150
  }
}
```

### 6.5 `provider/propChanged`

Notifies the host that a property of the provider has changed.

**Notification:**
```json
{
  "jsonrpc": "2.0",
  "method": "provider/propChanged",
  "params": {
    "propertyName": "displayName",
    "value": "Updated Extension Name"
  }
}
```

---

## 7. Type Mappings

This section defines how WinRT IDL types are represented in JSON.

### 7.1 IIconInfo

Icons with light and dark variants:

```json
{
  "light": {
    "icon": "path/to/light-icon.png",
    "data": "base64-encoded-image"
  },
  "dark": {
    "icon": "path/to/dark-icon.png",
    "data": "base64-encoded-image"
  }
}
```

Either `icon` (file path) or `data` (base64) may be provided. If neither is present, the field may be null or omitted.

### 7.2 ITag

Tags with optional styling:

```json
{
  "text": "TypeScript",
  "icon": {
    "light": { "icon": "ts.png" }
  },
  "foreground": "#3178C6",
  "background": "#F0F0F0",
  "toolTip": "TypeScript language"
}
```

All fields except `text` are optional.

### 7.3 OptionalColor

A color that may or may not be present:

```json
{
  "hasValue": true,
  "color": {
    "r": 255,
    "g": 128,
    "b": 0,
    "a": 255
  }
}
```

If `hasValue` is false, `color` may be omitted or null.

### 7.4 ICommandResult

The result of command invocation:

```json
{
  "kind": 1,
  "args": {
    "pageId": "page-results"
  }
}
```

`args` depends on `kind`:
- `0` (Success): `args` is null or omitted
- `1` (GoToPage): `args` = `{ pageId: string }`
- `2` (Toast): `args` = `{ message: string, icon?: IIconInfo, dismissAfterMs?: number }`
- `3` (Confirmation): `args` = confirmation dialog details
- `4` (GoToUrl): `args` = `{ url: string }`

### 7.5 KeyChord

Keyboard shortcut representation:

```json
{
  "modifiers": 2,
  "vkey": 70,
  "scanCode": 0
}
```

- `modifiers`: Bitfield (1=Ctrl, 2=Alt, 4=Shift, 8=Win)
- `vkey`: Virtual key code
- `scanCode`: Scan code (0 if not applicable)

### 7.6 IDetails

Detailed information about an item:

```json
{
  "heroImage": {
    "light": { "data": "base64..." }
  },
  "title": "Item Title",
  "body": "Description text",
  "metadata": [
    {
      "label": "Author",
      "value": "John Doe"
    }
  ]
}
```

All fields are optional.

### 7.7 IListItem

Extends ICommandItem with additional page-specific properties:

```json
{
  "id": "item-1",
  "displayName": "File Name",
  "description": "File path",
  "icon": {
    "light": { "icon": "file.png" }
  },
  "tags": [
    { "text": "Important" }
  ],
  "details": {
    "title": "Details",
    "body": "More info"
  },
  "section": "Recent Files",
  "textToSuggest": "FileName"
}
```

### 7.8 Enums

All enums are represented as numeric values matching the IDL definitions:

**CommandResultKind:**
- Success = 0
- GoToPage = 1
- Toast = 2
- Confirmation = 3
- GoToUrl = 4

**MessageState:**
- Trace = 0
- Debug = 1
- Info = 2
- Warning = 3
- Error = 4

**StatusState:**
- Loading = 0
- Success = 1
- Warning = 2
- Error = 3

---

## 8. Error Handling

### 8.1 JSON-RPC Error Codes

Standard JSON-RPC 2.0 error codes:

| Code | Message | Description |
|------|---------|-------------|
| -32700 | Parse error | Invalid JSON was received |
| -32600 | Invalid Request | The JSON sent is not a valid Request object |
| -32601 | Method not found | The method does not exist or is not available |
| -32602 | Invalid params | Invalid method parameter(s) |
| -32603 | Internal error | Internal JSON-RPC error |

### 8.2 Custom Error Codes

Extensions may use custom error codes in the range -32000 to -32099 for extension-specific failures:

| Code | Description |
|------|-------------|
| -32000 | Extension timeout |
| -32001 | Command not found |
| -32002 | Page not found |
| -32003 | Invalid state |

### 8.3 Error Response Example

```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32601,
    "message": "Method not found",
    "data": {
      "details": "The method 'provider/unknownMethod' is not implemented"
    }
  },
  "id": 42
}
```

### 8.4 Timeout Behavior

- Default request timeout: **10 seconds**
- If an extension does not respond within the timeout, the host treats it as a failure
- The host may retry or fall back to alternative behavior
- Extensions should be responsive and avoid long-running operations on the main thread

---

## 9. Object Identity & References

### 9.1 IDs and Stability

Pages, commands, and items must have stable IDs across requests:

- **Command IDs**: Must remain constant for the same logical command
- **Page IDs**: Must remain constant for the lifetime of the extension
- **Item IDs**: Must remain constant within a page session

### 9.2 ID Assignment

- The **extension** assigns all IDs
- The **host** uses IDs to reference objects in subsequent requests
- IDs are strings and should be unique within their scope

### 9.3 Example ID Lifecycle

```
Host calls provider/getTopLevelCommands
  → Extension returns [{ id: "cmd-search", ... }]
Host stores "cmd-search"

User selects command
Host calls command/invoke with commandId: "cmd-search"
  → Extension executes the command
  → Returns GoToPage with pageId: "page-results"

Host stores "page-results"

User interacts with page
Host calls listPage/getItems with pageId: "page-results"
  → Extension returns items with IDs like "item-1", "item-2", etc.
```

---

## 10. Session Management

### 10.1 Extension Lifetime

1. Host launches extension process
2. Host sends `initialize` request
3. Extension responds with capabilities
4. Host sends requests/notifications; extension sends notifications
5. Host sends `dispose` request when shutting down
6. Host terminates process

### 10.2 State Preservation

- Extensions should preserve state across requests within a session
- Pages and commands should maintain identity
- The host does not persist extension state between launches

### 10.3 Cleanup

When `dispose` is called:
- Extension should clean up resources
- Extension should not send further notifications
- Extension should exit cleanly within 5 seconds

---

## 11. Best Practices

### 11.1 Performance

- Keep response times under 1 second for typical requests
- Use pagination for large result sets (avoid returning all items at once)
- Cache data when appropriate to reduce latency

### 11.2 Reliability

- Implement proper error handling in all methods
- Return meaningful error messages to help diagnose issues
- Validate parameters and return appropriate error codes

### 11.3 User Experience

- Provide clear, concise descriptions for commands
- Use appropriate icons and formatting
- Report progress for long-running operations via `host/showStatus`
- Avoid blocking the UI thread with synchronous operations

### 11.4 Logging

- Use `host/logMessage` for debugging information
- Use appropriate message states (trace, debug, info, warning, error)
- Avoid excessive logging that may impact performance

---

## Example: Complete Flow

Here's a complete example of a search command workflow:

1. **Initialize:**
   ```json
   Host → Extension: { "jsonrpc": "2.0", "method": "initialize", ... }
   Extension → Host: { "jsonrpc": "2.0", "result": { "capabilities": { ... } }, ... }
   ```

2. **Get top-level commands:**
   ```json
   Host → Extension: { "jsonrpc": "2.0", "method": "provider/getTopLevelCommands", ... }
   Extension → Host: { "jsonrpc": "2.0", "result": [{ "id": "search", ... }], ... }
   ```

3. **User selects command:**
   ```json
   Host → Extension: { "jsonrpc": "2.0", "method": "command/invoke", "params": { "commandId": "search" }, ... }
   Extension → Host: { "jsonrpc": "2.0", "result": { "kind": 1, "args": { "pageId": "results" } }, ... }
   ```

4. **Get page items:**
   ```json
   Host → Extension: { "jsonrpc": "2.0", "method": "listPage/getItems", "params": { "pageId": "results", ... }, ... }
   Extension → Host: { "jsonrpc": "2.0", "result": { "items": [...], "totalItems": 42 }, ... }
   ```

5. **Dispose:**
   ```json
   Host → Extension: { "jsonrpc": "2.0", "method": "dispose", ... }
   Extension → Host: { "jsonrpc": "2.0", "result": {}, ... }
   ```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024 | Initial specification |
