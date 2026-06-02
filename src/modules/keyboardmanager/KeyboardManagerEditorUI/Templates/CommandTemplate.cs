// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class CommandTemplate
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;

        public string Executable { get; init; } = string.Empty;

        public string ArgsTemplate { get; init; } = string.Empty;

        public List<TemplateParameter> Parameters { get; init; } = new();
    }
}
