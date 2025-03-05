// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Interop
{
    public class ShortcutKeyMapping
    {
        public string OriginalKeys { get; set; } = string.Empty;

        public string TargetKeys { get; set; } = string.Empty;

        public string TargetApp { get; set; } = string.Empty;

        public ShortcutOperationType OperationType { get; set; }

        public string TargetText { get; set; } = string.Empty;

        public string ProgramPath { get; set; } = string.Empty;

        public string ProgramArgs { get; set; } = string.Empty;

        public string UriToOpen { get; set; } = string.Empty;
    }
}
