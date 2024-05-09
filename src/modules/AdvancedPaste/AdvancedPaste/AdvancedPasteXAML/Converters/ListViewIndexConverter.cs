// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AdvancedPaste.Converters
{
    public sealed class ListViewIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var presenter = value as ListViewItemPresenter;
            var item = VisualTreeHelper.GetParent(presenter) as ListViewItem;

            var listView = ItemsControl.ItemsControlFromItemContainer(item);
            int index = listView.IndexFromContainer(item) + 1;
#pragma warning disable CA1305 // Specify IFormatProvider
            return index.ToString();
#pragma warning restore CA1305 // Specify IFormatProvider
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
