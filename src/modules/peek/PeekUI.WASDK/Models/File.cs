// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PeekUI.WASDK.Models
{
    public class File
    {
        public File(string path)
        {
            Path = path;
        }

        public string Path { get; init; } = string.Empty;
    }
}
