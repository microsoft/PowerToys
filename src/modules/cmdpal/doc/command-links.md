# Command Palette links

> [!NOTE]
> Work in progress. This document is a draft and may change without notice.

> [!WARNING]
> This was proof-read and reformated by Clanker. It might have sneak some stupid mistakes in.

<!-- TOC-->
  - [1 Scope](#1-scope)
  - [2 Routes](#2-routes)
  - [3 Parsing](#3-parsing)
    - [3.1 Arguments and page options](#31-arguments-and-page-options)
  - [4 Activation pipeline](#4-activation-pipeline)
  - [5 Configuration and permissions](#5-configuration-and-permissions)
  - [6 Security and risks](#6-security-and-risks)
  - [7 To do](#7-to-do)
<!-- TOC -->

Command Palette registers the `x-cmdpal` URI scheme. We can extend it to support routes that
allow activation of Command Palette commands and pages.

URI activation makes links copyable and invocable from browsers, documentation, scripts,
and other applications through the Windows activation model.

## 1 Scope

Initial scope:
- fixed built-in routes,
- consent-gated reload,
- top-level command invocation.

What is not supported yet:
- nested command traversal,
- parameter-page values -- parameters currently lack stable IDs.

Top-level commands are the initial boundary because they already have global provider and
command identities. Nested and contextual commands may be dynamic or lack durable IDs.

CmdPal generates **Copy command link** only for top-level items that are not fallback or
dock items and have IDs accepted by the protocol. Generated links identify the command;
page query and filter state must be added explicitly.

## 2 Routes

| URI                                              | Authorization | Action                                              |
| ------------------------------------------------ | ------------- | --------------------------------------------------- |
| `x-cmdpal://background`                          | Built-in      | Start without showing a window.                     |
| `x-cmdpal://settings`                            | Built-in      | Open Settings.                                      |
| `x-cmdpal://extensions/gallery`                  | Built-in      | Open the extension gallery.                         |
| `x-cmdpal://extensions/gallery/{extension-id}`   | Built-in      | Open extension details.                             |
| `x-cmdpal://reload`                              | Consent       | Reload extensions.                                  |
| `x-cmdpal://commands/{provider-id}/{command-id}` | Consent       | Resolve and execute a registered top-level command. |

The built-in allowlist is intentionally small. Reviewed routes such as Settings and the
extension gallery navigate within CmdPal without executing extension commands. Reload and
command routes require consent because they execute behavior or change extension state on
behalf of an external caller. New routes must be explicitly classified.

## 3 Parsing

- Require an absolute `x-cmdpal` URI with no user info, fragment, non-default port,
  empty host, or repeated path separator.
- Match route literals case-insensitively.
- Split the escaped path before decoding. Each ID occupies one segment.
- Reject decoded `/`, `\`, control characters, and leading or trailing whitespace.
- Maximum decoded ID lengths: extension 256, provider 256, command 512.
- Reject unknown routes and incorrect segment counts.
- Treat query parameters as route-specific. Command routes accept only the parameters
  defined below; built-in routes currently ignore the query string.

Parsing produces typed routes so parsing and policy stay centralized and auditable. Strict
validation prevents malformed or partially understood input from reaching execution.

The protocol is currently unversioned. Existing route meanings are therefore a compatibility
surface; new behavior should use new route shapes or route-specific parameters instead of
reinterpreting an existing route.

### 3.1 Arguments and page options

```
x-cmdpal://commands/{provider-id}/{command-id}?filter=running&query=ssh
```

| Name     | Contract                                                         |
| -------- | ---------------------------------------------------------------- |
| `query`  | Nonblank search text; maximum 1,024 decoded characters.          |
| `filter` | Exact, case-sensitive filter ID; maximum 256 decoded characters. |

- Reject unknown or duplicate names, empty values, control characters, and queries over
  16 KiB encoded.
- Apply options before the first item fetch so the linked page does not briefly fetch or
  show the wrong state (content page doesn't have a search text).
- If the filter no longer exists, fail closed: show a page error and do not fetch
  unfiltered results. (???)
- Links containing `filter` cannot be remembered.

A filter selects provider-defined state, is not part of the permission key, and may
materially change page behavior; links containing a filter are therefore one-time. Query
text uses the existing search surface and may use a persistent grant.

## 4 Activation pipeline

1. Queue consent-gated routes; cap the queue at 16, coalesce identical pending routes, and serialize dialogs.
1. Resolve commands by provider and command ID. If top-level loading is active, wait for
   that phase for at most 15 seconds, then perform one lookup. Do not await late providers.
1. Require `IPage` or `IInvokableCommand` and validate page options before navigation.
1. Authorize with a remembered permission or the consent dialog.
1. Before dispatch, re-resolve and revalidate command, provider, package identity,
   command shape, and page options.

Queue limits, duplicate coalescing, serialized dialogs, and waiting only for the current
loading phase keep activation work bounded and connected to the user's initiating action.
Commands are re-resolved because a provider can reload or replace command wrappers while
the consent dialog is open; authorization must apply to the object that will execute.

Activation is fire-and-forget; the caller receives no execution result. The queue is FIFO,
exact duplicates are coalesced only while pending, and the same link can run again after the
previous request completes. Queue overflow and consent-gated routes received while the
feature is disabled are dropped without user feedback.

Malformed or unknown protocol activations are not dispatched. They currently fall through
the normal launch path and summon CmdPal. An unavailable command or invalid page option
instead summons CmdPal and shows an error dialog.

Consent and error dialogs are serialized and default to **Cancel**. Page links use normal
forward navigation and preserve the existing back stack. Consent, errors, and page links
raise the window; a remembered reload or invokable command can run without raising it unless
the command presents its own confirmation.

## 5 Configuration and permissions

- **Authorization:** Each non-built-in route requires one-time consent or a matching
  remembered permission.
- **Defaults:** External command links are enabled by default so links work without prior
  setup. User is protected by the consent dialog, so this is not a risk.
- **Setting:** **Settings > General > External command links**. Disabling it blocks reload
  and command routes and hides the **Copy command link** context-menu action; built-in
  routes remain enabled. Stored permissions are retained and become active again when the
  setting is re-enabled.
- **Consent choices:** Execute once, always allow, or cancel. Cancel is the default action.
  Routes containing `filter` are always one-time.
- **Permission key:** Operation kind, package family name, provider ID, and command ID. This
  reduces accidental grant reuse across extensions or operations. Permissions authorize a
  target command rather than a caller because protocol activation has no trustworthy caller
  identity.
- **Lifetime:** Remembered permissions do not expire. A key change or explicit revocation is
  required to invalidate one; an extension update can retain the same key.
- **Revocation:** The Settings UI can remove one permission or clear all permissions after
  confirmation.
- **Persistence failures:** Corrupt or unreadable storage is treated as an empty permission
  set. If **Always allow** cannot be saved, the current request is allowed once and no grant
  is retained.
- **Storage:** Permissions are stored in the user profile and protected with Windows DPAPI
  scoped to a user. Apps running as the same user can read and modify the permission file,
  but at least other users cannot modify it. At-rest protection is abstracted so stronger
  integrity protection can replace DPAPI later.
- **Migration:** The retired `AllowExternalReload` value is not carried forward. External
  command links default on because reload now requires consent or a remembered permission.

## 6 Security and risks

- **No caller identity:** Any website or local process can invoke the protocol. Windows
  protocol activation does not provide a trustworthy origin, so permissions cannot be
  scoped to the caller. Consent and the target permission key are the authorization boundary.
- **Remembered execution:** A remembered command runs without another prompt. An extension
  update can change behavior while retaining the same permission key, and permissions have
  no automatic expiry.
- **Missing package identity:** Providers without a package family name use an empty value
  in the permission key, leaving provider and command IDs as the target identity.
- **Background execution:** A remembered reload or invokable command can run without raising
  CmdPal. This is a consequence of the user's persistent consent.
- **Prompt spam:** Queue limits, coalescing, and serialization reduce disruption but are
  UX controls, not security boundaries.
- **Same-user tampering:** DPAPI does not prevent another process running as the user from
  replacing the permission file with a valid protected payload.
- **No enterprise policy:** A same-user process can modify the user setting or permission
  data outside CmdPal.
- **Permission elevation:** What if CmdPal is running elevated, can a bad actor abuse this to
  invoke a command as elevated?

## 7 To do

- **Parameter pages:** Add stable IDs, nested command traversal, and define `argN` encoding,
  limits, and permission identity.
- **Package identity:** Decide whether **Always allow** should be available when a provider
  has no package family name.
- **Protocol evolution:** Decide whether a version is needed and whether built-in routes
  should continue to ignore, reserve, or reject query parameters.
- **Command IDs**:
    - we need stable command ids;
    - we need nice command ids - they are now visible in the links.
- **Context menu item:** Adds yet another item to the context menu, which is already crowded.

---

See [Command Palette Extension Gallery](extension-gallery.md) for gallery feed and extension-ID details.
