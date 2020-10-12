// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public class SystemIOProvider : IIOProvider
    {
        public bool CreateDirectory(string path)
        {
            var directioryInfo = Directory.CreateDirectory(path);
            return directioryInfo != null;
        }

        public void DeleteDirectory(string path)
        {
            Directory.Delete(path, recursive: true);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string content)
        {
            File.WriteAllText(path, content);
        }
    }
}
