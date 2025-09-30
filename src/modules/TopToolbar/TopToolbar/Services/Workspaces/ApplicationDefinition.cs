// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class ApplicationDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("application")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("application-path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("package-full-name")]
        public string PackageFullName { get; set; } = string.Empty;

        [JsonPropertyName("app-user-model-id")]
        public string AppUserModelId { get; set; } = string.Empty;

        [JsonPropertyName("pwa-app-id")]
        public string PwaAppId { get; set; } = string.Empty;

        [JsonPropertyName("command-line-arguments")]
        public string CommandLineArguments { get; set; } = string.Empty;

        [JsonPropertyName("is-elevated")]
        public bool IsElevated { get; set; }

        [JsonPropertyName("can-launch-elevated")]
        public bool CanLaunchElevated { get; set; }

        [JsonPropertyName("minimized")]
        public bool Minimized { get; set; }

        [JsonPropertyName("maximized")]
        public bool Maximized { get; set; }

        [JsonPropertyName("monitor")]
        public int MonitorIndex { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public ApplicationPosition Position { get; set; } = new();

        internal sealed class ApplicationPosition
        {
            [JsonPropertyName("X")]
            public int X { get; set; }

            [JsonPropertyName("Y")]
            public int Y { get; set; }

            [JsonPropertyName("width")]
            public int Width { get; set; }

            [JsonPropertyName("height")]
            public int Height { get; set; }

            public bool IsEmpty => Width == 0 || Height == 0;
        }
    }
}
