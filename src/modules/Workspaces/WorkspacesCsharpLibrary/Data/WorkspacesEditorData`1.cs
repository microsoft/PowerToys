// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WorkspacesCsharpLibrary.Utils;

namespace WorkspacesCsharpLibrary.Data;

/// <summary>
/// Shared JSON serializer helper for Workspaces payloads.
/// </summary>
public class WorkspacesEditorData<T>
{
    [RequiresUnreferencedCode("JSON serialization uses reflection-based serializer.")]
    [RequiresDynamicCode("JSON serialization uses reflection-based serializer.")]
    public T Read(string file)
    {
        IOUtils ioUtils = new();
        string data = ioUtils.ReadFile(file);
        return JsonSerializer.Deserialize<T>(data, WorkspacesJsonOptions.EditorOptions)!;
    }

    [RequiresUnreferencedCode("JSON serialization uses reflection-based serializer.")]
    [RequiresDynamicCode("JSON serialization uses reflection-based serializer.")]
    public string Serialize(T data)
    {
        return JsonSerializer.Serialize(data, WorkspacesJsonOptions.EditorOptions);
    }
}
