// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Espresso.Shell.Core
{
    public class SettingsHelper
    {
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;

        public static FileStream GetSettingsFile(string path, int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                    return fileStream;
                }
                catch (IOException ex)
                {
                    var errorCode = Marshal.GetHRForException(ex) & ((1 << 16) - 1);
                    if (errorCode == ERROR_SHARING_VIOLATION || errorCode == ERROR_LOCK_VIOLATION)
                    {
                        Console.WriteLine("There was another process using the file, so couldn't pick the settings up.");
                    }

                    Thread.Sleep(50);
                }
            }

            return null;
        }
    }
}
