// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.Common.Helpers;
using Peek.FilePreviewer.Previewers.Drive.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Controls
{
    [INotifyPropertyChanged]
    public sealed partial class DriveControl : UserControl
    {
        [ObservableProperty]
        private Rect _spaceBarClip;

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(DrivePreviewData),
            typeof(DriveControl),
            new PropertyMetadata(null, new PropertyChangedCallback((d, e) => ((DriveControl)d).UpdateSpaceBar())));

        public DrivePreviewData? Source
        {
            get { return (DrivePreviewData)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public DriveControl()
        {
            this.InitializeComponent();
        }

        public string FormatType(string type)
        {
            return string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Drive_Type"), type);
        }

        public string FormatFileSystem(string fileSystem)
        {
            return string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Drive_FileSystem"), fileSystem);
        }

        public string FormatCapacity(ulong capacity)
        {
            return string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Drive_Capacity"), ReadableStringHelper.BytesToReadableString(capacity, false));
        }

        public string FormatFreeSpace(ulong freeSpace)
        {
            return string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Drive_FreeSpace"), ReadableStringHelper.BytesToReadableString(freeSpace, false));
        }

        public string FormatUsedSpace(ulong usedSpace)
        {
            return string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Drive_UsedSpace"), ReadableStringHelper.BytesToReadableString(usedSpace, false));
        }

        private void SizeChanged_Handler(object sender, SizeChangedEventArgs e)
        {
            UpdateSpaceBar();
        }

        private void UpdateSpaceBar()
        {
            if (Source != null && Source.PercentageUsage > 0)
            {
                var usedWidth = CapacityBar.ActualWidth * Source!.PercentageUsage;
                SpaceBarClip = new(0, 0, usedWidth, 16);
            }
            else
            {
                SpaceBarClip = new(0, 0, 0, 0);
            }
        }
    }
}
