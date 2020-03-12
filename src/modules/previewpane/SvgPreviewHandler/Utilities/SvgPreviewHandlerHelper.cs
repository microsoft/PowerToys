// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SvgPreviewHandler.Utilities
{
    /// <summary>
    /// Helper utilities for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewHandlerHelper
    {
        /// <summary>
        /// Dictionary of elements that are blocked from Svg for preview pane.
        /// </summary>
        private static Dictionary<string, bool> blockedElementsName = new Dictionary<string, bool>
        {
            { "script", true },
            { "image", true },
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
                    var elementName = element?.Name?.LocalName?.ToLower();
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
    }
}
