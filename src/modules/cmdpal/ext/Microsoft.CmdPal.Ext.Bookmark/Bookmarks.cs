// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public sealed class Bookmarks
{
    public List<BookmarkData> Data { get; set; } = [];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
    };

    public static Bookmarks ReadFromFile(string path)
    {
        var data = new Bookmarks();

        // if the file exists, load it and append the new item
        if (File.Exists(path))
        {
            var jsonStringReading = File.ReadAllText(path);

            if (!string.IsNullOrEmpty(jsonStringReading))
            {
                data = JsonSerializer.Deserialize<Bookmarks>(jsonStringReading, _jsonOptions) ?? new Bookmarks();
            }
        }

        return data;
    }

    public static void WriteToFile(string path, Bookmarks data)
    {
        var jsonString = JsonSerializer.Serialize(data, _jsonOptions);

        File.WriteAllText(BookmarksCommandProvider.StateJsonPath(), jsonString);
    }
}
