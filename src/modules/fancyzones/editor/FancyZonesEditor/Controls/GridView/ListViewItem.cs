// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace FancyZonesEditor.Controls
{
    public class ListViewItem : ListViewBaseItem
    {
        static ListViewItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ListViewItem), new FrameworkPropertyMetadata(typeof(ListViewItem)));
        }
    }
}
