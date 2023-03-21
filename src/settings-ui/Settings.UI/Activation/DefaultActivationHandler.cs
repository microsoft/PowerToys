// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Services;
using Windows.ApplicationModel.Activation;

namespace Microsoft.PowerToys.Settings.UI.Activation
{
    internal sealed class DefaultActivationHandler : ActivationHandler<IActivatedEventArgs>
    {
        private readonly Type navElement;

        public DefaultActivationHandler(Type navElement)
        {
            this.navElement = navElement;
        }

        protected override async Task HandleInternalAsync(IActivatedEventArgs args)
        {
            // When the navigation stack isn't restored, navigate to the first page and configure
            // the new page by passing required information in the navigation parameter
            object arguments = null;
            if (args is LaunchActivatedEventArgs launchArgs)
            {
                arguments = launchArgs.Arguments;
            }

            NavigationService.Navigate(navElement, arguments);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        protected override bool CanHandleInternal(IActivatedEventArgs args)
        {
            // None of the ActivationHandlers has handled the app activation
            return NavigationService.Frame.Content == null && navElement != null;
        }
    }
}
