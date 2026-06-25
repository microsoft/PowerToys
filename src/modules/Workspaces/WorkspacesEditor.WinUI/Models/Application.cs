// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

using Microsoft.UI.Xaml.Media.Imaging;

using WorkspacesCsharpLibrary.Models;
using WorkspacesEditor.Helpers;

namespace WorkspacesEditor.Models
{
    public enum WindowPositionKind
    {
        Custom = 0,
        Maximized = 1,
        Minimized = 2,
    }

    public partial class Application : BaseApplication, IDisposable
    {
        private bool _isInitialized;

        public Application()
        {
        }

        public Application(Application other)
        {
            Id = other.Id;
            AppName = other.AppName;
            AppPath = other.AppPath;
            AppTitle = other.AppTitle;
            PackageFullName = other.PackageFullName;
            AppUserModelId = other.AppUserModelId;
            PwaAppId = other.PwaAppId;
            CommandLineArguments = other.CommandLineArguments;
            IsElevated = other.IsElevated;
            CanLaunchElevated = other.CanLaunchElevated;
            Minimized = other.Minimized;
            Maximized = other.Maximized;
            Position = other.Position;
            MonitorNumber = other.MonitorNumber;
            Version = other.Version;

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

            public override readonly bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                WindowPosition pos = (WindowPosition)obj;
                return X == pos.X && Y == pos.Y && Width == pos.Width && Height == pos.Height;
            }

            public override readonly int GetHashCode()
            {
                return HashCode.Combine(X, Y, Width, Height);
            }
        }

        public string Id { get; set; }

        public string AppName { get; set; }

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
                Parent?.InitializePreview();
            }
        }

        public bool Minimized { get; set; }

        public bool Maximized { get; set; }

        public bool EditPositionEnabled => !Minimized && !Maximized;

        public int PositionComboboxIndex
        {
            get => Maximized ? (int)WindowPositionKind.Maximized : Minimized ? (int)WindowPositionKind.Minimized : (int)WindowPositionKind.Custom;
            set
            {
                Maximized = value == (int)WindowPositionKind.Maximized;
                Minimized = value == (int)WindowPositionKind.Minimized;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(EditPositionEnabled)));
                RedrawPreviewImage();
            }
        }

        private string _appMainParams;

        public string AppMainParams
        {
            get
            {
                string adminStr = ResourceLoaderInstance.ResourceLoader?.GetString("Admin") ?? "Admin";
                string argsStr = ResourceLoaderInstance.ResourceLoader?.GetString("Args") ?? "Args";

                _appMainParams = _isElevated ? adminStr : string.Empty;
                if (!string.IsNullOrWhiteSpace(CommandLineArguments))
                {
                    _appMainParams += (_appMainParams == string.Empty ? string.Empty : " | ") + argsStr + ": " + CommandLineArguments;
                }

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsAppMainParamVisible)));
                return _appMainParams;
            }
        }

        public bool IsAppMainParamVisible => !string.IsNullOrWhiteSpace(_appMainParams);

        [JsonIgnore]
        public bool IsHighlighted { get; set; }

        [JsonIgnore]
        public int RepeatIndex { get; set; }

        [JsonIgnore]
        public string RepeatIndexString => RepeatIndex <= 1 ? string.Empty : RepeatIndex.ToString(CultureInfo.InvariantCulture);

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
                _monitorSetup ??= Parent.GetMonitorForApp(this);

                return _monitorSetup;
            }
        }

        public void InitializationFinished()
        {
            _isInitialized = true;
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

        public string DeleteButtonContent
        {
            get
            {
                string deleteStr = ResourceLoaderInstance.ResourceLoader?.GetString("Delete") ?? "Remove";
                string addBackStr = ResourceLoaderInstance.ResourceLoader?.GetString("AddBack") ?? "Add back";
                return _isIncluded ? deleteStr : addBackStr;
            }
        }

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

        private BitmapImage _iconImage;

        [JsonIgnore]
        public BitmapImage IconImage
        {
            get
            {
                if (_iconImage == null)
                {
                    _iconImage = IconHelper.TryGetExecutableIcon(AppPath);
                    if (_iconImage == null && !string.IsNullOrEmpty(AppPath))
                    {
                        IsNotFound = true;
                    }
                }

                return _iconImage;
            }
        }

        internal void CommandLineTextChanged(string newCommandLineValue)
        {
            CommandLineArguments = newCommandLineValue;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(AppMainParams)));
        }

        public string Version { get; set; }

        public new void Dispose()
        {
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
