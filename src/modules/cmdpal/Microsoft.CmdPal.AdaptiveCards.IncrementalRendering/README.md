# Incremental Adaptive Card rendering

> [!WARNING] 
> Here be basilisks!
>
> This entire library is maintained by LLMs. Humans didn't write the code, and no human actually owns this code. 
>
> This library was generated to experiment with the big picture problem "can we update the content of a rendered Adaptive Card without replacing the entire card?"
>
> I would fully suspect that this is not the correct approach to solve this problem. It is _an_ approach however, and it's one that works well enough to continue experimenting with.

`IncrementalAdaptiveCardUpdater` renders an Adaptive Card into a stable `Border`.
Later calls update safe properties without replacing the visible form.
Unsupported changes replace the complete card. This allows the card to quickly update its content without losing the user's input or focus, or flickering images.

## Use the updater

Create one updater for each card host:

```csharp
var updater = new IncrementalAdaptiveCardUpdater(renderer, cardHost);
```

Update the card from a template and its data:

```csharp
await updater.UpdateAsync(templateJson, dataJson);
```

You can also update the card from an `AdaptiveCard`:

```csharp
await updater.UpdateAsync(card);
```

The updater owns the current rendered card, its snapshot, cancellation, and replacement behavior.
Consumers do not create snapshots or use the diff engine.

Pass custom parser registrations to the constructor when the card uses custom elements or actions.
Configure custom element renderers on the supplied `AdaptiveCardRenderer`.

## Patch custom elements

A custom element can update in place instead of replacing the card.
Register its type with `IncrementalPatchableElements` and pass the registration to the constructor:

```csharp
var patchable = new IncrementalPatchableElements().Add("Chart.Line");
var updater = new IncrementalAdaptiveCardUpdater(renderer, cardHost, elementParsers, actionParsers, patchable);
```

The control that the element renderer returns must implement `IIncrementalAdaptiveElementControl`.
`IncrementalState` reports a deterministic snapshot of the patchable state.
The updater calls `CanApplyIncrementalState` for every changed control before it changes anything, then calls `ApplyIncrementalState`.

For a registered element, every property is patchable except the host-owned ones.
Host-owned properties are applied by the Adaptive Cards renderer, so changing them replaces the complete card.
They are `type`, `id`, `isVisible`, `separator`, `spacing`, `height`, `fallback`, `requires`, `targetWidth`, `horizontalAlignment`, `grid.area`, `lang`, and the action properties.
A registered element always renders itself, so unless it has `requires`, its `fallback` is never drawn: the updater ignores it, and a fallback such as a `TextBlock` with the element's value doesn't stop other text from updating in place.
Register only element types that the renderer renders.
The updater treats the control as a leaf, so the control can change its internal tree freely.

## Update behavior

The updater changes plain text, inline SVG images, and registered custom elements in place.
Markdown, actions, inputs, layout changes, and unknown changes replace the complete card.

Changed SVG images load concurrently.
The updater waits for every SVG decode, then applies all image and text changes together as one atomic group.
A three-second timeout is the maximum wait for the complete image group.
If an image fails to load or the group times out, the updater replaces the complete card.

The updater finishes the active update without cancellation.
While that update runs, one pending slot keeps only the newest card.
When the active update finishes, the updater processes the pending card.

The updater validates all changes before it changes the visible tree.
If validation fails, the updater uses the rendered candidate as the new root.
If JSON parsing or a COM operation fails during snapshot creation, the updater uses the rendered candidate without a snapshot.
The next update replaces the complete card before incremental updates can resume.
