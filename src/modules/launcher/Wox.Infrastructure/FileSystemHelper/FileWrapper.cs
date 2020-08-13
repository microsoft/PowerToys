// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class FileWrapper : IFileWrapper
    {
        public FileWrapper()
        {
        }

        public string[] ReadAllLines(string path)
        {
            try
            {
                return File.ReadAllLines(path);
            }
            catch (IOException ex)
            {
                Log.Info($"File {path} is being accessed by another process| {ex.Message}");
                return new string[] { string.Empty };
            }
        }
    }
}
