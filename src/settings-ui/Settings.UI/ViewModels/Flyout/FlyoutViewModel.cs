// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Timers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels.Flyout
{
    public partial class FlyoutViewModel : IDisposable
    {
        private Timer _hideTimer;
        private bool _disposed;

        public bool CanHide { get; set; }

        public FlyoutViewModel()
        {
            CanHide = true;
            _hideTimer = new Timer();
            _hideTimer.Elapsed += HideTimer_Elapsed;
            _hideTimer.Interval = 1000;
            _hideTimer.Enabled = false;
        }

        private void HideTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CanHide = true;
            _hideTimer.Stop();
        }

        internal void DisableHiding()
        {
            CanHide = false;
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hideTimer?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
