using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;

namespace ColorPicker
{
    public partial class TransparentWindow : Window
    {
        private ActionBroker _broker = new ActionBroker();

        public TransparentWindow()
        {
            InitializeComponent();
            this.Cursor = System.Windows.Input.Cursors.Cross;
            SetWindowSizeToCoverAllScreens();
        }

        public void AddActionCallback(ActionBroker.ActionTypes action, ActionBroker.Callback callback)
        {
            _broker.AddCallback(action, callback);
        }

        private void OnMouseDown(object sender, EventArgs e)
        {
            _broker.ActionTriggered(ActionBroker.ActionTypes.Click, sender, e);
        }

        private void SetWindowSizeToCoverAllScreens()
        {
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
    }
}

