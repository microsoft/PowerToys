// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileExplorerScripts.Core;

namespace FileExplorerScripts;

internal sealed record InvocationRequest(string? ScriptId, IReadOnlyList<string> Files)
{
    public static InvocationRequest Read(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _))
        {
            throw new ArgumentException("Invalid Explorer request identifier.", nameof(id));
        }

        var path = Path.Combine(ExplorerSettings.RequestsPath, id + ".json");
        try
        {
            var file = new FileInfo(path);
            if (file.Length > 2 * 1024 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(ExplorerSettings.RequestsPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Invalid or oversized Explorer request.");
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var files = document.RootElement.GetProperty("files").EnumerateArray().Select(item => item.GetString()!).ToArray();
            ExplorerScriptService.ValidateSelection(files);
            return new InvocationRequest(document.RootElement.GetProperty("id").GetString(), files);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
