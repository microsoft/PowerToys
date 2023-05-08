// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.Common.Constants
{
    public static class TempFolderPath
    {
        public static string Path => $"{Environment.GetEnvironmentVariable("USERPROFILE")}\\AppData\\LocalLow\\Microsoft\\PowerToys\\Peek-Temp";
    }
}
