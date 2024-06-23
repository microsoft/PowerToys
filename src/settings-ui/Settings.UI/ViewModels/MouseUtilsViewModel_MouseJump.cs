// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MouseJump.Common.Helpers;
using MouseJump.Common.Imaging;
using MouseJump.Common.Models.Styles;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class MouseUtilsViewModel : Observable
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
                var previewStyle = new PreviewStyle(
                    canvasSize: new(
                        width: this.MouseJumpThumbnailSize.Width,
                        height: this.MouseJumpThumbnailSize.Height
                    ),
                    canvasStyle: new(
                        marginStyle: new(0),
                        borderStyle: new(
                            color: ConfigHelper.DeserializeFromConfigColorString(
                                this.MouseJumpBorderColor),
                            all: this.MouseJumpBorderThickness,
                            depth: this.MouseJumpBorder3dDepth
                        ),
                        paddingStyle: new(
                            all: this.MouseJumpBorderPadding
                        ),
                        backgroundStyle: new(
                            color1: ConfigHelper.DeserializeFromConfigColorString(
                                this.MouseJumpBackgroundColor1),
                            color2: ConfigHelper.DeserializeFromConfigColorString(
                                this.MouseJumpBackgroundColor2)
                        )
                    ),
                    screenStyle: new(
                        marginStyle: new(
                            all: this.MouseJumpScreenMargin
                        ),
                        borderStyle: new(
                            color: ConfigHelper.DeserializeFromConfigColorString(
                                this.MouseJumpBezelColor),
                            all: this.MouseJumpBezelThickness,
                            depth: this.MouseJumpBezel3dDepth
                        ),
                        paddingStyle: new(0),
                        backgroundStyle: new(
                            color1: ConfigHelper.DeserializeFromConfigColorString(
                                this.MouseJumpScreenColor1),
                            color2: ConfigHelper.DeserializeFromConfigColorString(
                                this.MouseJumpScreenColor2)
                        )
                    ));
                var screens = ScreenHelper.GetAllScreens()
                    .Select(screen => screen.DisplayArea).ToList();
                var previewLayout = LayoutHelper.GetPreviewLayout(
                    previewStyle: previewStyle,
                    screens: screens,
                    activatedLocation: new(0, 0));
                var imageCopyService = new DesktopImageRegionCopyService();
                using var previewImage = DrawingHelper.RenderPreview(
                    previewLayout,
                    imageCopyService);

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
