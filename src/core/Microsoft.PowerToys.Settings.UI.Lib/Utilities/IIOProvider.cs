// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public interface IIOProvider
    {
        bool FileExists(string path);

        bool DirectoryExists(string path);

        bool CreateDirectory(string path);

        void DeleteDirectory(string path);

        void WriteAllText(string path, string content);

        string ReadAllText(string path);
    }
}
