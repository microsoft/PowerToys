// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Windows.Forms;
using MouseWithoutBorders.Core;
using MouseWithoutBorders.Form.Settings;

namespace MouseWithoutBorders
{
    public partial class SettingsForm : System.Windows.Forms.Form
    {
        private bool _movingWindows;
        private Point _moveWindowOffset;
        private SettingsFormPage _currentPage;

        public SettingsForm()
        {
            InitializeComponent();

            toolTipManual.ToolTipTitle = Application.ProductName;
            Text = Application.ProductName;

            Logger.LogDebug("FIRST RUN, SHOWING THE FIRST SETUP PAGE.");

            Logger.LogDebug($"{nameof(Common.RunWithNoAdminRight)} = {Common.RunWithNoAdminRight}");

            if (Common.RunWithNoAdminRight)
            {
                SetControlPage(new SetupPage2aa());
            }
            else
            {
                SetControlPage(new SetupPage1());
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            MachineStuff.Settings = null;

            if (_currentPage != null)
            {
                Logger.LogDebug(_currentPage.Name + " closing.");
                _currentPage.OnPageClosing();
            }

            base.OnClosing(e);
        }

        internal SettingsFormPage GetCurrentPage()
        {
            return _currentPage;
        }

        internal void SetControlPage(SettingsFormPage page)
        {
            SuspendLayout();
            if (_currentPage != null)
            {
                _currentPage.NextPage -= PageNextPage;
                _currentPage.OnPageClosing();
                Controls.Remove(_currentPage);
            }

            if (page != null)
            {
                Logger.LogDebug("GOING TO NEXT PAGE: " + page.Name);
                page.BackColor = Color.Transparent;
                page.NextPage += PageNextPage;
                page.Location = contentPanel.Location;
                page.Visible = true;
                Controls.Add(page);
            }

            _currentPage = page;
            ResumeLayout(true);
            if (page == null)
            {
                Close();
            }
        }

        private void PageNextPage(object sender, PageEventArgs e)
        {
            SetControlPage(e.Page);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _movingWindows = true;
            _moveWindowOffset = new Point(e.X, e.Y);
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_movingWindows)
            {
                Point newLocation = Location;
                newLocation.X += e.X - _moveWindowOffset.X;
                newLocation.Y += e.Y - _moveWindowOffset.Y;
                Location = newLocation;
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_movingWindows)
            {
                _movingWindows = false;
            }

            base.OnMouseUp(e);
        }

        private void CloseWindowButtonClick(object sender, EventArgs e)
        {
            Close();
        }

        private string lastMessage = string.Empty;
        private static readonly string[] Separator = new string[] { "\r\n" };

        internal void ShowTip(ToolTipIcon icon, string msg, int durationInMilliseconds)
        {
            int x = 0;
            string text = msg + $"\r\n {(lastMessage.Equals(msg, StringComparison.OrdinalIgnoreCase) ? string.Empty : $"\r\nPrevious message/error: {lastMessage}")} ";
            lastMessage = msg;
            int y = (-text.Split(Separator, StringSplitOptions.None).Length * 15) - 30;

            toolTipManual.Hide(this);

            toolTipManual.ToolTipIcon = icon;
            toolTipManual.Show(text, this, x, y, durationInMilliseconds);
        }
    }
}
