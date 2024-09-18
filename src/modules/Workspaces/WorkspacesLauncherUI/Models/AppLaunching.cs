// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using ManagedCommon;
using Windows.Management.Deployment;

namespace WorkspacesLauncherUI.Models
{
    public class AppLaunching : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public string AppPath { get; set; }

        public bool Loading => LaunchState == "waiting";

        private Icon _icon;

        public Icon Icon
        {
            get
            {
                if (_icon == null)
                {
                    try
                    {
                        if (IsPackagedApp)
                        {
                            Uri uri = GetAppLogoByPackageFamilyName();
                            var bitmap = new Bitmap(uri.LocalPath);
                            var iconHandle = bitmap.GetHicon();
                            _icon = Icon.FromHandle(iconHandle);
                        }
                        else
                        {
                            _icon = Icon.ExtractAssociatedIcon(AppPath);
                        }
                    }
                    catch (Exception)
                    {
                        Logger.LogWarning($"Icon not found on app path: {AppPath}. Using default icon");
                        IsNotFound = true;
                        _icon = new Icon(@"images\DefaultIcon.ico");
                    }
                }

                return _icon;
            }
        }

        public string Name { get; set; }

        public string LaunchState { get; set; }

        public string StateGlyph
        {
            get => LaunchState switch
            {
                "launched" => "\U0000F78C",
                "failed" => "\U0000EF2C",
                _ => "\U0000EF2C",
            };
        }

        public System.Windows.Media.Brush StateColor
        {
            get => LaunchState switch
            {
                "launched" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 128, 0)),
                "failed" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 254, 0, 0)),
                _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 254, 0, 0)),
            };
        }

        private bool _isNotFound;

        [JsonIgnore]
        public bool IsNotFound
        {
            get
            {
                return _isNotFound;
            }

            set
            {
                if (_isNotFound != value)
                {
                    _isNotFound = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsNotFound)));
                }
            }
        }

        public Uri GetAppLogoByPackageFamilyName()
        {
            var pkgManager = new PackageManager();
            var pkg = pkgManager.FindPackagesForUser(string.Empty, PackagedId).FirstOrDefault();

            if (pkg == null)
            {
                return null;
            }

            return pkg.Logo;
        }

        private bool? _isPackagedApp;

        public string PackagedId { get; set; }

        public string PackagedName { get; set; }

        public string PackagedPublisherID { get; set; }

        public string Aumid { get; set; }

        public bool IsPackagedApp
        {
            get
            {
                if (_isPackagedApp == null)
                {
                    if (!AppPath.StartsWith("C:\\Program Files\\WindowsApps\\", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _isPackagedApp = false;
                    }
                    else
                    {
                        string appPath = AppPath.Replace("C:\\Program Files\\WindowsApps\\", string.Empty);
                        Regex packagedAppPathRegex = new Regex(@"(?<APPID>[^_]*)_\d+.\d+.\d+.\d+_x64__(?<PublisherID>[^\\]*)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        Match match = packagedAppPathRegex.Match(appPath);
                        _isPackagedApp = match.Success;
                        if (match.Success)
                        {
                            PackagedName = match.Groups["APPID"].Value;
                            PackagedPublisherID = match.Groups["PublisherID"].Value;
                            PackagedId = $"{PackagedName}_{PackagedPublisherID}";
                            Aumid = $"{PackagedId}!App";
                        }
                    }
                }

                return _isPackagedApp.Value;
            }
        }

        private BitmapImage _iconBitmapImage;

        public BitmapImage IconBitmapImage
        {
            get
            {
                if (_iconBitmapImage == null)
                {
                    try
                    {
                        Bitmap previewBitmap = new Bitmap(32, 32);
                        using (Graphics graphics = Graphics.FromImage(previewBitmap))
                        {
                            graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                            graphics.DrawIcon(Icon, new Rectangle(0, 0, 32, 32));
                        }

                        using (var memory = new MemoryStream())
                        {
                            previewBitmap.Save(memory, ImageFormat.Png);
                            memory.Position = 0;

                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                            bitmapImage.Freeze();

                            _iconBitmapImage = bitmapImage;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception while drawing icon for app with path: {AppPath}. Exception message: {e.Message}");
                    }
                }

                return _iconBitmapImage;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
