// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels using CommunityToolkit.Mvvm.
    /// Provides lifecycle methods and messaging support.
    /// </summary>
    public abstract class ViewModelBase : ObservableRecipient, IDisposable
    {
        private bool _isDisposed;
        private bool _isActive;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
        /// </summary>
        protected ViewModelBase()
            : base(WeakReferenceMessenger.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class with a custom messenger.
        /// </summary>
        /// <param name="messenger">The messenger instance to use.</param>
        protected ViewModelBase(IMessenger messenger)
            : base(messenger)
        {
        }

        /// <summary>
        /// Gets a value indicating whether this ViewModel is currently active (visible).
        /// </summary>
        public bool IsViewModelActive => _isActive;

        /// <summary>
        /// Called when the view associated with this ViewModel is navigated to.
        /// Override this method to perform initialization when the page is displayed.
        /// </summary>
        public virtual void OnNavigatedTo()
        {
            _isActive = true;
            IsActive = true; // Activates message subscriptions in ObservableRecipient
        }

        /// <summary>
        /// Called when the view associated with this ViewModel is navigated to.
        /// Override this method to perform async initialization when the page is displayed.
        /// </summary>
        /// <returns>A task representing the async operation.</returns>
        public virtual Task OnNavigatedToAsync()
        {
            OnNavigatedTo();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the view associated with this ViewModel is navigated away from.
        /// Override this method to perform cleanup when the page is no longer displayed.
        /// </summary>
        public virtual void OnNavigatedFrom()
        {
            _isActive = false;
            IsActive = false; // Deactivates message subscriptions in ObservableRecipient
        }

        /// <summary>
        /// Called when the page is loaded. This is triggered from the Page's Loaded event.
        /// </summary>
        public virtual void OnPageLoaded()
        {
            // Default implementation does nothing.
            // Override in derived classes to perform actions when the page is loaded.
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Deactivate message subscriptions
                    IsActive = false;
                }

                _isDisposed = true;
            }
        }
    }
}
