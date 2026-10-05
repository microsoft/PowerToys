// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamplePagesExtension.Pages;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class SampleUpdatingIconTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(10)]
    public async Task ReturnedReadersRemainUsableAcrossSequentialOpens(int delayMilliseconds)
    {
        byte[] expected = [1, 2, 3, 4];
        using var frame = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(frame))
        {
            writer.WriteBytes(expected);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        var reference = new ProgressStreamReference(frame, delayMilliseconds);
        for (var i = 0; i < 3; i++)
        {
            using var stream = await reference.OpenReadAsync();
            using var reader = new DataReader(stream);
            Assert.AreEqual(4U, await reader.LoadAsync(4));
            var actual = new byte[4];
            reader.ReadBytes(actual);
            CollectionAssert.AreEqual(expected, actual);
            reader.DetachStream();
        }

        Assert.IsTrue(frame.CanRead);
    }

    [TestMethod]
    public async Task ConcurrentReadersHaveIndependentPositionsAndLifetimes()
    {
        using var frame = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(frame))
        {
            writer.WriteBytes([1, 2, 3, 4]);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        var normal = new ProgressStreamReference(frame, delayMilliseconds: 0);
        var delayed = new ProgressStreamReference(frame, delayMilliseconds: 10);
        var reads = await Task.WhenAll(normal.OpenReadAsync().AsTask(), delayed.OpenReadAsync().AsTask(), normal.OpenReadAsync().AsTask());
        using var first = reads[0];
        using var second = reads[1];
        using var reopened = reads[2];
        using var reader = new DataReader(first);
        Assert.AreEqual(4U, await reader.LoadAsync(4));
        Assert.AreEqual((byte)1, reader.ReadByte());
        Assert.AreEqual(4UL, first.Position);
        Assert.AreEqual(0UL, second.Position);
        Assert.AreEqual(0UL, reopened.Position);
        reader.DetachStream();
        first.Dispose();

        second.Seek(2);
        using var secondReader = new DataReader(second);
        Assert.AreEqual(2U, await secondReader.LoadAsync(2));
        Assert.AreEqual((byte)3, secondReader.ReadByte());
        Assert.AreEqual(0UL, reopened.Position);
        secondReader.DetachStream();
    }
}
