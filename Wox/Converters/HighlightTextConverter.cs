using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace Wox.Converters
{
    public class HighlightTextConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo cultureInfo)
        {
            var text = value[0] as string;
            var highlightData = value[1] as List<int>;

            var textBlock = new Span();

            if (highlightData == null || !highlightData.Any())
            {
                // No highlight data, just return the text
                return new Run(text);
            }

            for (var i = 0; i < text.Length; i++)
            {
                var currentCharacter = text.Substring(i, 1);
                if (this.ShouldHighlight(highlightData, i))
                {
                    textBlock.Inlines.Add(new Bold(new Run(currentCharacter)));
                }
                else
                {
                    textBlock.Inlines.Add(new Run(currentCharacter));
                }
            }
            return textBlock;
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            return new[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
        }

        private bool ShouldHighlight(List<int> highlightData, int index)
        {
            return highlightData.Contains(index);
        }
    }
}
