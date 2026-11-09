// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;

namespace Microsoft.Workspaces.TestApp
{
    internal sealed class WorkspacesForm : Form
    {
        internal WorkspacesForm(string title, string payload, bool minimized)
        {
            Text = title;
            Name = "WorkspacesTestWindow";
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = new Rectangle(100, 120, 640, 420);
            WindowState = minimized ? FormWindowState.Minimized : FormWindowState.Normal;

            var titleLabel = new Label
            {
                Name = "TitleLabel",
                Text = title,
                UseMnemonic = false,
                AutoEllipsis = true,
                Bounds = new Rectangle(20, 20, ClientSize.Width - 40, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            var payloadLabel = new Label
            {
                Text = "Payload",
                AutoSize = true,
                Location = new Point(20, 84),
            };
            var payloadTextBox = new TextBox
            {
                Name = "PayloadTextBox",
                AccessibleName = "Payload",
                Text = payload,
                ReadOnly = true,
                Multiline = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both,
                Bounds = new Rectangle(20, 110, ClientSize.Width - 40, ClientSize.Height - 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.AddRange([titleLabel, payloadLabel, payloadTextBox]);
        }
    }
}
