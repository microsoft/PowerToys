using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorPicker.ColorPickingFunctionality
{
    class TransparentWindowController
    {

        TransparentWindow currentCursorWindow = new TransparentWindow();

        public void SetCursorToCrossOutsideCurrentWindow()
        {
            currentCursorWindow.Show();
        }

        public void ResetCursor()
        {
            currentCursorWindow.Hide();
        }

        public void Close()
        {
            // TODO: make this work
            Debug.WriteLine("Closing");
            currentCursorWindow.Close();
            currentCursorWindow = null;
        }
    }
}
