using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wox.Helper;

namespace Wox
{
    public partial class Msg : Window
    {
        Storyboard fadeOutStoryboard = new Storyboard();
        private bool closing = false;

        public Msg()
        {
            InitializeComponent();
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dipWorkingArea = WindowIntelopHelper.TransformPixelsToDIP(this,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height);
            Left = dipWorkingArea.X - this.Width;
            Top = dipWorkingArea.Y;
            showAnimation.From = dipWorkingArea.Y;
            showAnimation.To = dipWorkingArea.Y - Height;

            // Create the fade out storyboard
            fadeOutStoryboard.Completed += new EventHandler(fadeOutStoryboard_Completed);
            DoubleAnimation fadeOutAnimation = new DoubleAnimation(dipWorkingArea.Y - Height, dipWorkingArea.Y, new Duration(TimeSpan.FromSeconds(0.3)))
            {
                AccelerationRatio = 0.2
            };
            Storyboard.SetTarget(fadeOutAnimation, this);
            Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(TopProperty));
            fadeOutStoryboard.Children.Add(fadeOutAnimation);


            imgClose.Source = new BitmapImage(new Uri("Images\\close.pn", UriKind.Relative));
            //imgClose.Source = new BitmapImage(new Uri(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Images\\close.png")));
            imgClose.MouseUp += imgClose_MouseUp;
        }

        void imgClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!closing)
            {
                closing = true;
                fadeOutStoryboard.Begin();
            }
        }

        private void fadeOutStoryboard_Completed(object sender, EventArgs e)
        {
            Close();
        }

        public void Show(string title, string subTitle, string icopath)
        {
            tbTitle.Text = title;
            tbSubTitle.Text = subTitle;
            if (string.IsNullOrEmpty(subTitle))
            {
                tbSubTitle.Visibility = Visibility.Collapsed;
            }
            if (!File.Exists(icopath))
            {
                imgIco.Source = new BitmapImage(new Uri("Images\\app.png", UriKind.Relative));
            }
            else {
                imgIco.Source = new BitmapImage(new Uri(icopath));
            }

            Show();

            Dispatcher.InvokeAsync(async () =>
                                   {
                                       if (!closing)
                                       {
                                           closing = true;
                                           await Dispatcher.InvokeAsync(fadeOutStoryboard.Begin);
                                       }
                                   });
        }
    }
}
