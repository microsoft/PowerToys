// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using Common.UI;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels.Flyout
{
    public class FlyoutViewModel : IDisposable
    {
        private Timer _hideTimer;
        private bool _disposed;

        public bool CanHide { get; set; }

        private bool _windows10;

        public bool Windows10
        {
            get => _windows10;
            set
            {
                if (_windows10 != value)
                {
                    _windows10 = value;
                    OnPropertyChanged();
                }
            }
        }

        public FlyoutViewModel()
        {
            CanHide = true;
            _hideTimer = new Timer();
            _hideTimer.Elapsed += HideTimer_Elapsed;
            _hideTimer.Interval = 1000;
            _hideTimer.Enabled = false;
            _windows10 = !OSVersionHelper.IsWindows11();
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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
