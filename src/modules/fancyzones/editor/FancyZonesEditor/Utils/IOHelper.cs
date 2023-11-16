// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace Microsoft.FancyZonesEditor.UITests.Utils
{
    public class IOHelper
    {
        private readonly IFileSystem _fileSystem = new FileSystem();

        public IOHelper()
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
                var attempts = 0;
                while (attempts < 10)
                {
                    try
                    {
                        using (Stream inputStream = _fileSystem.File.Open(fileName, FileMode.Open))
                        using (StreamReader reader = new StreamReader(inputStream))
                        {
                            string data = reader.ReadToEnd();
                            inputStream.Close();
                            return data;
                        }
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
