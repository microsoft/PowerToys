// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

[TestClass]
public sealed class StorageEnvironmentTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static readonly string[] SnippetFileNames = { TextExpanderSnippetFileNames.Current };

    [TestMethod]
    public void PollingCallsBackOnlyWhenAWatchedFileArrivesOrChanges()
    {
        // The folder doesn't exist yet, so no FileSystemWatcher sees it and only polling can.
        using var root = new TempFolder();
        string folder = Path.Combine(root.Path, "snippets");
        string file = Path.Combine(folder, TextExpanderSnippetFileNames.Current);
        int calls = 0;
        using IDisposable watch = new TextExpanderStorageEnvironment(PollInterval).WatchForFiles(folder, SnippetFileNames, () => Interlocked.Increment(ref calls));

        Thread.Sleep(PollInterval * 10);
        Assert.AreEqual(0, Volatile.Read(ref calls), "Nothing changed, so there is nothing to read.");

        Directory.CreateDirectory(folder);
        File.WriteAllText(file + ".tmp", SnippetJson.Of("!a=A"));
        File.Move(file + ".tmp", file);
        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref calls) > 0, WaitLimit), "A file arriving is seen.");

        Thread.Sleep(PollInterval * 10);
        Assert.AreEqual(1, Volatile.Read(ref calls), "An unchanged file isn't reported again.");

        File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddMinutes(-1));
        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref calls) > 1, WaitLimit), "A changed write time is seen.");
    }
}
