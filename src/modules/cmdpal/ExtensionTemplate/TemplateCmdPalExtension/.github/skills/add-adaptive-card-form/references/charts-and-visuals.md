# Charts and visuals in Adaptive Cards

Command Palette renders these elements from the current Adaptive Cards schema natively, so form and content pages can show live dashboards without images:

| Element | Shows |
|---------|-------|
| `Chart.Line` | One or more series over time, such as usage history |
| `Chart.Gauge` | A value on a half circle, optionally split into colored segments |
| `Chart.Donut` / `Chart.Pie` | Parts of a whole, with a legend |
| `Chart.VerticalBar` / `Chart.HorizontalBar` | One value per category |
| `Chart.HorizontalBar.Stacked` | Several parts per bar, such as memory by type |
| `ProgressBar` | Progress, or an indeterminate wait |
| `Badge` | A short status label, optionally with an icon |
| `Icon` | A Fluent icon, such as `DeveloperBoard` beside a CPU tile's title |

Containers, column sets, and columns also accept `roundedCorners` and `showBorder`, for card-style tiles.

The elements follow the [Adaptive Cards schema](https://adaptivecards.microsoft.com/). Command Palette reads only the properties that the schema defines, with the schema's defaults, so a card that renders here renders the same way in other hosts that support the elements. Older versions of Command Palette, and hosts without chart support, drop unknown elements. **Always set a `fallback`**: `"drop"`, or a simpler element such as a `TextBlock` with the value.

## Bind live data

Bind arrays and numbers with an expression that is the whole value. The template keeps the JSON type, so `"data": "${series}"` receives your array:

```csharp
TemplateJson = """
{
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    {
      "type": "Chart.Line",
      "yAxisTitle": "% Utilization",
      "xAxisTitle": "60 seconds",
      "yMin": 0,
      "yMax": 100,
      "showLegend": false,
      "data": "${series}",
      "fallback": { "type": "TextBlock", "text": "${current}" }
    }
  ]
}
""";
```

To update the chart, set `DataJson` again. Command Palette updates charts in place, so a line chart scrolls smoothly instead of redrawing the card. Keep the template the same between updates; only the data should change.

```csharp
var data = new JsonObject
{
    ["current"] = "42%",
    ["series"] = new JsonArray
    {
        (JsonNode)new JsonObject
        {
            ["legend"] = "CPU",
            ["color"] = "categoricalBlue",
            ["values"] = values, // [{ "y": 12.5 }, { "y": 13 }, ...]
        },
    },
};
DataJson = data.ToJsonString();
```

Every value is an object, and `y` is a number that defaults to `0`. The schema has no way to mark a missing sample, so send only the samples you have: a history that's still filling up is a shorter series.

## Colors

Chart colors (`color` on a chart, a series, a bar, a slice, or a segment) are names from the schema:

- Semantic: `good`, `warning`, `attention`, and `neutral`.
- Categorical: `categoricalBlue`, `categoricalLightBlue`, `categoricalTeal`, `categoricalGreen`, `categoricalLime`, `categoricalMarigold`, `categoricalRed`, `categoricalPurple`, and `categoricalLavender`.
- Sequential (`sequential1`-`sequential8`) and diverging (`divergingBlue`, `divergingTeal`, `divergingYellow`, `divergingRed`, and others).

Names have a light theme and a dark theme variant. Without a color, items take the colors of `colorSet` in order: `categorical` by default, starting with `categoricalBlue`. The bars of `Chart.VerticalBar` and `Chart.HorizontalBar` share the first color unless the chart sets `colorSet`. Set `colorSet` to `sequential` or `diverging` to use another set. A gauge without segments uses the user's accent color. In high contrast, items take the contrast theme's system colors in turn, the lines of a line chart also differ by dash pattern, and every third and fourth slice, segment, or part is an outline instead of filled.

`ProgressBar`, `Badge`, and `Icon` have their own, shorter lists, below.

## Chart.Line

| Property | Description |
|----------|-------------|
| `data` | Series: `[{ "legend": "...", "color": "...", "values": [{ "x": "...", "y": 1 }] }]`. `x` labels are optional. |
| `title`, `showTitle` | A title, shown only when `showTitle` is `true`. |
| `xAxisTitle`, `yAxisTitle` | Labels under and above the plot. Put the unit in the axis title, such as `% Utilization`. |
| `yMin`, `yMax` | Fix the value range. Without them, the range fits the data. |
| `color`, `colorSet` | Series colors when a series doesn't set its own. |
| `showLegend` | Shows the legend; the default is `true`. A single series without a `legend` has nothing to show. |

Command Palette draws smooth lines with a soft fill under them. A chart narrower than 400 pixels, such as one in a dashboard tile, draws as a sparkline: just the lines, without the title, axis labels, grid lines, or legend.

## Chart.Gauge

| Property | Description |
|----------|-------------|
| `value`, `min`, `max` | The value and its range. Without `max`, the segments' total sets it. |
| `segments` | Optional colored ranges: `[{ "legend": "Normal", "size": 60, "color": "good" }]`. Without segments, the gauge fills up to the value. |
| `valueFormat` | `Percentage` (default) or `Fraction`, such as `3/5`. |
| `title`, `showTitle` | A title, shown only when `showTitle` is `true`. |
| `subLabel` | A label under the value. |
| `showNeedle`, `showMinMax`, `showLegend` | Show the needle (a marker where the value falls), the range labels, and the segment legend. All default to `true`. |

## Chart.Donut and Chart.Pie

| Property | Description |
|----------|-------------|
| `data` | Slices: `[{ "legend": "Apps", "value": 182, "color": "categoricalBlue" }]`. The legend shows each slice's share. |
| `title`, `showTitle` | A title, shown only when `showTitle` is `true`. |
| `colorSet` | Colors for slices that don't set one. |
| `value` | `Chart.Donut`: a label in the center, such as `"512 GB"`. |
| `showLegend` | Shows the legend; the default is `true`. |

## Chart.VerticalBar

| Property | Description |
|----------|-------------|
| `data` | Bars: `[{ "x": "Mon", "y": 12, "color": "..." }]`. |
| `title`, `showTitle` | A title, shown only when `showTitle` is `true`. |
| `xAxisTitle`, `yAxisTitle` | Labels. |
| `showBarValues` | Shows each bar's value; the default is `false`. |
| `yMin`, `yMax` | The value range. Without them, the range fits the data. |
| `color`, `colorSet` | One color for every bar, or a color per bar. |

Labels that would overlap are skipped.

## Chart.HorizontalBar

| Property | Description |
|----------|-------------|
| `data` | Bars: `[{ "x": "Browser", "y": 34, "color": "..." }]`. Values can't be negative. |
| `title`, `showTitle` | A title, shown only when `showTitle` is `true`. |
| `xAxisTitle`, `yAxisTitle` | The category axis title, under the labels, and the value axis title, under the bars. |
| `displayMode` | `AbsoluteWithAxis` (default): a value axis under the bars. `AbsoluteNoAxis`: each value at the end of its bar. `PartToWhole`: each bar's share of the total, as a percentage. |
| `color`, `colorSet` | One color for every bar, or a color per bar. |

Labels longer than 40% of the chart's width are trimmed, with the full label in a tooltip.

## Chart.HorizontalBar.Stacked

`data` is a list of bars, each with a `title` and its own `data` of parts: `[{ "title": "Desktop", "data": [{ "legend": "In use", "value": 18, "color": "categoricalPurple" }] }]`. A legend appears once for all bars, and a legend keeps the same color in every bar. Bars are scaled to the longest bar, so a single bar fills the width. `showLegend` defaults to `true`, and `title` shows only when `showTitle` is `true`.

## ProgressBar

`value` and `max` (default 100). Without `value`, the bar is indeterminate. `color` is `accent` (default), `good`, `warning`, or `attention`.

## Badge

`text`, plus:

- `style`: `default`, `subtle`, `informative`, `accent`, `good`, `warning`, or `attention`.
- `appearance`: `filled` (default) or `tint`.
- `shape`: `circular` (default), `rounded`, or `square`. Command Palette's own cards use `rounded`, a rectangle with rounded corners, which matches the cards' tiles.
- `size`: `medium` (default), `large`, or `extraLarge`.
- `icon`: a Fluent icon name, optionally followed by `,filled`, such as `"CheckmarkCircle,filled"`.
- `iconPosition`: `before` (default) or `after` the text.
- `tooltip`.

## Icon

`name` is a Fluent icon name from the Adaptive Cards icon catalog, such as `Calendar`, `Wifi1`, or `Battery7`. Command Palette draws icons with Segoe Fluent Icons, so it supports the common names that have a matching glyph, including system names (`DeveloperBoard`, `Ram`, `HardDrive`, `Desktop`, `NetworkAdapter`, `Wifi1` to `Wifi4`, `Battery0` to `Battery10`, `BatteryCharge`), status names (`Checkmark`, `CheckmarkCircle`, `Warning`, `ErrorCircle`, `Info`), and common actions. For other names, Command Palette draws the icon's `fallback`, so give each icon a `fallback`: `"drop"`, or a `TextBlock` or an `Image`.

- `size`: `xxSmall` (12 px), `xSmall` (16), `Small` (20), `Standard` (24, the default), `Medium` (28), `Large` (32), `xLarge` (40), or `xxLarge` (48).
- `style`: `Regular` (default) or `Filled`. Icons without a filled glyph use the regular one.
- `color`: `Default`, `Dark`, `Light`, `Accent`, `Good`, `Warning`, or `Attention`, as for text.
- `horizontalAlignment`: `left` (default), `center`, or `right`.

Put a small icon in an `auto` column before a title, and center it vertically:

```json
{
  "type": "ColumnSet",
  "columns": [
    {
      "type": "Column",
      "width": "auto",
      "verticalContentAlignment": "center",
      "items": [ { "type": "Icon", "name": "DeveloperBoard", "size": "xSmall", "fallback": "drop" } ]
    },
    {
      "type": "Column",
      "width": "stretch",
      "spacing": "small",
      "verticalContentAlignment": "center",
      "items": [ { "type": "TextBlock", "text": "CPU", "size": "small", "weight": "bolder" } ]
    }
  ]
}
```

## Not supported yet

Command Palette doesn't render `Chart.VerticalBar.Grouped` or `ProgressRing`. Like any element it doesn't support, it drops them, even with a `fallback`: the WinUI 3 Adaptive Cards renderer doesn't show the fallback of an element it can't render. Other hosts still use the fallback. Command Palette also ignores these properties: `thickness`, `valueColor`, and `showOutlines` on donut, pie, and gauge charts; `maxWidth` on charts; and `selectAction` on `Icon`. A `Chart.Line` spaces its points evenly, even when its `x` values are numbers or dates. Charts with any of these still render.

## Tiles with roundedCorners and showBorder

Use an emphasis container with rounded corners and a border for a dashboard tile. Set `"height": "stretch"` so tiles in a row line up. A line chart in a tile is narrow, so it draws as a sparkline:

```json
{
  "type": "ColumnSet",
  "columns": [
    {
      "type": "Column",
      "width": "stretch",
      "items": [
        {
          "type": "Container",
          "style": "emphasis",
          "roundedCorners": true,
          "showBorder": true,
          "height": "stretch",
          "items": [
            { "type": "TextBlock", "text": "CPU", "size": "small", "weight": "bolder" },
            { "type": "TextBlock", "text": "${cpu}", "size": "extraLarge", "weight": "bolder", "spacing": "none" },
            { "type": "Chart.Line", "yMin": 0, "showLegend": false, "data": "${cpuSeries}", "fallback": "drop" }
          ]
        }
      ]
    }
  ]
}
```

## Design tips

- Lead with the number people came for, in `extraLarge` text, and put the chart under it.
- Fix `yMin` and `yMax` for percentages, so 5% looks like 5%. Let rates fit their data.
- Use one color per metric, and the same color for the same metric on every card.
- Keep the template stable and update only `DataJson`, so updates animate instead of redraw.
- The Samples extension's "Charts and visuals" page shows every element with live data.
