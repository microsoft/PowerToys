﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace WorkspacesEditor.Utils
{
    public class IOUtils
    {
        private readonly IFileSystem _fileSystem = new FileSystem();

        public IOUtils()
        {
        }

        public void WriteFile(string fileName, string data)
        {
            _fileSystem.File.WriteAllText(fileName, data);
        }

        public string ReadFile(string fileName)
        {
            if (_fileSystem.File.Exists(fileName))
            {
                int attempts = 0;
                while (attempts < 10)
                {
                    try
                    {
                        using FileSystemStream inputStream = _fileSystem.File.Open(fileName, FileMode.Open);
                        using StreamReader reader = new(inputStream);
                        string data = reader.ReadToEnd();
                        inputStream.Close();
                        return data;
                    }
                    catch (Exception)
                    {
                        Task.Delay(10).Wait();
                    }

                    attempts++;
                }
            }

            return string.Empty;
        }
    }
}
