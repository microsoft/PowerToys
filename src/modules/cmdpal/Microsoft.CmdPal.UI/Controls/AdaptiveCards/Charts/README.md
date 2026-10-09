# Native Adaptive Cards visuals

The WinUI 3 Adaptive Cards renderer doesn't support the charts and visuals in the current Adaptive Cards schema, so Command Palette renders them itself: `Chart.Line`, `Chart.Gauge`, `Chart.Donut`, `Chart.Pie`, `Chart.VerticalBar`, `Chart.HorizontalBar`, `Chart.HorizontalBar.Stacked`, `ProgressBar`, `Badge`, and `Icon`. Cards stay standard Adaptive Cards JSON, and extensions give each element a `fallback` for other hosts.

Extension authors: see `ExtensionTemplate\TemplateCmdPalExtension\.github\skills\add-adaptive-card-form\references\charts-and-visuals.md`, and the Samples extension's "Charts and visuals" page.

## How an element works

Each element has two parts, joined in `AdaptiveVisualElements`:

- **Model** (`*Model.cs`): parses the element JSON with `ChartJson`, leniently. Invalid values read as missing and add a warning. Models don't use WinUI, so the unit tests link them directly. `IncrementalState` is the element's canonical JSON.
- **Control** (`*Control.cs`): an `AdaptiveVisualControl` that draws the model with shapes and text. It re-renders when the theme or, for width-dependent layouts, the width changes.

Models read only the properties that the element's page in the [Adaptive Cards element reference](https://adaptivecards.microsoft.com/) defines, with the defaults it gives, such as `showTitle` off and `showLegend` on. Don't add properties of your own: a card that relies on one renders differently in other hosts, and the schema doesn't allow it. Make rendering choices in the control instead. For example, line charts are always smooth and filled, and a line chart narrower than `LineChartLayout.CompactWidth` draws as a sparkline.

`AdaptiveVisualElements.Types` lists every element with its parser and control factory. `ContentFormControl` registers the parsers, the renderers, and `PatchableElements`, so the incremental updater can move a new model into an existing control instead of rebuilding the card. A line chart that gains one sample scrolls instead of redrawing.

`AdaptiveContainerDecoratorRenderer` (one folder up) adds `roundedCorners` and `showBorder` to containers, column sets, and columns with the `CmdPal.Adaptive.Container.*` styles in `ContentFormControl.xaml`.

## Theming

- `ChartPalette` resolves color names (semantic, categorical, sequential, and diverging) to a light theme and a dark theme variant.
- `ChartTheme` adds the accent color, text and track colors, and high contrast, where data colors become the contrast theme's system colors in turn, line series also differ by dash pattern, and the third and fourth slices, segments, or parts are outlines (`ChartPalette.IsHighContrastOutline`).
- `AdaptiveCardsConfig` builds the host config from `AdaptiveCardThemeTokens`, so text sizes, spacing, and container styles match Fluent, and cards re-render when the theme changes.

## Icons

`Icon` elements and the `Badge` `icon` property name icons from the Fluent System Icons catalog that Adaptive Cards uses, such as `Calendar` or `Wifi1`. Command Palette draws them with Segoe Fluent Icons, the font it already uses for glyphs, so `FluentIconGlyphs` maps each name to the Segoe Fluent Icons glyph that draws the same symbol, with a filled glyph where one exists. Names without a matching glyph aren't listed. For them, the element renderer draws the icon's `fallback` itself with `AdaptiveFallbackRenderer`, because the WinUI 3 renderer renders an element's fallback but doesn't show it, and `IconModel.HasGlyph` tells the incremental updater that the icon doesn't draw itself.

To add a name, check that it's in the Adaptive Cards icon catalog (the `IconName` type of `@microsoft/teams.cards`), and compare the two glyphs side by side before adding it. Some glyphs, such as `RAM`, are only in Segoe Fluent Icons; on Windows 10 without that font they don't draw, as with other Command Palette glyphs.

## Add an element

1. Add a model that implements `IAdaptiveVisualModel` with a static `Parse(string elementJson, ICollection<string> warnings)`. Read the properties on the element's reference page, and nothing else.
2. Add a control that derives from `AdaptiveVisualControl`, and implement `RenderCore` and `ApplyModel`.
3. Add the element to `AdaptiveVisualElements.Types`.
4. Link the model in `Tests\Microsoft.CmdPal.UI.UnitTests` and add parsing tests.
5. Document the element for extension authors, and add it to the Samples extension.

## Upstream

These renderers follow the Adaptive Cards schema, so they can move to the open-source WinUI 3 renderer in [microsoft/AdaptiveCards](https://github.com/microsoft/AdaptiveCards). Until then, Command Palette registers them through the renderer's public custom element APIs, without a fork.
