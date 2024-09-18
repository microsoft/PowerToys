// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

using ManagedCommon;
using Windows.Management.Deployment;

namespace WorkspacesEditor.Models
{
    public class Application : INotifyPropertyChanged, IDisposable
    {
        private bool _isInitialized;

        public event PropertyChangedEventHandler PropertyChanged;

        public Application()
        {
        }

        public Application(Application other)
        {
            AppName = other.AppName;
            AppPath = other.AppPath;
            AppTitle = other.AppTitle;
            PackageFullName = other.PackageFullName;
            AppUserModelId = other.AppUserModelId;
            CommandLineArguments = other.CommandLineArguments;
            IsElevated = other.IsElevated;
            CanLaunchElevated = other.CanLaunchElevated;
            Minimized = other.Minimized;
            Maximized = other.Maximized;
            Position = other.Position;
            MonitorNumber = other.MonitorNumber;

            Parent = other.Parent;
            IsNotFound = other.IsNotFound;
            IsHighlighted = other.IsHighlighted;
            RepeatIndex = other.RepeatIndex;
            PackagedId = other.PackagedId;
            PackagedName = other.PackagedName;
            PackagedPublisherID = other.PackagedPublisherID;
            Aumid = other.Aumid;
            IsExpanded = other.IsExpanded;
            IsIncluded = other.IsIncluded;
        }

        public Project Parent { get; set; }

        public struct WindowPosition
        {
            public int X { get; set; }

            public int Y { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public static bool operator ==(WindowPosition left, WindowPosition right)
            {
                return left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
            }

            public static bool operator !=(WindowPosition left, WindowPosition right)
            {
                return left.X != right.X || left.Y != right.Y || left.Width != right.Width || left.Height != right.Height;
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                WindowPosition pos = (WindowPosition)obj;
                return X == pos.X && Y == pos.Y && Width == pos.Width && Height == pos.Height;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public string AppName { get; set; }

        public string AppPath { get; set; }

        public string AppTitle { get; set; }

        public string PackageFullName { get; set; }

        public string AppUserModelId { get; set; }

        public string CommandLineArguments { get; set; }

        private bool _isElevated;

        public bool IsElevated
        {
            get => _isElevated;
            set
            {
                _isElevated = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(AppMainParams)));
            }
        }

        public bool CanLaunchElevated { get; set; }

        internal void SwitchDeletion()
        {
            IsIncluded = !IsIncluded;
            RedrawPreviewImage();
        }

        private void RedrawPreviewImage()
        {
            if (_isInitialized)
            {
                Parent.Initialize(App.ThemeManager.GetCurrentTheme());
            }
        }

        private bool _minimized;

        public bool Minimized
        {
            get => _minimized;
            set
            {
                _minimized = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Minimized)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(EditPositionEnabled)));
                RedrawPreviewImage();
            }
        }

        private bool _maximized;

        public bool Maximized
        {
            get => _maximized;
            set
            {
                _maximized = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Maximized)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(EditPositionEnabled)));
                RedrawPreviewImage();
            }
        }

        public bool EditPositionEnabled { get => !Minimized && !Maximized; }

        private string _appMainParams;

        public string AppMainParams
        {
            get
            {
                _appMainParams = _isElevated ? Properties.Resources.Admin : string.Empty;
                if (!string.IsNullOrWhiteSpace(CommandLineArguments))
                {
                    _appMainParams += (_appMainParams == string.Empty ? string.Empty : " | ") + Properties.Resources.Args + ": " + CommandLineArguments;
                }

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsAppMainParamVisible)));
                return _appMainParams;
            }
        }

        public bool IsAppMainParamVisible { get => !string.IsNullOrWhiteSpace(_appMainParams); }

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

        [JsonIgnore]
        public bool IsHighlighted { get; set; }

        [JsonIgnore]
        public int RepeatIndex { get; set; }

        [JsonIgnore]
        public string RepeatIndexString
        {
            get
            {
                return RepeatIndex <= 1 ? string.Empty : RepeatIndex.ToString(CultureInfo.InvariantCulture);
            }
        }

        [JsonIgnore]
        private Icon _icon = null;

        [JsonIgnore]
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

        private WindowPosition _position;

        public WindowPosition Position
        {
            get => _position;
            set
            {
                _position = value;
                _scaledPosition = null;
            }
        }

        private WindowPosition? _scaledPosition;

        public WindowPosition ScaledPosition
        {
            get
            {
                if (_scaledPosition == null)
                {
                    double scaleFactor = MonitorSetup.Dpi / 96.0;
                    _scaledPosition = new WindowPosition()
                    {
                        X = (int)(scaleFactor * Position.X),
                        Y = (int)(scaleFactor * Position.Y),
                        Height = (int)(scaleFactor * Position.Height),
                        Width = (int)(scaleFactor * Position.Width),
                    };
                }

                return _scaledPosition.Value;
            }
        }

        public int MonitorNumber { get; set; }

        private MonitorSetup _monitorSetup;

        public MonitorSetup MonitorSetup
        {
            get
            {
                if (_monitorSetup == null)
                {
                    _monitorSetup = Parent.Monitors.Where(x => x.MonitorNumber == MonitorNumber).FirstOrDefault();
                }

                return _monitorSetup;
            }
        }

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public void InitializationFinished()
        {
            _isInitialized = true;
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

        private bool _isExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsExpanded)));
                }
            }
        }

        public string DeleteButtonContent { get => _isIncluded ? Properties.Resources.Delete : Properties.Resources.AddBack; }

        private bool _isIncluded = true;

        public bool IsIncluded
        {
            get => _isIncluded;
            set
            {
                if (_isIncluded != value)
                {
                    _isIncluded = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsIncluded)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(DeleteButtonContent)));
                    if (!_isIncluded)
                    {
                        IsExpanded = false;
                    }
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        internal void CommandLineTextChanged(string newCommandLineValue)
        {
            CommandLineArguments = newCommandLineValue;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(AppMainParams)));
        }

        internal void MaximizedChecked()
        {
            Minimized = false;
        }

        internal void MinimizedChecked()
        {
            Maximized = false;
        }
    }
}
