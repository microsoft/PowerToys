using System;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace FastDelete.ShellExtension
{
    public class FooterPanel : TableLayoutPanel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            VisualStyleRenderer renderer = new VisualStyleRenderer("AEROWIZARD", 4, 0);
            renderer.DrawBackground(e.Graphics, e.ClipRectangle);
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            Invalidate();
        }
    }
}
