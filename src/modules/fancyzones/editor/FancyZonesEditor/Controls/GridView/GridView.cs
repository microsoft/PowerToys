// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace FancyZonesEditor.Controls
{
    public class GridView : ListViewBase
    {
        static GridView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GridView), new FrameworkPropertyMetadata(typeof(GridView)));
        }

        public GridView()
        {
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is GridViewItem;
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new GridViewItem();
        }
    }
}
