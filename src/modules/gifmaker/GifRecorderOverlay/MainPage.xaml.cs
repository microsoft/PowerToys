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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ResizableRectangle rect;
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
            mainCanvas.Visibility = 0;
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
            if ((string)RecordPauseButton.Tag == "pause")
            {
                RecordPauseButton.Icon = new SymbolIcon(Symbol.Target);
                RecordPauseButton.Label = "Record";
                RecordPauseButton.Tag = "record";
            }
        }

        private void mainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                mainCanvas.CapturePointer(e.Pointer);
                var ptrPoint = e.GetCurrentPoint(mainCanvas);
                var point1 = ptrPoint.Position;
                if (rect != null)
                {
                    mainCanvas.Children.Remove(rect);
                }
                rect = new ResizableRectangle(mainCanvas, point1);
                mainCanvas.Children.Add(rect);
                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }

        private void mainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var ptrPoint = e.GetCurrentPoint(mainCanvas);
                if (ptrPoint.Properties.IsLeftButtonPressed)
                {
                    if (rect != null)
                    {
                        var point2 = ptrPoint.Position;
                        rect.SetCoordinates(point2);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void mainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (rect.IsZeroSize())
                {
                    mainCanvas.Children.Remove(rect);
                    rect = null;
                }
                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }
    }
}
