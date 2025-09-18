// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public class OOBEItem
    {
        public string Header { get; set; }

        public string Description { get; set; }

        public string Icon { get; set; }

        public object Content { get; set; }
    }
}
