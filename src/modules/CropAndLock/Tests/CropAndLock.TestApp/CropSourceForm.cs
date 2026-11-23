// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;

namespace Microsoft.CropAndLock.TestApp
{
    internal sealed class CropSourceForm : Form
    {
        internal const string WindowTitlePrefix = "Crop And Lock UI test source";

        private readonly Font inputFont = new(FontFamily.GenericSansSerif, 18, GraphicsUnit.Pixel);

        internal CropSourceForm()
        {
            var work = Screen.PrimaryScreen!.WorkingArea;
            Text = $"{WindowTitlePrefix} {Guid.NewGuid():N}";
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(work.Left + 24, work.Top + 24, Math.Min(900, work.Width - 80), Math.Min(650, work.Height - 80));
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(235, 235, 235);

            var input = new TextBox
            {
                Name = "CropSourceInput",
                Bounds = InputRectangle,
                Font = inputFont,
            };
            var first = new Panel { Bounds = new Rectangle(100, 160, 155, 80), BackColor = Color.DarkBlue };
            var second = new Panel { Bounds = new Rectangle(285, 160, 155, 80), BackColor = Color.OrangeRed };
            input.TextChanged += (_, _) =>
            {
                first.BackColor = input.Text.StartsWith('w') ? Color.Gold : Color.DarkBlue;
                second.BackColor = input.Text.StartsWith('w') ? Color.DarkGreen : Color.OrangeRed;
            };
            Controls.AddRange([input, first, second]);
        }

        internal static Rectangle CropRectangle => new(80, 90, 380, 170);

        internal static Rectangle InputRectangle => new(100, 110, 340, 32);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                inputFont.Dispose();
            }
        }
    }
}
