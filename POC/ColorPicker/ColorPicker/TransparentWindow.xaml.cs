using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;

using System;

namespace ColorPicker
{
    public partial class TransparentWindow : Window
    {

        private ActionBroker broker = new ActionBroker();
        public TransparentWindow()
        {
            InitializeComponent();
            this.Cursor = System.Windows.Input.Cursors.Cross;


            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            foreach (Screen s in Screen.AllScreens)
            {
                minX = Math.Min(minX, s.Bounds.X);
                maxX = Math.Max(maxX, s.Bounds.X + s.Bounds.Width);
                minY = Math.Min(minY, s.Bounds.Y);
                maxY = Math.Max(maxY, s.Bounds.Y + s.Bounds.Height);
            }

            Width = maxX - minX;
            Left = minX;
            Height = maxY - minY;
            Top = minY;
        }

        public void HandleMouseDown(object sender, EventArgs e)
        {
            broker.ActionTriggered(ActionBroker.ActionTypes.Click, sender, e);
        }

        internal void AddActionCallBack(ActionBroker.ActionTypes action, ActionBroker.Callback callback)
        {
            broker.AddCallBack(action, callback);
        }
    }
}

