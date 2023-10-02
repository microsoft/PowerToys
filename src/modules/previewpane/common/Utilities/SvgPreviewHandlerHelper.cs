// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Common.Utilities
{
    /// <summary>
    /// Helper utilities for Svg Preview Handler.
    /// </summary>
    public static class SvgPreviewHandlerHelper
    {
        private const string WidthAttribute = "width=\"";
        private const string HeightAttribute = "height=\"";
        private const string StyleAttribute = "style=\"";
        private const string ViewboxAttribute = "viewBox=\"";

        /// <summary>
        /// Dictionary of elements in lower case that are blocked from Svg for preview pane.
        /// Reference for list of Svg Elements: https://developer.mozilla.org/docs/Web/SVG/Element.
        /// </summary>
        private static Dictionary<string, bool> blockedElementsName = new Dictionary<string, bool>
        {
            { "script", true },
            { "image", true },
            { "feimage", true },
        };

        /// <summary>
        /// Check if any of the blocked elements present in Svg.
        /// </summary>
        /// <param name="svgData">Input Svg.</param>
        /// <returns>Returns true in case any of the blocked element is present otherwise false.</returns>
        public static bool CheckBlockedElements(string svgData)
        {
            bool foundBlockedElement = false;
            if (string.IsNullOrWhiteSpace(svgData))
            {
                return foundBlockedElement;
            }

            // Check if any of the blocked element is present. If failed to parse or iterate over Svg return default false.
            // No need to throw because all the external content and script are blocked on the Web Browser Control itself.
            try
            {
                var doc = XDocument.Parse(svgData);
                var elements = doc.Descendants().ToList();
                foreach (XElement element in elements)
                {
                    // Using Invariant since we are doing an exact match for HTML tags and we want it to behave the same in every culture
                    var elementName = element?.Name?.LocalName?.ToLowerInvariant();
                    if (elementName != null && blockedElementsName.ContainsKey(elementName))
                    {
                        foundBlockedElement = true;

                        // No need to iterate further since we are displaying info bar with condition of atleast one occurrence of blocked element is present.
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }

            return foundBlockedElement;
        }

        private static string GetAttributeValue(int attributeNameLength, string data, int startIndex)
        {
            if (startIndex == -1)
            {
                return string.Empty;
            }

            int start = startIndex + attributeNameLength;
            int end = data.IndexOf("\"", start, StringComparison.InvariantCultureIgnoreCase);
            return data.Substring(start, end - start);
        }

        private static string RemoveAttribute(string data, int startIndex, string attributeName, out int numRemoved)
        {
            numRemoved = 0;

            if (startIndex == -1)
            {
                return data;
            }

            int end = data.IndexOf("\"", startIndex + attributeName.Length, StringComparison.InvariantCultureIgnoreCase) + 1;
            numRemoved = end - startIndex;
            return data.Remove(startIndex, numRemoved);
        }

        private static string RemoveXmlProlog(string s, int prefixLength, out int numRemoved)
        {
            numRemoved = 0;
            int startIndex = s.IndexOf("<?xml", 0, prefixLength, StringComparison.OrdinalIgnoreCase);
            if (startIndex != -1)
            {
                int endIndex = s.IndexOf("?>", startIndex, StringComparison.InvariantCultureIgnoreCase);
                if (endIndex != -1)
                {
                    numRemoved = endIndex + 2 - startIndex;
                    return s.Remove(startIndex, numRemoved);
                }
            }

            return s;
        }

        private static int FindFirstXmlOpenTagIndex(string s)
        {
            int index = 0;

            while ((index = s.IndexOf('<', index)) != -1)
            {
                if (index < s.Length - 1 && s[index + 1] != '?' && s[index + 1] != '!')
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private static int FindFirstXmlCloseTagIndex(string s, int openTagIndex)
        {
            int index = 1;

            while ((index = s.IndexOf('>', openTagIndex)) != -1)
            {
                if (index > 0 && s[index - 1] != '?')
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        /// <summary>
        /// Add proper
        /// </summary>
        /// <param name="stringSvgData">Input Svg</param>
        /// <returns>Returns modified svgData with added style</returns>
        public static string AddStyleSVG(string stringSvgData)
        {
            int firstXmlOpenTagIndex = FindFirstXmlOpenTagIndex(stringSvgData);
            if (firstXmlOpenTagIndex == -1)
            {
                return stringSvgData;
            }

            int firstXmlCloseTagIndex = FindFirstXmlCloseTagIndex(stringSvgData, firstXmlOpenTagIndex);
            if (firstXmlCloseTagIndex == -1)
            {
                return stringSvgData;
            }

            stringSvgData = RemoveXmlProlog(stringSvgData, firstXmlOpenTagIndex, out int numRemoved);

            firstXmlOpenTagIndex -= numRemoved;
            firstXmlCloseTagIndex -= numRemoved;

            int widthIndex = stringSvgData.IndexOf(WidthAttribute, firstXmlOpenTagIndex, firstXmlCloseTagIndex, StringComparison.InvariantCultureIgnoreCase);
            int heightIndex = stringSvgData.IndexOf(HeightAttribute, firstXmlOpenTagIndex, firstXmlCloseTagIndex, StringComparison.InvariantCultureIgnoreCase);
            int styleIndex = stringSvgData.IndexOf(StyleAttribute, firstXmlOpenTagIndex, firstXmlCloseTagIndex, StringComparison.InvariantCultureIgnoreCase);

            string width = GetAttributeValue(WidthAttribute.Length, stringSvgData, widthIndex);
            string height = GetAttributeValue(HeightAttribute.Length, stringSvgData, heightIndex);
            string oldStyle = GetAttributeValue(StyleAttribute.Length, stringSvgData, styleIndex);

            bool hasViewBox = stringSvgData.IndexOf(ViewboxAttribute, firstXmlOpenTagIndex, firstXmlCloseTagIndex - firstXmlOpenTagIndex, StringComparison.InvariantCultureIgnoreCase) != -1;

            stringSvgData = RemoveAttribute(stringSvgData, widthIndex, WidthAttribute, out numRemoved);
            if (heightIndex != -1 && heightIndex > widthIndex)
            {
                heightIndex -= numRemoved;
            }

            if (styleIndex != -1 && styleIndex > widthIndex)
            {
                styleIndex -= numRemoved;
            }

            firstXmlCloseTagIndex -= numRemoved;

            stringSvgData = RemoveAttribute(stringSvgData, heightIndex, HeightAttribute, out numRemoved);
            if (styleIndex != -1 && styleIndex > heightIndex)
            {
                styleIndex -= numRemoved;
            }

            firstXmlCloseTagIndex -= numRemoved;

            stringSvgData = RemoveAttribute(stringSvgData, styleIndex, StyleAttribute, out numRemoved);
            firstXmlCloseTagIndex -= numRemoved;

            width = CheckUnit(width);
            height = CheckUnit(height);

            string widthR = RemoveUnit(width);
            string heightR = RemoveUnit(height);

            string centering = "position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%);";

            // max-width and max-height not supported. Extra CSS is needed for it to work.
            string scaling = $"max-width: {width} ; max-height: {height} ;";
            scaling += $"  _height:expression(this.scrollHeight &gt; {heightR} ? &quot; {height}&quot; : &quot;auto&quot;); _width:expression(this.scrollWidth &gt; {widthR} ? &quot;{width}&quot; : &quot;auto&quot;);";

            string newStyle = $"style=\"{scaling}{centering}{oldStyle}\"";
            int insertAt = firstXmlCloseTagIndex;

            stringSvgData = stringSvgData.Insert(insertAt, " " + newStyle);

            if (!hasViewBox)
            {
                // Fixes https://github.com/microsoft/PowerToys/issues/18107
                string viewBox = $"viewBox=\"0 0 {widthR} {heightR}\"";
                stringSvgData = stringSvgData.Insert(insertAt, " " + viewBox);
            }

            return stringSvgData;
        }

        /// <summary>
        /// If there is a CSS unit at the end return the same string, else return the string with a px unit at the end
        /// </summary>
        /// <param name="length">CSS length</param>
        /// <returns>Returns modified length</returns>
        private static string CheckUnit(string length)
        {
            string[] cssUnits = { "cm", "mm", "in", "px", "pt", "pc", "em", "ex", "ch", "rem", "vw", "vh", "vmin", "vmax", "%" };
            foreach (var unit in cssUnits)
            {
                if (length.EndsWith(unit, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    return length;
                }
            }

            return length + "px";
        }

        /// <summary>
        /// Swaps positions of default and svg namespace definitions if default namespace comes first in original SVG data
        /// </summary>
        /// <param name="svgData">SVG data</param>
        /// <returns>Returns modified SVG data</returns>
        public static string SwapNamespaces(string svgData)
        {
            const string defaultNamespace = "xmlns=\"http://www.w3.org/2000/svg\"";
            const string svgNamespace = "xmlns:svg=\"http://www.w3.org/2000/svg\"";

            int defaultNamespaceIndex = svgData.IndexOf(defaultNamespace, StringComparison.InvariantCultureIgnoreCase);
            int svgNamespaceIndex = svgData.IndexOf(svgNamespace, StringComparison.InvariantCultureIgnoreCase);

            if (defaultNamespaceIndex != -1 && svgNamespaceIndex != -1 && defaultNamespaceIndex < svgNamespaceIndex)
            {
                svgData = svgData.Replace(defaultNamespace, "{0}", StringComparison.InvariantCultureIgnoreCase);
                svgData = svgData.Replace(svgNamespace, "{1}", StringComparison.InvariantCultureIgnoreCase);
                svgData = string.Format(CultureInfo.InvariantCulture, svgData, svgNamespace, defaultNamespace);
            }

            return svgData;
        }

        /// <summary>
        /// Remove a CSS unit from the end of the string
        /// </summary>
        /// <param name="length">CSS length</param>
        /// <returns>Returns modified length</returns>
        private static string RemoveUnit(string length)
        {
            string[] cssUnits = { "cm", "mm", "in", "px", "pt", "pc", "em", "ex", "ch", "rem", "vw", "vh", "vmin", "vmax", "%" };
            foreach (var unit in cssUnits)
            {
                if (length.EndsWith(unit, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    length = length.Remove(length.Length - unit.Length);
                    return length;
                }
            }

            return length;
        }
    }
}
