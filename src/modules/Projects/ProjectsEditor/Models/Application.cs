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

namespace ProjectsEditor.Models
{
    public class Application : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Project Parent { get; set; }

        public struct WindowPosition
        {
            public int X { get; set; }

            public int Y { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }
        }

        public IntPtr Hwnd { get; set; }

        public string AppPath { get; set; }

        public string AppTitle { get; set; }

        public string CommandLineArguments { get; set; }

        public bool Minimized { get; set; }

        public bool Maximized { get; set; }

        [JsonIgnore]
        public bool IsSelected { get; set; }

        [JsonIgnore]
        public bool IsHighlighted { get; set; }

        [JsonIgnore]
        public int RepeatIndex { get; set; }

        [JsonIgnore]
        public string RepeatIndexString
        {
            get
            {
                return RepeatIndex == 0 ? string.Empty : RepeatIndex.ToString(CultureInfo.InvariantCulture);
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
                        _icon = Icon.ExtractAssociatedIcon(AppPath);
                    }
                    catch (Exception)
                    {
                        _icon = new Icon(@"images\DefaultIcon.ico");
                    }
                }

                return _icon;
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
                    catch (Exception)
                    {
                        // todo
                    }
                }

                return _iconBitmapImage;
            }
        }

        [JsonIgnore]
        public string AppName
        {
            get
            {
                if (File.Exists(AppPath))
                {
                    return Path.GetFileNameWithoutExtension(AppPath);
                }

                return AppPath.Split('\\').LastOrDefault();
            }
        }

        public WindowPosition Position { get; set; }

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

        internal bool IsMyAppPath(string path)
        {
            if (!IsPackagedApp)
            {
                return path.Equals(AppPath, StringComparison.Ordinal);
            }
            else
            {
                return path.Contains(PackagedName + "_", StringComparison.InvariantCultureIgnoreCase) && path.Contains(PackagedPublisherID, StringComparison.InvariantCultureIgnoreCase);
            }
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

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
