using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinAlfred.Plugin;
using Brush = System.Windows.Media.Brush;

namespace WinAlfred
{
    public partial class ResultItem : UserControl
    {
        private bool selected;

        public Result Result { get; private set; }

        public bool Selected
        {
            get
            {
                return selected;
            }
            set
            {
                selected = value;
                BrushConverter bc = new BrushConverter();
                Background = selected ? (Brush)(bc.ConvertFrom("#d1d1d1")) : (Brush)(bc.ConvertFrom("#ebebeb"));
                if (selected)
                {
                    img.Visibility = Visibility.Visible;
                    img.Source = new BitmapImage(new Uri(Directory.GetCurrentDirectory()+"\\Images\\enter.png"));
                }
                else
                {
                    img.Visibility = Visibility.Hidden;
                }
            }
        }

        public void SetIndex(int index)
        {
            tbIndex.Text = index.ToString();
        }

        public ResultItem(Result result)
        {

            InitializeComponent();
            Result = result;

            tbTitle.Text = result.Title;
            tbSubTitle.Text = result.SubTitle;
            string path = string.Empty;
            if (!string.IsNullOrEmpty(result.IcoPath) && result.IcoPath.Contains(":\\") && File.Exists(result.IcoPath))
            {
                path = result.IcoPath;
            }
            else if (!string.IsNullOrEmpty(result.IcoPath) && File.Exists(result.PluginDirectory + result.IcoPath))
            {
                path = result.PluginDirectory + result.IcoPath;
            }

            if (!string.IsNullOrEmpty(path))
            {
                if (path.ToLower().EndsWith(".exe") || path.ToLower().EndsWith(".lnk"))
                {
                    imgIco.Source = GetIcon(path);
                }
                else
                {
                    imgIco.Source = new BitmapImage(new Uri(path));
                }
            }
        }

        public static ImageSource GetIcon(string fileName)
        {
            Icon icon = Icon.ExtractAssociatedIcon(fileName);
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        new Int32Rect(0, 0, icon.Width, icon.Height),
                        BitmapSizeOptions.FromEmptyOptions());
        }
    }
}
