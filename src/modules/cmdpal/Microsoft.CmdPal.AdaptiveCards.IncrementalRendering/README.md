# Command Palette incremental Adaptive Card rendering

`IncrementalAdaptiveCardUpdater` renders an Adaptive Card into a stable `Border`.
Later calls update safe properties without replacing the visible form.
Unsupported changes replace the complete card.

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
A three-second timeout prevents an old image load from blocking the next update.
A new update cancels the active update.

The updater validates all changes before it changes the visible tree.
If validation fails, the updater uses the rendered candidate as the new root.
