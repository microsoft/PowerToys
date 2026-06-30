// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class TemplateParameter
    {
        public string Name { get; init; } = string.Empty;

        public string LabelResourceKey { get; init; } = string.Empty;

        public string Type { get; init; } = "Text";

        public bool Required { get; init; } = true;

        public List<TemplateChoice>? Choices { get; init; }
    }
}
