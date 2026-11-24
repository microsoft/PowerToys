// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI.UnitTests
{
    internal sealed partial class TestMainViewModel : MainViewModel
    {
        internal TestMainViewModel()
            : base(_ => { }, _ => throw new InvalidOperationException("Unexpected warning."), (action, _) => action(), () => true, () => { })
        {
        }
    }
}
