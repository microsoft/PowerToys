// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Interface for navigation services to enable testability and abstraction
    /// from the static NavigationService class.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Occurs when navigation has completed.
        /// </summary>
        event NavigatedEventHandler Navigated;

        /// <summary>
        /// Occurs when navigation has failed.
        /// </summary>
        event NavigationFailedEventHandler NavigationFailed;

        /// <summary>
        /// Gets or sets the navigation frame.
        /// </summary>
        Frame Frame { get; set; }

        /// <summary>
        /// Gets a value indicating whether navigation can go back.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Gets a value indicating whether navigation can go forward.
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
        /// Navigates to the specified page type.
        /// </summary>
        /// <param name="pageType">The type of page to navigate to.</param>
        /// <param name="parameter">Optional navigation parameter.</param>
        /// <param name="infoOverride">Optional navigation transition override.</param>
        /// <returns>True if navigation was successful.</returns>
        bool Navigate(Type pageType, object parameter = null, NavigationTransitionInfo infoOverride = null);

        /// <summary>
        /// Navigates to the specified page type.
        /// </summary>
        /// <typeparam name="T">The type of page to navigate to.</typeparam>
        /// <param name="parameter">Optional navigation parameter.</param>
        /// <param name="infoOverride">Optional navigation transition override.</param>
        /// <returns>True if navigation was successful.</returns>
        bool Navigate<T>(object parameter = null, NavigationTransitionInfo infoOverride = null)
            where T : Page;

        /// <summary>
        /// Ensures a page of the specified type is selected.
        /// </summary>
        /// <param name="pageType">The type of page to ensure is selected.</param>
        void EnsurePageIsSelected(Type pageType);
    }

    /// <summary>
    /// Wrapper around the static NavigationService to implement INavigationService.
    /// Allows for gradual migration and testability.
    /// </summary>
    public class NavigationServiceWrapper : INavigationService
    {
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
            set => NavigationService.Frame = value;
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
            => NavigationService.Navigate(pageType, parameter, infoOverride);

        /// <inheritdoc/>
        public bool Navigate<T>(object parameter = null, NavigationTransitionInfo infoOverride = null)
            where T : Page
            => NavigationService.Navigate<T>(parameter, infoOverride);

        /// <inheritdoc/>
        public void EnsurePageIsSelected(Type pageType) => NavigationService.EnsurePageIsSelected(pageType);
    }
}
