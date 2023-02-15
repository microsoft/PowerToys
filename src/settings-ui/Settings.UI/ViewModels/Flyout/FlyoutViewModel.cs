// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels.Flyout
{
    public class FlyoutViewModel
    {
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

        private Timer hideTimer;

        public FlyoutViewModel()
        {
            CanHide = true;
            hideTimer = new Timer();
            hideTimer.Elapsed += HideTimer_Elapsed;
            hideTimer.Interval = 1000;
            hideTimer.Enabled = false;
            _windows10 = !Helper.Windows11();
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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
