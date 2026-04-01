// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace WorkspacesCsharpLibrary.Data;

public struct ApplicationWrapper
{
    public struct WindowPositionWrapper
    {
        [JsonPropertyName("X")]
        public int X { get; set; }

        [JsonPropertyName("Y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("application")]
    public string Application { get; set; }

    [JsonPropertyName("application-path")]
    public string ApplicationPath { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("package-full-name")]
    public string PackageFullName { get; set; }

    [JsonPropertyName("app-user-model-id")]
    public string AppUserModelId { get; set; }

    [JsonPropertyName("pwa-app-id")]
    public string PwaAppId { get; set; }

    [JsonPropertyName("command-line-arguments")]
    public string CommandLineArguments { get; set; }

    [JsonPropertyName("is-elevated")]
    public bool IsElevated { get; set; }

    [JsonPropertyName("can-launch-elevated")]
    public bool CanLaunchElevated { get; set; }

    [JsonPropertyName("minimized")]
    public bool Minimized { get; set; }

    [JsonPropertyName("maximized")]
    public bool Maximized { get; set; }

    [JsonPropertyName("position")]
    public WindowPositionWrapper Position { get; set; }

    [JsonPropertyName("monitor")]
    public int Monitor { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}
