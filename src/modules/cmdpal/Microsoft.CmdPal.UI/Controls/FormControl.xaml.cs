// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FormControl : UserControl
{
    private static readonly AdaptiveCardRenderer _renderer;
    private FormViewModel? _viewModel;

    public FormViewModel? ViewModel { get => _viewModel; set => AttachViewModel(value); }

    static FormControl()
    {
        var settings = new UISettings();

        // yep this is the way to check if you're in light theme or dark.
        // yep it's this dumb
        var foreground = settings.GetColorValue(UIColorType.Foreground);
        var lightTheme = foreground.R < 128;
        _renderer = new AdaptiveCardRenderer
        {
            HostConfig = AdaptiveHostConfig.FromJsonString(lightTheme ? LightHostConfig : DarkHostConfig).HostConfig,
        };
    }

    public FormControl()
    {
        this.InitializeComponent();
    }

    private void AttachViewModel(FormViewModel? vm)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = vm;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            var c = _viewModel.Card;
            if (c != null)
            {
                DisplayCard(c);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModel.Card))
        {
            var c = ViewModel.Card;
            if (c != null)
            {
                DisplayCard(c);
            }
        }
    }

    private void DisplayCard(AdaptiveCardParseResult result)
    {
        var rendered = _renderer.RenderAdaptiveCard(result.AdaptiveCard);
        rendered.Action += Rendered_Action;
        ContentGrid.Children.Clear();
        ContentGrid.Children.Add(rendered.FrameworkElement);
    }

    private void Rendered_Action(RenderedAdaptiveCard sender, AdaptiveActionEventArgs args)
    {
    }

    private static readonly string DarkHostConfig = """
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

    private static readonly string LightHostConfig = """
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
