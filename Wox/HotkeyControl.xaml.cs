using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.i18n;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Wox
{
    public partial class HotkeyControl : UserControl
    {
        public HotkeyModel CurrentHotkey { get; private set; }
        public bool CurrentHotkeyAvailable { get; private set; }

        public event EventHandler OnHotkeyChanged;

        protected virtual void OnOnHotkeyChanged()
        {
            EventHandler handler = OnHotkeyChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public HotkeyControl()
        {
            InitializeComponent();
        }

        private void TbHotkey_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            tbMsg.Visibility = Visibility.Hidden;

            //when alt is pressed, the real key should be e.SystemKey
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            string text = string.Empty;
            SpecialKeyState specialKeyState = GlobalHotkey.Instance.CheckModifiers();
            if (specialKeyState.AltPressed)
            {
                text += "Alt";
            }
            if (specialKeyState.CtrlPressed)
            {
                text += string.IsNullOrEmpty(text) ? "Ctrl" : " + Ctrl";
            }
            if (specialKeyState.ShiftPressed)
            {
                text += string.IsNullOrEmpty(text) ? "Shift" : " + Shift";
            }
            if (specialKeyState.WinPressed)
            {
                text += string.IsNullOrEmpty(text) ? "Win" : " + Win";
            }
            if (string.IsNullOrEmpty(text))
            {
                text += "Ctrl + Alt";
            }

            if (IsKeyACharOrNumber(key))
            {
                text += " + " + key;
            }
            else if (key == Key.Space)
            {
                text += " + Space";
            }
            else
            {
                return;
            }

            if (text == tbHotkey.Text)
            {
                return;
            }

            Dispatcher.DelayInvoke("HotkeyAvailableTest", o => SetHotkey(text), TimeSpan.FromMilliseconds(300));
        }

        public void SetHotkey(string keyStr, bool triggerValidate = true)
        {
            tbMsg.Visibility = Visibility.Visible;
            tbHotkey.Text = keyStr;
            tbHotkey.Select(tbHotkey.Text.Length, 0);
            CurrentHotkey = new HotkeyModel(keyStr);

            if (triggerValidate)
            {
                CurrentHotkeyAvailable = CheckHotAvailabel(CurrentHotkey);
                if (!CurrentHotkeyAvailable)
                {
                    tbMsg.Foreground = new SolidColorBrush(Colors.Red);
                    tbMsg.Text = InternationalizationManager.Internationalization.GetTranslation("hotkeyUnavailable");
                }
                else
                {
                    tbMsg.Foreground = new SolidColorBrush(Colors.Green);
                    tbMsg.Text = InternationalizationManager.Internationalization.GetTranslation("succeed");
                }
                OnOnHotkeyChanged();
            }
        }

        private bool CheckHotAvailabel(HotkeyModel hotkey)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace("HotkeyAvailableTest", hotkey.CharKey, hotkey.ModifierKeys, OnHotkey);

                return true;
            }
            catch
            {
            }
            finally
            {
                HotkeyManager.Current.Remove("HotkeyAvailableTest");
            }

            return false;
        }

        private void OnHotkey(object sender, HotkeyEventArgs e)
        {

        }

        private static bool IsKeyACharOrNumber(Key key)
        {
            return (key >= Key.A && key <= Key.Z) || (key >= Key.D0 && key <= Key.D9);
        }
    }
}
