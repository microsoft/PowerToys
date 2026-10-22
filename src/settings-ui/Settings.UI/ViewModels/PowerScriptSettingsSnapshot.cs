// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

/// <summary>The Host's source-associated local settings snapshot, not an execution authorization.</summary>
public sealed class PowerScriptSettingsSnapshot
{
    public string EntryPath { get; set; } = string.Empty;

    public string SettingsPath { get; set; } = string.Empty;

    public bool HasDescriptor { get; set; }

    public bool HasLocalSettings { get; set; }

    public JsonObject Recommended { get; set; } = new();

    public JsonObject Effective { get; set; } = new();

    public JsonObject Overrides { get; set; } = new();

    public string[] ValidationErrors { get; set; } = Array.Empty<string>();

    public bool HasLocalMxcSettings { get; set; }

    public PowerScriptLocalMxcSettings Mxc { get; set; }
}
