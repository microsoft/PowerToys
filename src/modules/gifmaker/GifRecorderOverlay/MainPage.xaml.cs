using GifRecorderOverlay.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GifRecorderOverlay
{
    public sealed partial class MainPage : Page
    {
        private ResizableRectangle Rect;
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Cross, 1); 
            RecordPauseButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            if ((string)RecordPauseButton.Tag == "pause")
            {
                RecordPauseButton.Icon = new SymbolIcon(Symbol.Target);
                RecordPauseButton.Label = "Record";
                RecordPauseButton.Tag = "record";
            }
        }
        private void RecordPauseButton_Click(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 1); 
            if ((string)RecordPauseButton.Tag == "record")
            {
                RecordPauseButton.Icon = new SymbolIcon(Symbol.Pause);
                RecordPauseButton.Label = "Pause";
                RecordPauseButton.Tag = "pause";
                StopButton.IsEnabled = true;
                Rect.IsSelected = false;
            }
            else
            {
                RecordPauseButton.Icon = new SymbolIcon(Symbol.Target);
                RecordPauseButton.Label = "Record";
                RecordPauseButton.Tag = "record";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            RecordPauseButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            MainCanvas.Children.Remove(Rect);
            Rect = null;
            if ((string)RecordPauseButton.Tag == "pause")
            {
                RecordPauseButton.Icon = new SymbolIcon(Symbol.Target);
                RecordPauseButton.Label = "Record";
                RecordPauseButton.Tag = "record";
            }
        }

        private void MainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                MainCanvas.CapturePointer(e.Pointer);
                var ptrPoint = e.GetCurrentPoint(MainCanvas);
                var point1 = ptrPoint.Position;
                if (Rect != null)
                {
                    MainCanvas.Children.Remove(Rect);
                }
                Rect = new ResizableRectangle(MainCanvas, point1);
                MainCanvas.Children.Add(Rect);
                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }

        private void MainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var ptrPoint = e.GetCurrentPoint(MainCanvas);
                if (ptrPoint.Properties.IsLeftButtonPressed)
                {
                    if (Rect != null)
                    {
                        var point2 = ptrPoint.Position;
                        Rect.SetCoordinates(point2);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void MainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (Rect.IsZeroSize())
                {
                    MainCanvas.Children.Remove(Rect);
                    Rect = null;
                }
                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }
    }
}
