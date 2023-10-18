// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Peek.FilePreviewer.Previewers.Archives.Models
{
    public class ArchiveItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DirectoryTemplate { get; set; }

        public DataTemplate? FileTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            if (item is ArchiveItem archiveItem)
            {
                return archiveItem.Type == ArchiveItemType.Directory ? DirectoryTemplate : FileTemplate;
            }

            throw new ArgumentException("Item must be an ArchiveItem", nameof(item));
        }
    }
}
