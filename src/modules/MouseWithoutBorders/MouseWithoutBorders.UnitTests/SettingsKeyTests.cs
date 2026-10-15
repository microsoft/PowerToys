// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class SettingsKeyTests
{
    private static readonly string[] PairedMachineMatrix = ["LOCAL", "REMOTE", string.Empty, string.Empty];
    private static readonly string[] UnpairedMachineMatrix = ["LOCAL", string.Empty, string.Empty, string.Empty];

    [TestMethod]
    public void SettingsRequiresPairingKeyCodec()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new Class.Settings(null!));
    }

    [DataTestMethod]
    [DataRow(MouseWithoutBordersIpc.PairingConfiguredIndicator)]
    [DataRow(MouseWithoutBordersIpc.PairingSetupRequiredIndicator)]
    public async Task PresentationIndicatorsCannotBecomePairingKeys(string indicator)
    {
        using var identity = WindowsIdentity.GetCurrent();
        foreach (IPairingKeyCodec codec in new IPairingKeyCodec[] { new PassthroughPairingKeyCodec(), new DpapiPairingKeyCodec(identity.User!) })
        {
            var properties = new MouseWithoutBordersProperties();
            var settings = CreateSettings(properties, codec);
            Assert.ThrowsException<ArgumentException>(() => settings.MyKey = indicator);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                settings.SavePairingSettingsAsync(indicator, PairedMachineMatrix, false, _ => Assert.Fail("Invalid input was published.")));

            properties.SecurityKey.Value = codec.Encrypt(indicator);
            Assert.ThrowsException<ArgumentException>(() => _ = settings.MyKey);
            Assert.IsFalse(settings.HasPairingKey);
        }
    }

    [TestMethod]
    public void EmptyDisplayDoesNotGenerateOrSaveAKey()
    {
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = string.Empty;
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        Assert.AreEqual(string.Empty, settings.PairingKeyDisplayText);
        Assert.AreEqual(string.Empty, properties.SecurityKey.Value);
    }

    [TestMethod]
    public void DisplayTextDoesNotChangeRuntimeKeySemantics()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        settings.MyKey = "TEST-KEY";

        Assert.AreEqual(MouseWithoutBordersIpc.PairingConfiguredIndicator, settings.PairingKeyDisplayText);
        Assert.AreEqual("TEST-KEY", settings.MyKey);
    }

    [TestMethod]
    public void OrdinaryDisplayUsesSettingsValue()
    {
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        settings.MyKey = "TEST-KEY";

        Assert.AreEqual("TEST-KEY", settings.PairingKeyDisplayText);
        Assert.AreEqual("TEST-KEY", settings.MyKey);
    }

    [TestMethod]
    public void SettingsPropertySerializesProtectedValueAndReadsPlaintext()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        settings.MyKey = "TEST-KEY";

        var document = new MouseWithoutBordersSettings { Properties = properties };
        var json = document.ToJsonString();
        StringAssert.Contains(json, PairingKeyCodec.EncryptedPrefix);
        Assert.IsFalse(json.Contains("TEST-KEY", StringComparison.Ordinal));
        Assert.AreEqual("TEST-KEY", settings.MyKey);
        Assert.AreEqual("TEST-KEY", settings.MyKey);

        var reloaded = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(json)!;
        var reader = CreateSettings(reloaded.Properties, new DpapiPairingKeyCodec(identity.User!));
        Assert.AreEqual("TEST-KEY", reader.MyKey);
        Assert.AreEqual(properties.SecurityKey.Value, reloaded.Properties.SecurityKey.Value);
    }

    [TestMethod]
    public void ReloadComparesDecodedKeyRatherThanCiphertext()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        var previousKey = Encryption.MyKey;
        var previousMagic = Encryption.MagicNumber;
        var previousCorrupted = Encryption.KeyCorrupted;
        try
        {
            settings.MyKey = "FIRST";
            Encryption.MyKey = "FIRST";
            Assert.IsFalse(settings.UpdateRuntimeKey());
            settings.MyKey = "SECOND";
            Assert.IsTrue(settings.UpdateRuntimeKey());
            Assert.AreEqual("SECOND", Encryption.MyKey);
            Assert.IsFalse(settings.UpdateRuntimeKey());

            properties.SecurityKey.Value = "mwb-dpapi:v1:invalid";
            Assert.IsTrue(settings.UpdateRuntimeKey());
            Assert.AreEqual(string.Empty, Encryption.MyKey);
            Assert.AreEqual(0u, Encryption.MagicNumber);
            Assert.IsFalse(settings.HasPairingKey);
        }
        finally
        {
            Encryption.MyKey = previousKey;
            Encryption.MagicNumber = previousMagic;
            Encryption.KeyCorrupted = previousCorrupted;
        }
    }

    [TestMethod]
    public void SwitchingToNormalModeDoesNotUseCiphertextAsKey()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var service = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        service.MyKey = "SERVICE-KEY";
        var normal = CreateSettings(properties, new PassthroughPairingKeyCodec());
        Assert.ThrowsException<ArgumentException>(() => _ = normal.MyKey);
        Assert.IsFalse(normal.HasPairingKey);
        normal.MyKey = "NORMAL-KEY";
        Assert.AreEqual("NORMAL-KEY", properties.SecurityKey.Value);
        Assert.AreEqual("NORMAL-KEY", normal.MyKey);
    }

    [TestMethod]
    public void ServiceCodecDoesNotImportPlaintextAndExplicitSetupReplacesIt()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = "OLD-PLAINTEXT";
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        Assert.ThrowsException<CryptographicException>(() => _ = settings.MyKey);
        Assert.IsFalse(settings.HasPairingKey);
        Assert.AreEqual("OLD-PLAINTEXT", properties.SecurityKey.Value);
        settings.MyKey = "NEW-KEY";
        Assert.AreEqual("NEW-KEY", settings.MyKey);
        Assert.IsTrue(settings.HasPairingKey);
    }

    [TestMethod]
    public void NormalEmptySettingsStillGenerateAnOrdinaryKey()
    {
        if (Class.Settings.IsProtectedMode)
        {
            Assert.Inconclusive("This case requires a normal user token.");
        }

        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = string.Empty;
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        Assert.IsTrue(PairingKeyCodec.IsOrdinaryKey(settings.MyKey));
        Assert.AreEqual(settings.MyKey, properties.SecurityKey.Value);
    }

    [TestMethod]
    public void SystemModeAndPairingAuthorizationAreIndependentOfJson()
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var user = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        Assert.IsTrue(Program.IsLocalSystemCredentialMode(system, configuredServiceMode: false));
        Assert.IsFalse(Program.IsLocalSystemCredentialMode(user, configuredServiceMode: true));
        Assert.ThrowsException<UnauthorizedAccessException>(() =>
            Program.EnsurePairingOperationAuthorized(true, new SettingsSyncAuthorization(false)));
        Program.EnsurePairingOperationAuthorized(false, new SettingsSyncAuthorization(false));
        Program.EnsurePairingOperationAuthorized(true, new SettingsSyncAuthorization(true));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ModeMismatchLeavesSavedKeyUnusedUntilExplicitReplacement(bool serviceMode)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var serviceCodec = new DpapiPairingKeyCodec(identity.User!);
        var normalCodec = new PassthroughPairingKeyCodec();
        properties.SecurityKey.Value = serviceMode ? "OLD-NORMAL-KEY" : serviceCodec.Encrypt("OLD-SERVICE-KEY");
        var savedValue = properties.SecurityKey.Value;
        var settings = CreateSettings(properties, serviceMode ? serviceCodec : normalCodec);
        var writer = new TestSettingsUtils(() => { });
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        SetField(settings, "_settingsUtils", writer);
        var previousKey = Encryption.MyKey;
        var previousMagic = Encryption.MagicNumber;
        var previousCorrupted = Encryption.KeyCorrupted;
        var previousDesktop = Common.RunOnLogonDesktop;
        Common.RunOnLogonDesktop = false;
        try
        {
            if (serviceMode)
            {
                Assert.ThrowsException<CryptographicException>(() => _ = settings.MyKey);
            }
            else
            {
                Assert.ThrowsException<ArgumentException>(() => _ = settings.MyKey);
            }

            Assert.IsFalse(settings.HasPairingKey);
            Encryption.MyKey = "PREVIOUS-RUNTIME-KEY";
            Assert.IsTrue(settings.UpdateRuntimeKey());
            Assert.AreEqual(string.Empty, Encryption.MyKey);
            Assert.AreEqual(0u, Encryption.MagicNumber);
            Assert.IsFalse(settings.UpdateRuntimeKey());
            Assert.AreEqual(savedValue, properties.SecurityKey.Value);
            Assert.AreEqual(0, writer.Writes.Count);

            Program.EnsurePairingOperationAuthorized(serviceMode, new SettingsSyncAuthorization(true));
            await settings.SavePairingSettingsAsync("EXPLICIT-REPLACEMENT", PairedMachineMatrix, false, _ => { });
            Assert.IsTrue(settings.UpdateRuntimeKey());
            Assert.AreEqual("EXPLICIT-REPLACEMENT", settings.MyKey);
            Assert.AreEqual("EXPLICIT-REPLACEMENT", Encryption.MyKey);
            Assert.IsTrue(settings.HasPairingKey);
            Assert.AreEqual(1, writer.Writes.Count);
            Assert.AreEqual(properties.SecurityKey.Value, JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json!)!.Properties.SecurityKey.Value);
            Assert.AreEqual(serviceMode, PairingKeyCodec.HasValidEncryptedFormat(properties.SecurityKey.Value));
            if (serviceMode)
            {
                Assert.IsFalse(properties.SecurityKey.Value.Contains("EXPLICIT-REPLACEMENT", StringComparison.Ordinal));
            }
            else
            {
                Assert.AreEqual("EXPLICIT-REPLACEMENT", properties.SecurityKey.Value);
            }
        }
        finally
        {
            Encryption.MyKey = previousKey;
            Encryption.MagicNumber = previousMagic;
            Encryption.KeyCorrupted = previousCorrupted;
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [TestMethod]
    public async Task SaveCompletionWaitsForPersistenceAndPreservesCiphertext()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        settings.MyKey = "TEST-KEY";
        var ciphertext = properties.SecurityKey.Value;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writer = new TestSettingsUtils(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Test did not release the settings writer.");
            }
        });
        SetField(settings, "_settingsUtils", writer);
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        var previousDesktop = Common.RunOnLogonDesktop;
        Common.RunOnLogonDesktop = false;
        try
        {
            properties.WrapMouse = !properties.WrapMouse;
            var save = settings.SaveSettingsAsync();
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
                Assert.IsFalse(save.IsCompleted);
            }
            finally
            {
                release.Set();
            }

            await save;
            Assert.IsFalse(writer.Json!.Contains("TEST-KEY", StringComparison.Ordinal));
            var persisted = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json)!;
            Assert.AreEqual(ciphertext, persisted.Properties.SecurityKey.Value);
            Assert.AreEqual("TEST-KEY", settings.MyKey);
        }
        finally
        {
            release.Set();
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [TestMethod]
    public async Task RetryingSaveCompletesBeforeQueuedSaveAndLatestCiphertextWins()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        var attempts = 0;
        var writer = new TestSettingsUtils(() =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            if (attempt == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test did not release the settings writer.");
                }

                throw new IOException("Test retry.");
            }

            if (attempt == 3)
            {
                secondEntered.Set();
                if (!releaseSecond.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test did not release the second settings writer.");
                }
            }
        });
        SetField(settings, "_settingsUtils", writer);
        var previousDesktop = Common.RunOnLogonDesktop;
        Common.RunOnLogonDesktop = false;
        try
        {
            settings.MyKey = "FIRST";
            var firstCiphertext = properties.SecurityKey.Value;
            var first = settings.SaveSettingsAsync();
            Task? second = null;
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
                settings.MyKey = "SECOND";
                var secondCiphertext = properties.SecurityKey.Value;
                second = settings.SaveSettingsAsync();
                Assert.IsFalse(first.IsCompleted);
                Assert.IsFalse(second.IsCompleted);
                Assert.AreEqual(1, Volatile.Read(ref attempts));
                release.Set();
                await first.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.IsTrue(secondEntered.Wait(TimeSpan.FromSeconds(10)));
                Assert.IsFalse(second.IsCompleted);
                releaseSecond.Set();
                await second;
                Assert.AreEqual(3, attempts);
                Assert.AreEqual(firstCiphertext, JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Writes[0])!.Properties.SecurityKey.Value);
                Assert.AreEqual(secondCiphertext, JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Writes[1])!.Properties.SecurityKey.Value);
                Assert.AreEqual(writer.Writes[1], writer.Json);
            }
            finally
            {
                release.Set();
                releaseSecond.Set();
                await Task.WhenAll(first, second ?? Task.CompletedTask);
            }
        }
        finally
        {
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [TestMethod]
    public async Task FailedSaveDoesNotPreventQueuedSave()
    {
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var attempts = 0;
        var writer = new TestSettingsUtils(() =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test did not release the settings writer.");
                }

                throw new UnauthorizedAccessException("Test write denied.");
            }
        });
        SetField(settings, "_settingsUtils", writer);
        var previousDesktop = Common.RunOnLogonDesktop;
        Common.RunOnLogonDesktop = false;
        try
        {
            settings.MyKey = "FIRST";
            var first = settings.SaveSettingsAsync();
            Task? second = null;
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
                settings.MyKey = "SECOND";
                settings.SaveSettings();
                second = (Task)typeof(Class.Settings).GetField("_saveSettingsTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(settings)!;
                Assert.IsFalse(second.IsCompleted);
            }
            finally
            {
                release.Set();
                await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => first);
                await (second ?? Task.CompletedTask);
            }

            Assert.AreEqual(2, attempts);
            Assert.AreEqual("SECOND", JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json!)!.Properties.SecurityKey.Value);
        }
        finally
        {
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [TestMethod]
    public async Task PersistenceFailureFaultsAwaitedSave()
    {
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        settings.MyKey = "TEST-KEY";
        SetField(settings, "_settingsUtils", new TestSettingsUtils(() => throw new UnauthorizedAccessException("Test write denied.")));
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        var previousDesktop = Common.RunOnLogonDesktop;
        Common.RunOnLogonDesktop = false;
        try
        {
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => settings.SaveSettingsAsync());
        }
        finally
        {
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedPairingLeavesSettingsAndRuntimeUnchanged(bool generate)
    {
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = "OLD-KEY";
        properties.EasyMouse.Value = 0;
        properties.MachineMatrixString = new List<string> { "LOCAL", "OLD", string.Empty, string.Empty };
        properties.MachinePool.Value = "OLD:NONE,:,:,:";
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        var reject = true;
        var writer = new TestSettingsUtils(() =>
        {
            if (reject)
            {
                throw new UnauthorizedAccessException("Test write denied.");
            }
        });
        SetField(settings, "_settingsUtils", writer);
        writer.Json = new MouseWithoutBordersSettings { Properties = properties }.ToJsonString();
        var originalJson = writer.Json;
        var previousKey = Encryption.MyKey;
        var matrixField = typeof(MachineStuff).GetField("mcMatrix", BindingFlags.Static | BindingFlags.NonPublic)!;
        var poolField = typeof(MachineStuff).GetField("_machinePool", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousMatrix = matrixField.GetValue(null);
        var previousPool = poolField.GetValue(null);
        var runtimeMatrix = new[] { "LOCAL", "OLD", string.Empty, string.Empty };
        var runtimePool = new MachinePool();
        runtimePool.Initialize(runtimeMatrix);
        matrixField.SetValue(null, runtimeMatrix);
        poolField.SetValue(null, runtimePool);
        Encryption.MyKey = "OLD-KEY";
        var poolString = runtimePool.SerializedAsString();
        var previousDesktop = Common.RunOnLogonDesktop;
        var previousMachineName = Common.MachineName;
        Common.RunOnLogonDesktop = false;
        Common.MachineName = "LOCAL";
        try
        {
            var helperType = typeof(Program).GetNestedType("SettingsSyncHelper", BindingFlags.NonPublic)!;
            var helper = Activator.CreateInstance(
                helperType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { new SettingsSyncAuthorization(true), settings },
                null)!;
            var method = helperType.GetMethod(generate ? "GenerateNewKey" : "ConnectToMachine")!;
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                (Task)method.Invoke(helper, generate ? null : new object[] { "REMOTE", "NEW-KEY" })!);
            Assert.AreEqual("OLD-KEY", settings.MyKey);
            Assert.AreEqual(0, settings.EasyMouse);
            Assert.AreEqual("LOCAL,OLD,,", settings.MachineMatrixString);
            Assert.AreEqual("OLD:NONE,:,:,:", settings.MachinePoolString);
            Assert.AreEqual("OLD-KEY", Encryption.MyKey);
            Assert.AreEqual(poolString, MachineStuff.MachinePool.SerializedAsString());
            Assert.AreSame(runtimeMatrix, matrixField.GetValue(null));
            Assert.AreEqual(originalJson, writer.Json);
            Assert.AreEqual(0, writer.Writes.Count);
            reject = false;
            properties.WrapMouse = false;
            await settings.SaveSettingsAsync();
            var persisted = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json!)!.Properties;
            Assert.AreEqual("OLD-KEY", persisted.SecurityKey.Value);
            Assert.AreEqual(0, persisted.EasyMouse.Value);
            CollectionAssert.AreEqual(properties.MachineMatrixString, persisted.MachineMatrixString);
            Assert.AreEqual(properties.MachinePool.Value, persisted.MachinePool.Value);
            Assert.IsFalse(persisted.WrapMouse);
        }
        finally
        {
            Encryption.MyKey = previousKey;
            matrixField.SetValue(null, previousMatrix);
            poolField.SetValue(null, previousPool);
            Common.MachineName = previousMachineName;
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [TestMethod]
    public async Task AutosavesAroundPairingPreserveAcknowledgedKeyAndOrdinaryChanges()
    {
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = "OLD-KEY";
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var attempts = 0;
        var writer = new TestSettingsUtils(() =>
        {
            if (Interlocked.Increment(ref attempts) == 2)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test did not release the pairing writer.");
                }
            }
        });
        SetField(settings, "_settingsUtils", writer);
        settings.PauseInstantSaving = false;
        var previousDesktop = Common.RunOnLogonDesktop;
        Common.RunOnLogonDesktop = false;
        Task pairing = Task.CompletedTask;
        Task after = Task.CompletedTask;
        try
        {
            var before = settings.SaveSettingsAsync();
            pairing = settings.SavePairingSettingsAsync("NEW-KEY", PairedMachineMatrix, true, _ => { });
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsFalse(settings.PauseInstantSaving);
            Assert.AreEqual("OLD-KEY", settings.MyKey);
            properties.WrapMouse = false;
            after = settings.SaveSettingsAsync();
            release.Set();
            await Task.WhenAll(before, pairing, after);
            Assert.AreEqual("OLD-KEY", JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Writes[0])!.Properties.SecurityKey.Value);
            foreach (var json in writer.Writes.Skip(1))
            {
                var persisted = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(json)!.Properties;
                Assert.AreEqual("NEW-KEY", persisted.SecurityKey.Value);
                CollectionAssert.AreEqual(PairedMachineMatrix, persisted.MachineMatrixString);
            }

            Assert.IsFalse(JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json!)!.Properties.WrapMouse);
            Assert.IsFalse(settings.PauseInstantSaving);
            settings.EasyMouse = 0;
            await (Task)typeof(Class.Settings).GetField("_saveSettingsTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(settings)!;
            Assert.AreEqual(0, JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json!)!.Properties.EasyMouse.Value);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(pairing, after);
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(null)]
    [DataRow(" \t\r\n")]
    public void EmptyDecodedRuntimeKeyDoesNotStartCryptoStreams(string key)
    {
        var previousKey = Encryption.MyKey;
        try
        {
            Encryption.MyKey = key;
            using var stream = new MemoryStream();
            Assert.ThrowsException<CryptographicException>(() => Encryption.GetEncryptedStream(stream));
            Assert.ThrowsException<CryptographicException>(() => Encryption.GetDecryptedStream(stream));
            Assert.AreEqual(0L, stream.Length);
        }
        finally
        {
            Encryption.MyKey = previousKey;
        }
    }

    [TestMethod]
    public async Task WatcherWaitsForPairingAndRestoresSavingBeforeNextSetter()
    {
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = "OLD-KEY";
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        SetField(settings, "_settings", new MouseWithoutBordersSettings { Properties = properties });
        using var writing = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        using var reading = new ManualResetEventSlim();
        using var releaseRead = new ManualResetEventSlim();
        var attempts = 0;
        var writer = new TestSettingsUtils(
            () =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    writing.Set();
                    if (!releaseWrite.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException("Test did not release the pairing writer.");
                    }
                }
            },
            () =>
            {
                reading.Set();
                if (!releaseRead.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test did not release the settings reader.");
                }
            });
        typeof(SettingsUtils).GetField("_file", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            writer,
            DispatchProxy.Create<System.IO.Abstractions.IFile, ExistingSettingsFile>());
        SetField(settings, "_settingsUtils", writer);
        settings.PauseInstantSaving = false;
        var previousDesktop = Common.RunOnLogonDesktop;
        var previousKey = Encryption.MyKey;
        Common.RunOnLogonDesktop = false;
        Task pairing = Task.CompletedTask;
        Task reload = Task.CompletedTask;
        try
        {
            pairing = settings.SavePairingSettingsAsync("NEW-KEY", UnpairedMachineMatrix, true, _ => { });
            Assert.IsTrue(writing.Wait(TimeSpan.FromSeconds(10)));
            reload = settings.ReloadSettingsAsync();
            Assert.IsFalse(reading.IsSet);
            Assert.IsFalse(settings.PauseInstantSaving);
            Encryption.MyKey = "NEW-KEY";
            releaseWrite.Set();
            await pairing;
            Assert.IsTrue(reading.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsFalse(settings.PauseInstantSaving);
            releaseRead.Set();
            await reload;
            Assert.IsFalse(settings.PauseInstantSaving);
            settings.EasyMouse = 0;
            await (Task)typeof(Class.Settings).GetField("_saveSettingsTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(settings)!;
            var persisted = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(writer.Json!)!.Properties;
            Assert.AreEqual(0, persisted.EasyMouse.Value);
            Assert.AreEqual("NEW-KEY", persisted.SecurityKey.Value);
        }
        finally
        {
            releaseWrite.Set();
            releaseRead.Set();
            await Task.WhenAll(pairing, reload);
            Encryption.MyKey = previousKey;
            Common.RunOnLogonDesktop = previousDesktop;
        }
    }

    public class ExistingSettingsFile : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.Name == "Exists" ? true : throw new NotSupportedException();
    }

    private sealed class TestSettingsUtils(Action write, Action? read = null) : SettingsUtils(new System.IO.Abstractions.FileSystem())
    {
        internal string? Json { get; set; }

        internal List<string> Writes { get; } = new();

        public override T GetSettingsOrDefault<T>(string powertoy = "", string fileName = "settings.json")
        {
            read?.Invoke();
            return (T)(object)JsonSerializer.Deserialize<MouseWithoutBordersSettings>(Json!)!;
        }

        public override void SaveSettingsOrThrow(string jsonSettings, string powertoy = "", string fileName = "settings.json")
        {
            write();
            Json = jsonSettings;
            Writes.Add(jsonSettings);
        }
    }

    private static Class.Settings CreateSettings(MouseWithoutBordersProperties properties, IPairingKeyCodec codec)
    {
        var settings = (Class.Settings)RuntimeHelpers.GetUninitializedObject(typeof(Class.Settings));
        SetField(settings, "_loadingSettingsLock", Activator.CreateInstance(typeof(System.Threading.Lock))!);
        SetField(settings, "_saveSettingsTask", Task.CompletedTask);
        SetField(settings, "_pairingSettingsTask", Task.CompletedTask);
        SetField(settings, "_properties", properties);
        SetField(settings, "_pairingKeyCodec", codec);
        settings.PauseInstantSaving = true;
        return settings;
    }

    private static void SetField(Class.Settings settings, string name, object value) =>
        typeof(Class.Settings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(settings, value);
}
