// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// Remove blocked elements from the Input Svg.
        /// </summary>
        /// <param name="svgData">Input Svg to remove the blocked elements from.</param>
        /// <param name="foundBlockedElement">Set true if any blocked element is present in the Svg otherwise false.</param>
        /// <returns>Svg with removed blocked elements if present.</returns>
        public static string RemoveElements(string svgData, out bool foundBlockedElement)
        {
            foundBlockedElement = false;
            var doc = XDocument.Parse(svgData);
            var elements = doc.Descendants().ToList();
            foreach (XElement element in elements)
            {
                if (blockedElementsName.ContainsKey(element.Name.LocalName.ToLower()))
                {
                    element.Remove();
                    foundBlockedElement = true;
                }
            }

            return doc.ToString();
        }
    }
}
