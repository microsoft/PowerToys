// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Builds the Adaptive Cards host config JSON from theme tokens. Sizes and spacing follow the
/// Fluent type ramp and 4px grid: Caption 12, Body 14, Body Large 16, Subtitle 20, Title 28.
/// </summary>
internal static class AdaptiveHostConfigJson
{
    public const string FontFamily = "Segoe UI Variable Text, Segoe UI";
    public const string MonospaceFontFamily = "Cascadia Mono, Consolas, Courier New";

    public static string Create(AdaptiveCardThemeTokens tokens) => $$"""
{
  "spacing": {
    "small": 4,
    "default": 8,
    "medium": 16,
    "large": 24,
    "extraLarge": 32,
    "padding": 12
  },
  "separator": {
    "lineThickness": 1,
    "lineColor": "{{tokens.Separator}}"
  },
  "supportsInteractivity": true,
  "fontTypes": {
    "default": {
      "fontFamily": "{{FontFamily}}",
      "fontSizes": {
        "small": 12,
        "default": 14,
        "medium": 16,
        "large": 20,
        "extraLarge": 28
      },
      "fontWeights": {
        "lighter": 300,
        "default": 400,
        "bolder": 600
      }
    },
    "monospace": {
      "fontFamily": "{{MonospaceFontFamily}}",
      "fontSizes": {
        "small": 12,
        "default": 12,
        "medium": 14,
        "large": 18,
        "extraLarge": 26
      },
      "fontWeights": {
        "lighter": 300,
        "default": 400,
        "bolder": 600
      }
    }
  },
  "containerStyles": {
    "default": {{ContainerStyle("#00000000", tokens)}},
    "emphasis": {{ContainerStyle(tokens.EmphasisBackground, tokens)}},
    "accent": {{ContainerStyle(tokens.AccentBackground, tokens)}},
    "good": {{ContainerStyle(tokens.GoodBackground, tokens)}},
    "warning": {{ContainerStyle(tokens.WarningBackground, tokens)}},
    "attention": {{ContainerStyle(tokens.AttentionBackground, tokens)}}
  },
  "imageSizes": {
    "small": 16,
    "medium": 24,
    "large": 32
  },
  "actions": {
    "maxActions": 5,
    "spacing": "default",
    "buttonSpacing": 8,
    "showCard": {
      "actionMode": "inline",
      "inlineTopMargin": 8
    },
    "actionsOrientation": "horizontal",
    "actionAlignment": "stretch"
  },
  "adaptiveCard": {
    "allowCustomStyle": false
  },
  "imageSet": {
    "imageSize": "medium",
    "maxImageHeight": 100
  },
  "factSet": {
    "title": {
      "color": "default",
      "size": "default",
      "isSubtle": false,
      "weight": "bolder",
      "wrap": true,
      "maxWidth": 150
    },
    "value": {
      "color": "default",
      "size": "default",
      "isSubtle": false,
      "weight": "default",
      "wrap": true
    },
    "spacing": 8
  },
  "textStyles": {
    "heading": {
      "size": "large",
      "weight": "bolder",
      "color": "default",
      "isSubtle": false,
      "fontType": "default"
    },
    "columnHeader": {
      "size": "medium",
      "weight": "bolder",
      "color": "default",
      "isSubtle": false,
      "fontType": "default"
    }
  }
}
""";

    private static string ContainerStyle(string background, AdaptiveCardThemeTokens tokens) => $$"""
{
      "backgroundColor": "{{background}}",
      "borderColor": "#00000000",
      "foregroundColors": {
        "default": { "default": "{{tokens.Foreground}}", "subtle": "{{tokens.ForegroundSubtle}}" },
        "accent": { "default": "{{tokens.Accent}}", "subtle": "{{Subtle(tokens.Accent)}}" },
        "good": { "default": "{{tokens.Good}}", "subtle": "{{Subtle(tokens.Good)}}" },
        "warning": { "default": "{{tokens.Warning}}", "subtle": "{{Subtle(tokens.Warning)}}" },
        "attention": { "default": "{{tokens.Attention}}", "subtle": "{{Subtle(tokens.Attention)}}" }
      }
    }
""";

    private static string Subtle(string color) => AdaptiveCardThemeTokens.WithAlpha(color, 0xC0);
}
