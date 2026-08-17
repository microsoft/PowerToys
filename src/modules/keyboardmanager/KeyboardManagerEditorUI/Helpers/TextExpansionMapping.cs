// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    public sealed class TextExpansionMapping
    {
        public string Id { get; set; } = string.Empty;

        public string SourceText { get; set; } = string.Empty;

        public List<int> ActivationKeys { get; set; } = new();

        public List<string> ActivationKeyNames { get; set; } = new();

        public string ReplacementText { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
    }
}
