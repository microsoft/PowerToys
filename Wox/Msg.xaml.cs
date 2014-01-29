using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timer = System.Threading.Timer;

namespace Wox
{
    public partial class Msg : Window
    {
        Storyboard fadeOutStoryboard = new Storyboard();
        private bool closing = false;

        public Msg()
        {
            InitializeComponent();

            Left = Screen.PrimaryScreen.WorkingArea.Right - this.Width;
            Top = Screen.PrimaryScreen.Bounds.Bottom;
            showAnimation.From = Screen.PrimaryScreen.Bounds.Bottom;
            showAnimation.To = Screen.PrimaryScreen.WorkingArea.Bottom - Height;

            // Create the fade out storyboard
            fadeOutStoryboard.Completed += new EventHandler(fadeOutStoryboard_Completed);
            DoubleAnimation fadeOutAnimation = new DoubleAnimation(Screen.PrimaryScreen.WorkingArea.Bottom - Height, Screen.PrimaryScreen.Bounds.Bottom, new Duration(TimeSpan.FromSeconds(0.3)))
            {
                AccelerationRatio = 0.2
            };
            Storyboard.SetTarget(fadeOutAnimation, this);
            Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(TopProperty));
            fadeOutStoryboard.Children.Add(fadeOutAnimation);

            imgClose.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\Images\\close.png"));
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
            if (!File.Exists(icopath))
            {
                icopath = AppDomain.CurrentDomain.BaseDirectory + "Images\\app.png";
            }
            imgIco.Source = new BitmapImage(new Uri(icopath));
            Show();

            Dispatcher.DelayInvoke("ShowMsg",
                                   o =>
                                   {
                                       if (!closing)
                                       {
                                           closing = true;
                                           Dispatcher.Invoke(new Action(fadeOutStoryboard.Begin));
                                       }
                                   }, TimeSpan.FromSeconds(3));
        }
    }
}
