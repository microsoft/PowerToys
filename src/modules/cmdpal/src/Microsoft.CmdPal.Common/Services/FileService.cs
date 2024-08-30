// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.CmdPal.Common.Contracts;

namespace Microsoft.CmdPal.Common.Services;

public class FileService : IFileService
{
    private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

#pragma warning disable CS8603 // Possible null reference return.
    public T Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            using var fileStream = File.OpenText(path);
            return JsonSerializer.Deserialize<T>(fileStream.BaseStream);
        }

        return default;
    }
#pragma warning restore CS8603 // Possible null reference return.

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonSerializer.Serialize(content);
        File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, _encoding);
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
