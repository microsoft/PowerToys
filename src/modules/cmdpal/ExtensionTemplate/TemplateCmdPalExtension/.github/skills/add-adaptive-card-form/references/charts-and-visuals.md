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
| `Badge` | A short status label |

Containers, column sets, and columns also accept `roundedCorners` and `showBorder`, for card-style tiles.

The elements follow the [Adaptive Cards schema](https://adaptivecards.microsoft.com/), so the same JSON renders in other hosts that support it. Older versions of Command Palette, and hosts without chart support, drop unknown elements. **Always set a `fallback`**: `"drop"`, or a simpler element such as a `TextBlock` with the value.

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
      "valueFormat": "percentage",
      "fill": "gradient",
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

## Colors

Every `color` property accepts:

- Semantic names: `accent` (the user's accent color), `good`, `warning`, `attention`, and `neutral`.
- Categorical names: `categoricalBlue`, `categoricalLightBlue`, `categoricalTeal`, `categoricalGreen`, `categoricalLime`, `categoricalMarigold`, `categoricalRed`, `categoricalPurple`, and `categoricalLavender`.
- Sequential (`sequential1`-`sequential8`) and diverging (`divergingBlue`, `divergingTeal`, `divergingYellow`, `divergingRed`, and others) names.

Names have a light theme and a dark theme variant, and switch to system colors in high contrast. Prefer names over hex values so charts stay readable in every theme. Set `colorSet` (`categorical`, `sequential`, or `diverging`) to color items in order without naming each color.

## Chart.Line

| Property | Description |
|----------|-------------|
| `data` | Series: `[{ "legend": "...", "color": "...", "values": [{ "x": "...", "y": 1 }] }]`. A `null` `y` leaves a gap, so pad the start of a history to keep a fixed time window. `x` labels are optional. |
| `title`, `xAxisTitle`, `yAxisTitle` | Labels around the plot. |
| `yMin`, `yMax` | Fix the value range. Without them, the range fits the data. |
| `valueFormat` | `number` (default) or `percentage` for values that are already percentages. |
| `color`, `colorSet` | Series colors when a series doesn't set its own. |
| `fill` | Command Palette: `gradient` fills under each line. Default `none`. |
| `curve` | Command Palette: `smooth` (default, never overshoots the data) or `linear`. |
| `style` | Command Palette: `sparkline` draws a compact line without axes or labels, for tiles. |
| `showLegend` | Command Palette: shows the legend. By default, charts with more than one series show one. |
| `minHeight` | Command Palette: the smallest plot height, such as `"120px"`. |

## Chart.Gauge

| Property | Description |
|----------|-------------|
| `value`, `min`, `max` | The value and its range. Without `max`, the segments' total sets it. |
| `segments` | Optional colored ranges: `[{ "legend": "Normal", "size": 60, "color": "good" }]`. A marker shows where the value falls. Without segments, the gauge fills up to the value. |
| `valueFormat` | `percentage` (default) or `fraction`, such as `3/5`. |
| `title`, `subLabel` | A title, and a label under the value. |
| `showMinMax`, `showLegend` | Show the range labels and the segment legend. Both default to `true`. |
| `color` | Command Palette: the fill color of a gauge without segments. |

## Chart.Donut and Chart.Pie

| Property | Description |
|----------|-------------|
| `data` | Slices: `[{ "legend": "Apps", "value": 182, "color": "categoricalBlue" }]`. The legend shows each slice's share. |
| `title`, `colorSet` | A title, and colors for slices that don't set one. |
| `value` | Command Palette, `Chart.Donut` only: a label in the center, such as `"512 GB"`. |
| `showLegend` | Command Palette: set `false` to hide the legend. |

## Chart.VerticalBar and Chart.HorizontalBar

| Property | Description |
|----------|-------------|
| `data` | Bars: `[{ "x": "Mon", "y": 12, "color": "..." }]`. |
| `title`, `xAxisTitle`, `yAxisTitle` | Labels. |
| `showBarValues` | Shows each bar's value. |
| `color`, `colorSet` | One color for every bar, or a color per bar. |
| `yMin`, `yMax`, `valueFormat` | Command Palette: the value range and format, as for `Chart.Line`. |

## Chart.HorizontalBar.Stacked

`data` is a list of bars, each with a `title` and its own `data` of parts: `[{ "title": "Desktop", "data": [{ "legend": "In use", "value": 18, "color": "categoricalPurple" }] }]`. A legend appears once for all bars, and a legend keeps the same color in every bar. Bars are scaled to the longest bar, so a single bar fills the width. Set `showLegend` to `false` to hide the legend.

## ProgressBar

`value` and `max` (default 100). Without `value`, the bar is indeterminate. `color` takes a color name; the default is the accent color.

## Badge

`text`, plus:

- `style`: `default`, `subtle`, `accent`, `informative`, `good`, `warning`, `attention`, or `important`.
- `appearance`: `filled` (default) or `tint`.
- `shape`: `rounded` (default), `square`, or `circular`.
- `size`: `medium` (default), `large`, or `extraLarge`.
- `tooltip`.

## Tiles with roundedCorners and showBorder

Use an emphasis container with rounded corners and a border for a dashboard tile. Set `"height": "stretch"` so tiles in a row line up:

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
            { "type": "Chart.Line", "style": "sparkline", "yMin": 0, "fill": "gradient", "data": "${cpuSeries}", "fallback": "drop" }
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
