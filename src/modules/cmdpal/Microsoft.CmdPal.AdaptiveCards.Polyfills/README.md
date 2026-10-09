# Adaptive Cards polyfills

The WinUI 3 Adaptive Cards renderer doesn't support everything in the current Adaptive Cards schema. This library fills the gaps until it does:

- The `Chart.Line`, `Chart.Gauge`, `Chart.Donut`, `Chart.Pie`, `Chart.VerticalBar`, `Chart.HorizontalBar`, `Chart.HorizontalBar.Stacked`, `ProgressBar`, `Badge`, and `Icon` elements.
- `roundedCorners` and `showBorder` on containers, column sets, and columns.

Cards stay standard Adaptive Cards JSON, and extensions give each element a `fallback` for other hosts. Nothing here is specific to Command Palette, so each piece can move to the open-source renderer and be deleted here (see [Upstream](#upstream)).

Extension authors: see `ExtensionTemplate\TemplateCmdPalExtension\.github\skills\add-adaptive-card-form\references\charts-and-visuals.md`, and the Samples extension's "Charts and visuals" page.

## Use it

`AdaptiveCardPolyfills` is the entry point. Command Palette's `ContentFormControl` calls it three times:

```csharp
AdaptiveCardPolyfills.RegisterParsers(AdaptiveCardParserRegistrations.ElementParsers);
AdaptiveCardPolyfills.RegisterRenderers(renderer, RS_.GetString);
_cardUpdater = new IncrementalAdaptiveCardUpdater(..., AdaptiveCardPolyfills.PatchableElements);
```

The host provides:

- **Strings.** The library has no resources of its own, so `RegisterRenderers` takes a lookup for the `AdaptiveChart_*` strings in Command Palette's `Resources.resw`: chart names and summaries for screen readers, and the suffixes of compact numbers such as 12.3K.
- **Container styles.** `roundedCorners` and `showBorder` apply the `CmdPal.Adaptive.Container.Rounded`, `CmdPal.Adaptive.Container.Bordered`, and `CmdPal.Adaptive.Container.RoundedBordered` styles from the renderer's override styles (`CardOverrideStyles` in `ContentFormControl.xaml`).

`AdaptiveCustomElementJson` is public too: Command Palette's settings inputs, which are custom elements, use it to read and write the properties every element has.

## How an element works

Each element has two parts, joined in `AdaptiveCardPolyfills`:

- **Model** (`*Model.cs`): parses the element JSON with `ChartJson`, leniently. Invalid values read as missing and add a warning. Models don't use WinUI, so unit tests cover them without a window. `IncrementalState` is the element's canonical JSON.
- **Control** (`*Control.cs`): an `AdaptiveVisualControl` that draws the model with shapes and text. It re-renders when the theme or, for width-dependent layouts, the width changes.

Models read only the properties that the element's page in the [Adaptive Cards element reference](https://adaptivecards.microsoft.com/) defines, with the defaults it gives, such as `showTitle` off and `showLegend` on. Don't add properties of your own: a card that relies on one renders differently in other hosts, and the schema doesn't allow it. Make rendering choices in the control instead. For example, line charts are always smooth and filled, a line chart narrower than `LineChartLayout.CompactWidth` draws as a sparkline, and a gauge fills up to its value in the color of the segment that the value falls in, with the segments as a thin scale.

`AdaptiveCardPolyfills` lists every element with its parser and control factory. `PatchableElements` lets the incremental updater move a new model into an existing control instead of rebuilding the card, so a line chart that gains one sample scrolls instead of redrawing.

`AdaptiveContainerDecoratorRenderer` wraps the built-in container, column set, and column renderers to apply the container styles.

## Theming

- `ChartPalette` resolves color names (semantic, categorical, sequential, and diverging) to a light theme and a dark theme variant.
- `ChartTheme` adds the accent color, text and track colors, and high contrast: data takes the contrast theme's colors in turn, line series also differ by dash pattern, and every third and fourth part is an outline.
- The host config sets text sizes, spacing, and container styles. Command Palette builds it to match Fluent, in `AdaptiveCardsConfig`.

## Icons

`Icon` elements and the `Badge` `icon` property name icons from the Fluent System Icons catalog that Adaptive Cards uses, such as `Calendar` or `Wifi1`. They're drawn with Segoe Fluent Icons, the font Command Palette already uses for glyphs, so `FluentIconGlyphs` maps each name to the Segoe Fluent Icons glyph that draws the same symbol, with a filled glyph where one exists. Names without a matching glyph aren't listed. For them, `AdaptiveFallbackRenderer` draws the icon's `fallback`, since the WinUI 3 renderer drops fallback content, and `IconModel.HasGlyph` tells the incremental updater that the icon doesn't draw itself.

To add a name, check that it's in the Adaptive Cards icon catalog (the `IconName` type of `@microsoft/teams.cards`), and compare the two glyphs side by side before adding it. Some glyphs, such as `RAM`, are only in Segoe Fluent Icons; on Windows 10 without that font they don't draw, as with other Command Palette glyphs.

## Add an element

1. Add a model that implements `IAdaptiveVisualModel` with a static `Parse(string elementJson, ICollection<string> warnings)`. Read the properties on the element's reference page, and nothing else.
2. Add a control that derives from `AdaptiveVisualControl`, and implement `RenderCore` and `ApplyModel`. Read any text it shows with `ChartStrings.Get`, and add the string to Command Palette's `Resources.resw`.
3. Add the element to the list in `AdaptiveCardPolyfills`.
4. Add parsing tests to `Tests\Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests`.
5. Document the element for extension authors, and add it to the Samples extension.

## Upstream

Everything here is registered through the WinUI 3 renderer's public custom element APIs, without a fork. Each piece can move to the renderer in [microsoft/AdaptiveCards](https://github.com/microsoft/AdaptiveCards), which is C++/WinRT, so moving one means porting it:

| Piece | What upstream needs |
|---|---|
| The chart elements | New object model classes and XAML renderers. These controls and their tests are a reference implementation. |
| `ProgressBar`, `Badge`, `Icon` | The object model classes from the shared C++ model in [microsoft/AdaptiveCards-Mobile](https://github.com/microsoft/AdaptiveCards-Mobile), and XAML renderers. |
| `roundedCorners`, `showBorder` | The parsing from AdaptiveCards-Mobile's shared model, and support in the container renderers. |
| `AdaptiveFallbackRenderer` | A fix in `XamlBuilder::RenderAsUIElement`, which renders an element's fallback but returns an empty result. |

When a renderer release adds one:

1. Update `AdaptiveCards.Rendering.WinUI3` in `Directory.Packages.props`. Registrations here replace the renderer's own, so the update alone doesn't change any card.
2. Compare the two. The renderer's controls don't update in place, so a live chart would be rebuilt on every update unless the renderer adds that.
3. Remove the element from `AdaptiveCardPolyfills`, with its files and tests. Extensions don't change, since they send schema JSON.
4. When nothing is left, move `AdaptiveCustomElementJson` back to Command Palette, and delete this project and its tests.