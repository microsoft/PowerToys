// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO.Abstractions;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public interface IFileVersionInfoWrapper
{
    FileVersionInfo? GetVersionInfo(string path);

    string FileDescription { get; set; }
}

public class FileVersionInfoWrapper : IFileVersionInfoWrapper
{
    private readonly IFile _file;

    public FileVersionInfoWrapper()
        : this(new FileSystem().File)
    {
    }

    public FileVersionInfoWrapper(IFile file)
    {
        _file = file;
    }

    public FileVersionInfo? GetVersionInfo(string path)
    {
        if (_file.Exists(path))
        {
            return FileVersionInfo.GetVersionInfo(path);
        }
        else
        {
            return null;
        }
    }

    public string FileDescription { get; set; } = string.Empty;
}
