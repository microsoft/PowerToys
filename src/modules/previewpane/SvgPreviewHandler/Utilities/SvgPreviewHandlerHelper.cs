// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.PowerToys.PreviewHandler.Svg.Utilities
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
            }

            return foundBlockedElement;
        }

        /// <summary>
        /// Add attribute style to SVG element in svgData so that it displays properly
        /// </summary>
        /// <param name="svgData">Input Svg</param>
        /// <returns>Returns modified svgData with added style</returns>
        public static string ScaleSvg(string svgData)
        {
            XElement contacts = XElement.Parse(svgData);
            var attributes = contacts.Attributes();
            string width = string.Empty;
            string height = string.Empty;

            // Get width and height of element and remove it afterwards because it will be added inside style attribute
            for (int i = 0; i < attributes.Count(); i++)
            {
                if (attributes.ElementAt(i).Name == "height")
                {
                    height = attributes.ElementAt(i).Value;
                    attributes.ElementAt(i).Remove();
                }

                if (attributes.ElementAt(i).Name == "width")
                {
                    width = attributes.ElementAt(i).Value;
                    attributes.ElementAt(i).Remove();
                }
            }

            // Set style that will center SVG
            string centering = "position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%);";

            // Set style that will downscale SVG if the preview window is smaller than default SVG size
            string scaling = $"width: min(100%,{width}px); height: min(100%,{height}px);";
            string style = scaling + centering;
            attributes = attributes.Append(new XAttribute("style", style));
            contacts.ReplaceAttributes(attributes);
            return contacts.ToString();
        }
    }
}
