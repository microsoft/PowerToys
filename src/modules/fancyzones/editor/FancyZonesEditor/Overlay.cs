// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using Microsoft.VisualStudio.Utilities;

namespace FancyZonesEditor
{
    public class Overlay
    {
        private MainWindow _mainWindow;

        private LayoutPreview _layoutPreview;
        private UserControl _editor;

        public List<Monitor> Monitors { get; private set; }

        public Rect WorkArea
        {
            get
            {
                if (Monitors.Count > 0 && CurrentDesktop < Monitors.Count)
                {
                    return Monitors[CurrentDesktop].Device.WorkAreaRect;
                }

                return default(Rect);
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

                return default(Window);
            }
        }

        public List<Rect> UsedWorkAreas { get; private set; }

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

                    var prevSettings = CurrentLayoutSettings;
                    _currentDesktop = value;

                    MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;
                    if (settings != null)
                    {
                        settings.UpdateDesktopDependantProperties(prevSettings);
                    }

                    Update();
                }
            }
        }

        private int _currentDesktop = 0;

        public bool SpanZonesAcrossMonitors
        {
            get
            {
                return _spanZonesAcrossMonitors;
            }

            set
            {
                _spanZonesAcrossMonitors = value;

                if (_spanZonesAcrossMonitors)
                {
                    Rect workArea = default(Rect);
                    Rect bounds = default(Rect);

                    foreach (Monitor monitor in Monitors)
                    {
                        workArea = Rect.Union(workArea, monitor.Device.WorkAreaRect);
                        bounds = Rect.Union(bounds, monitor.Device.Bounds);
                    }

                    Monitors.Clear();
                    Monitors.Add(new Monitor(bounds, workArea, true));
                }
            }
        }

        private bool _spanZonesAcrossMonitors;

        public Overlay()
        {
            UsedWorkAreas = new List<Rect>();
            Monitors = new List<Monitor>();

            var monitors = MonitorInfoUtils.GetMonitors();
            for (int i = 0; i < monitors.Length; i++)
            {
                var monitor = monitors[i];
                Add(monitor.Bounds, monitor.WorkArea, monitor.Primary);
            }
        }

        public void Show()
        {
            _layoutPreview = new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 0.5,
            };

            ShowLayout();
            OpenMainWindow();
        }

        public void ShowLayout()
        {
            UpdateSelectedLayoutModel();

            var window = CurrentLayoutWindow;
            window.Content = _layoutPreview;
            window.DataContext = CurrentDataContext;

            for (int i = 0; i < DesktopsCount; i++)
            {
                Monitors[i].Window.Show();
            }
        }

        public void OpenEditor(LayoutModel model)
        {
            _layoutPreview = null;
            if (CurrentDataContext is GridLayoutModel)
            {
                _editor = new GridEditor();
            }
            else if (CurrentDataContext is CanvasLayoutModel)
            {
                _editor = new CanvasEditor();
            }

            CurrentLayoutWindow.Content = _editor;

            EditorWindow window;
            if (model is GridLayoutModel)
            {
                window = new GridEditorWindow();
            }
            else
            {
                window = new CanvasEditorWindow();
            }

            window.Owner = Monitors[App.Overlay.CurrentDesktop].Window;
            window.DataContext = model;
            window.Show();
        }

        public void CloseEditor()
        {
            _editor = null;
            _layoutPreview = new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 0.5,
            };

            CurrentLayoutWindow.Content = _layoutPreview;

            OpenMainWindow();
        }

        public void CloseLayoutWindow()
        {
            for (int i = 0; i < DesktopsCount; i++)
            {
                Monitors[i].Window.Close();
            }
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

        private void UpdateSelectedLayoutModel()
        {
            LayoutModel foundModel = null;
            LayoutSettings currentApplied = CurrentLayoutSettings;

            MainWindowSettingsModel settings = ((App)Application.Current).MainWindowSettings;

            // reset previous selected layout
            foreach (LayoutModel model in MainWindowSettingsModel.CustomModels)
            {
                if (model.IsSelected)
                {
                    model.IsSelected = false;
                    break;
                }
            }

            foreach (LayoutModel model in settings.DefaultModels)
            {
                if (model.IsSelected)
                {
                    model.IsSelected = false;
                    break;
                }
            }

            // set new layout
            if (currentApplied.Type == LayoutType.Custom)
            {
                foreach (LayoutModel model in MainWindowSettingsModel.CustomModels)
                {
                    if ("{" + model.Guid.ToString().ToUpper() + "}" == currentApplied.ZonesetUuid.ToUpper())
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }
            else
            {
                foreach (LayoutModel model in settings.DefaultModels)
                {
                    if (model.Type == currentApplied.Type)
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = settings.DefaultModels[0];
            }

            foundModel.IsSelected = true;
            CurrentDataContext = foundModel;
        }

        private void Add(Rect bounds, Rect workArea, bool primary)
        {
            var monitor = new Monitor(bounds, workArea, primary);

            bool inserted = false;
            var workAreaRect = workArea;
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

        public Int32Rect[] GetZoneRects()
        {
            if (_editor != null)
            {
                if (_editor is GridEditor gridEditor)
                {
                    return ZoneRectsFromPanel(gridEditor.PreviewPanel);
                }
                else
                {
                    // CanvasEditor
                    return ZoneRectsFromPanel(((CanvasEditor)_editor).Preview);
                }
            }
            else
            {
                // One of the predefined zones (neither grid or canvas editor used).
                return _layoutPreview.GetZoneRects();
            }
        }

        private Int32Rect[] ZoneRectsFromPanel(Panel previewPanel)
        {
            // TODO: the ideal here is that the ArrangeRects logic is entirely inside the model, so we don't have to walk the UIElement children to get the rect info
            int count = previewPanel.Children.Count;
            Int32Rect[] zones = new Int32Rect[count];

            for (int i = 0; i < count; i++)
            {
                FrameworkElement child = (FrameworkElement)previewPanel.Children[i];
                Point topLeft = child.TransformToAncestor(previewPanel).Transform(default);

                zones[i].X = (int)topLeft.X;
                zones[i].Y = (int)topLeft.Y;
                zones[i].Width = (int)child.ActualWidth;
                zones[i].Height = (int)child.ActualHeight;
            }

            return zones;
        }
    }
}
