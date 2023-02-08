// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Timers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels.Flyout
{
    public class FlyoutViewModel
    {
        public bool CanHide { get; set; }

        private Timer hideTimer;

        public FlyoutViewModel()
        {
            CanHide = true;
            hideTimer = new Timer();
            hideTimer.Elapsed += HideTimer_Elapsed;
            hideTimer.Interval = 1000;
            hideTimer.Enabled = false;
        }

        private void HideTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CanHide = true;
            hideTimer.Stop();
        }

        internal void DisableHiding()
        {
            CanHide = false;
            hideTimer.Stop();
            hideTimer.Start();
        }
    }
}
