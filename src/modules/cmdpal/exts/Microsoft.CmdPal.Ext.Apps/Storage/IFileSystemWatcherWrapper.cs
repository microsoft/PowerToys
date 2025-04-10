// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.IO;

namespace Microsoft.CmdPal.Ext.Apps.Storage;

public interface IFileSystemWatcherWrapper
{
    // Events to watch out for
    event FileSystemEventHandler Created;

    event FileSystemEventHandler Deleted;

    event FileSystemEventHandler Changed;

    event RenamedEventHandler Renamed;

    // Properties of File System watcher
    Collection<string> Filters { get; set; }

    bool EnableRaisingEvents { get; set; }

    NotifyFilters NotifyFilter { get; set; }

    string Path { get; set; }

    bool IncludeSubdirectories { get; set; }
}
