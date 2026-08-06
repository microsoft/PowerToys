// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

using SystemThread = System.Threading.Thread;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class ReceivedDestinationFileTests
{
    [TestMethod]
    public void ExecuteImpersonatedActionRevertsWhenCallbackThrows()
    {
        var reverted = false;

        Assert.ThrowsException<InvalidOperationException>(() => Launch.ExecuteImpersonatedAction(
            () => throw new InvalidOperationException(),
            () => reverted = true));

        Assert.IsTrue(reverted);
    }

    [TestMethod]
    public void ExecuteImpersonatedActionRetriesRevertAfterFirstFailure()
    {
        var reversionAttempts = 0;
        var failFastCalled = false;

        _ = Assert.ThrowsException<Win32Exception>(() => Launch.ExecuteImpersonatedAction(
            () => { },
            () => ++reversionAttempts == 2,
            () => 5,
            (_, _) => failFastCalled = true));

        Assert.AreEqual(2, reversionAttempts);
        Assert.IsFalse(failFastCalled);
    }

    [TestMethod]
    public void ExecuteImpersonatedActionFailsFastWhenRevertRetryFails()
    {
        var reversionAttempts = 0;
        Exception? failFastException = null;

        _ = Assert.ThrowsException<Win32Exception>(() => Launch.ExecuteImpersonatedAction(
            () => { },
            () =>
            {
                reversionAttempts++;
                return false;
            },
            () => 5,
            (_, exception) => failFastException = exception));

        Assert.AreEqual(2, reversionAttempts);
        Assert.IsInstanceOfType<Win32Exception>(failFastException!);
    }

    [TestMethod]
    public void PaddedPayloadIsAcceptedWhenExpectedBytesWereWritten()
    {
        using var destination = new MemoryStream();
        var receivedCount = 0L;
        var paddedPayload = new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 };

        var writtenCount = Clipboard.WriteReceivedData(
            destination,
            paddedPayload,
            paddedPayload.Length,
            expectedLength: 4,
            ref receivedCount);

        Assert.AreEqual(paddedPayload.Length, receivedCount);
        Assert.AreEqual(4, writtenCount);
        Assert.IsTrue(Clipboard.HasExpectedReceivedDataLength(destination, expectedLength: 4));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, destination.ToArray());
    }

    [TestMethod]
    public void SecondReceiveIsRejectedWhenFirstDoesNotFinishWithinWaitTimeout()
    {
        using var firstReceiveStarted = new ManualResetEventSlim();
        using var allowFirstReceiveToFinish = new ManualResetEventSlim();
        var firstReceiveThread = new SystemThread(() =>
        {
            firstReceiveStarted.Set();
            allowFirstReceiveToFinish.Wait();
        });

        firstReceiveThread.Start();
        Assert.IsTrue(firstReceiveStarted.Wait(millisecondsTimeout: 1000));

        try
        {
            Assert.IsFalse(Clipboard.CanStartClipboardReceive(firstReceiveThread, SystemThread.CurrentThread, waitMilliseconds: 1));
        }
        finally
        {
            allowFirstReceiveToFinish.Set();
            Assert.IsTrue(firstReceiveThread.Join(millisecondsTimeout: 1000));
        }
    }

    [TestMethod]
    public void IncompleteNewDestinationIsDeletedAfterWriteFailure()
    {
        var directory = CreateTestDirectory();
        var path = Path.Combine(directory, "received.txt");

        try
        {
            try
            {
                using var destination = CreateDestinationFile(path);
                destination.Stream.WriteByte(1);
                throw new IOException("Simulated write failure.");
            }
            catch (IOException)
            {
            }

            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(0, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void IncompleteExistingDestinationPreservesOriginalContents()
    {
        var directory = CreateTestDirectory();
        var path = Path.Combine(directory, "received.txt");
        File.WriteAllText(path, "existing");

        try
        {
            try
            {
                using var destination = CreateDestinationFile(path);
                destination.Stream.WriteByte(1);
                throw new IOException("Simulated write failure.");
            }
            catch (IOException)
            {
            }

            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual("existing", File.ReadAllText(path));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CompleteExistingDestinationReplacesOriginalContents()
    {
        var directory = CreateTestDirectory();
        var path = Path.Combine(directory, "received.txt");
        File.WriteAllText(path, "existing");

        try
        {
            using (var destination = CreateDestinationFile(path))
            {
                destination.Stream.WriteByte(1);
                destination.Complete();
            }

            CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(path));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MaximumLengthDestinationNameCanBeStaged()
    {
        var directory = CreateTestDirectory();
        var path = Path.Combine(directory, new string('a', 255));

        try
        {
            using (var destination = CreateDestinationFile(path))
            {
                destination.Stream.WriteByte(1);
                destination.Complete();
            }

            CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ReceivedDestinationFile CreateDestinationFile(string path)
    {
        return new ReceivedDestinationFile(path, File.Delete, (source, destination) => File.Move(source, destination, overwrite: true));
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        return directory;
    }
}
