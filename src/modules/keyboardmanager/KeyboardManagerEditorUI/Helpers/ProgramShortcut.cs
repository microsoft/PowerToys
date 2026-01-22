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
    public class ProgramShortcut
    {
        public List<string> Shortcut { get; set; } = new List<string>();

        public string AppToRun { get; set; } = string.Empty;

        public string Args { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string Id { get; set; } = string.Empty;
    }
}
