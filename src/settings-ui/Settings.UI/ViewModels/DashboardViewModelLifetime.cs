// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal sealed class DashboardViewModelLifetime
    {
        private readonly Func<DashboardViewModel> _createViewModel;
        private bool _unloaded;

        internal DashboardViewModelLifetime(Func<DashboardViewModel> createViewModel)
        {
            _createViewModel = createViewModel;
            ViewModel = _createViewModel();
        }

        internal DashboardViewModel ViewModel { get; private set; }

        internal bool Load()
        {
            if (!_unloaded)
            {
                return false;
            }

            ViewModel = _createViewModel();
            _unloaded = false;
            return true;
        }

        internal void Unload()
        {
            if (_unloaded)
            {
                return;
            }

            ViewModel.Dispose();
            _unloaded = true;
        }
    }
}
