using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinAlfred.Helper;

namespace WinAlfreds.Helper
{
    public sealed class KeyboardHook : IDisposable
    {
        // Registers a hot key with Windows.
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        // Unregisters the hot key with Windows.
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        /// <summary>
        /// Represents the window that is used internally to get the messages.
        /// </summary>
        private class Window : NativeWindow, IDisposable
        {
            private static int wmHotkey = 0x0312;

            public Window()
            {
                // create the handle for the window.
                CreateHandle(new CreateParams());
            }

            /// <summary>
            /// Overridden to get the notifications.
            /// </summary>
            /// <param name="m"></param>
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                // check if we got a hot key pressed.
                if (m.Msg == wmHotkey)
                {
                    // get the keys.
                    Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                    XModifierKeys xModifier = (XModifierKeys)((int)m.LParam & 0xFFFF);

                    // invoke the event to notify the parent.
                    if (KeyPressed != null)
                        KeyPressed(this, new KeyPressedEventArgs(xModifier, key));
                }
            }

            public event EventHandler<KeyPressedEventArgs> KeyPressed;

            #region IDisposable Members

            public void Dispose()
            {
                DestroyHandle();
            }

            #endregion
        }

        private Window window = new Window();
        private int currentId;

        public KeyboardHook()
        {
            // register the event of the inner native window.
            window.KeyPressed += delegate(object sender, KeyPressedEventArgs args)
            {
                if (KeyPressed != null)
                    KeyPressed(this, args);
            };
        }

        /// <summary>
        /// Registers a hot key in the system.
        /// </summary>
        /// <param name="xModifier">The modifiers that are associated with the hot key.</param>
        /// <param name="key">The key itself that is associated with the hot key.</param>
        public void RegisterHotKey(XModifierKeys xModifier, Keys key)
        {
            // increment the counter.
            currentId = currentId + 1;

            // register the hot key.
            if (!RegisterHotKey(window.Handle, currentId, (uint)xModifier, (uint)key))
            {
                Log.Error("Couldn’t register the hot key.");
#if (DEBUG)
                {
                    throw new InvalidOperationException("Couldn’t register the hot key.");
                }
#endif
            }
        }

        /// <summary>
        /// A hot key has been pressed.
        /// </summary>
        public event EventHandler<KeyPressedEventArgs> KeyPressed;

        #region IDisposable Members

        public void Dispose()
        {
            // unregister all the registered hot keys.
            for (int i = currentId; i > 0; i--)
            {
                UnregisterHotKey(window.Handle, i);
            }

            // dispose the inner native window.
            window.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Event Args for the event that is fired after the hot key has been pressed.
    /// </summary>
    public class KeyPressedEventArgs : EventArgs
    {
        private XModifierKeys xModifier;
        private Keys key;

        internal KeyPressedEventArgs(XModifierKeys xModifier, Keys key)
        {
            this.xModifier = xModifier;
            this.key = key;
        }

        public XModifierKeys XModifier
        {
            get { return xModifier; }
        }

        public Keys Key
        {
            get { return key; }
        }
    }

    /// <summary>
    /// The enumeration of possible modifiers.
    /// </summary>
    public enum XModifierKeys : uint
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }
}
