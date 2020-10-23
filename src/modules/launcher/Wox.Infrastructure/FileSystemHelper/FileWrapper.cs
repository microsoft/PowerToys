// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;
using Wox.Plugin.Logger;

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
            catch (System.Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                Log.Info($"Unable to read File: {path}| {ex.Message}", GetType());

                return new string[] { string.Empty };
            }
        }
    }
}
