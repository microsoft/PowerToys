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
    /// Class representing an each item in the list view, each one a Registry Value.
    /// </summary>
    public class RegistryValue
    {
        // Static members
        private static Uri uriStringValue = new Uri("ms-appx:///Assets/string32.png");
        private static Uri uriBinaryValue = new Uri("ms-appx:///Assets/data32.png");

        public string Name { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }

        public Uri ImageUri
        {
            // Based off the Type of the item, pass back the correct image Uri used by the Binding of the DataGrid
            get
            {
                switch (Type)
                {
                    case "REG_SZ":
                    case "REG_EXAND_SZ":
                    case "REG_MULTI_SZ":
                        return uriStringValue;
                }

                return uriBinaryValue;
            }
        }

        public RegistryValue(string name, string type, string value)
        {
            this.Name = name;
            this.Type = type;
            this.Value = value;
        }
    }
}
