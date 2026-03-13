// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ImageResizer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ImageResizer.Views
{
    public partial class PageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate InputTemplate { get; set; }

        public DataTemplate ProgressTemplate { get; set; }

        public DataTemplate ResultsTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item switch
            {
                InputViewModel => InputTemplate,
                ProgressViewModel => ProgressTemplate,
                ResultsViewModel => ResultsTemplate,
                _ => base.SelectTemplateCore(item),
            };
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
