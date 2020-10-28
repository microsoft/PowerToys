// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using Microsoft.VisualStudio.Utilities;

namespace FancyZonesEditor
{
    public class OverlayWindowsManager
    {
        private LayoutOverlayWindow[] _layoutWindows;

        private LayoutPreview _layoutPreview;
        private UserControl _editor;

        private static MainWindow _mainWindow = new MainWindow();

        public Window CurrentLayoutWindow
        {
            get
            {
                return _layoutWindows[Settings.CurrentDesktopId];
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
                _layoutWindows[Settings.CurrentDesktopId].DataContext = value;
            }
        }

        private object _dataContext;

        public OverlayWindowsManager()
        {
            _layoutWindows = new LayoutOverlayWindow[Settings.DesktopsCount];

            _layoutPreview = new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 0.5,
            };

            var colors = new Brush[] { Brushes.Yellow, Brushes.Orange, Brushes.OrangeRed };

            for (int i = 0; i < Settings.DesktopsCount; i++)
            {
                _layoutWindows[i] = new LayoutOverlayWindow();

                if (Settings.DebugMode)
                {
                    _layoutWindows[i].Opacity = 0.5;
                    _layoutWindows[i].Background = colors[i % colors.Length];
                }

                var wa = WorkArea.GetWorkingArea(i);
                var workArea = DpiAwareness.DeviceToLogicalRect(_layoutWindows[i], wa);

                _layoutWindows[i].Left = workArea.X;
                _layoutWindows[i].Top = workArea.Y;
                _layoutWindows[i].Width = workArea.Width;
                _layoutWindows[i].Height = workArea.Height;
            }
        }

        public void Show()
        {
            ShowLayout();
            OpenMainWindow();
        }

        public void ShowLayout()
        {
            _layoutWindows[Settings.CurrentDesktopId].Content = _layoutPreview;
            _layoutWindows[Settings.CurrentDesktopId].DataContext = CurrentDataContext;

            for (int i = 0; i < Settings.DesktopsCount; i++)
            {
                _layoutWindows[i].Content = _layoutPreview;
                _layoutWindows[i].DataContext = CurrentDataContext;

                _layoutWindows[i].Show();
            }
        }

        public void OpenEditor()
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

            _layoutWindows[Settings.CurrentDesktopId].Content = _editor;
        }

        public void CloseEditor()
        {
            _editor = null;
            _layoutPreview = new LayoutPreview
            {
                IsActualSize = true,
                Opacity = 0.5,
            };

            _layoutWindows[Settings.CurrentDesktopId].Content = _layoutPreview;

            OpenMainWindow();
        }

        public void Update()
        {
            CloseLayout();
            _mainWindow.Update();
            ShowLayout();
        }

        public void CloseLayoutWindow()
        {
            for (int i = 0; i < Settings.DesktopsCount; i++)
            {
                _layoutWindows[i].Close();
            }
        }

        private void CloseLayout()
        {
            _layoutWindows[Settings.CurrentDesktopId].Content = null;
            _layoutWindows[Settings.CurrentDesktopId].DataContext = null;
        }

        private void OpenMainWindow()
        {
            // reset main window owner to keep it on the top
            _mainWindow.Owner = CurrentLayoutWindow;
            _mainWindow.ShowActivated = true;
            _mainWindow.Topmost = true;
            _mainWindow.Show();

            // window is set to topmost to make sure it shows on top of PowerToys settings page
            // we can reset topmost flag now
            _mainWindow.Topmost = false;
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
