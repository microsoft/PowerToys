// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class RunPluginMetadataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MetadataAuthorTemplate { get; set; }

        public DataTemplate MetadataVersionTemplate { get; set; }

        public DataTemplate MetadataWebsiteTemplate { get; set; }

        /// <summary>
        /// Gets or sets an empty template used as fall back in case of malformed data
        /// </summary>
        public DataTemplate EmptyMetadataTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is PluginMetadataViewModel optionViewModel)
            {
                return optionViewModel.Type switch
                {
                    PluginMetadataViewModel.PluginMetadataType.Version => MetadataVersionTemplate,
                    PluginMetadataViewModel.PluginMetadataType.Author => MetadataAuthorTemplate,
                    PluginMetadataViewModel.PluginMetadataType.Link => MetadataWebsiteTemplate,
                    _ => EmptyMetadataTemplate,
                };
            }

            throw new ArgumentException("Item must be an PluginAdditionalOptionViewModel", nameof(item));
        }
    }
}
