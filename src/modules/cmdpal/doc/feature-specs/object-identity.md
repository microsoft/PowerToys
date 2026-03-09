---
author: OpenAI Codex
created on: 2026-03-09
last updated: 2026-03-09
issue id: n/a
---

# Instance Identity for SDK Objects

## Status

Draft proposal.

## Summary

Command Palette currently often treats object reference equality as identity.
That is not reliable for dynamic lists, fallback results, or any provider that
recreates objects while still referring to the same logical item.

Across COM / WinRT boundaries, the host often sees proxies or rematerialized
wrappers instead of the original object instance. That means default equality is
too weak to drive reuse in hot paths.

The result is avoidable work:

- unnecessary wrapper and view model recreation
- extra list churn during refresh
- repeated icon/detail rebinding
- more UI notifications than needed
- visible flicker and reduced perceived responsiveness while typing

This proposal is also intentionally low-risk from a compatibility perspective:

- the contract is optional
- existing extensions continue to work unchanged
- the host can keep using synthesized identity when explicit instance identity
  is missing
- extensions can opt in gradually where the performance benefit is worth it

This proposal adds an optional SDK contract:

```csharp
interface IObjectWithInstanceIdentity
{
    String InstanceIdentity { get; };
}
```

The purpose is to let the host recognize when two different object instances
represent the same logical item.

## Problem

Default object equality is not enough for SDK objects crossing COM / WinRT
boundaries.

In practice, the host often receives proxies or freshly materialized wrappers,
not the original in-proc object instance. Two host-side objects may represent
the same remote logical item while still failing reference equality. Likewise, a
provider may recreate an item and return a new proxy for the same logical
result.

Without explicit instance identity:

- list refreshes can recreate view models unnecessarily
- fallback results can look like full replacements instead of updates
- icon/detail updates can cause avoidable churn and flicker
- selection and focus are harder to preserve
- the host needs heuristics to guess whether two objects are "the same"

This is mostly a performance and responsiveness problem.

## Proposal

Add an optional interface:

```csharp
interface IObjectWithInstanceIdentity
{
    String InstanceIdentity { get; };
}
```

Toolkit base classes may implement this with a default empty value so
extensions can opt in without extra plumbing.

The host should prefer explicit instance identity when available and only fall
back to synthesized keys when it is missing.

## Semantics

`InstanceIdentity` means:

> "This object represents the same logical item as another object with the same
> instance identity, within the same owning source."

It should be:

- stable while the logical item remains the same
- unique among sibling items from the same source
- cheap to compute
- independent from query text, rank, or current visual state

It is not:

- a command id
- a provider id
- a fallback `QueryId`
- a security boundary
- a globally unique system identifier

## Host Behavior

The host should treat instance identity as source-scoped, never global.

Examples:

- provider id + instance identity
- fallback source id + instance identity
- page id + instance identity

Collisions between different providers or extensions are therefore acceptable as
long as the owning source is different. Two different providers may both return
`InstanceIdentity = "settings"` or `InstanceIdentity = "readme"`, and the host
should still treat those as different logical items because their source scope
is different.

Within a single source, duplicate instance identities should be treated as a
provider bug. The host should tolerate that safely, for example by logging the
collision and falling back to a disambiguated synthetic key for that refresh,
rather than assuming the items are interchangeable.

If two items from the same source have the same `InstanceIdentity`, the host
should prefer to treat them as the same logical item for:

- wrapper reuse
- view model reuse
- preserving selection/focus
- in-place icon/detail updates
- minimizing UI churn

If the item does not implement `IObjectWithInstanceIdentity`, the host may
synthesize a best-effort key from other stable properties. That should remain a
compatibility fallback, not the preferred path.

## Extension Guidance

Extensions should provide `InstanceIdentity` whenever the host may see multiple
instances of the same logical item across time.

Good candidates:

- dynamic list items
- fallback results
- search results
- saved entities
- items with asynchronous property updates

Good examples:

```csharp
item.InstanceIdentity = canonicalPath;
item.InstanceIdentity = savedConnection.Id;
item.InstanceIdentity = "search-web";
```

Bad examples:

```csharp
item.InstanceIdentity = query;
item.InstanceIdentity = $"{query}:{DateTime.Now.Ticks}";
item.InstanceIdentity = $"{title}:{rank}";
```

The same logical item should keep the same instance identity even if its title,
subtitle, icon, rank, or current query changes.

## Compatibility

This proposal is additive.

- existing extensions continue to work unchanged
- the host can keep synthesizing fallback identities when needed
- extensions can opt in gradually

## Open Questions

- Should more SDK types expose instance identity explicitly?
- Should host-side list/view-model caches eventually be identity-aware by
  default?
- Should some result types strongly recommend instance identity, even if it
  remains optional?

## Recommendation

Add `IObjectWithInstanceIdentity` as an optional SDK contract.

Extensions should use it for dynamic or frequently recreated items. The host
should use it as the preferred basis for reuse and incremental updates.

This is a small API addition with meaningful impact on performance, visual
stability, and maintainability.
