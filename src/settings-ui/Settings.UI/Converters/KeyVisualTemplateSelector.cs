// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    internal sealed class KeyVisualTemplateSelector : DataTemplateSelector
    {
        public DataTemplate KeyVisualTemplate { get; set; }

        public DataTemplate CommaTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var stringValue = item as string;
            return stringValue == KeysDataModel.CommaSeparator ? CommaTemplate : KeyVisualTemplate;
        }
    }
}
