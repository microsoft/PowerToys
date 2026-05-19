// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class PowerToysCliCatalog
    {
        public int SchemaVersion { get; init; }

        public List<CommandTemplateModule> Modules { get; init; } = new();
    }

    public sealed class CommandTemplateModule
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;

        public string? IconGlyph { get; init; }

        public List<CommandTemplate> Commands { get; init; } = new();
    }

    public sealed class CommandTemplate
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;

        public string Executable { get; init; } = string.Empty;

        public string ArgsTemplate { get; init; } = string.Empty;

        public List<TemplateParameter> Parameters { get; init; } = new();
    }

    public sealed class TemplateParameter
    {
        public string Name { get; init; } = string.Empty;

        public string LabelResourceKey { get; init; } = string.Empty;

        public string Type { get; init; } = "Text";

        public bool Required { get; init; } = true;

        public List<TemplateChoice>? Choices { get; init; }
    }

    public sealed class TemplateChoice
    {
        public string Value { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;
    }
}
