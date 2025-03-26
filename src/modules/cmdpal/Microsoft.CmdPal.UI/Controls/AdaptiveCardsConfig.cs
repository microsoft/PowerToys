// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.Rendering.WinUI3;

namespace Microsoft.CmdPal.UI.Controls;

public sealed class AdaptiveCardsConfig
{
    public static AdaptiveHostConfig Light { get; }

    public static AdaptiveHostConfig Dark { get; }

    static AdaptiveCardsConfig()
    {
        Light = AdaptiveHostConfig.FromJsonString(LightHostConfigString).HostConfig;
        Dark = AdaptiveHostConfig.FromJsonString(DarkHostConfigString).HostConfig;
    }

    public static readonly string DarkHostConfigString = """
{
  "spacing": {
    "small": 4,
    "default": 8,
    "medium": 20,
    "large": 30,
    "extraLarge": 40,
    "padding": 8
  },
  "separator": {
    "lineThickness": 0,
    "lineColor": "#C8FFFFFF"
  },
  "supportsInteractivity": true,
  "fontTypes": {
    "default": {
      "fontFamily": "'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif",
      "fontSizes": {
        "small": 12,
        "default": 12,
        "medium": 14,
        "large": 20,
        "extraLarge": 26
      },
      "fontWeights": {
        "lighter": 200,
        "default": 400,
        "bolder": 600
      }
    },
    "monospace": {
      "fontFamily": "'Courier New', Courier, monospace",
      "fontSizes": {
        "small": 12,
        "default": 12,
        "medium": 14,
        "large": 18,
        "extraLarge": 26
      },
      "fontWeights": {
        "lighter": 200,
        "default": 400,
        "bolder": 600
      }
    }
  },
  "containerStyles": {
    "default": {
      "backgroundColor": "#00000000",
      "borderColor": "#00000000",
      "foregroundColors": {
        "default": {
          "default": "#FFFFFF",
          "subtle": "#C8FFFFFF"
        },
        "accent": {
          "default": "#0063B1",
          "subtle": "#880063B1"
        },
        "attention": {
          "default": "#FF5555",
          "subtle": "#DDFF5555"
        },
        "good": {
          "default": "#54a254",
          "subtle": "#DD54a254"
        },
        "warning": {
          "default": "#c3ab23",
          "subtle": "#DDc3ab23"
        }
      }
    },
    "emphasis": {
      "backgroundColor": "#09FFFFFF",
      "borderColor": "#09FFFFFF",
      "foregroundColors": {
        "default": {
          "default": "#FFFFFF",
          "subtle": "#C8FFFFFF"
        },
        "accent": {
          "default": "#2E89FC",
          "subtle": "#882E89FC"
        },
        "attention": {
          "default": "#FF5555",
          "subtle": "#DDFF5555"
        },
        "good": {
          "default": "#54a254",
          "subtle": "#DD54a254"
        },
        "warning": {
          "default": "#c3ab23",
          "subtle": "#DDc3ab23"
        }
      }
    }
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

    public static readonly string LightHostConfigString = """
{
  "spacing": {
    "small": 4,
    "default": 8,
    "medium": 20,
    "large": 30,
    "extraLarge": 40,
    "padding": 8
  },
  "separator": {
    "lineThickness": 0,
    "lineColor": "#606060"
  },
  "supportsInteractivity": true,
  "fontTypes": {
    "default": {
      "fontFamily": "'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif",
      "fontSizes": {
        "small": 12,
        "default": 12,
        "medium": 14,
        "large": 20,
        "extraLarge": 26
      },
      "fontWeights": {
        "lighter": 200,
        "default": 400,
        "bolder": 600
      }
    },
    "monospace": {
      "fontFamily": "'Courier New', Courier, monospace",
      "fontSizes": {
        "small": 12,
        "default": 12,
        "medium": 14,
        "large": 18,
        "extraLarge": 26
      },
      "fontWeights": {
        "lighter": 200,
        "default": 400,
        "bolder": 600
      }
    }
  },
  "containerStyles": {
    "default": {
      "backgroundColor": "#00000000",
      "borderColor": "#00000000",
      "foregroundColors": {
        "default": {
          "default": "#E6000000",
          "subtle": "#99000000"
        },
        "accent": {
          "default": "#0063B1",
          "subtle": "#880063B1"
        },
        "attention": {
          "default": "#C00000",
          "subtle": "#DDC00000"
        },
        "good": {
          "default": "#54a254",
          "subtle": "#DD54a254"
        },
        "warning": {
          "default": "#c3ab23",
          "subtle": "#DDc3ab23"
        }
      }
    },
    "emphasis": {
      "backgroundColor": "#80F6F6F6",
      "borderColor": "#80F6F6F6",
      "foregroundColors": {
        "default": {
          "default": "#E6000000",
          "subtle": "#99000000"
        },
        "accent": {
          "default": "#2E89FC",
          "subtle": "#882E89FC"
        },
        "attention": {
          "default": "#C00000",
          "subtle": "#DDC00000"
        },
        "good": {
          "default": "#54a254",
          "subtle": "#DD54a254"
        },
        "warning": {
          "default": "#c3ab23",
          "subtle": "#DDc3ab23"
        }
      }
    }
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
}
