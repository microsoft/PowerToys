// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Media;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Utility class for finding FrameworkElements within the visual tree of a DependencyObject.
    /// </summary>
    internal sealed class FrameworkElementFinder
    {
        private DependencyObject root;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameworkElementFinder"/> class with the specified root element.
        /// </summary>
        /// <param name="rootElement">The root element of the visual tree.</param>
        public FrameworkElementFinder(DependencyObject rootElement)
        {
            root = rootElement;
        }

        /// <summary>
        /// Finds a FrameworkElement by its Uid within the visual tree.
        /// </summary>
        /// <param name="uid">The Uid to search for.</param>
        /// <returns>The found FrameworkElement, or null if not found.</returns>
        public FrameworkElement FindElementByUid(string uid)
        {
            return FindElementByUidRecursive(root, uid);
        }

        private FrameworkElement FindElementByUidRecursive(DependencyObject current, string uid)
        {
            if (current == null)
            {
                return null;
            }

            if (current is FrameworkElement element && element.Uid == uid)
            {
                return element;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(current, i);
                FrameworkElement foundElement = FindElementByUidRecursive(child, uid);
                if (foundElement != null)
                {
                    return foundElement;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a FrameworkElement by its Name within the visual tree.
        /// </summary>
        /// <param name="name">The Name to search for.</param>
        /// <returns>The found FrameworkElement, or null if not found.</returns>
        public FrameworkElement FindElementByName(string name)
        {
            return FindElementByNameRecursive(root, name);
        }

        private FrameworkElement FindElementByNameRecursive(DependencyObject current, string name)
        {
            if (current == null)
            {
                return null;
            }

            if (current is FrameworkElement element && element.Name == name)
            {
                return element;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(current, i);
                FrameworkElement foundElement = FindElementByNameRecursive(child, name);
                if (foundElement != null)
                {
                    return foundElement;
                }
            }

            return null;
        }
    }
}
