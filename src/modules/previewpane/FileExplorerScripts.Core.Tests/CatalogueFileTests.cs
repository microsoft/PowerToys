// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileExplorerScripts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
public sealed class CatalogueFileTests
{
    [TestMethod]
    public async Task FirstPublicationCreatesTheCatalogueWithoutAReplacementTarget()
    {
        using var fixture = new Fixture();
        var destination = Path.Combine(fixture.Root, "catalogue.json");
        var temporary = fixture.Write("pending.json", "first snapshot");
        await CatalogueFile.PublishAsync(temporary, destination, CancellationToken.None);
        Assert.AreEqual("first snapshot", await File.ReadAllTextAsync(destination));
        Assert.IsFalse(File.Exists(temporary));
    }

    [TestMethod]
    public async Task PublicationPreservesAnOpenDeleteSharingSnapshot()
    {
        using var fixture = new Fixture();
        var destination = fixture.Write("catalogue.json", "old snapshot");
        var temporary = fixture.Write("pending.json", "new snapshot");
        using var snapshot = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        await CatalogueFile.PublishAsync(temporary, destination, CancellationToken.None);
        using var reader = new StreamReader(snapshot);
        Assert.AreEqual("old snapshot", await reader.ReadToEndAsync());
        Assert.AreEqual("new snapshot", await File.ReadAllTextAsync(destination));
    }

    [TestMethod]
    public async Task TransientReaderContentionRetriesOnlyPublication()
    {
        using var fixture = new Fixture();
        var destination = fixture.Write("catalogue.json", "old snapshot");
        var temporary = fixture.Write("pending.json", "new snapshot");
        using var reader = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var publication = CatalogueFile.PublishAsync(temporary, destination, CancellationToken.None);
        await Task.Delay(60);
        Assert.IsFalse(publication.IsCompleted);
        reader.Dispose();
        await publication.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("new snapshot", await File.ReadAllTextAsync(destination));
    }

    [TestMethod]
    public async Task PersistentContentionIsBoundedAndPreservesBothFiles()
    {
        using var fixture = new Fixture();
        var destination = fixture.Write("catalogue.json", "old snapshot");
        var temporary = fixture.Write("pending.json", "new snapshot");
        using var reader = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        try
        {
            await CatalogueFile.PublishAsync(temporary, destination, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Fail("Publication must report persistent reader contention.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.AreEqual("old snapshot", await File.ReadAllTextAsync(destination));
            Assert.AreEqual("new snapshot", await File.ReadAllTextAsync(temporary));
        }
    }

    [TestMethod]
    public async Task CancellationStopsContentionRetries()
    {
        using var fixture = new Fixture();
        var destination = fixture.Write("catalogue.json", "old snapshot");
        var temporary = fixture.Write("pending.json", "new snapshot");
        using var reader = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var cancellation = new CancellationTokenSource();
        var publication = CatalogueFile.PublishAsync(temporary, destination, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => publication);
        Assert.AreEqual("old snapshot", await File.ReadAllTextAsync(destination));
    }

    [TestMethod]
    public async Task MissingSourceIsNotRetriedOrHidden()
    {
        using var fixture = new Fixture();
        var destination = fixture.Write("catalogue.json", "old snapshot");
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() =>
            CatalogueFile.PublishAsync(Path.Combine(fixture.Root, "absent.json"), destination, CancellationToken.None));
        Assert.AreEqual("old snapshot", await File.ReadAllTextAsync(destination));
    }
}
