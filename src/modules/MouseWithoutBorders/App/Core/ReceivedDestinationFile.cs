// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace MouseWithoutBorders.Core;

internal sealed class ReceivedDestinationFile : IDisposable
{
    private readonly Action<string, string> commitFile;
    private readonly Action<string> deleteFile;
    private readonly string destinationPath;
    private readonly string stagingPath;
    private bool completed;

    internal ReceivedDestinationFile(string destinationPath, Action<string> deleteFile, Action<string, string> commitFile)
    {
        this.destinationPath = destinationPath;
        this.deleteFile = deleteFile;
        this.commitFile = commitFile;

        stagingPath = Path.Combine(
            Path.GetDirectoryName(destinationPath) ?? ".",
            $".{Guid.NewGuid():N}.partial");
        Stream = new FileStream(stagingPath, FileMode.CreateNew);
    }

    internal FileStream Stream { get; }

    internal void Complete()
    {
        Stream.Flush();
        Stream.Dispose();
        commitFile(stagingPath, destinationPath);
        completed = true;
    }

    public void Dispose()
    {
        Stream.Dispose();

        if (!completed)
        {
            deleteFile(stagingPath);
        }
    }
}
