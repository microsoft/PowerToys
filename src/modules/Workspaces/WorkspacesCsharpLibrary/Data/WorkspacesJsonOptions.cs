// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using WorkspacesCsharpLibrary.Utils;

namespace WorkspacesCsharpLibrary.Data;

internal static class WorkspacesJsonOptions
{
    internal static readonly JsonSerializerOptions EditorOptions = new()
    {
        PropertyNamingPolicy = new DashCaseNamingPolicy(),
        WriteIndented = true,
    };
}
