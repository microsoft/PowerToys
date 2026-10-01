// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal sealed class PageViewModelLifetime<T> : IDisposable
        where T : class, IDisposable
    {
        private readonly Func<T> _createViewModel;
        private CancellationTokenSource _cancellation = new();
        private bool _unloaded;
        private bool _disposed;

        internal PageViewModelLifetime(Func<T> createViewModel)
        {
            _createViewModel = createViewModel;
            ViewModel = _createViewModel();
            CancellationToken = _cancellation.Token;
        }

        internal T ViewModel { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        internal bool Load()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_unloaded)
            {
                return false;
            }

            ViewModel = _createViewModel();
            _cancellation = new CancellationTokenSource();
            CancellationToken = _cancellation.Token;
            _unloaded = false;
            return true;
        }

        internal void Unload()
        {
            if (_unloaded)
            {
                return;
            }

            _unloaded = true;
            var cancellation = _cancellation;
            var viewModel = ViewModel;
            cancellation.Cancel();
            cancellation.Dispose();
            viewModel.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Unload();
            GC.SuppressFinalize(this);
        }
    }
}
