// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FancyZonesEditor.Logs;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class Overlay
    {
        private MainWindow _mainWindow;
        private LayoutPreview _layoutPreview;
        private UserControl _editorLayout;
        private EditorWindow _editorWindow;

        public List<Monitor> Monitors { get; private set; }

        public Rect WorkArea
        {
            get
            {
                if (Monitors.Count > 0 && CurrentDesktop < Monitors.Count)
                {
                    return Monitors[CurrentDesktop].Device.WorkAreaRect;
                }

                return default;
            }
        }

        public LayoutSettings CurrentLayoutSettings
        {
            get
            {
                if (Monitors.Count > 0 && CurrentDesktop < Monitors.Count)
                {
                    return Monitors[CurrentDesktop].Settings;
                }

                return new LayoutSettings();
            }
        }

        public Window CurrentLayoutWindow
        {
            get
            {
                if (Monitors.Count > 0 && CurrentDesktop < Monitors.Count)
                {
                    return Monitors[CurrentDesktop].Window;
                }

                return default;
            }
        }

        public List<Rect> WorkAreas { get; private set; }

        public object CurrentDataContext
        {
            get
            {
                return _dataContext;
            }

            set
            {
                _dataContext = value;
                CurrentLayoutWindow.DataContext = value;
            }
        }

        private object _dataContext;

        public int DesktopsCount
        {
            get
            {
                return Monitors.Count;
            }
        }

        public int CurrentDesktop
        {
            get
            {
                return _currentDesktop;
            }

            set
            {
                if (value != _currentDesktop)
                {
                    if (value < 0 || value >= DesktopsCount)
                    {
                        return;
                    }

                    _currentDesktop = value;

                    MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;
                    if (settings != null)
                    {
                        settings.SetAppliedModel(null);
                        settings.UpdateTemplateModels();
                    }

                    Update();
                }
            }
        }

        private int _currentDesktop;

        public bool SpanZonesAcrossMonitors { get; set; }

        public bool MultiMonitorMode
        {
            get
            {
                return DesktopsCount > 1 && !SpanZonesAcrossMonitors;
            }
        }

        public Overlay()
        {
            WorkAreas = new List<Rect>();
            Monitors = new List<Monitor>();
        }

        public void Show()
        {
            Logger.LogTrace();

            var mainWindowSettings = ((App)Application.Current).MainWindowSettings;
            if (_layoutPreview != null)
            {
                mainWindowSettings.PropertyChanged -= _layoutPreview.ZoneSettings_PropertyChanged;
            }

            _layoutPreview = new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 1,
            };

            mainWindowSettings.PropertyChanged += _layoutPreview.ZoneSettings_PropertyChanged;

            ShowLayout();
            OpenMainWindow();
        }

        public void ShowLayout()
        {
            Logger.LogTrace();

            MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;
            CurrentDataContext = settings.UpdateSelectedLayoutModel();

            var window = CurrentLayoutWindow;
            window.Content = _layoutPreview;
            window.DataContext = CurrentDataContext;

            if (_layoutPreview != null)
            {
                _layoutPreview.UpdatePreview();
            }

            for (int i = 0; i < DesktopsCount; i++)
            {
                if (!Monitors[i].Window.IsVisible)
                {
                    Monitors[i].Window.Show();
                }
            }
        }

        public void SetLayoutSettings(Monitor monitor, LayoutModel model)
        {
            if (model == null)
            {
                return;
            }

            monitor.Settings.ZonesetUuid = model.Uuid;
            monitor.Settings.Type = model.Type;
            monitor.Settings.SensitivityRadius = model.SensitivityRadius;
            monitor.Settings.ZoneCount = model.TemplateZoneCount;

            if (model is GridLayoutModel grid)
            {
                monitor.Settings.ShowSpacing = grid.ShowSpacing;
                monitor.Settings.Spacing = grid.Spacing;
            }
            else
            {
                monitor.Settings.ShowSpacing = false;
                monitor.Settings.Spacing = 0;
            }
        }

        public void OpenEditor(LayoutModel model)
        {
            Logger.LogTrace();

            _layoutPreview = null;
            if (CurrentDataContext is GridLayoutModel)
            {
                _editorLayout = new GridEditor();
            }
            else if (CurrentDataContext is CanvasLayoutModel)
            {
                _editorLayout = new CanvasEditor();
            }

            CurrentLayoutWindow.Content = _editorLayout;

            if (model is GridLayoutModel)
            {
                _editorWindow = new GridEditorWindow();
            }
            else
            {
                _editorWindow = new CanvasEditorWindow();
            }

            _editorWindow.Owner = Monitors[App.Overlay.CurrentDesktop].Window;
            _editorWindow.DataContext = model;
            _editorWindow.Show();
        }

        public void CloseEditor()
        {
            Logger.LogTrace();

            var mainWindowSettings = ((App)Application.Current).MainWindowSettings;

            _editorLayout = null;

            if (_layoutPreview != null)
            {
                mainWindowSettings.PropertyChanged -= _layoutPreview.ZoneSettings_PropertyChanged;
            }

            _layoutPreview = new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 1,
            };

            mainWindowSettings.PropertyChanged += _layoutPreview.ZoneSettings_PropertyChanged;

            CurrentLayoutWindow.Content = _layoutPreview;

            OpenMainWindow();
        }

        public void FocusEditor()
        {
            if (_editorLayout == null)
            {
                return;
            }

            if (_editorLayout is CanvasEditor canvasEditor)
            {
                canvasEditor.FocusZone();
            }
            else if (_editorLayout is GridEditor gridEditor)
            {
                gridEditor.FocusZone();
            }
        }

        public void FocusEditorWindow()
        {
            if (_editorWindow != null)
            {
                _editorWindow.Focus();
            }
        }

        public void CloseLayoutWindow()
        {
            for (int i = 0; i < DesktopsCount; i++)
            {
                Monitors[i].Window.Close();
            }
        }

        public double ScaleCoordinateWithCurrentMonitorDpi(double coordinate)
        {
            if (Monitors.Count == 0)
            {
                return coordinate;
            }

            double minimalDpi = Monitors[0].Device.Dpi;
            foreach (Monitor monitor in Monitors)
            {
                if (minimalDpi > monitor.Device.Dpi)
                {
                    minimalDpi = monitor.Device.Dpi;
                }
            }

            if (minimalDpi == 0 || Monitors[CurrentDesktop].Device.Dpi == 0)
            {
                return coordinate;
            }

            double scaleFactor = minimalDpi / Monitors[CurrentDesktop].Device.Dpi;
            return Math.Round(coordinate * scaleFactor);
        }

        private void Update()
        {
            CloseLayout();

            if (_mainWindow != null)
            {
                _mainWindow.Update();
            }

            ShowLayout();
        }

        private void CloseLayout()
        {
            var window = CurrentLayoutWindow;
            window.Content = null;
            window.DataContext = null;
        }

        private void OpenMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(SpanZonesAcrossMonitors, WorkArea);
            }

            // reset main window owner to keep it on the top
            _mainWindow.Owner = CurrentLayoutWindow;
            _mainWindow.ShowActivated = true;
            _mainWindow.Topmost = true;
            _mainWindow.Show();

            // window is set to topmost to make sure it shows on top of PowerToys settings page
            // we can reset topmost flag now
            _mainWindow.Topmost = false;
        }

        public void AddMonitor(Monitor monitor)
        {
            bool inserted = false;
            var workAreaRect = monitor.Device.WorkAreaRect;
            for (int i = 0; i < Monitors.Count && !inserted; i++)
            {
                var rect = Monitors[i].Device.WorkAreaRect;
                if (workAreaRect.Left < rect.Left && (workAreaRect.Top <= rect.Top || workAreaRect.Top == 0))
                {
                    Monitors.Insert(i, monitor);
                    inserted = true;
                }
                else if (workAreaRect.Left == rect.Left && workAreaRect.Top < rect.Top)
                {
                    Monitors.Insert(i, monitor);
                    inserted = true;
                }
            }

            if (!inserted)
            {
                Monitors.Add(monitor);
            }
        }
    }
}
