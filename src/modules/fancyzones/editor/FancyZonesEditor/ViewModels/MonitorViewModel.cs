// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FancyZonesEditor.Utils;

namespace FancyZonesEditor.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        private const int MaxPreviewDisplaySize = 180;
        private const int MinPreviewDisplaySize = 120;

        public delegate void MonitorChangedEvent(MonitorChangedEventArgs args);

        public ObservableCollection<MonitorInfoModel> MonitorInfoForViewModel { get; set; }

        public static double DesktopPreviewMultiplier { get; private set; }

        public MonitorViewModel()
        {
            MonitorInfoForViewModel = new ObservableCollection<MonitorInfoModel>();
            double maxDimension = 0, minDimension = double.MaxValue;

            int i = 1;
            foreach (var monitor in App.Overlay.Monitors)
            {
                Device device = monitor.Device;
                var size = device.MonitorSize;
                maxDimension = System.Math.Max(System.Math.Max(maxDimension, size.Height), size.Width);
                minDimension = System.Math.Min(System.Math.Min(minDimension, size.Height), size.Width);

                MonitorInfoForViewModel.Add(new MonitorInfoModel(i, (int)size.Height, (int)size.Width, device.Dpi, App.Overlay.CurrentDesktop == i - 1));
                i++;
            }

            double maxMultiplier = MaxPreviewDisplaySize / maxDimension;
            double minMultiplier = MinPreviewDisplaySize / minDimension;
            DesktopPreviewMultiplier = (minMultiplier + maxMultiplier) / 2.5;
        }

        /// <summary>
        /// Selects the monitor the user clicked and makes it the current desktop.
        /// The generated <c>SelectCommand</c> replaces the hand-written
        /// <c>RelayCommand&lt;MonitorInfoModel&gt;</c> the WPF editor used.
        /// </summary>
        /// <param name="monitorInfo">The clicked monitor, or <see langword="null"/> when the click hit empty space.</param>
        [RelayCommand]
        private void Select(MonitorInfoModel monitorInfo)
        {
            if (monitorInfo == null)
            {
                return;
            }

            MonitorInfoForViewModel[App.Overlay.CurrentDesktop].Selected = false;
            MonitorInfoForViewModel[monitorInfo.Index - 1].Selected = true;

            App.Overlay.CurrentDesktop = monitorInfo.Index - 1;
        }
    }
}
