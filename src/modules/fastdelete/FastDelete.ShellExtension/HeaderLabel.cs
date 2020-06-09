using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Sunburst.SharingManager.Controls
{
    public class MainInstructionLabel : Label
    {
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font
        {
            get => base.Font;
            set => throw new NotSupportedException();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color ForeColor
        {
            get => base.ForeColor;
            set => throw new NotSupportedException();
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            UpdateStyle();
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            UpdateStyle();
        }

        private void UpdateStyle()
        {
            VisualStyleRenderer renderer = new VisualStyleRenderer("TEXTSTYLE", 1, 0);
            base.ForeColor = renderer.GetColor(ColorProperty.TextColor);

            using var graphics = CreateGraphics();
            try
            {
                int hr = NativeMethods.GetThemeFont(renderer.Handle, graphics.GetHdc(), 1, 0, 210, out var lf);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                base.Font = Font.FromLogFont(lf);
            }
            finally
            {
                graphics.ReleaseHdc();
            }
        }

        // TODO: Once we move to .NET 5 Preview 6 or later, we can use FontProperty.TextFont
        // as introduced in https://github.com/dotnet/winforms/pull/3341.
        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct LOGFONT
            {
                public const int LF_FACESIZE = 32;
                public int lfHeight;
                public int lfWidth;
                public int lfEscapement;
                public int lfOrientation;
                public int lfWeight;
                public byte lfItalic;
                public byte lfUnderline;
                public byte lfStrikeOut;
                public byte lfCharSet;
                public byte lfOutPrecision;
                public byte lfClipPrecision;
                public byte lfQuality;
                public byte lfPitchAndFamily;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = LF_FACESIZE)]
                public string lfFaceName;
            }

            [DllImport("uxtheme", ExactSpelling = true, CharSet = CharSet.Unicode)]
            internal static extern int GetThemeFont(IntPtr hTheme, IntPtr hdc, int iPartId, int iStateId, int iPropId, out LOGFONT pFont);
        }
    }
}
