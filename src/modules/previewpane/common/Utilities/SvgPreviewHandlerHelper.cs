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
        /// <summary>
        /// Dictionary of elements in lower case that are blocked from Svg for preview pane.
        /// Reference for list of Svg Elements: https://developer.mozilla.org/en-US/docs/Web/SVG/Element.
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
#pragma warning disable CA1308 // Normalize strings to uppercase
                    var elementName = element?.Name?.LocalName?.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
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

        /// <summary>
        /// Add proper
        /// </summary>
        /// <param name="stringSvgData">Input Svg</param>
        /// <returns>Returns modified svgData with added style</returns>
        public static string AddStyleSVG(string stringSvgData)
        {
            XElement svgData = XElement.Parse(stringSvgData);

            var attributes = svgData.Attributes();
            string width = string.Empty;
            string height = string.Empty;
            string widthR = string.Empty;
            string heightR = string.Empty;
            string oldStyle = string.Empty;
            bool hasViewBox = false;

            // Get width and height of element and remove it afterwards because it will be added inside style attribute
            for (int i = 0; i < attributes.Count(); i++)
            {
                if (attributes.ElementAt(i).Name == "height")
                {
                    height = attributes.ElementAt(i).Value;
                    attributes.ElementAt(i).Remove();
                    i--;
                }
                else if (attributes.ElementAt(i).Name == "width")
                {
                    width = attributes.ElementAt(i).Value;
                    attributes.ElementAt(i).Remove();
                    i--;
                }
                else if (attributes.ElementAt(i).Name == "style")
                {
                    oldStyle = attributes.ElementAt(i).Value;
                    attributes.ElementAt(i).Remove();
                    i--;
                }
                else if (attributes.ElementAt(i).Name == "viewBox")
                {
                    hasViewBox = true;
                }
            }

            svgData.ReplaceAttributes(attributes);

            height = CheckUnit(height);
            width = CheckUnit(width);
            heightR = RemoveUnit(height);
            widthR = RemoveUnit(width);

            string centering = "position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%);";

            // max-width and max-height not supported. Extra CSS is needed for it to work.
            string scaling = $"max-width: {width} ; max-height: {height} ;";
            scaling += $"  _height:expression(this.scrollHeight > {heightR} ? \" {height}\" : \"auto\"); _width:expression(this.scrollWidth > {widthR} ? \"{width}\" : \"auto\");";

            svgData.Add(new XAttribute("style", scaling + centering + oldStyle));

            if (!hasViewBox)
            {
                // Fixes https://github.com/microsoft/PowerToys/issues/18107
                string viewBox = $"0 0 {widthR} {heightR}";
                svgData.Add(new XAttribute("viewBox", viewBox));
            }

            return svgData.ToString();
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
