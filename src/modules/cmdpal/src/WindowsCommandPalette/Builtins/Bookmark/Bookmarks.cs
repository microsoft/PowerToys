// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Json.Path;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using Windows.System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Run.Bookmarks;

public sealed class Bookmarks
{
    public List<BookmarkData> Data { get; set; } = [];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
    };

    public static Bookmarks ReadFromFile(string path)
    {
        Bookmarks data = null;

        // if the file exists, load it and append the new item
        if (File.Exists(path))
        {
            var jsonStringReading = File.ReadAllText(path);

            if (!string.IsNullOrEmpty(jsonStringReading))
            {
                data = JsonSerializer.Deserialize<Bookmarks>(jsonStringReading, _jsonOptions);
            }
        }

        data ??= new Bookmarks();

        return data;
    }

    public static void WriteToFile(string path, Bookmarks data)
    {
        var jsonString = JsonSerializer.Serialize<Bookmarks>(data, _jsonOptions);
        File.WriteAllText(BookmarksActionProvider.StateJsonPath(), jsonString);
    }
}
