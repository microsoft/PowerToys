// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Models
{
    public class PasteFormat
    {
        public IconElement Icon { get; set; }

        public string Name { get; set; }

        public PasteFormats Format { get; set; }
    }
}
