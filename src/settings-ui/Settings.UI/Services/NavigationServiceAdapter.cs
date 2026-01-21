// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Adapter that wraps the static NavigationService to implement INavigationService.
    /// This allows for dependency injection while maintaining backward compatibility.
    /// </summary>
    public class NavigationServiceAdapter : INavigationService
    {
        private const int DefaultCacheSize = 10;

        /// <inheritdoc/>
        public event NavigatedEventHandler Navigated
        {
            add => NavigationService.Navigated += value;
            remove => NavigationService.Navigated -= value;
        }

        /// <inheritdoc/>
        public event NavigationFailedEventHandler NavigationFailed
        {
            add => NavigationService.NavigationFailed += value;
            remove => NavigationService.NavigationFailed -= value;
        }

        /// <inheritdoc/>
        public Frame Frame
        {
            get => NavigationService.Frame;
            set
            {
                NavigationService.Frame = value;

                // Enable page caching for better navigation performance
                if (value != null && value.CacheSize < DefaultCacheSize)
                {
                    value.CacheSize = DefaultCacheSize;
                }
            }
        }

        /// <inheritdoc/>
        public bool CanGoBack => NavigationService.CanGoBack;

        /// <inheritdoc/>
        public bool CanGoForward => NavigationService.CanGoForward;

        /// <inheritdoc/>
        public bool GoBack() => NavigationService.GoBack();

        /// <inheritdoc/>
        public void GoForward() => NavigationService.GoForward();

        /// <inheritdoc/>
        public bool Navigate(Type pageType, object parameter = null, NavigationTransitionInfo infoOverride = null)
        {
            return NavigationService.Navigate(pageType, parameter, infoOverride);
        }

        /// <inheritdoc/>
        public async Task<bool> NavigateAsync(Type pageType, object parameter = null, NavigationTransitionInfo infoOverride = null)
        {
            var result = NavigationService.Navigate(pageType, parameter, infoOverride);

            if (result && Frame?.Content is FrameworkElement element)
            {
                // If the page's DataContext implements IAsyncInitializable, await its initialization
                if (element.DataContext is IAsyncInitializable asyncInit && !asyncInit.IsInitialized)
                {
                    await asyncInit.InitializeAsync();
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public bool Navigate<T>(object parameter = null, NavigationTransitionInfo infoOverride = null)
            where T : Page
        {
            return NavigationService.Navigate<T>(parameter, infoOverride);
        }

        /// <inheritdoc/>
        public void EnsurePageIsSelected(Type pageType)
        {
            NavigationService.EnsurePageIsSelected(pageType);
        }

        /// <inheritdoc/>
        public void ClearBackStack()
        {
            Frame?.BackStack.Clear();
        }
    }
}
