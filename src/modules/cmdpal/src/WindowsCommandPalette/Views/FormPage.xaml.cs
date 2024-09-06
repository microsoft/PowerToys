// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.ViewManagement;

namespace WindowsCommandPalette.Views;

public sealed partial class FormPage : Page
{
    private readonly AdaptiveCardRenderer _renderer = new();

    public FormPageViewModel? ViewModel { get; set; }

    public FormPage()
    {
        this.InitializeComponent();
        UISettings settings = new UISettings();

        // yep this is the way to check if you're in light theme or dark.
        // yep it's this dumb
        var foreground = settings.GetColorValue(UIColorType.Foreground);
        var lightTheme = foreground.R < 128;
        _renderer.HostConfig = AdaptiveHostConfig.FromJsonString(lightTheme ? LightHostConfig : DarkHostConfig).HostConfig;
    }

    private void AddCardElement(FormViewModel form)
    {
        form.RenderToXaml(_renderer);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel = (FormPageViewModel?)e.Parameter;
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.InitialRender().ContinueWith((t) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var form in this.ViewModel.Forms)
                {
                    AddCardElement(form);
                }

                FormContent.Focus(FocusState.Programmatic);
            });
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        FormContent.Focus(FocusState.Programmatic);
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel?.GoBack();
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
    "small": 12,
    "medium": 16,
    "large": 20
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
    "small": 12,
    "medium": 16,
    "large": 20
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
