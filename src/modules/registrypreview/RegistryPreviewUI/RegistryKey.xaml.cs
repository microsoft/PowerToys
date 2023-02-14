// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Management.Core;
using Windows.Storage;

namespace RegistryPreview
{
    /// <summary>
    /// Class representing an each item in the tree view, each one a Registry Key;
    /// FullPath is so we can re-select the node after a live update
    /// Tag is an Array of ListViewItems that below to each key
    /// </summary>
    public class RegistryKey
    {
        public string Name { get; set; }

        public string FullPath { get; set; }

        public object Tag { get; set; }

        public RegistryKey(string name, string fullPath)
        {
            this.Name = name;
            this.FullPath = fullPath;
        }
    }
}
