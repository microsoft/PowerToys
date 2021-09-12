// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace PowerToys.Settings
{
    internal class Utils
    {
        public static void FitToScreen(Window window)
        {
            if (SystemParameters.WorkArea.Width < window.Width)
            {
                window.Width = SystemParameters.WorkArea.Width;
            }

            if (SystemParameters.WorkArea.Height < window.Height)
            {
                window.Height = SystemParameters.WorkArea.Height;
            }
        }

        public static void CenterToScreen(Window window)
        {
            if (SystemParameters.WorkArea.Height <= window.Height)
            {
                window.Top = 0;
            }
            else
            {
                window.Top = (SystemParameters.WorkArea.Height - window.Height) / 2;
            }

            if (SystemParameters.WorkArea.Width <= window.Width)
            {
                window.Left = 0;
            }
            else
            {
                window.Left = (SystemParameters.WorkArea.Width - window.Width) / 2;
            }
        }

        public static void ShowHide(Window window)
        {
            // To limit the visual flickering, show the window with a size of 0,0
            // and don't show it in the taskbar
            var originalHeight = window.Height;
            var originalWidth = window.Width;
            var originalMinHeight = window.MinHeight;
            var originalMinWidth = window.MinWidth;

            window.MinHeight = 0;
            window.MinWidth = 0;
            window.Height = 0;
            window.Width = 0;
            window.ShowInTaskbar = false;

            window.Show();
            window.Hide();

            window.Height = originalHeight;
            window.Width = originalWidth;
            window.MinHeight = originalMinHeight;
            window.MinWidth = originalMinWidth;
            window.ShowInTaskbar = true;
        }
    }
}
