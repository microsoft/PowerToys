// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Interface for navigation service that supports async navigation and page caching.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Raised when navigation has completed.
        /// </summary>
        event NavigatedEventHandler Navigated;

        /// <summary>
        /// Raised when navigation has failed.
        /// </summary>
        event NavigationFailedEventHandler NavigationFailed;

        /// <summary>
        /// Gets or sets the navigation frame.
        /// </summary>
        Frame Frame { get; set; }

        /// <summary>
        /// Gets a value indicating whether we can navigate back.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Gets a value indicating whether we can navigate forward.
        /// </summary>
        bool CanGoForward { get; }

        /// <summary>
        /// Navigates back in the navigation stack.
        /// </summary>
        /// <returns>True if navigation was successful.</returns>
        bool GoBack();

        /// <summary>
        /// Navigates forward in the navigation stack.
        /// </summary>
        void GoForward();

        /// <summary>
        /// Navigates to a page synchronously.
        /// </summary>
        /// <param name="pageType">The type of page to navigate to.</param>
        /// <param name="parameter">Optional navigation parameter.</param>
        /// <param name="infoOverride">Optional navigation transition.</param>
        /// <returns>True if navigation was successful.</returns>
        bool Navigate(Type pageType, object parameter = null, NavigationTransitionInfo infoOverride = null);

        /// <summary>
        /// Navigates to a page asynchronously, waiting for ViewModel initialization.
        /// </summary>
        /// <param name="pageType">The type of page to navigate to.</param>
        /// <param name="parameter">Optional navigation parameter.</param>
        /// <param name="infoOverride">Optional navigation transition.</param>
        /// <returns>A task that completes when navigation and initialization are done.</returns>
        Task<bool> NavigateAsync(Type pageType, object parameter = null, NavigationTransitionInfo infoOverride = null);

        /// <summary>
        /// Navigates to a page synchronously using generics.
        /// </summary>
        /// <typeparam name="T">The type of page to navigate to.</typeparam>
        /// <param name="parameter">Optional navigation parameter.</param>
        /// <param name="infoOverride">Optional navigation transition.</param>
        /// <returns>True if navigation was successful.</returns>
        bool Navigate<T>(object parameter = null, NavigationTransitionInfo infoOverride = null)
            where T : Page;

        /// <summary>
        /// Ensures a page is selected when the frame content is null.
        /// </summary>
        /// <param name="pageType">The type of page to navigate to if nothing is selected.</param>
        void EnsurePageIsSelected(Type pageType);

        /// <summary>
        /// Clears the navigation back stack.
        /// </summary>
        void ClearBackStack();
    }
}
