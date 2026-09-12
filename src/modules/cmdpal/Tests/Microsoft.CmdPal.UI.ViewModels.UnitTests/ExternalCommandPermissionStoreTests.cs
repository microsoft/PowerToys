// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ExternalCommandPermissionStoreTests
{
    private string _testDirectory = null!;
    private string _filePath = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"{nameof(ExternalCommandPermissionStoreTests)}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _filePath = Path.Combine(_testDirectory, "permissions.dat");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RememberAsync_UsesProtectorAndRoundTrips()
    {
        var protector = new TestDataProtector();
        var permission = CreatePermission("command.one");

        using (var writer = new ExternalCommandPermissionStore(protector, _filePath))
        {
            Assert.IsTrue(await writer.RememberAsync(permission));
        }

        var protectedText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(_filePath));
        Assert.IsFalse(protectedText.Contains(permission.CommandName, StringComparison.Ordinal));
        Assert.AreEqual(1, protector.ProtectCalls);

        using var reader = new ExternalCommandPermissionStore(protector, _filePath);
        Assert.IsTrue(await reader.IsAllowedAsync(permission.Key));

        var permissions = await reader.GetAllAsync();
        Assert.AreEqual(1, permissions.Count);
        Assert.AreEqual(permission, permissions[0]);
        Assert.AreEqual(1, protector.UnprotectCalls);
    }

    [TestMethod]
    public async Task GetAllAsync_CorruptProtectedData_FailsClosed()
    {
        await File.WriteAllBytesAsync(_filePath, [0x01, 0x02, 0x03]);
        using var store = new ExternalCommandPermissionStore(new TestDataProtector(), _filePath);

        var permissions = await store.GetAllAsync();

        Assert.AreEqual(0, permissions.Count);
        Assert.IsFalse(await store.IsAllowedAsync(CreatePermission("command.one").Key));
    }

    [TestMethod]
    public async Task GetAllAsync_StructurallyInvalidState_FailsClosed()
    {
        string[] invalidStates =
        [
            """{"Permissions":[null]}""",
            """{"Permissions":[{"Key":null,"CommandName":"Command","ProviderName":"Provider"}]}""",
            """{"Permissions":[{"Key":{"Kind":"Command","PackageFamilyName":"Sample.Package_family","ProviderId":"sample.provider","CommandId":"command.one"},"CommandName":null,"ProviderName":"Provider"}]}""",
            """{"Permissions":[{"Key":{"Kind":999,"PackageFamilyName":"Sample.Package_family","ProviderId":"sample.provider","CommandId":"command.one"},"CommandName":"Command","ProviderName":"Provider"}]}""",
        ];

        foreach (var invalidState in invalidStates)
        {
            var protector = new TestDataProtector();
            var protectedData = await protector.ProtectAsync(Encoding.UTF8.GetBytes(invalidState));
            await File.WriteAllBytesAsync(_filePath, protectedData);

            using var store = new ExternalCommandPermissionStore(protector, _filePath);
            Assert.AreEqual(0, (await store.GetAllAsync()).Count, invalidState);
            Assert.IsFalse(await store.IsAllowedAsync(CreatePermission("command.one").Key), invalidState);
        }
    }

    [TestMethod]
    public async Task RevokeAndClear_UpdatePersistedPermissionsAndRaiseEvents()
    {
        using var store = new ExternalCommandPermissionStore(new TestDataProtector(), _filePath);
        var first = CreatePermission("command.one");
        var second = CreatePermission("command.two");
        var eventCount = 0;
        store.PermissionsChanged += (_, _) => eventCount++;

        await store.RememberAsync(first);
        await store.RememberAsync(second);
        Assert.IsTrue(await store.RevokeAsync(first.Key));
        Assert.IsFalse(await store.IsAllowedAsync(first.Key));
        Assert.IsTrue(await store.IsAllowedAsync(second.Key));
        Assert.IsTrue(await store.ClearAsync());

        Assert.AreEqual(0, (await store.GetAllAsync()).Count);
        Assert.AreEqual(4, eventCount);
    }

    [TestMethod]
    public async Task RememberAsync_ProtectionFailure_DoesNotGrantPermission()
    {
        var protector = new TestDataProtector { FailProtection = true };
        var permission = CreatePermission("command.one");
        using var store = new ExternalCommandPermissionStore(protector, _filePath);

        Assert.IsFalse(await store.RememberAsync(permission));
        Assert.IsFalse(await store.IsAllowedAsync(permission.Key));
        Assert.IsFalse(File.Exists(_filePath));
    }

    [TestMethod]
    public async Task RememberAsync_InvalidPermission_DoesNotCorruptState()
    {
        var permission = new ExternalCommandPermission(
            new ExternalCommandPermissionKey(
                (ExternalCommandKind)999,
                "Sample.Package_family",
                "sample.provider",
                "command.one"),
            "Command",
            "Provider");
        using var store = new ExternalCommandPermissionStore(new TestDataProtector(), _filePath);

        Assert.IsFalse(await store.RememberAsync(permission));
        Assert.AreEqual(0, (await store.GetAllAsync()).Count);
        Assert.IsFalse(File.Exists(_filePath));
    }

    [TestMethod]
    public async Task Dispose_AllowsActiveOperationToFinishAndRejectsQueuedOperations()
    {
        var protector = new BlockingDataProtector();
        var store = new ExternalCommandPermissionStore(protector, _filePath);

        var activeOperation = store.RememberAsync(CreatePermission("command.one"));
        await protector.ProtectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var queuedOperation = store.GetAllAsync();
        store.Dispose();
        protector.ContinueProtection.TrySetResult();

        Assert.IsTrue(await activeOperation);
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await queuedOperation);
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await store.GetAllAsync());
    }

    private static ExternalCommandPermission CreatePermission(string commandId) =>
        new(
            new ExternalCommandPermissionKey(
                ExternalCommandKind.Command,
                "Sample.Package_family",
                "sample.provider",
                commandId),
            $"Command {commandId}",
            "Sample provider");

    private sealed class TestDataProtector : IAtRestDataProtector
    {
        private static readonly byte[] Header = [0x43, 0x4D, 0x44, 0x50];

        public int ProtectCalls { get; private set; }

        public int UnprotectCalls { get; private set; }

        public bool FailProtection { get; init; }

        public Task<byte[]> ProtectAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProtectCalls++;
            if (FailProtection)
            {
                throw new InvalidOperationException("Protection failed for the test.");
            }

            var output = new byte[Header.Length + plaintext.Length];
            Header.CopyTo(output, 0);
            for (var i = 0; i < plaintext.Length; i++)
            {
                output[Header.Length + i] = (byte)(plaintext.Span[i] ^ 0xA5);
            }

            return Task.FromResult(output);
        }

        public Task<byte[]> UnprotectAsync(ReadOnlyMemory<byte> protectedData, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UnprotectCalls++;
            if (protectedData.Length < Header.Length ||
                !protectedData.Span[..Header.Length].SequenceEqual(Header))
            {
                throw new InvalidDataException("Protected data has an invalid header.");
            }

            var output = new byte[protectedData.Length - Header.Length];
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = (byte)(protectedData.Span[Header.Length + i] ^ 0xA5);
            }

            return Task.FromResult(output);
        }
    }

    private sealed class BlockingDataProtector : IAtRestDataProtector
    {
        public TaskCompletionSource ProtectionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ContinueProtection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<byte[]> ProtectAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken = default)
        {
            ProtectionStarted.TrySetResult();
            await ContinueProtection.Task.WaitAsync(cancellationToken);
            return plaintext.ToArray();
        }

        public Task<byte[]> UnprotectAsync(ReadOnlyMemory<byte> protectedData, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(protectedData.ToArray());
        }
    }
}
