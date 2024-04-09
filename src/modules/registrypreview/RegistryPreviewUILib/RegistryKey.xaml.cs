// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RegistryPreviewUILib
{
    /// <summary>
    /// Class representing an each item in the tree view, each one a Registry Key;
    /// FullPath is so we can re-select the node after a live update
    /// Tag is an Array of ListViewItems that stores all the children for the current object
    /// </summary>
    public class RegistryKey
    {
        public string Name { get; set; }

        public string FullPath { get; set; }

        public string Image { get; set; }

        public string ToolTipText { get; set; }

        public object Tag { get; set; }

        public RegistryKey(string name, string fullPath, string image, string toolTipText)
        {
            this.Name = name;
            this.FullPath = fullPath;
            this.Image = image;
            this.ToolTipText = toolTipText;
        }
    }
}
