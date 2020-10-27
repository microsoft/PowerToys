// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FancyZonesEditor.Models;
using FancyZonesEditor.Utils;
using Microsoft.VisualStudio.Utilities;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for EditorOverlay.xaml
    /// </summary>
    public partial class EditorOverlay : Window
    {
        public static EditorOverlay Current { get; set; }

        public Window LayoutWindow
        {
            get
            {
                return _layoutWindows[Settings.CurrentDesktopId];
            }
        }

        private readonly Settings _settings = ((App)Application.Current).ZoneSettings;

        private LayoutOverlayWindow[] _layoutWindows;

        private LayoutPreview _layoutPreview;
        private UserControl _editor;

        private static MainWindow _mainWindow = new MainWindow();

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

        public EditorOverlay()
        {
            InitializeComponent();
            Current = this;

            _layoutWindows = new LayoutOverlayWindow[Settings.DesktopsCount];

            Rect workAreaUnion = WorkArea.WorkAreasUnion;
            Left = workAreaUnion.Left;
            Top = workAreaUnion.Top;
            Width = workAreaUnion.Width;
            Height = workAreaUnion.Height;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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

            ShowLayout();
            OpenMainWindow();
        }

        public void ShowLayout()
        {
            _layoutWindows[Settings.CurrentDesktopId].Content = _layoutPreview;
            _layoutWindows[Settings.CurrentDesktopId].DataContext = Current.DataContext;

            for (int i = 0; i < Settings.DesktopsCount; i++)
            {
                _layoutWindows[i].Show();
            }
        }

        // These event handlers are used to track the current state of the Shift and Ctrl keys on the keyboard
        // They reflect that current state into properties on the Settings object, which the Zone view will listen to in editing mode
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            _settings.IsShiftKeyPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _settings.IsCtrlKeyPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            _settings.IsShiftKeyPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _settings.IsCtrlKeyPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            base.OnPreviewKeyUp(e);
        }

        private void OpenMainWindow()
        {
            // reset main window owner to keep it on the top
            _mainWindow.Owner = this;
            _mainWindow.ShowActivated = true;
            _mainWindow.Topmost = true;
            _mainWindow.Show();

            // window is set to topmost to make sure it shows on top of PowerToys settings page
            // we can reset topmost flag now
            _mainWindow.Topmost = false;
        }

        public void OpenEditor()
        {
            _layoutPreview = null;
            if (DataContext is GridLayoutModel)
            {
                _editor = new GridEditor();
            }
            else if (DataContext is CanvasLayoutModel)
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

            Current.Close();
        }

        private void CloseLayout()
        {
            _layoutWindows[Settings.CurrentDesktopId].Content = null;
            _layoutWindows[Settings.CurrentDesktopId].DataContext = null;
        }

        private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_layoutWindows[Settings.CurrentDesktopId] != null)
            {
                _layoutWindows[Settings.CurrentDesktopId].DataContext = Current.DataContext;
            }
        }
    }
}
