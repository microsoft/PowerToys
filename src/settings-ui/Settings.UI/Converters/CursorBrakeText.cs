// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    /// <summary>
    /// Fills a RichTextBlock with an edge editor line: runs of text, and each monitor as its numbered
    /// icon followed by its name. Only a RichTextBlock's paragraphs take an InlineUIContainer, and
    /// x:Bind cannot bind blocks, so the parts arrive through this attached property. The icons are
    /// decorative to screen readers, so the owner carries the line's plain form, numbers included, as
    /// its automation name.
    /// </summary>
    public static class CursorBrakeText
    {
        // The icon is a little taller than the text's capitals, so it sits slightly lower to center
        // on them rather than on the baseline.
        private const double IconSize = 16;
        private const double IconDrop = 3;
        private const double IconGap = 4;

        public static readonly DependencyProperty PartsProperty = DependencyProperty.RegisterAttached(
            "Parts", typeof(IReadOnlyList<CursorBrakeTextPart>), typeof(CursorBrakeText), new PropertyMetadata(null, OnPartsChanged));

        public static IReadOnlyList<CursorBrakeTextPart> GetParts(RichTextBlock textBlock) => (IReadOnlyList<CursorBrakeTextPart>)textBlock.GetValue(PartsProperty);

        public static void SetParts(RichTextBlock textBlock, IReadOnlyList<CursorBrakeTextPart> value) => textBlock.SetValue(PartsProperty, value);

        private static void OnPartsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not RichTextBlock textBlock)
            {
                return;
            }

            textBlock.Blocks.Clear();
            if (e.NewValue is not IReadOnlyList<CursorBrakeTextPart> parts)
            {
                return;
            }

            // A line that fails to build falls back to its plain text, so the page keeps working.
            try
            {
                textBlock.Blocks.Add(Build(parts, withIcons: true));
            }
            catch (Exception ex)
            {
                Logger.LogError($"CursorBrake could not show a line with monitor icons, so it shows plain text: {ex.Message}");
                textBlock.Blocks.Clear();
                textBlock.Blocks.Add(Build(parts, withIcons: false));
            }
        }

        private static Paragraph Build(IReadOnlyList<CursorBrakeTextPart> parts, bool withIcons)
        {
            var paragraph = new Paragraph();
            foreach (var part in parts)
            {
                if (!withIcons)
                {
                    paragraph.Inlines.Add(new Run { Text = part.Plain });
                    continue;
                }

                if (part.IsMonitor)
                {
                    var icon = new MonitorIcon
                    {
                        MonitorNumber = part.MonitorNumber,
                        IsBuiltIn = part.BuiltIn,
                        IconSize = IconSize,
                        Margin = new Thickness(0, 0, string.IsNullOrEmpty(part.Text) ? 0 : IconGap, 0),
                        RenderTransform = new TranslateTransform { Y = IconDrop },
                    };
                    paragraph.Inlines.Add(new InlineUIContainer { Child = icon });
                }

                if (!string.IsNullOrEmpty(part.Text))
                {
                    paragraph.Inlines.Add(new Run { Text = part.Text });
                }
            }

            return paragraph;
        }
    }
}
