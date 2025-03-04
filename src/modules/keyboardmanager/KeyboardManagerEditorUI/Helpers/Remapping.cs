// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Helpers
{
    public class Remapping
    {
        public List<string> OriginalKeys { get; set; } = new List<string>();

        public List<string> RemappedKeys { get; set; } = new List<string>();

        public bool IsAllApps { get; set; } = true;

        public string AppName { get; set; } = "All apps";

        public bool IsEnabled { get; set; } = true;
    }
}
