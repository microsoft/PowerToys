// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using global::PowerToys.GPOWrapper;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using MouseJump.Common.Helpers;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Settings;
using MouseJump.Models.Styles;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class MouseUtilsViewModel : PageViewModelBase
    {
        private GpoRuleConfigured _jumpEnabledGpoRuleConfiguration;
        private bool _jumpEnabledStateIsGPOConfigured;
        private bool _isMouseJumpEnabled;

        internal MouseJumpSettings MouseJumpSettingsConfig { get; set; }

        private void InitializeMouseJumpSettings(ISettingsRepository<MouseJumpSettings> mouseJumpSettingsRepository)
        {
            ArgumentNullException.ThrowIfNull(mouseJumpSettingsRepository);
            this.MouseJumpSettingsConfig = mouseJumpSettingsRepository.SettingsConfig;

            this.MouseJumpSettingsConfig.Properties.ThumbnailSize.PropertyChanged += this.MouseJumpThumbnailSizePropertyChanged;
        }

        private void InitializeMouseJumpEnabledValues()
        {
            _jumpEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMouseJumpEnabledValue();
            if (_jumpEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _jumpEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _jumpEnabledStateIsGPOConfigured = true;
                _isMouseJumpEnabled = _jumpEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isMouseJumpEnabled = GeneralSettingsConfig.Enabled.MouseJump;
            }
        }

        public bool IsMouseJumpEnabled
        {
            get => _isMouseJumpEnabled;
            set
            {
                if (_jumpEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isMouseJumpEnabled != value)
                {
                    _isMouseJumpEnabled = value;

                    GeneralSettingsConfig.Enabled.MouseJump = value;
                    OnPropertyChanged(nameof(_isMouseJumpEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyMouseJumpPropertyChanged();
                }
            }
        }

        public bool IsJumpEnabledGpoConfigured
        {
            get => _jumpEnabledStateIsGPOConfigured;
        }

        public HotkeySettings MouseJumpActivationShortcut
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (MouseJumpSettingsConfig.Properties.ActivationShortcut != value)
                {
                    MouseJumpSettingsConfig.Properties.ActivationShortcut = value ?? MouseJumpSettingsConfig.Properties.DefaultActivationShortcut;
                    NotifyMouseJumpPropertyChanged();
                }
            }
        }

        public MouseJumpThumbnailSize MouseJumpThumbnailSize
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.ThumbnailSize;
            }

            set
            {
                if ((MouseJumpSettingsConfig.Properties.ThumbnailSize.Width != value?.Width)
                    && (MouseJumpSettingsConfig.Properties.ThumbnailSize.Height != value?.Height))
                {
                    MouseJumpSettingsConfig.Properties.ThumbnailSize = value;
                    NotifyMouseJumpPropertyChanged();
                }
            }
        }

        public ImageSource MouseJumpPreviewImage
        {
            get
            {
                // build the display info used to generate the preview image in the settings dialog
                // (a fake two-screen desktop showing the user's wallpaper - see MouseJumpPreviewDesktop)
                var primaryScreen = MouseJumpPreviewDesktop.PrimaryScreen;
                var secondaryScreen = MouseJumpPreviewDesktop.SecondaryScreen;
                var displayInfo = new DisplayInfo([
                    new DeviceInfo(
                        hostname: "FakeDisplay1",
                        localhost: true,
                        screens: [
                            new(
                                handle: IntPtr.Zero,
                                primary: false,
                                displayArea: new RectangleInfo(secondaryScreen.X, secondaryScreen.Y, secondaryScreen.Width, secondaryScreen.Height),
                                workingArea: new RectangleInfo(secondaryScreen.X, secondaryScreen.Y, secondaryScreen.Width, secondaryScreen.Height)),
                            new(
                                handle: IntPtr.Zero,
                                primary: true,
                                displayArea: new RectangleInfo(primaryScreen.X, primaryScreen.Y, primaryScreen.Width, primaryScreen.Height),
                                workingArea: new RectangleInfo(primaryScreen.X, primaryScreen.Y, primaryScreen.Width, primaryScreen.Height)),
                        ]),
                ]);

                var desktopSize = LayoutHelper.GetCombinedScreenBounds(
                        displayInfo.Devices[0].Screens
                            .Select(screen => screen.DisplayArea)
                            .ToList())
                    .Size;

                /*
                    magic number 283 is the content height left in the settings card after removing the top and bottom chrome:

                        300px settings card height - 1px top border - 7px top margin - 8px bottom margin - 1px bottom border = 283px image height

                    this ensures we get a preview image scaled at 100% so borders, etc, are shown at exact pixel sizes in the preview
                */
                const int settingsCardHeight = 300;
                const int settingsCardTopBorder = 1;
                const int settingsCardTopMargin = 7;
                const int settingsCardBottomMargin = 8;
                const int settingsCardBottomBorder = 1;
                const int settingsCardContentHeight = // 283
                    settingsCardHeight - settingsCardTopBorder - settingsCardTopMargin - settingsCardBottomBorder - settingsCardBottomMargin;

                // TODO: we only calculate settingsCardContentHeight and assume the
                // preview image is constrained by height, not by width. That's true
                // today but if the fake displayInfo above or the Settings UI form's
                // MinWidth are changed in future this might create an image that is
                // too wide for the control, and we might need to calculate
                // settingsCardContentWidth as well.
                var canvasSize = new SizeInfo(desktopSize.Width, settingsCardContentHeight).Clamp(desktopSize);

                var previewType = Enum.TryParse<PreviewType>(this.MouseJumpPreviewType, true, out var previewTypeResult)
                    ? previewTypeResult
                    : PreviewType.Bezelled;
                var previewStyle = previewType switch
                {
                    PreviewType.Compact => StyleHelper.CompactPreviewStyle.WithCanvasSize(desktopSize),
                    PreviewType.Bezelled => StyleHelper.BezelledPreviewStyle.WithCanvasSize(desktopSize),
                    PreviewType.Custom => new PreviewStyle(
                        canvasSize: canvasSize,
                        canvasStyle: new(
                            marginStyle: new(0),
                            borderStyle: new(
                                color: ColorHelper.DeserializeFromConfigColorString(
                                    this.MouseJumpBorderColor),
                                all: this.MouseJumpBorderThickness,
                                depth: this.MouseJumpBorder3dDepth
                            ),
                            paddingStyle: new(
                                all: this.MouseJumpBorderPadding
                            ),
                            backgroundStyle: new(
                                color1: ColorHelper.DeserializeFromConfigColorString(
                                    this.MouseJumpBackgroundColor1),
                                color2: ColorHelper.DeserializeFromConfigColorString(
                                    this.MouseJumpBackgroundColor2)
                            )
                        ),
                        screenStyle: new(
                            marginStyle: new(
                                all: this.MouseJumpScreenMargin
                            ),
                            borderStyle: new(
                                color: ColorHelper.DeserializeFromConfigColorString(
                                    this.MouseJumpBezelColor),
                                all: this.MouseJumpBezelThickness,
                                depth: this.MouseJumpBezel3dDepth
                            ),
                            paddingStyle: new(0),
                            backgroundStyle: new(
                                color1: ColorHelper.DeserializeFromConfigColorString(
                                    this.MouseJumpScreenColor1),
                                color2: ColorHelper.DeserializeFromConfigColorString(
                                    this.MouseJumpScreenColor2)
                            )
                        ),
                        extraColors: []),
                    _ => throw new InvalidOperationException(
                        $"Unhandled {nameof(MouseJumpPreviewType)} '{previewType}'"),
                };

                var activatedScreen = displayInfo.Devices[0].Screens[0];

                var previewLayout = LayoutHelper.GetFormLayout(
                    previewStyle: previewStyle,
                    displayInfo: displayInfo,
                    maximumSize: canvasSize);

                var imageCopyService = new SmoothImageRegionCopyService(MouseJumpPreviewDesktop.Image);
                var previewImageTask = DrawingHelper.RenderPreviewAsync(
                    previewLayout.CanvasLayout,
                    activatedScreen,
                    new() { imageCopyService });
                using var previewImage = previewImageTask.GetAwaiter().GetResult();

                // save the image to a memory stream
                using var stream = new MemoryStream();
                previewImage.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                // load the memory stream into a bitmap image
                var bitmap = new BitmapImage();
                var rnd = stream.AsRandomAccessStream();
                bitmap.DecodePixelWidth = previewImage.Width;
                bitmap.DecodePixelHeight = previewImage.Height;
                bitmap.SetSource(rnd);
                return bitmap;
            }
        }

        public string MouseJumpPreviewType
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.PreviewType;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.PreviewType)
                {
                    MouseJumpSettingsConfig.Properties.PreviewType = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public string MouseJumpBackgroundColor1
        {
            get
            {
                var value = MouseJumpSettingsConfig.Properties.BackgroundColor1;
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                return value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(MouseJumpSettingsConfig.Properties.BackgroundColor1, StringComparison.OrdinalIgnoreCase))
                {
                    MouseJumpSettingsConfig.Properties.BackgroundColor1 = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public string MouseJumpBackgroundColor2
        {
            get
            {
                var value = MouseJumpSettingsConfig.Properties.BackgroundColor2;
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                return value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(MouseJumpSettingsConfig.Properties.BackgroundColor2, StringComparison.OrdinalIgnoreCase))
                {
                    MouseJumpSettingsConfig.Properties.BackgroundColor2 = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public int MouseJumpBorderThickness
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.BorderThickness;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.BorderThickness)
                {
                    MouseJumpSettingsConfig.Properties.BorderThickness = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public string MouseJumpBorderColor
        {
            get
            {
                var value = MouseJumpSettingsConfig.Properties.BorderColor;
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                return value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(MouseJumpSettingsConfig.Properties.BorderColor, StringComparison.OrdinalIgnoreCase))
                {
                    MouseJumpSettingsConfig.Properties.BorderColor = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public int MouseJumpBorder3dDepth
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.Border3dDepth;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.Border3dDepth)
                {
                    MouseJumpSettingsConfig.Properties.Border3dDepth = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public int MouseJumpBorderPadding
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.BorderPadding;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.BorderPadding)
                {
                    MouseJumpSettingsConfig.Properties.BorderPadding = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public int MouseJumpBezelThickness
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.BezelThickness;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.BezelThickness)
                {
                    MouseJumpSettingsConfig.Properties.BezelThickness = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public string MouseJumpBezelColor
        {
            get
            {
                var value = MouseJumpSettingsConfig.Properties.BezelColor;
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                return value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(MouseJumpSettingsConfig.Properties.BezelColor, StringComparison.OrdinalIgnoreCase))
                {
                    MouseJumpSettingsConfig.Properties.BezelColor = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public int MouseJumpBezel3dDepth
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.Bezel3dDepth;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.Bezel3dDepth)
                {
                    MouseJumpSettingsConfig.Properties.Bezel3dDepth = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public int MouseJumpScreenMargin
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.ScreenMargin;
            }

            set
            {
                if (value != MouseJumpSettingsConfig.Properties.ScreenMargin)
                {
                    MouseJumpSettingsConfig.Properties.ScreenMargin = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public string MouseJumpScreenColor1
        {
            get
            {
                var value = MouseJumpSettingsConfig.Properties.ScreenColor1;
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                return value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(MouseJumpSettingsConfig.Properties.ScreenColor1, StringComparison.OrdinalIgnoreCase))
                {
                    MouseJumpSettingsConfig.Properties.ScreenColor1 = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public string MouseJumpScreenColor2
        {
            get
            {
                var value = MouseJumpSettingsConfig.Properties.ScreenColor2;
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                return value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(MouseJumpSettingsConfig.Properties.ScreenColor2, StringComparison.OrdinalIgnoreCase))
                {
                    MouseJumpSettingsConfig.Properties.ScreenColor2 = value;
                    NotifyMouseJumpPropertyChanged();
                    NotifyMouseJumpPropertyChanged(nameof(this.MouseJumpPreviewImage));
                }
            }
        }

        public void MouseJumpThumbnailSizePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyMouseJumpPropertyChanged(nameof(MouseJumpThumbnailSize));
        }

        public void NotifyMouseJumpPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndMouseJumpSettings outsettings = new SndMouseJumpSettings(MouseJumpSettingsConfig);
            SndModuleSettings<SndMouseJumpSettings> ipcMessage = new SndModuleSettings<SndMouseJumpSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(MouseJumpSettingsConfig.ToJsonString(), MouseJumpSettings.ModuleName);
        }
    }
}
