// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AdvancedPaste.Controls
{
    public sealed partial class ClipboardHistoryItemPreviewControl : UserControl
    {
        public static readonly DependencyProperty ClipboardItemProperty = DependencyProperty.Register(
            nameof(ClipboardItem),
            typeof(ClipboardItem),
            typeof(ClipboardHistoryItemPreviewControl),
            new PropertyMetadata(defaultValue: null, OnClipboardItemChanged));

        public ClipboardItem ClipboardItem
        {
            get => (ClipboardItem)GetValue(ClipboardItemProperty);
            set => SetValue(ClipboardItemProperty, value);
        }

        // Computed properties for display
        public string Header => ClipboardItem != null ? GetHeaderFromFormat(ClipboardItem.Format) : string.Empty;

        public string IconGlyph => ClipboardItem != null ? GetGlyphFromFormat(ClipboardItem.Format) : string.Empty;

        public string ContentText => ClipboardItem?.Content ?? string.Empty;

        public ImageSource ContentImage => ClipboardItem?.Image;

        public DateTimeOffset? Timestamp => ClipboardItem?.Timestamp ?? ClipboardItem?.Item?.Timestamp;

        public bool HasImage => ContentImage is not null;

        public bool HasText => !string.IsNullOrEmpty(ContentText) && !HasImage && !HasColor;

        public bool HasGlyph => !HasImage && !HasText && !HasColor && !string.IsNullOrEmpty(IconGlyph);

        public bool HasColor => ClipboardItemHelper.IsRgbHexColor(ContentText);

        public ClipboardHistoryItemPreviewControl()
        {
            InitializeComponent();
        }

        private static void OnClipboardItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClipboardHistoryItemPreviewControl control)
            {
                // Notify bindings that all computed properties may have changed
                control.Bindings.Update();
            }
        }

        private static string GetHeaderFromFormat(ClipboardFormat format)
        {
            // Check flags in priority order (most specific first)
            if (format.HasFlag(ClipboardFormat.Image))
            {
                return GetStringOrFallback("ClipboardPreviewCategoryImage", "Image");
            }

            if (format.HasFlag(ClipboardFormat.Video))
            {
                return GetStringOrFallback("ClipboardPreviewCategoryVideo", "Video");
            }

            if (format.HasFlag(ClipboardFormat.Audio))
            {
                return GetStringOrFallback("ClipboardPreviewCategoryAudio", "Audio");
            }

            if (format.HasFlag(ClipboardFormat.File))
            {
                return GetStringOrFallback("ClipboardPreviewCategoryFile", "File");
            }

            if (format.HasFlag(ClipboardFormat.Text) || format.HasFlag(ClipboardFormat.Html))
            {
                return GetStringOrFallback("ClipboardPreviewCategoryText", "Text");
            }

            return GetStringOrFallback("ClipboardPreviewCategoryUnknown", "Clipboard");
        }

        private static string GetGlyphFromFormat(ClipboardFormat format)
        {
            // Check flags in priority order (most specific first)
            if (format.HasFlag(ClipboardFormat.Image))
            {
                return "\uEB9F"; // Image icon
            }

            if (format.HasFlag(ClipboardFormat.Video))
            {
                return "\uE714"; // Video icon
            }

            if (format.HasFlag(ClipboardFormat.Audio))
            {
                return "\uE189"; // Audio icon
            }

            if (format.HasFlag(ClipboardFormat.File))
            {
                return "\uE8A5"; // File icon
            }

            if (format.HasFlag(ClipboardFormat.Text) || format.HasFlag(ClipboardFormat.Html))
            {
                return "\uE8D2"; // Text icon
            }

            return "\uE77B"; // Generic clipboard icon
        }

        private static string GetStringOrFallback(string resourceKey, string fallback)
        {
            var value = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
