# Command Palette incremental Adaptive Card rendering

This project is an independent hard copy of the XAML-neutral ordered-tree planner originally
prototyped in RunUX. There is no source or binary dependency on the Windows repository.

The library deliberately contains no WinUI or Adaptive Cards renderer objects. Command Palette's
WinUI adapter creates immutable snapshots, asks this library for a conservative plan, and either
patches allowlisted properties or atomically replaces the rendered card root.

The initial allowlist is authored `TextBlock.text` plus the content of inline `data:image/svg+xml`
images. SVG updates are decoded before the retained `Image.Source` is changed, so the old pixels
remain visible until the replacement is ready. Every other Adaptive Card semantic remains in the
replacement-sensitive fingerprint. This keeps form inputs and action handlers on their current
`RenderedAdaptiveCard` lease while falling back for structural, interactive, external-resource, or
unknown changes.

The WinUI adapter gets each authored image URL from the Adaptive Cards renderer's
`ElementTagContent.CardElement` metadata. It does not use `SvgImageSource.UriSource`: the stock
renderer loads data-URI SVGs through `SetSourceAsync`, so `UriSource` is normally empty even after a
successful render. Inline-SVG fingerprint suppression is enabled only when every authored SVG maps
to exactly one rendered `Image`; incomplete or ambiguous mappings use atomic root replacement.
Image targets are resolved from that metadata on every commit instead of being cached by managed
WinRT-wrapper identity. SVG decode is time-bounded, and text patches commit independently before
image decoding, so an image failure cannot stall subsequent live card updates.

Live updates use a single-flight queue. The active render/decode always completes. While it is
active, incoming updates replace one pending slot, so the renderer commits the active update and
then the newest available update without accumulating work or starving under a fast producer.