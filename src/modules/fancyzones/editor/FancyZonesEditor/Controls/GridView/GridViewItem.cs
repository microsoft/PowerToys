// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace FancyZonesEditor.Controls
{
    public class GridViewItem : ListViewBaseItem
    {
        static GridViewItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GridViewItem), new FrameworkPropertyMetadata(typeof(GridViewItem)));
        }
    }
}
