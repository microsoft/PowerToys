// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System.Collections.Generic;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Peek.Common.Models;
    using Peek.UI.Helpers;

    public partial class MainWindowViewModel : ObservableObject
    {
        public void AttemptLeftNavigation()
        {
            fileQuery.UpdateCurrentItemIndex(fileQuery.CurrentItemIndex - 1);
        }

        public void AttemptRightNavigation()
        {
            fileQuery.UpdateCurrentItemIndex(fileQuery.CurrentItemIndex + 1);
        }

        [ObservableProperty]
        private FileQuery fileQuery = new ();
    }
}
