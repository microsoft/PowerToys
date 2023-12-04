// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace FancyZonesEditor.Controls
{
    public class ListView : ListViewBase
    {
        static ListView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ListView), new FrameworkPropertyMetadata(typeof(ListView)));
        }

        public ListView()
        {
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is ListViewItem;
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ListViewItem();
        }
    }
}
