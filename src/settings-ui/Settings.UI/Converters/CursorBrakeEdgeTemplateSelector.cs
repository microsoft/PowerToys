// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    /// <summary>
    /// Picks the template for each item of the CursorBrake edge editor's list
    /// (<see cref="CursorBrakeEdgesViewModel.ListItems"/>).
    /// </summary>
    public sealed partial class CursorBrakeEdgeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MapTemplate { get; set; }

        public DataTemplate EdgeTemplate { get; set; }

        public DataTemplate DirectionTemplate { get; set; }

        public DataTemplate HeadingTemplate { get; set; }

        public DataTemplate DisconnectedTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item switch
            {
                CursorBrakeEdgesViewModel => MapTemplate,
                CursorBrakeEdgeRowViewModel => EdgeTemplate,
                CursorBrakeEdgeDirectionViewModel => DirectionTemplate,
                CursorBrakeDisconnectedEdgeViewModel => DisconnectedTemplate,
                _ => HeadingTemplate,
            };
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
