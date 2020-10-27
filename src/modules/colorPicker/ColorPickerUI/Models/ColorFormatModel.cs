// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Media;

namespace ColorPicker.Models
{
    public class ColorFormatModel
    {
        public string FormatName { get; set; }

        public Func<Color, string> Convert { get; set; }
    }
}
