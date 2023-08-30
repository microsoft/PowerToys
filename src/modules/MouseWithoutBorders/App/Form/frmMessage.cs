// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    public partial class FrmMessage : System.Windows.Forms.Form
    {
        private int lifeTime = 10;
        private int lifeSpent;

        public FrmMessage(string text, string bigText, int lifeTime)
        {
            InitializeComponent();

            Left = Top = 10;
            UpdateInfo(text, bigText, lifeTime);
        }

        public void UpdateInfo(string text, string bigText, int lifeTime)
        {
            textExtraInfo.Visible = !string.IsNullOrEmpty(bigText);
            textBoxMessage.Text = "\r\n" + text;
            textExtraInfo.Text = bigText;
            lifeSpent = 0;
            this.lifeTime = lifeTime;
        }

        public void UpdateBigText(string bigText)
        {
            textExtraInfo.Text = bigText;
        }

        private void TimerLife_Tick(object sender, EventArgs e)
        {
            if (lifeSpent++ >= lifeTime)
            {
                Common.HideTopMostMessage();
            }

            labelLifeTime.Text = (lifeTime - lifeSpent).ToString(CultureInfo.CurrentCulture);
        }

        private void FrmMessage_FormClosed(object sender, FormClosedEventArgs e)
        {
            Common.NullTopMostMessage();
        }
    }
}
