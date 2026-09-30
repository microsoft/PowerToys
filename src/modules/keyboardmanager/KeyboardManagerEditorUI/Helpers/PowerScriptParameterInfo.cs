// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>A typed PowerScript parameter rendered by Keyboard Manager using its own controls.</summary>
    public sealed class PowerScriptParameterInfo
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = "string";

        public bool IsRequired { get; init; }

        public string? Label { get; init; }

        public string? Description { get; init; }

        public string? Default { get; init; }

        public List<string> Options { get; init; } = new();

        public int? Min { get; init; }

        public int? Max { get; init; }

        public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Name : Label;
    }
}
