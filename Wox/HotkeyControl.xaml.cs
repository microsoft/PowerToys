using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wox.Helper;
using Wox.Plugin;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Wox
{
    public partial class HotkeyControl : UserControl
    {
        public HotkeyControl()
        {
            InitializeComponent();
        }

        private void TbHotkey_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            //when alt is pressed, the real key should be e.SystemKey
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            string text = string.Empty;
            SpecialKeyState specialKeyState = new GloablHotkey().CheckModifiers();
            if (specialKeyState.AltPressed)
            {
                text += "Alt ";
            }
            if (specialKeyState.CtrlPressed)
            {
                text += "Ctrl ";
            }
            if (specialKeyState.ShiftPressed)
            {
                text += "Shift ";
            }
            if (specialKeyState.WinPressed)
            {
                text += "Win ";
            }
            if(IsKeyAChar(key))
            {
                text += e.Key.ToString();
            }
            if (key == Key.Space)
            {
                text += "Space";
            }

            tbHotkey.Text = text;
        }

        private static bool IsKeyAChar(Key key)
        {
            return key >= Key.A && key <= Key.Z;
        }
    }
}
