# ColorPicker Overlay Sizing Design

## Context

The WinUI ColorPicker overlay emulates WPF `SizeToContent` by measuring its
content and caching the resulting window size. The cache is currently
invalidated through `WindowEx.Content.SizeChanged`.

`WindowEx.Content` is its internal root grid. That grid remains arranged to the
current HWND size when bound tooltip text changes, so its `SizeChanged` event
does not report the child's new desired size. As a result, the overlay keeps its
initial dimensions and can clip longer color representations or a newly visible
color-name row.

## Goals

- Resize the overlay in both directions when its desired content size changes.
- Cover width changes from color representation text.
- Cover height and width changes from the optional color-name row.
- Preserve mixed-DPI cursor positioning.
- Keep measurement out of the mouse-position polling path.
- Coalesce multiple related property changes into one layout measurement.

## Non-goals

- Change the overlay visual design, animation, offsets, or z-order behavior.
- Change ColorPicker sampling cadence or screen-capture behavior.
- Refactor unrelated ViewModel, editor, style, or application lifecycle code.

## Design

### Content invalidation signal

`ColorPickerView` will expose a `DesiredSizeInvalidated` event. It already owns
the ViewModel property subscription used for accessibility, so it is the
appropriate boundary for translating content-semantic changes into a
view-sizing signal.

The view will request invalidation:

- after it is loaded;
- after `ColorName` changes, which follows `ColorText` in
  `MainViewModel.SetColorDetails`;
- after `ShowColorName` changes.

Using the `ColorName` notification avoids two resize requests for each sampled
color while still observing the completed `ColorText` and `ColorName` update.

### Coalesced resize request

`ColorPickerOverlayWindow` will subscribe to `DesiredSizeInvalidated` during
`InitializeCursorFollow`. The existing `Content.SizeChanged` subscription will
be removed.

The window will use a pending flag and a low-priority `DispatcherQueue`
callback to coalesce requests. Deferring the callback allows XAML bindings,
visual states, and layout invalidation to settle before measurement. If queueing
fails because the dispatcher is shutting down, the pending flag will be reset
without logging in this UI hot path.

### Measurement and positioning

The deferred callback will call the existing cursor-positioning path with
resizing enabled. The measurement will target `ColorPickerViewControl`
directly, using unconstrained logical size, then convert the desired width and
height to physical pixels using the target display's DPI scale.

The calculated size replaces the cached width and height even when it is
smaller than the previous value. Existing edge flipping,
`FlyoutWindowHelper.MoveAndResizeOnDisplay`, and topmost reassertion remain
unchanged.

Mouse-position notifications continue to call the positioning path with
resizing disabled, so cursor-follow polling performs no layout measurement.

## Testing

- Add unit tests for the property-name classification that drives
  `DesiredSizeInvalidated`, covering `ColorName`, `ShowColorName`, and unrelated
  ViewModel properties.
- Add a focused test for the resize-request coalescing logic, including queue
  rejection and reset after the queued callback executes.
- Run the complete ColorPicker unit-test project.
- Perform a deterministic current-bits UI regression check with fixed screen
  colors:
  - short RGB value to long RGB value grows the window and shows the full text;
  - long RGB value to short RGB value shrinks the window;
  - enabling the color-name row adjusts height and renders the complete row.
- Confirm mixed-DPI positioning code remains unchanged and the overlay still
  follows the cursor without activation.

## Acceptance Criteria

- No tooltip text is clipped after a content change.
- The overlay grows and shrinks to the current desired content size.
- A color-name visibility change updates the window height.
- At most one queued measurement is pending at a time.
- Mouse-position-only updates do not measure content.
- Existing unit tests pass and the deterministic UI regression check passes on
  the local Release build.
