// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class FlyoutMenuItem
    {
        public string Label { get; set; }

        public bool IsEnabled { get; set; }

        public string Icon { get; set; }

        public string ToolTip { get; set; }

        public string Tag { get; set; }
    }
}
