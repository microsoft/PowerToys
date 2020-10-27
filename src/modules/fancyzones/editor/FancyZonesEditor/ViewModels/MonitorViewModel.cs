// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using FancyZonesEditor.Utils;

namespace FancyZonesEditor.ViewModels
{
    public class MonitorViewModel : INotifyPropertyChanged
    {
        private const int MaxPreviewDisplaySize = 160;
        private const int MinPreviewDisplaySize = 98;

        public event PropertyChangedEventHandler PropertyChanged;

        public delegate void MonitorChangedEventHandler(MonitorChangedEventArgs args);

        public ObservableCollection<MonitorInfoModel> Monitors { get; set; }

        public static bool IsDesktopsPanelVisible
        {
            get
            {
                return Settings.DesktopsCount > 1 && !Settings.SpanZonesAcrossMonitors;
            }
        }

        public static double DesktopPreviewMultiplier { get; private set; }

        public Visibility DesktopsPanelVisibility
        {
            get
            {
                return IsDesktopsPanelVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public RelayCommand AddCommand { get; set; }

        public RelayCommand DeleteCommand { get; set; }

        public RelayCommand<MonitorInfoModel> SelectCommand { get; set; }

        public MonitorViewModel()
        {
            SelectCommand = new RelayCommand<MonitorInfoModel>(SelectCommandExecute, SelectCommandCanExecute);

            Monitors = new ObservableCollection<MonitorInfoModel>();
            double maxDimension = 0, minDimension = double.MaxValue;

            int i = 1;
            foreach (WorkAreaData screen in WorkArea.Monitors)
            {
                var bounds = screen.Bounds;
                maxDimension = System.Math.Max(System.Math.Max(maxDimension, bounds.Height), bounds.Width);
                minDimension = System.Math.Min(System.Math.Min(minDimension, bounds.Height), bounds.Width);

                Monitors.Add(new MonitorInfoModel(i, (int)bounds.Height, (int)bounds.Width, screen.Dpi, Settings.CurrentDesktopId == i - 1));
                i++;
            }

            double maxMultipier = MaxPreviewDisplaySize / maxDimension;
            double minMultipier = MinPreviewDisplaySize / minDimension;
            DesktopPreviewMultiplier = (minMultipier + maxMultipier) / 2;
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SelectCommandCanExecute(MonitorInfoModel monitorInfo)
        {
            return true;
        }

        private void SelectCommandExecute(MonitorInfoModel monitorInfo)
        {
            Monitors[Settings.CurrentDesktopId].Selected = false;
            Monitors[monitorInfo.Index - 1].Selected = true;

            Settings settings = ((App)Application.Current).ZoneSettings;
            settings.CurrentDesktop = monitorInfo.Index - 1;
            settings.UpdateSelectedLayoutModel();

            App.Overlay.Update();
        }
    }
}
