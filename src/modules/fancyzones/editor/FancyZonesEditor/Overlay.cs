// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FancyZonesEditor
{
    public class Overlay : IDisposable
    {
        private readonly LayoutBackup _layoutBackup = new LayoutBackup();

        private MainWindow _mainWindow;
        private LayoutPreview _layoutPreview;
        private UserControl _editorLayout;
        private EditorWindow _editorWindow;
        private GridEditorWindow _gridEditorWindow;
        private CanvasEditorWindow _canvasEditorWindow;
        private object _dataContext;
        private int _currentDesktop;
        private bool _isDisposed;

        public Overlay()
        {
            WorkAreas = new List<Rect>();
            Monitors = new List<Monitor>();
        }

        public List<Monitor> Monitors { get; private set; }

        public List<Rect> WorkAreas { get; private set; }

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

        public LayoutOverlayWindow CurrentLayoutWindow
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

        public object CurrentDataContext
        {
            get
            {
                return _dataContext;
            }

            set
            {
                _dataContext = value;
                CurrentLayoutWindow.OverlayDataContext = value;
            }
        }

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

                    // Detach the shared preview from the old monitor before CurrentLayoutWindow
                    // starts resolving to the new one. A WinUI element cannot have two parents.
                    CloseLayout();
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

        public bool SpanZonesAcrossMonitors { get; set; }

        public bool MultiMonitorMode
        {
            get
            {
                return DesktopsCount > 1 && !SpanZonesAcrossMonitors;
            }
        }

        public void Show()
        {
            Logger.LogTrace();

            if (DesktopsCount == 0)
            {
                // Parsing the editor parameters failed, so there is nothing to draw on. Bail out
                // rather than dereferencing the missing overlay window; the parsing error is
                // reported separately.
                Logger.LogError("No monitors were parsed from the editor parameters");
                return;
            }

            SuspendLayoutPreview();

            var mainWindowSettings = ((App)Application.Current).MainWindowSettings;
            _layoutPreview ??= new LayoutPreview
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
            window.OverlayContent = _layoutPreview;
            window.OverlayDataContext = CurrentDataContext;

            if (_layoutPreview != null)
            {
                _layoutPreview.AttachModel(CurrentDataContext as LayoutModel);
                _layoutPreview.UpdatePreview();
            }

            for (int i = 0; i < DesktopsCount; i++)
            {
                if (!Monitors[i].Window.Visible)
                {
                    Monitors[i].Window.Activate();

                    // Activate() alone does not reliably lift every overlay above the windows
                    // already on its monitor - Windows can refuse the foreground change - so each
                    // overlay is raised explicitly, without stealing activation from the picker.
                    NativeMethods.BringWindowToTopNoActivate(Monitors[i].Window.Hwnd);
                }
            }
        }

        public void OpenEditor(LayoutModel model)
        {
            Logger.LogTrace();

            // Context-menu and new-layout paths enter the zone editor without first opening the
            // properties dialog. Ensure every editor session has a matching cancellation backup,
            // while preserving the earlier backup when editing continues from that dialog.
            if (!_layoutBackup.Matches(model))
            {
                _layoutBackup.Backup(model);
            }

            SuspendLayoutPreview();

            if (model is GridLayoutModel grid)
            {
                _editorLayout = new GridEditor(grid);
                _gridEditorWindow ??= new GridEditorWindow(grid);
                _editorWindow = _gridEditorWindow;
            }
            else if (model is CanvasLayoutModel canvas)
            {
                _editorLayout = new CanvasEditor(canvas);
                _canvasEditorWindow ??= new CanvasEditorWindow(canvas);
                _editorWindow = _canvasEditorWindow;
            }

            CurrentLayoutWindow.OverlayContent = _editorLayout;

            _editorWindow.PrepareForEditing(model, Monitors[CurrentDesktop].Window.Hwnd);
        }

        public void CloseEditor()
        {
            Logger.LogTrace();

            _editorLayout = null;

            SuspendLayoutPreview();

            var mainWindowSettings = ((App)Application.Current).MainWindowSettings;
            _layoutPreview ??= new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 1,
            };

            mainWindowSettings.PropertyChanged += _layoutPreview.ZoneSettings_PropertyChanged;

            CurrentLayoutWindow.OverlayContent = _layoutPreview;
            _layoutPreview.AttachModel(CurrentDataContext as LayoutModel);
            _layoutPreview.UpdatePreview();

            OpenMainWindow();
        }

        private void SuspendLayoutPreview()
        {
            if (_layoutPreview == null)
            {
                return;
            }

            var mainWindowSettings = ((App)Application.Current).MainWindowSettings;
            mainWindowSettings.PropertyChanged -= _layoutPreview.ZoneSettings_PropertyChanged;
            _layoutPreview.DetachModel();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            SuspendLayoutPreview();
            _layoutBackup.Dispose();
            GC.SuppressFinalize(this);
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
            _editorWindow?.Activate();
        }

        public void StartEditing(LayoutModel model)
        {
            _layoutBackup.Backup(model);
        }

        public void EndEditing(LayoutModel modelToRestore)
        {
            if (modelToRestore != null)
            {
                _layoutBackup.Restore(modelToRestore);
            }

            _layoutBackup.Clear();
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

        private void Update()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Update();
            }

            ShowLayout();
        }

        private void CloseLayout()
        {
            var window = CurrentLayoutWindow;
            window.OverlayContent = null;
            window.OverlayDataContext = null;
        }

        private void OpenMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(SpanZonesAcrossMonitors, WorkArea);
            }

            // reset main window owner to keep it on the top
            _mainWindow.SetOwner(CurrentLayoutWindow.Hwnd);

            // The picker is concealed rather than hidden while a zone editor is open, so it has
            // to be revealed again here.
            _mainWindow.Reveal();

            // window is set to topmost to make sure it shows on top of PowerToys settings page
            // we can reset topmost flag right after it is shown
            _mainWindow.IsAlwaysOnTop = true;
            _mainWindow.Activate();
            _mainWindow.IsAlwaysOnTop = false;
        }
    }
}
