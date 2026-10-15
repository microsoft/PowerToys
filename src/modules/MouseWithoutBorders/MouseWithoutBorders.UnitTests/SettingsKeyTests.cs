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
    [TestMethod]
    public void SettingsPropertySerializesProtectedValueAndReadsPlaintext()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        settings.MyKey = "TEST-KEY";

        var document = new MouseWithoutBordersSettings { Properties = properties };
        var json = document.ToJsonString();
        StringAssert.Contains(json, MouseWithoutBordersPairingHelper.EncryptedPrefix);
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
        Assert.IsTrue(MouseWithoutBordersPairingHelper.IsOrdinaryKey(settings.MyKey));
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
        Assert.ThrowsException<InvalidOperationException>(() => Program.EnsureConnectOperationAvailable(true, false));
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
    [DataRow("")]
    [DataRow("********")]
    [DataRow("mwb-dpapi:v1:AQID")]
    public void UnavailableRuntimeKeyDoesNotStartCryptoStreams(string key)
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

    private sealed class TestSettingsUtils(Action write) : SettingsUtils(new System.IO.Abstractions.FileSystem())
    {
        internal string? Json { get; private set; }

        public override void SaveSettingsOrThrow(string jsonSettings, string powertoy = "", string fileName = "settings.json")
        {
            write();
            Json = jsonSettings;
        }
    }

    private static Class.Settings CreateSettings(MouseWithoutBordersProperties properties, IPairingKeyCodec codec)
    {
        var settings = (Class.Settings)RuntimeHelpers.GetUninitializedObject(typeof(Class.Settings));
        SetField(settings, "_loadingSettingsLock", Activator.CreateInstance(typeof(System.Threading.Lock))!);
        SetField(settings, "_properties", properties);
        SetField(settings, "_pairingKeyCodec", codec);
        settings.PauseInstantSaving = true;
        return settings;
    }

    private static void SetField(Class.Settings settings, string name, object value) =>
        typeof(Class.Settings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(settings, value);
}
