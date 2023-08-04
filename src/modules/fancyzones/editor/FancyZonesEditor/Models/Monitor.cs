// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using FancyZonesEditor.Utils;

namespace FancyZonesEditor.Models
{
    public class Monitor
    {
        public LayoutOverlayWindow Window { get; private set; }

        public Device Device { get; set; }

        public LayoutSettings Settings
        {
            get
            {
                if (_settings != null)
                {
                    return _settings;
                }

                return DefaultLayoutSettings;
            }

            set
            {
                _settings = value;
            }
        }

        public bool IsInitialized
        {
            get
            {
                return _settings != null;
            }
        }

        public MonitorConfigurationType MonitorConfigurationType
        {
            get
            {
                return Device.MonitorSize.Width > Device.MonitorSize.Height ? MonitorConfigurationType.Horizontal : MonitorConfigurationType.Vertical;
            }
        }

        public Monitor(Rect workArea, Size monitorSize)
        {
            Window = new LayoutOverlayWindow();
            Device = new Device(workArea, monitorSize);

            if (App.DebugMode)
            {
                long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                PropertyInfo[] properties = typeof(Brushes).GetProperties();
                Window.Opacity = 0.5;
                Window.Background = (Brush)properties[milliseconds % properties.Length].GetValue(null, null);
            }

            Window.KeyUp += ((App)Application.Current).App_KeyUp;
            Window.KeyDown += ((App)Application.Current).App_KeyDown;

            Window.Left = workArea.X;
            Window.Top = workArea.Y;
            Window.Width = workArea.Width;
            Window.Height = workArea.Height;
        }

        public Monitor(string monitorName, string monitorInstanceId, string monitorSerialNumber, string virtualDesktop, int dpi, Rect workArea, Size monitorSize)
            : this(workArea, monitorSize)
        {
            Device = new Device(monitorName, monitorInstanceId, monitorSerialNumber, virtualDesktop, dpi, workArea, monitorSize);
        }

        private LayoutSettings _settings;

        public void Scale(double scaleFactor)
        {
            Device.Scale(scaleFactor);

            var workArea = Device.WorkAreaRect;
            Window.Left = workArea.X;
            Window.Top = workArea.Y;
            Window.Width = workArea.Width;
            Window.Height = workArea.Height;
        }

        public void SetLayoutSettings(LayoutModel model)
        {
            if (model == null)
            {
                return;
            }

            if (_settings == null)
            {
                _settings = new LayoutSettings();
            }

            _settings.ZonesetUuid = model.Uuid;
            _settings.Type = model.Type;
            _settings.SensitivityRadius = model.SensitivityRadius;
            _settings.ZoneCount = model.TemplateZoneCount;

            if (model is GridLayoutModel grid)
            {
                _settings.ShowSpacing = grid.ShowSpacing;
                _settings.Spacing = grid.Spacing;
            }
            else
            {
                _settings.ShowSpacing = false;
                _settings.Spacing = 0;
            }
        }

        private LayoutSettings DefaultLayoutSettings
        {
            get
            {
                LayoutSettings settings = new LayoutSettings();
                if (MonitorConfigurationType == MonitorConfigurationType.Vertical)
                {
                    settings.Type = LayoutType.Rows;
                }

                return settings;
            }
        }
    }
}
