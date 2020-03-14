using Microsoft.PowerToys.Settings.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ImageResizerPage : Page
    {
        public ImageResizerViewModel ViewModel { get; } = new ImageResizerViewModel();

        public ObservableCollection<ResizeSize> Sizes { get; set; }

        public ImageResizerPage()
        {
            this.InitializeComponent();

            Sizes = new ObservableCollection<ResizeSize>();
            Sizes.Add(new ResizeSize() { Title = "Small", Width = 854, Height = 480 });
            Sizes.Add(new ResizeSize() { Title = "Medium", Width = 1366, Height = 768 });
            Sizes.Add(new ResizeSize() { Title = "Large", Width = 1920, Height = 1080 });
            Sizes.Add(new ResizeSize() { Title = "Phone", Width = 320, Height = 569 });
        }

        private void AddSizeButton_Click(object sender, RoutedEventArgs e)
        {
            Sizes.Add(new ResizeSize() { Title = "", Width = 0, Height = 0 });
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            Button DelBtn = sender as Button;

            ResizeSize S = DelBtn.DataContext as ResizeSize;
            Sizes.Remove(S);
        }
    }

    public class ResizeSize
    {
        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}