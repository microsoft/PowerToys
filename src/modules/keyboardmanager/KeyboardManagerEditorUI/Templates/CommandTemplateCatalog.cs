// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class CommandTemplateCatalog
    {
        private const string ResourceName = "KeyboardManagerEditorUI.Templates.powertoyscli.json";
        private const int SupportedSchemaVersion = 1;

        private static readonly Lazy<CommandTemplateCatalog> _instance = new(() => Load());

        public static CommandTemplateCatalog Instance => _instance.Value;

        public PowerToysCliCatalog Data { get; }

        private CommandTemplateCatalog(PowerToysCliCatalog data)
        {
            Data = data;
        }

        public CommandTemplate? TryFind(string? templateId)
        {
            if (string.IsNullOrEmpty(templateId))
            {
                return null;
            }

            return Data.Modules
                .SelectMany(m => m.Commands)
                .FirstOrDefault(c => c.Id == templateId);
        }

        private static CommandTemplateCatalog Load()
        {
            var assembly = typeof(CommandTemplateCatalog).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{ResourceName}' not found. " +
                    "Check KeyboardManagerEditorUI.csproj <EmbeddedResource> entry.");

            var data = JsonSerializer.Deserialize(
                stream,
                CommandTemplateJsonContext.Default.PowerToysCliCatalog)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize '{ResourceName}' — JsonSerializer returned null.");

            if (data.SchemaVersion != SupportedSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported powertoyscli.json schemaVersion={data.SchemaVersion}; " +
                    $"expected {SupportedSchemaVersion}.");
            }

            if (data.Modules.Count == 0)
            {
                throw new InvalidOperationException(
                    "powertoyscli.json has zero modules — at least one module is required.");
            }

            return new CommandTemplateCatalog(data);
        }
    }
}
