// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ImageResizer.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ImageResizer.Views
{
    public partial class SizeDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ResizeSizeTemplate { get; set; }
        public DataTemplate CustomSizeTemplate { get; set; }
        public DataTemplate AiSizeTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is AiSize)
            {
                return AiSizeTemplate;
            }

            if (item is CustomSize)
            {
                return CustomSizeTemplate;
            }

            if (item is ResizeSize)
            {
                return ResizeSizeTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
