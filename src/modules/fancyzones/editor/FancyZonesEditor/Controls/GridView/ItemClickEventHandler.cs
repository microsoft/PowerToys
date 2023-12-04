// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace FancyZonesEditor.Controls
{
    public delegate void ItemClickEventHandler(object sender, ItemClickEventArgs e);

#pragma warning disable SA1649 // File name should match first type name
    public sealed class ItemClickEventArgs : RoutedEventArgs
#pragma warning restore SA1649 // File name should match first type name
    {
        public ItemClickEventArgs()
        {
        }

        public object ClickedItem { get; internal set; }
    }
}
