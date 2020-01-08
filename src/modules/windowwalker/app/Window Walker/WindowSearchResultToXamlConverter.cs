// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml;
using WindowWalker.Components;

namespace WindowWalker
{
    /// <summary>
    /// Converts a string containing valid XAML into WPF objects.
    /// </summary>
    [ValueConversion(typeof(SearchResult), typeof(object))]
    public sealed class WindowSearchResultToXamlConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string containing valid XAML into WPF objects.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <param name="targetType">This parameter is not used.</param>
        /// <param name="parameter">This parameter is not used.</param>
        /// <param name="culture">This parameter is not used.</param>
        /// <returns>A WPF object.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SearchResult input)
            {
                string withTags;

                if (input.BestScoreSource == SearchResult.TextType.ProcessName)
                {
                    withTags = input.Result.Title;
                    withTags += $" ({InsertHighlightTags(input.Result.ProcessName, input.SearchMatchesInProcessName)})";
                }
                else
                {
                    withTags = InsertHighlightTags(input.Result.Title, input.SearchMatchesInTitle);
                    withTags += $" ({input.Result.ProcessName})";
                }

                withTags = SecurityElement.Escape(withTags);

                withTags = withTags.Replace("[[", "<Run Background=\"{DynamicResource SecondaryAccentBrush}\" FontSize=\"18\" FontWeight=\"Bold\" Foreground=\"{DynamicResource SecondaryAccentForegroundBrush}\">").
                                    Replace("]]", "</Run>");

                string wrappedInput = string.Format("<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TextWrapping=\"Wrap\">{0}</TextBlock>", withTags);

                using (StringReader stringReader = new StringReader(wrappedInput))
                {
                    using (XmlReader xmlReader = XmlReader.Create(stringReader))
                    {
                        return XamlReader.Load(xmlReader);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Converts WPF framework objects into a XAML string.
        /// </summary>
        /// <param name="value">The WPF Famework object to convert.</param>
        /// <param name="targetType">This parameter is not used.</param>
        /// <param name="parameter">This parameter is not used.</param>
        /// <param name="culture">This parameter is not used.</param>
        /// <returns>A string containg XAML.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("This converter cannot be used in two-way binding.");
        }

        private string InsertHighlightTags(string content, List<int> indexes)
        {
            int offset = 0;
            var result = content.Replace("[[", "**").Replace("]]", "**");

            string startTag = "[[";
            string stopTag = "]]";

            foreach (var index in indexes)
            {
                result = result.Insert(index + offset, startTag);
                result = result.Insert(index + offset + startTag.Length + 1, stopTag);

                offset += startTag.Length + stopTag.Length;
            }

            return result;
        }
    }
}
