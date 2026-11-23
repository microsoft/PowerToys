// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace FancyZonesEditorCommon.Utils
{
    public class IOUtils
    {
        private readonly IFileSystem _fileSystem;

        public IOUtils()
            : this(new FileSystem())
        {
        }

        internal IOUtils(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public void WriteFile(string fileName, string data)
        {
            _fileSystem.File.WriteAllText(fileName, data);
        }

        public string ReadFile(string fileName)
        {
            return ReadFile(fileName, optional: false);
        }

        internal string ReadFileOrDefault(string fileName)
        {
            return ReadFile(fileName, optional: true);
        }

        private string ReadFile(string fileName, bool optional)
        {
            if (!optional && !_fileSystem.File.Exists(fileName))
            {
                return string.Empty;
            }

            for (int attempts = 0; attempts < 10; attempts++)
            {
                try
                {
                    using (FileSystemStream inputStream = _fileSystem.File.Open(fileName, FileMode.Open))
                    using (StreamReader reader = new StreamReader(inputStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (FileNotFoundException) when (optional)
                {
                    return null;
                }
                catch (DirectoryNotFoundException) when (optional && IsMissingDirectory(fileName))
                {
                    return null;
                }
                catch (Exception ex) when (!optional || (ex is IOException and not FileNotFoundException and not DirectoryNotFoundException && attempts < 9))
                {
                    Task.Delay(10).Wait();
                }
            }

            return string.Empty;
        }

        private bool IsMissingDirectory(string fileName)
        {
            string directory = _fileSystem.Path.GetDirectoryName(_fileSystem.Path.GetFullPath(fileName));
            while (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    return (_fileSystem.File.GetAttributes(directory) & FileAttributes.Directory) != 0;
                }
                catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    // ERROR_PATH_NOT_FOUND can also mean an existing ancestor is a regular file.
                    directory = _fileSystem.Path.GetDirectoryName(directory);
                }
            }

            return true;
        }
    }
}
