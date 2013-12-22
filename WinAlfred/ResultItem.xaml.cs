using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinAlfred.Plugin;

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
                Background = selected ? Brushes.Gray : Brushes.White;
            }
        }

        public ResultItem(Result result)
        {

            InitializeComponent();
            Result = result;

            tbTitle.Text = result.Title;
            tbSubTitle.Text = result.SubTitle;
            if (!string.IsNullOrEmpty(result.IcoPath))
            {
                imgIco.Source = new BitmapImage(new Uri(result.PluginDirectory + result.IcoPath));
            }
        }
    }
}
