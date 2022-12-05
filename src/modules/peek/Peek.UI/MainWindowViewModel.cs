// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System.Collections.Generic;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Peek.Common.Models;

    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private File? currentFile;

        [ObservableProperty]
        private List<File> files = new ();
    }
}
