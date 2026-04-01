// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Peek.FilePreviewer.Previewers.Archives.Models
{
    public partial class ArchiveItem : ObservableObject
    {
        [ObservableProperty]
        public partial string Name { get; set; }

        [ObservableProperty]
        public partial ArchiveItemType Type { get; set; }

        [ObservableProperty]
        public partial ImageSource? Icon { get; set; }

        [ObservableProperty]
        public partial ulong Size { get; set; }

        [ObservableProperty]
        public partial bool IsExpanded { get; set; }

        public ObservableCollection<ArchiveItem> Children { get; }

        public ArchiveItem(string name, ArchiveItemType type, ImageSource? icon)
        {
            Name = name;
            Type = type;
            Icon = icon;
            Children = new ObservableCollection<ArchiveItem>();
        }
    }
}
