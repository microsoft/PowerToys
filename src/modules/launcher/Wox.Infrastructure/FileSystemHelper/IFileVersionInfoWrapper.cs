// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Wox.Infrastructure.FileSystemHelper
{
    public interface IFileVersionInfoWrapper
    {
        FileVersionInfo GetVersionInfo(string path);

        string FileDescription { get; set; }
    }
}
