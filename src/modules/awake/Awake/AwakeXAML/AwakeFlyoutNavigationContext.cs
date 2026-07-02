// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.ViewModels;

namespace Awake
{
    /// <summary>
    /// Carries the shared <see cref="AwakeFlyoutViewModel"/> (and a request-to-close callback)
    /// between the flyout pages hosted in <see cref="AwakeShellPage"/>'s navigation frame.
    /// </summary>
    internal sealed class AwakeFlyoutNavigationContext
    {
        public AwakeFlyoutNavigationContext(AwakeFlyoutViewModel viewModel, Action requestClose)
        {
            ViewModel = viewModel;
            RequestClose = requestClose;
        }

        public AwakeFlyoutViewModel ViewModel { get; }

        public Action RequestClose { get; }
    }
}
