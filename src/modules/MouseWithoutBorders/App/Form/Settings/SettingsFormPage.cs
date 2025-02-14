// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Forms;

using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using MouseWithoutBorders.Form.Settings;

namespace MouseWithoutBorders
{
    public partial class SettingsFormPage : UserControl
    {
        public event EventHandler<PageEventArgs> NextPage;

        protected bool BackButtonVisible
        {
            get => BackButton.Visible;
            set => BackButton.Visible = value;
        }

        public SettingsFormPage()
        {
            InitializeComponent();
        }

        public virtual void OnPageClosing()
        {
        }

        protected void SendNextPage(SettingsFormPage page)
        {
            NextPage?.Invoke(this, new PageEventArgs(page));
        }

        protected virtual SettingsFormPage CreateBackPage()
        {
            return null;
        }

        protected string GetSecureKey()
        {
            return Common.MyKey;
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            SendNextPage(CreateBackPage());
        }

        private void ButtonSkip_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "It is strongly recommended that you complete the setup first! Are you sure you want to skip these steps?",
                Application.ProductName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                Setting.Values.FirstRun = false;
                MachineStuff.CloseSetupForm();
                MachineStuff.ShowMachineMatrix();
            }
        }
    }
}
