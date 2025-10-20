// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Controls;
using AdvancedPaste.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Controls
{
    public sealed partial class ClipboardHistoryItemPreviewControl : UserControl
    {
        public static readonly DependencyProperty ClipboardItemProperty = DependencyProperty.Register(
            nameof(ClipboardItem),
            typeof(ClipboardItem),
            typeof(ClipboardHistoryItemPreviewControl),
            new PropertyMetadata(defaultValue: null));

        public ClipboardItem ClipboardItem
        {
            get => (ClipboardItem)GetValue(ClipboardItemProperty);
            set => SetValue(ClipboardItemProperty, value);
        }

        public ClipboardHistoryItemPreviewControl()
        {
            InitializeComponent();
        }
    }
}
