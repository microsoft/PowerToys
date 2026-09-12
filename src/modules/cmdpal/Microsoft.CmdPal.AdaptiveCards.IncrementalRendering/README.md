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

## Update behavior

The updater changes plain text and inline SVG images in place.
Markdown, actions, inputs, layout changes, and unknown changes replace the complete card.

Changed SVG images load concurrently.
The updater commits each image as soon as its SVG decode is complete.
A three-second timeout is the maximum wait for the complete image group.

The updater finishes the active update without cancellation.
While that update runs, one pending slot keeps only the newest card.
When the active update finishes, the updater processes the pending card.

The updater validates all changes before it changes the visible tree.
If validation fails, the updater uses the rendered candidate as the new root.
