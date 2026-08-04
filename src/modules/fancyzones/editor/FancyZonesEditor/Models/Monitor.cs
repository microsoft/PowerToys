// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

using FancyZonesEditor.Utils;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FancyZonesEditor.Models
{
    public class Monitor
    {
        private LayoutSettings _settings;
        private Rect _virtualWorkArea;

        public Monitor(Rect workArea, Size monitorSize)
        {
            Window = new LayoutOverlayWindow();
            Device = new Device(workArea, monitorSize);

            if (App.DebugMode)
            {
                long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                PropertyInfo[] properties = typeof(Colors).GetProperties();
                Window.OverlayOpacity = 0.5;
                Window.OverlayBackground = new SolidColorBrush((Windows.UI.Color)properties[milliseconds % properties.Length].GetValue(null, null));
            }

            var app = (App)Application.Current;
            Window.AddKeyHandlers(app.App_KeyDown, app.App_KeyUp);

            // Store for DPI-unaware positioning
            _virtualWorkArea = workArea;

            // The HWND already exists once the WinUI Window is constructed, so the overlay can be
            // positioned right away using the same DPI-unaware context as the C++ backend.
            ApplyWorkAreaPosition();
        }

        public Monitor(string monitorName, string monitorInstanceId, string monitorSerialNumber, string virtualDesktop, int dpi, Rect workArea, Size monitorSize)
            : this(workArea, monitorSize)
        {
            Device = new Device(monitorName, monitorInstanceId, monitorSerialNumber, virtualDesktop, dpi, workArea, monitorSize);
        }

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

        public void Scale(double scaleFactor)
        {
            Device.Scale(scaleFactor);

            _virtualWorkArea = Device.WorkAreaRect;
            ApplyWorkAreaPosition();
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

        private void ApplyWorkAreaPosition()
        {
            // Reposition window using DPI-unaware context to match the virtual coordinates
            // from the FancyZones C++ backend (which uses a DPI-unaware thread)
            NativeMethods.SetWindowPositionDpiUnaware(
                Window.Hwnd,
                (int)_virtualWorkArea.X,
                (int)_virtualWorkArea.Y,
                (int)_virtualWorkArea.Width,
                (int)_virtualWorkArea.Height);
        }
    }
}
