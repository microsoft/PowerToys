// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileExplorerScripts.Core.Tests;

internal sealed class Fixture : IDisposable
{
    internal Fixture()
    {
        Root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N")[..8])).FullName;
        Destination = Directory.CreateDirectory(Path.Combine(Root, "out")).FullName;
        Input = Write("sample.md", "original input");
    }

    internal string Root { get; }

    internal string Destination { get; }

    internal string Input { get; }

    internal string Write(string name, string contents)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
