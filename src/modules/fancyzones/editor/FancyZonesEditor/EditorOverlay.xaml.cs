// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for EditorOverlay.xaml
    /// </summary>
    public partial class EditorOverlay : Window
    {
        public Int32Rect[] GetZoneRects()
        {
            // TODO: the ideal here is that the ArrangeRects logic is entirely inside the model, so we don't have to walk the UIElement children to get the rect info
            Panel previewPanel = null;

            if (_editor != null)
            {
                GridEditor gridEditor = _editor as GridEditor;
                if (gridEditor != null)
                {
                    previewPanel = gridEditor.PreviewPanel;
                }
                else
                {
                    //CanvasEditor
                    previewPanel = ((CanvasEditor)_editor).Preview;
                }
            }
            else
            {
                previewPanel = _layoutPreview.PreviewPanel;
            }

            var count = previewPanel.Children.Count;
            Int32Rect[] zones = new Int32Rect[count];

            int i = 0;
            foreach (FrameworkElement child in previewPanel.Children)
            {
                Point topLeft = child.TransformToAncestor(previewPanel).Transform(new Point());

                var right = topLeft.X + child.ActualWidth;
                var bottom = topLeft.Y + child.ActualHeight;
                zones[i].X = (int)topLeft.X;
                zones[i].Y = (int)topLeft.Y;
                zones[i].Width = (int)child.ActualWidth;
                zones[i].Height = (int)child.ActualHeight;
                i++;
            }

            return zones;
        }

        public static EditorOverlay Current;
        public EditorOverlay()
        {
            InitializeComponent();
            Current = this;

            Left = _settings.WorkArea.Left;
            Top = _settings.WorkArea.Top;
            Width = _settings.WorkArea.Width;
            Height = _settings.WorkArea.Height;
        }

        void onLoad(object sender, RoutedEventArgs e)
        {
            ShowLayoutPicker();
        }

        public void ShowLayoutPicker()
        {
            DataContext = null;

            _editor = null;
            _layoutPreview = new LayoutPreview();
            _layoutPreview.IsActualSize = true;
            _layoutPreview.Opacity = 0.5;
            Content = _layoutPreview;

            MainWindow window = new MainWindow();
            window.Owner = this;
            window.ShowActivated = true;
            window.Topmost = true;
            window.Show();

            // window is set to topmost to make sure it shows on top of PowerToys settings page
            // we can reset topmost flag now
            window.Topmost = false;
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

        public void Edit()
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

            Content = _editor;
        }

        private Settings _settings = ((App)Application.Current).ZoneSettings;
        private LayoutPreview _layoutPreview;
        private UserControl _editor;
    }
}
