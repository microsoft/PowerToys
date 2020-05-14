using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AnimatedGifRecorder.Views
{
    /// <summary>
    /// Interaction logic for SampleToolbar.xaml
    /// </summary>
    public partial class SampleToolbar : UserControl
    {
        public SampleToolbar()
        {
            InitializeComponent();
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Cross;
            RecordPauseButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            if ((string)RecordPauseButton.Tag == "Pause")
            {
                // RecordPauseButton.Template.FindName("RecordPauseImage", RecordPauseButton).SetValue(Image.SourceProperty, new BitmapImage(new Uri(@"media-record-8x.png", UriKind.Relative)));
                RecordPauseButton.Tag = "Record";
            }
        }

        private void RecordPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if ((string)RecordPauseButton.Tag == "Record")
            {
                // RecordPauseButton.Icon = new SymbolIcon(Symbol.Pause);
                RecordPauseButton.Tag = "Pause";
                StopButton.IsEnabled = true;
            }
            else
            {
                // RecordPauseButton.Icon = new SymbolIcon(Symbol.Target);
                RecordPauseButton.Tag = "Record";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            RecordPauseButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            if ((string)RecordPauseButton.Tag == "pause")
            {
                // RecordPauseButton.Icon = new SymbolIcon(Symbol.Target);
                RecordPauseButton.Tag = "record";
            }
        }
    }
}
