// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace PowerLauncher.Converters
{
    public class HighlightTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var text = values[0] as string;
            var highlightData = values[1] as List<int>;
            var selected = values[2] as bool? == true;

            if (highlightData == null || highlightData.Count == 0)
            {
                // No highlight data, just return the text
                return new Run(text);
            }

            var textBlock = new Span();
            for (var i = 0; i < text.Length; i++)
            {
                var currentCharacter = text.Substring(i, 1);
                if (ShouldHighlight(highlightData, i))
                {
                    textBlock.Inlines.Add(new Run(currentCharacter)
                    {
                        FontWeight = FontWeights.SemiBold,
                    });
                }
                else
                {
                    textBlock.Inlines.Add(new Run(currentCharacter));
                }
            }

            return textBlock;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
        }

        private static bool ShouldHighlight(List<int> highlightData, int index)
        {
            return highlightData.Contains(index);
        }
    }
}
