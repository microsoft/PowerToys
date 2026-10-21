// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Settings
{
    public class CliBindingSettings
    {
        public string CommandId { get; set; } = string.Empty;

        public Dictionary<string, string?> Parameters { get; set; } = new Dictionary<string, string?>();
    }
}
