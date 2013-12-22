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
                BrushConverter bc = new BrushConverter();
                Background = selected ? (Brush)(bc.ConvertFrom("#d1d1d1")) : (Brush)(bc.ConvertFrom("#ebebeb"));
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
            if (!string.IsNullOrEmpty(result.IcoPath))
            {
                imgIco.Source = new BitmapImage(new Uri(result.PluginDirectory + result.IcoPath));
            }
        }
    }
}
