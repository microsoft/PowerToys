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
    public void SettingsRequiresPairingKeyCodec()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new Class.Settings(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new Class.Settings(null!, new MouseWithoutBordersSettings(), null!));
    }

    [TestMethod]
    public void SettingsListenerStartsAfterKeyInitializationOnUserDesktop()
    {
        var source = ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Class\Program.cs");
        var initialization = source.IndexOf("InitAndCleanup.Init();", StringComparison.Ordinal);
        var listener = source.IndexOf("StartSettingSyncThread();", StringComparison.Ordinal);

        Assert.IsTrue(initialization >= 0 && listener > initialization);
        StringAssert.Contains(source[initialization..listener], "if (!Common.RunOnLogonDesktop)");
        Assert.IsFalse(source[initialization..listener].Contains("Encryption.MyKey", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LegacyFormsValidateBeforeChangingPairingState()
    {
        var matrix = ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Form\frmMatrix.cs");
        StringAssert.Contains(matrix, "UpdateKey(Regex.Replace(textBoxEnc.Text, @\"\\s+\", string.Empty))");
        var update = matrix[matrix.IndexOf("private bool UpdateKey(", StringComparison.Ordinal)..];
        Assert.IsTrue(update.IndexOf("Encryption.IsKeyValid(newKey,", StringComparison.Ordinal) < update.IndexOf("Setting.Values.MyKey = newKey;", StringComparison.Ordinal));

        var wizard = ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Form\Settings\SetupPage2a.cs");
        StringAssert.Contains(wizard, "Encryption.IsKeyValid(Regex.Replace(SecurityCodeField.Text, @\"\\s+\", string.Empty), out _)");
        var click = wizard[wizard.IndexOf("private void LinkButtonClick(", StringComparison.Ordinal)..];
        var validation = click.IndexOf("if (!Encryption.IsKeyValid(key,", StringComparison.Ordinal);
        Assert.IsTrue(validation >= 0);
        foreach (var mutation in new[] { "Encryption.MyKey = key;", "Setting.Values.MyKey =", "MachineStuff.MachineMatrix =", "MachineStuff.MachinePool.Initialize(", "SendNextPage(" })
        {
            Assert.IsTrue(click.IndexOf(mutation, StringComparison.Ordinal) > validation, mutation);
        }

        StringAssert.Contains(click[validation..click.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal)], "return;");
    }

    private static string ReadRepositorySource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Repository source must be available for the wiring check.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
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

    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:!")]
    [DataRow("mwb-dpapi:v1:")]
    [DataRow("mwb-dpapi:v2:TEST")]
    public void EncryptedDisplayDoesNotValidateStoredKey(string value)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = value;
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));

        Assert.AreEqual(MouseWithoutBordersIpc.PairingConfiguredIndicator, settings.PairingKeyDisplayText);
        Assert.ThrowsException<CryptographicException>(() => _ = settings.MyKey);
        Assert.AreEqual(value, properties.SecurityKey.Value);
    }

    [DataTestMethod]
    [DataRow("TEST-KEY")]
    [DataRow("********")]
    [DataRow("                ")]
    public void UnencryptedDisplayUsesSettingsValue(string value)
    {
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        settings.MyKey = value;

        Assert.AreEqual(value, settings.PairingKeyDisplayText);
        Assert.AreEqual(value, settings.MyKey);
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
    public void RepeatedServiceKeyReusesCiphertextAndChangedKeyReplacesIt()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = string.Empty;
        var codec = new DpapiPairingKeyCodec(identity.User!);
        var settings = CreateSettings(properties, codec);
        settings.MyKey = "TEST-KEY";
        var stored = properties.SecurityKey.Value;

        settings.MyKey = "TEST-KEY";

        Assert.AreEqual(stored, properties.SecurityKey.Value);
        Assert.AreEqual("TEST-KEY", codec.Decrypt(properties.SecurityKey.Value));
        settings.MyKey = "test-key";
        Assert.AreNotEqual(stored, properties.SecurityKey.Value);
        Assert.AreEqual("test-key", codec.Decrypt(properties.SecurityKey.Value));
    }

    [TestMethod]
    public void KeySetterKeepsInstantSavingOutsideCiphertextReplacement()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        const string relativePath = @"src\modules\MouseWithoutBorders\App\Class\Setting.cs";
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Repository source must be available for the settings save wiring check.");
        var source = File.ReadAllText(Path.Combine(directory.FullName, relativePath)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = source.IndexOf("internal string MyKey\n", StringComparison.Ordinal);
        var end = source.IndexOf("internal bool UpdateRuntimeKey()", start, StringComparison.Ordinal);
        var setter = source.Substring(start, end - start);
        setter = setter.Substring(setter.IndexOf("set\n", StringComparison.Ordinal));

        Assert.IsFalse(setter.Contains("return", StringComparison.Ordinal));
        StringAssert.Contains(
            setter,
            "_properties.SecurityKey.Value = storedKey;\n\n                    if (!PauseInstantSaving)\n                    {\n                        SaveSettings();\n                    }");
    }

    [TestMethod]
    public void IdenticalKeyDoesNotInvokeEncryptionAgain()
    {
        var encryptions = 0;
        var codec = new CallbackPairingKeyCodec(() => encryptions++);
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = string.Empty;
        var settings = CreateSettings(properties, codec);

        settings.MyKey = "TEST-KEY";
        settings.MyKey = "TEST-KEY";

        Assert.AreEqual(1, encryptions);
        Assert.AreEqual("TEST-KEY", properties.SecurityKey.Value);
    }

    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:!")]
    [DataRow("mwb-dpapi:v1:")]
    [DataRow("mwb-dpapi:v2:TEST")]
    [DataRow("OLD-PLAINTEXT")]
    public void ExplicitServiceKeyReplacesUnreadableStoredValue(string stored)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = stored;
        var codec = new DpapiPairingKeyCodec(identity.User!);
        var settings = CreateSettings(properties, codec);

        settings.MyKey = "REPLACEMENT";

        Assert.AreNotEqual(stored, properties.SecurityKey.Value);
        Assert.AreEqual("REPLACEMENT", codec.Decrypt(properties.SecurityKey.Value));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("mwb-dpapi:v1:!")]
    public void PassthroughSetterPreservesExactInput(string value)
    {
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());

        settings.MyKey = value;
        Assert.AreEqual(value, properties.SecurityKey.Value);
        settings.MyKey = value;
        Assert.AreEqual(value, properties.SecurityKey.Value);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void InvalidNewServiceKeyDoesNotReuseUnreadableStoredValue(string value)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = "mwb-dpapi:v1:!";
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));

        if (value is null)
        {
            Assert.ThrowsException<ArgumentNullException>(() => settings.MyKey = value);
        }
        else
        {
            Assert.ThrowsException<ArgumentException>(() => settings.MyKey = value);
        }

        Assert.AreEqual("mwb-dpapi:v1:!", properties.SecurityKey.Value);
    }

    [TestMethod]
    public void FailedKeyConversionDoesNotReplaceStoredValue()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        settings.MyKey = "PREVIOUS-KEY";
        var stored = properties.SecurityKey.Value;

        Assert.ThrowsException<ArgumentException>(() => settings.MyKey = "mwb-dpapi:v1:TEST");

        Assert.AreEqual(stored, properties.SecurityKey.Value);
        Assert.AreEqual("PREVIOUS-KEY", settings.MyKey);
    }

    [TestMethod]
    public void KeyConversionResolvesCurrentPropertiesAfterCodecReturns()
    {
        var previous = new MouseWithoutBordersProperties();
        previous.SecurityKey.Value = "PREVIOUS";
        var loaded = new MouseWithoutBordersSettings();
        loaded.Properties.SecurityKey.Value = "RELOADED";
        Class.Settings settings = null!;
        var codec = new CallbackPairingKeyCodec(() =>
        {
            SetField(settings, "_settings", loaded);
            SetField(settings, "_properties", loaded.Properties);
        });
        settings = CreateSettings(previous, codec);

        settings.MyKey = "ACCEPTED";

        Assert.AreEqual("ACCEPTED", loaded.Properties.SecurityKey.Value);
        Assert.AreEqual("ACCEPTED", settings.MyKey);
        Assert.AreEqual("PREVIOUS", previous.SecurityKey.Value);
    }

    [TestMethod]
    public void ReusedKeyResolvesCurrentPropertiesAfterDecryptionReturns()
    {
        var previous = new MouseWithoutBordersProperties();
        previous.SecurityKey.Value = "ACCEPTED";
        var loaded = new MouseWithoutBordersSettings();
        loaded.Properties.SecurityKey.Value = "RELOADED";
        Class.Settings settings = null!;
        var encryptions = 0;
        var codec = new CallbackPairingKeyCodec(
            () => encryptions++,
            () => settings.ApplyLoadedSettings(loaded, () => { }, () => { }, () => { }, () => { }));
        settings = CreateSettings(previous, codec);
        var previousKey = Encryption.MyKey;
        try
        {
            // Keep the reload's runtime-key comparison from reopening sockets.
            Encryption.MyKey = "RELOADED";
            settings.MyKey = "ACCEPTED";

            Assert.AreEqual("ACCEPTED", loaded.Properties.SecurityKey.Value);
            Assert.AreEqual("ACCEPTED", previous.SecurityKey.Value);
            Assert.AreEqual(0, encryptions);
        }
        finally
        {
            Encryption.MyKey = previousKey;
        }
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
        }
        finally
        {
            Encryption.MyKey = previousKey;
            Encryption.MagicNumber = previousMagic;
            Encryption.KeyCorrupted = previousCorrupted;
        }
    }

    [TestMethod]
    public void SwitchingToNormalModeRequiresExplicitReplacementOfProtectedKey()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        var service = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        service.MyKey = "SERVICE-KEY";
        var stored = properties.SecurityKey.Value;
        var normal = CreateSettings(properties, new PassthroughPairingKeyCodec());
        Assert.ThrowsException<CryptographicException>(() => _ = normal.MyKey);
        Assert.AreEqual(stored, properties.SecurityKey.Value);
        Assert.AreEqual(MouseWithoutBordersIpc.PairingConfiguredIndicator, normal.PairingKeyDisplayText);
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
        Assert.AreEqual("OLD-PLAINTEXT", properties.SecurityKey.Value);
        settings.MyKey = "NEW-KEY";
        Assert.AreEqual("NEW-KEY", settings.MyKey);
    }

    [TestMethod]
    public void NormalEmptySettingsStillGenerateAnUnencryptedKey()
    {
        if (Class.Settings.IsProtectedMode)
        {
            Assert.Inconclusive("This case requires a normal user token.");
        }

        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = string.Empty;
        var settings = CreateSettings(properties, new PassthroughPairingKeyCodec());
        settings.PauseInstantSaving = false;
        Assert.IsFalse(settings.IsPairingKeyModeMismatch);
        Assert.IsFalse(string.IsNullOrWhiteSpace(settings.MyKey));
        Assert.IsFalse(PairingKeyCodec.IsEncryptedValue(settings.MyKey));
        Assert.AreEqual(settings.MyKey, properties.SecurityKey.Value);
    }

    [TestMethod]
    public void PairingAuthorizationIsIndependentOfJson()
    {
        Assert.ThrowsException<UnauthorizedAccessException>(() =>
            Program.EnsurePairingOperationAuthorized(true, false));
        Program.EnsurePairingOperationAuthorized(false, false);
        Program.EnsurePairingOperationAuthorized(false, true);
        Program.EnsurePairingOperationAuthorized(true, true);
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void ModeMismatchPreservesSavedValueUntilExplicitReplacement(bool serviceMode, bool firstRun, bool showOriginalUI)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.FirstRun = firstRun;
        properties.ShowOriginalUI = showOriginalUI;
        var serviceCodec = new DpapiPairingKeyCodec(identity.User!);
        var normalCodec = new PassthroughPairingKeyCodec();
        properties.SecurityKey.Value = serviceMode ? "OLD-NORMAL-KEY" : serviceCodec.Encrypt("OLD-SERVICE-KEY");
        var savedValue = properties.SecurityKey.Value;
        var settings = CreateSettings(properties, serviceMode ? serviceCodec : normalCodec);
        var previousKey = Encryption.MyKey;
        var previousMagic = Encryption.MagicNumber;
        var previousCorrupted = Encryption.KeyCorrupted;
        try
        {
            Encryption.MyKey = "PREVIOUS-RUNTIME-KEY";
            Assert.IsTrue(settings.UpdateRuntimeKey());
            Assert.AreEqual(string.Empty, Encryption.MyKey);
            Assert.AreEqual(0u, Encryption.MagicNumber);
            Assert.IsTrue(settings.IsPairingKeyModeMismatch);
            Assert.IsFalse(Encryption.KeyCorrupted);
            Assert.ThrowsException<CryptographicException>(() => _ = settings.MyKey);
            if (!serviceMode)
            {
                Assert.AreEqual(MouseWithoutBordersIpc.PairingConfiguredIndicator, settings.PairingKeyDisplayText);
            }

            Assert.IsFalse(settings.UpdateRuntimeKey());
            Assert.IsFalse(Encryption.KeyCorrupted);
            Assert.AreEqual(savedValue, properties.SecurityKey.Value);

            settings.MyKey = "EXPLICIT-REPLACEMENT";
            Assert.IsTrue(settings.UpdateRuntimeKey());
            Assert.AreEqual("EXPLICIT-REPLACEMENT", settings.MyKey);
            Assert.AreEqual("EXPLICIT-REPLACEMENT", Encryption.MyKey);
            Assert.AreEqual(serviceMode, PairingKeyCodec.IsEncryptedValue(properties.SecurityKey.Value));
            Assert.IsFalse(settings.IsPairingKeyModeMismatch);
        }
        finally
        {
            Encryption.MyKey = previousKey;
            Encryption.MagicNumber = previousMagic;
            Encryption.KeyCorrupted = previousCorrupted;
        }
    }

    [TestMethod]
    public void LegacyStartupModeMismatchSourceContract()
    {
        // WinForms startup cannot run in this unit test; check its wiring to the runtime-tested settings boundary.
        var startup = ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Core\InitAndCleanup.cs");
        StringAssert.Contains(startup, "Encryption.KeyCorrupted = !Setting.Values.IsPairingKeyModeMismatch;");
        var matrix = ReadRepositorySource(@"src\modules\MouseWithoutBorders\App\Core\MachineStuff.cs");
        matrix = matrix[matrix.IndexOf("internal static void ShowMachineMatrix()", StringComparison.Ordinal)..];
        StringAssert.Contains(matrix, "if (!Setting.Values.ShowOriginalUI)");
        StringAssert.Contains(matrix, "if (Setting.Values.FirstRun && !Setting.Values.IsPairingKeyModeMismatch && !Common.AtLeastOneSocketConnected())");
        StringAssert.Contains(matrix, "MachineStuff.ShowSetupForm();");
        StringAssert.Contains(matrix, "Common.MatrixForm = new FrmMatrix();");
    }

    [TestMethod]
    public void ProtectedKeyFailureStillReportsCorruptionWithoutReplacingSavedValue()
    {
        const string stored = "mwb-dpapi:v1:!";
        using var identity = WindowsIdentity.GetCurrent();
        var properties = new MouseWithoutBordersProperties();
        properties.SecurityKey.Value = stored;
        var settings = CreateSettings(properties, new DpapiPairingKeyCodec(identity.User!));
        var previousKey = Encryption.MyKey;
        var previousMagic = Encryption.MagicNumber;
        var previousCorrupted = Encryption.KeyCorrupted;
        try
        {
            Encryption.MyKey = string.Empty;
            Encryption.KeyCorrupted = false;
            Assert.IsFalse(settings.IsPairingKeyModeMismatch);
            Assert.IsFalse(settings.UpdateRuntimeKey());
            Assert.IsTrue(Encryption.KeyCorrupted);
            Assert.AreEqual(stored, properties.SecurityKey.Value);
        }
        finally
        {
            Encryption.MyKey = previousKey;
            Encryption.MagicNumber = previousMagic;
            Encryption.KeyCorrupted = previousCorrupted;
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

    private static Class.Settings CreateSettings(MouseWithoutBordersProperties properties, IPairingKeyCodec codec)
    {
        var settings = (Class.Settings)RuntimeHelpers.GetUninitializedObject(typeof(Class.Settings));
        SetField(settings, "_loadingSettingsLock", Activator.CreateInstance(typeof(System.Threading.Lock))!);
        SetField(settings, "_pairingKeyLock", Activator.CreateInstance(typeof(System.Threading.Lock))!);
        SetField(settings, "_properties", properties);
        SetField(settings, "_pairingKeyCodec", codec);
        settings.PauseInstantSaving = true;
        return settings;
    }

    private static void SetField(Class.Settings settings, string name, object value) =>
        typeof(Class.Settings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(settings, value);

    private sealed class CallbackPairingKeyCodec(Action duringEncryption, Action? duringDecryption = null) : IPairingKeyCodec
    {
        private bool _isDecrypting;

        public string Encrypt(string value)
        {
            duringEncryption();
            return value;
        }

        public string Decrypt(string value)
        {
            if (!_isDecrypting)
            {
                _isDecrypting = true;
                try
                {
                    duringDecryption?.Invoke();
                }
                finally
                {
                    _isDecrypting = false;
                }
            }

            return value;
        }

        public bool IsEncrypted(string value) => false;
    }
}
