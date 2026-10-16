// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Security.Principal;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using Newtonsoft.Json.Linq;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class ServicePairingCredentialTests
{
    private static readonly string[] ExpectedRotationValues = ["FIRST", "SECOND"];
    private static readonly string[] ExpectedConcurrentRotationValues = ["FIRST", "SECOND", "THIRD"];
    private static readonly string[] ExpectedSupersededRotationValues = ["FIRST", "THIRD"];

    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(AppContext.BaseDirectory, "ServicePairingCredentialTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
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
    public void LocalSystemModeCannotBeDisabledByConfiguration()
    {
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var userSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        Assert.IsTrue(Program.IsLocalSystemCredentialMode(systemSid, configuredServiceMode: false));
        Assert.IsTrue(Program.IsLocalSystemCredentialMode(systemSid, configuredServiceMode: true));
        Assert.IsFalse(Program.IsLocalSystemCredentialMode(userSid, configuredServiceMode: true));
    }

    [TestMethod]
    public void ProtectedRoundTripStoresExpectedFormatAndScope()
    {
        var store = CreateStore("owner");
        var saved = store.Write("ABCD-EFGH", 0);
        var document = JObject.Parse(File.ReadAllText(store.CredentialPath));

        Assert.AreEqual(1L, saved.Revision);
        Assert.AreEqual(ServicePairingCredentialStore.CurrentFormatVersion, (int)document["Version"]!);
        Assert.AreEqual("owner", (string)document["OwnerSid"]!);
        Assert.AreNotEqual("ABCD-EFGH", (string)document["ProtectedKey"]!);
        Assert.AreEqual(saved, store.Read());
    }

    [TestMethod]
    public void InvalidKeyMaterialIsRejected()
    {
        var store = CreateStore("owner");

        Assert.ThrowsException<ArgumentException>(() => store.Write(string.Empty, 0));
        Assert.ThrowsException<ArgumentException>(() => store.Write("  ", 0));
        Assert.ThrowsException<ArgumentException>(() => store.Write(ServicePairingCredentialStore.ConfiguredIndicator, 0));
    }

    [TestMethod]
    public void InvalidDocumentsAreRejected()
    {
        var store = CreateStore("owner");
        Assert.ThrowsException<InvalidDataException>(() => store.Read());

        Directory.CreateDirectory(Path.GetDirectoryName(store.CredentialPath)!);
        File.WriteAllText(store.CredentialPath, "not-json");
        Assert.ThrowsException<InvalidDataException>(() => store.Read());
        Assert.AreEqual("REPLACEMENT", store.Write("REPLACEMENT", 0).Key);

        File.WriteAllText(store.CredentialPath, """{"Version":1,"OwnerSid":"owner","Revision":1,"ProtectedKey":"not-base64"}""");
        Assert.ThrowsException<InvalidDataException>(() => store.Read());

        File.Delete(store.CredentialPath);
        store.Write("VALID", 0);
        var otherOwnerStore = CreateStore("other-owner");
        Directory.CreateDirectory(Path.GetDirectoryName(otherOwnerStore.CredentialPath)!);
        File.Copy(store.CredentialPath, otherOwnerStore.CredentialPath, overwrite: true);
        Assert.ThrowsException<InvalidDataException>(() => otherOwnerStore.Read());
    }

    [TestMethod]
    public void MissingProtectedCredentialDoesNotReuseSharedCredential()
    {
        Assert.AreEqual(
            MouseWithoutBordersIpc.PairingSetupRequiredIndicator,
            MouseWithoutBorders.Class.Settings.GetSharedSecurityKeySettingValue(
                protectedMode: true,
                protectedCredentialConfigured: false,
                currentValue: "EXISTING-KEY"));
    }

    [TestMethod]
    public void MissingProtectedCredentialDoesNotCreateReplacementMaterial()
    {
        var store = CreateStore("owner");
        using var manager = new ServicePairingCredentialManager(store, new TestNotifier(), _ => Assert.Fail());

        Assert.IsFalse(manager.TryLoad(out var key));
        Assert.AreEqual(string.Empty, key);
        Assert.IsFalse(File.Exists(store.CredentialPath));
    }

    [TestMethod]
    public void ExplicitProvisioningUpdatesSharedIndicator()
    {
        var store = CreateStore("owner");
        store.Write("NEW-KEY", 0);

        Assert.AreEqual(
            MouseWithoutBordersIpc.PairingConfiguredIndicator,
            MouseWithoutBorders.Class.Settings.GetSharedSecurityKeySettingValue(
                protectedMode: true,
                protectedCredentialConfigured: true,
                currentValue: "EXISTING-KEY"));
    }

    [TestMethod]
    public void RestartLoadsProtectedCredential()
    {
        var store = CreateStore("owner");
        store.Write("RESTART-KEY", 0);
        using var manager = new ServicePairingCredentialManager(store, new TestNotifier(), _ => { });

        Assert.IsTrue(manager.TryLoad(out var key));
        Assert.AreEqual("RESTART-KEY", key);
    }

    [TestMethod]
    public void NormalModeKeepsExistingSharedValue()
    {
        Assert.AreEqual(
            "EXISTING-KEY",
            MouseWithoutBorders.Class.Settings.GetSharedSecurityKeySettingValue(
                protectedMode: false,
                protectedCredentialConfigured: false,
                currentValue: "EXISTING-KEY"));
    }

    [TestMethod]
    public void ProtectedModeReturnsCredentialStateIndicator()
    {
        Assert.AreEqual(
            MouseWithoutBordersIpc.PairingConfiguredIndicator,
            MouseWithoutBorders.Class.Settings.GetSharedSecurityKeySettingValue(
                protectedMode: true,
                protectedCredentialConfigured: true,
                currentValue: "EXISTING-KEY"));
        Assert.AreEqual(
            MouseWithoutBordersIpc.PairingSetupRequiredIndicator,
            MouseWithoutBorders.Class.Settings.GetSharedSecurityKeySettingValue(
                protectedMode: true,
                protectedCredentialConfigured: false,
                currentValue: "EXISTING-KEY"));
    }

    [TestMethod]
    public void StaleWriterCannotReplaceNewerCredential()
    {
        var first = CreateStore("owner");
        var second = CreateStore("owner");
        var initial = first.Write("FIRST", 0);

        Assert.AreEqual(initial, second.Read());
        var replacement = first.Write("SECOND", initial.Revision);
        Assert.ThrowsException<InvalidOperationException>(() => second.Write("STALE", initial.Revision));
        Assert.AreEqual(replacement, first.Read());
    }

    [TestMethod]
    public void RotationNotificationReloadsAndInvokesCredentialChangedCallback()
    {
        var store = CreateStore("owner");
        var notifier = new TestNotifier();
        var observed = new List<string>();
        using var manager = new ServicePairingCredentialManager(store, notifier, observed.Add);

        manager.Save("FIRST");
        Assert.AreEqual("FIRST", observed.Single());

        var external = CreateStore("owner");
        var first = external.Read();
        external.Write("SECOND", first.Revision);
        notifier.Trigger();

        CollectionAssert.AreEqual(ExpectedRotationValues, observed);
    }

    [TestMethod]
    public void ConcurrentRotationCallbacksApplyCredentialsInRevisionOrder()
    {
        var store = CreateStore("owner");
        var notifier = new TestNotifier();
        var observed = new List<string>();
        using var secondStarted = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        using var manager = new ServicePairingCredentialManager(
            store,
            notifier,
            key =>
            {
                if (key == "SECOND")
                {
                    secondStarted.Set();
                    Assert.IsTrue(releaseSecond.Wait(TimeSpan.FromSeconds(5)));
                }

                lock (observed)
                {
                    observed.Add(key);
                }
            });

        manager.Save("FIRST");
        var external = CreateStore("owner");
        var first = external.Read();
        var second = external.Write("SECOND", first.Revision);

        var secondNotification = Task.Run(notifier.Trigger);
        Assert.IsTrue(secondStarted.Wait(TimeSpan.FromSeconds(5)));

        external.Write("THIRD", second.Revision);
        var thirdNotification = Task.Run(notifier.Trigger);
        Assert.IsTrue(SpinWait.SpinUntil(() => manager.CurrentRevision == 3, TimeSpan.FromSeconds(5)));
        releaseSecond.Set();

        Assert.IsTrue(Task.WaitAll([secondNotification, thirdNotification], TimeSpan.FromSeconds(5)));
        lock (observed)
        {
            CollectionAssert.AreEqual(ExpectedConcurrentRotationValues, observed);
        }
    }

    [TestMethod]
    public void ReadOnlyLoadDoesNotSuppressRotationCallback()
    {
        var store = CreateStore("owner");
        var notifier = new TestNotifier();
        var observed = new List<string>();
        using var manager = new ServicePairingCredentialManager(store, notifier, observed.Add);

        manager.Save("FIRST");
        var external = CreateStore("owner");
        var first = external.Read();
        external.Write("SECOND", first.Revision);

        Assert.IsTrue(manager.TryLoad(out var key));
        Assert.AreEqual("SECOND", key);
        Assert.AreEqual(first.Revision, manager.CurrentRevision);

        notifier.Trigger();
        CollectionAssert.AreEqual(ExpectedRotationValues, observed);
    }

    [TestMethod]
    public void DeferredApplyCannotRestoreSupersededCredential()
    {
        var store = CreateStore("owner");
        var notifier = new TestNotifier();
        var observed = new List<string>();
        using var manager = new ServicePairingCredentialManager(store, notifier, observed.Add);

        manager.Save("FIRST");
        var second = manager.Save("SECOND", applyToCurrentProcess: false);
        var external = CreateStore("owner");
        var third = external.Write("THIRD", second.Revision);
        notifier.Trigger();

        manager.ApplyCredentialToCurrentProcess(second);

        Assert.AreEqual(third.Revision, manager.CurrentRevision);
        CollectionAssert.AreEqual(ExpectedSupersededRotationValues, observed);
    }

    [TestMethod]
    public void PairingOperationsRequireManagerCapability()
    {
        Program.EnsurePairingOperationAuthorized(false, new TestAuthorization(false));
        Assert.ThrowsException<UnauthorizedAccessException>(() =>
            Program.EnsurePairingOperationAuthorized(true, new TestAuthorization(false)));
        Program.EnsurePairingOperationAuthorized(true, new TestAuthorization(true));
    }

    [TestMethod]
    public void UpgradeStateRequiresNewKeyBeforeConnect()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            Program.EnsureConnectOperationAvailable(
                protectedMode: true,
                protectedCredentialConfigured: false));
        Program.EnsureConnectOperationAvailable(
            protectedMode: true,
            protectedCredentialConfigured: true);
        Program.EnsureConnectOperationAvailable(
            protectedMode: false,
            protectedCredentialConfigured: false);
    }

    private ServicePairingCredentialStore CreateStore(string owner)
    {
        return new ServicePairingCredentialStore(
            _testDirectory,
            owner,
            new TestProtector(),
            $@"Local\PowerToys.MWB.Pairing.Test.{ServicePairingCredentialStore.CreateStableName(_testDirectory + owner)}",
            applyAccessControl: false);
    }

    private sealed class TestProtector : IServicePairingProtector
    {
        public byte[] Protect(byte[] value, byte[] entropy)
        {
            return SHA256.HashData(entropy).Concat(value).ToArray();
        }

        public byte[] Unprotect(byte[] value, byte[] entropy)
        {
            var prefix = SHA256.HashData(entropy);
            if (value.Length <= prefix.Length || !value.AsSpan(0, prefix.Length).SequenceEqual(prefix))
            {
                throw new CryptographicException();
            }

            return value[prefix.Length..];
        }
    }

    private sealed class TestNotifier : IServicePairingChangeNotifier
    {
        private Action? _callback;

        public void Signal()
        {
        }

        public void Start(Action callback)
        {
            _callback = callback;
        }

        public void Trigger()
        {
            _callback!();
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestAuthorization : ISettingsSyncAuthorization
    {
        internal TestAuthorization(bool allowed)
        {
            CanManageServicePairing = allowed;
        }

        public bool CanManageServicePairing { get; }
    }
}
