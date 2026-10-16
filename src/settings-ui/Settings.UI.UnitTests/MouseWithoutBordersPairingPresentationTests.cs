// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public sealed class MouseWithoutBordersPairingPresentationTests
{
    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:TEST", true)]
    [DataRow("mwb-dpapi:v1:", true)]
    [DataRow("mwb-dpapi:v1:!", true)]
    [DataRow("mwb-dpapi:v2:TEST", true)]
    [DataRow("MWB-DPAPI:v1:TEST", true)]
    [DataRow("mwb-dpapi:v1:A QID", true)]
    [DataRow("ordinary", false)]
    [DataRow(null, false)]
    public void ClassifierRecognizesReservedFamilyWithoutValidatingPayload(string value, bool reserved)
    {
        Assert.AreEqual(reserved, PairingKeyCodec.IsEncryptedValue(value));
    }

    [TestMethod]
    public void LegacyPairingSourceWiringPreservesNormalInputAndGuardsServiceMode()
    {
        var setup = ReadSource(@"src\modules\MouseWithoutBorders\App\Form\Settings\SetupPage2a.cs");
        StringAssert.Contains(setup, "SecurityCodeField.Enabled = !Class.Settings.IsProtectedMode;");
        StringAssert.Contains(setup, "LinkButton.Enabled = !Class.Settings.IsProtectedMode && SecurityCodeField.Text.Length >= 16 && ComputerNameField.Text.Trim().Length > 0;");
        StringAssert.Contains(setup, "if (Class.Settings.IsProtectedMode)");
        StringAssert.Contains(setup, "Encryption.MyKey = Regex.Replace(SecurityCodeField.Text, @\"\\s+\", string.Empty);");
        StringAssert.Contains(setup, "Setting.Values.MyKey = Encryption.MyKey;");
        var matrix = ReadSource(@"src\modules\MouseWithoutBorders\App\Form\frmMatrix.cs");
        StringAssert.Contains(matrix, "textBoxEnc.ReadOnly = Class.Settings.IsProtectedMode;");
        StringAssert.Contains(matrix, "buttonNewKey.Enabled = !Class.Settings.IsProtectedMode;");
        StringAssert.Contains(matrix, "textBoxEnc.Text = Setting.Values.PairingKeyDisplayText;");
        StringAssert.Contains(matrix, "if (Class.Settings.IsProtectedMode)");
        StringAssert.Contains(matrix, "if (!Encryption.IsKeyValid(newKey, out string rv))");
        StringAssert.Contains(matrix, "Setting.Values.MyKey = newKey;");
        StringAssert.Contains(matrix, "Encryption.MyKey = newKey;");
        StringAssert.Contains(matrix, "Setting.Values.MyKey = Encryption.MyKey = Encryption.CreateRandomKey();");
    }

    [TestMethod]
    public void PairingViewModelSourceWiringKeepsUiStateOutOfSettings()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        StringAssert.Contains(source, "PairingKeyCodec.IsEncryptedValue(Settings.Properties.SecurityKey.Value)");
        StringAssert.Contains(source, "public bool CanManagePairing => !_isPairingPending && (!UseService || IsElevated);");
        StringAssert.Contains(source, "var generatedKey = await endpoint.GenerateNewKey();");
        StringAssert.Contains(source, "_servicePairingKey = useService ? generatedKey : null;");
        StringAssert.Contains(source, "await endpoint.ConnectToMachine(pcName, securityKey);");
        StringAssert.Contains(source.Replace("\r\n", "\n"), "_servicePairingKey = useService ? securityKey : null;\n                    OnPropertyChanged(nameof(SecurityKey));");
        StringAssert.Contains(source, "public async Task<bool> SubmitConnectionRequestAsync(string pcName, string securityKey)");
        StringAssert.Contains(source, "pcName = pcName?.Trim();");
        StringAssert.Contains(source, "securityKey = securityKey?.Trim();");
        foreach (var removedInput in new[] { "_connectSecurityKey", "_connectPCName", "ConnectSecurityKey", "ConnectPCName" })
        {
            Assert.IsFalse(source.Contains(removedInput, StringComparison.Ordinal));
        }

        StringAssert.Contains(source, "if (_disposed || UseService != useService || (useService && !IsElevated))");
        Assert.IsFalse(source.Contains("TryUpdateAsync", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Settings.Properties.SecurityKey.Value = ", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("CanEditSettings", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void PairingPageSourceContractPassesInputsAndClearsOnlyAfterSuccessfulConnect()
    {
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs").Replace("\r\n", "\n");
        var start = page.IndexOf("private async Task Connect()", StringComparison.Ordinal);
        var end = page.IndexOf("private async void ServiceMode_Toggled(", start, StringComparison.Ordinal);
        Assert.AreEqual(
            """
            private async Task Connect()
                    {
                        var pcName = ConnectPCNameTextBox.Text;
                        var securityKey = ConnectSecurityKeyTextBox.Text.Trim();
                        if (await ViewModel.SubmitConnectionRequestAsync(pcName, securityKey))
                        {
                            ConnectPCNameTextBox.Text = string.Empty;
                            ConnectSecurityKeyTextBox.Text = string.Empty;
                            ViewModel.ConnectFieldsVisible = false;
                        }
                    }
            """.Replace("\r\n", "\n"),
            page.Substring(start, end - start).Trim());
        var clearStart = page.IndexOf("private void ClearPairingUI()", end, StringComparison.Ordinal);
        var clear = page.Substring(clearStart, page.IndexOf("public void RefreshEnabledState()", clearStart, StringComparison.Ordinal) - clearStart);
        StringAssert.Contains(clear, "ViewModel.ResetPairingUiState();");
        StringAssert.Contains(clear, "ConnectPCNameTextBox.Text = string.Empty;");
        StringAssert.Contains(clear, "ConnectSecurityKeyTextBox.Text = string.Empty;");
        StringAssert.Contains(page, "Loaded += async (s, e) =>\n            {\n                ClearPairingUI();");
        StringAssert.Contains(page, "Unloaded += (s, e) => ClearPairingUI();");
        StringAssert.Contains(page.Substring(end, clearStart - end), "ClearPairingUI();");
        var xaml = XDocument.Parse(ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml"));
        var serviceToggle = xaml.Descendants().Single(element => (string)element.Attribute("IsOn") == "{x:Bind ViewModel.UseService, Mode=TwoWay}");
        Assert.AreEqual("ServiceMode_Toggled", (string)serviceToggle.Attribute("Toggled"));

        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        start = source.IndexOf("public async Task<bool> SubmitConnectionRequestAsync(", StringComparison.Ordinal);
        end = source.IndexOf("private async Task<ISettingsSyncHelper.MachineSocketState[]>", start, StringComparison.Ordinal);
        var request = source.Substring(start, end - start);
        StringAssert.Contains(request, "if (!CanConnectPairing)");
        StringAssert.Contains(request, "_isPairingPending = true;");
        StringAssert.Contains(request, "_hasPairingError = true;");
        StringAssert.Contains(request, "catch (Exception ex)");
        StringAssert.Contains(request, "return false;");
        StringAssert.Contains(request, "return true;");
        Assert.IsFalse(request.Contains("ConnectFieldsVisible =", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PairingViewModelSourceContractLoadsCurrentKeyOnlyForElevatedServiceMode()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs").Replace("\r\n", "\n");
        var start = source.IndexOf("public async Task LoadPairingKeyAsync()", StringComparison.Ordinal);
        var end = source.IndexOf("public async Task<bool> SubmitConnectionRequestAsync(", start, StringComparison.Ordinal);
        var request = source.Substring(start, end - start);
        var initialGuard = request.IndexOf("if (_disposed || !IsEnabled || !UseService || !IsElevated || PairingSetupRequired)", StringComparison.Ordinal);
        var semaphore = request.IndexOf("using (await _ipcSemaphore.EnterAsync())", StringComparison.Ordinal);
        var roleGuard = request.IndexOf("if (_disposed || !IsEnabled || !UseService || !IsElevated || PairingSetupRequired)", semaphore, StringComparison.Ordinal);
        var helper = request.IndexOf("await GetSettingsSyncHelperAsync()", StringComparison.Ordinal);
        var read = request.IndexOf("var key = await endpoint.GetCurrentPairingKey();", StringComparison.Ordinal);
        var resultGuard = request.IndexOf("if (_disposed || !IsEnabled || UseService != useService || !IsElevated)", StringComparison.Ordinal);
        var display = request.IndexOf("_servicePairingKey = key;", StringComparison.Ordinal);
        Assert.IsTrue(initialGuard >= 0 && initialGuard < semaphore && semaphore < roleGuard && roleGuard < helper && helper < read && read < resultGuard && resultGuard < display);
        StringAssert.Contains(request, "catch (Exception ex)");
        StringAssert.Contains(request, "_hasPairingError = true;");
        StringAssert.Contains(request, "OnPropertyChanged(nameof(HasPairingError));");
        StringAssert.Contains(request, "if (_servicePairingKey != key)");
        Assert.IsFalse(request.Contains("_servicePairingKey = null;", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("SaveSettings", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("NotifyPropertyChanged(", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("Settings.Properties.SecurityKey.Value", StringComparison.Ordinal));
        var getterStart = source.IndexOf("public string SecurityKey =>", StringComparison.Ordinal);
        var getter = source.Substring(getterStart, source.IndexOf("public bool WrapMouse", getterStart, StringComparison.Ordinal) - getterStart);
        StringAssert.Contains(getter, "UseService\n            ? IsElevated\n                ? _servicePairingKey ?? string.Empty");
        StringAssert.Contains(getter, ": PairingSetupRequired ? string.Empty : Settings.Properties.SecurityKey.Value;");
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs").Replace("\r\n", "\n");
        StringAssert.Contains(page, "ViewModel.OnPageLoaded();\n                await ViewModel.LoadPairingKeyAsync();");
        StringAssert.Contains(page, "ViewModel.NotifyUpdatedSettings();\n                    await ViewModel.LoadPairingKeyAsync();");
        var resources = XDocument.Parse(ReadSource(@"src\settings-ui\Settings.UI\Strings\en-us\Resources.resw"));
        Assert.AreEqual("Pairing could not be loaded or updated. Try again.", resources.Root.Elements("data").Single(element => (string)element.Attribute("name") == "MouseWithoutBorders_PairingUpdateFailed.Message").Element("value").Value);
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        StringAssert.Contains(reload, "if (UseService != useService || Settings.Properties.SecurityKey.Value != storedKey)");
        StringAssert.Contains(reload, "if (SecurityKey != displayedKey)");
    }

    [TestMethod]
    public void SocketReopenSourceContractSkipsConstructionQuietlyWithoutRuntimeKey()
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Core\Common.cs");
        var start = source.IndexOf("internal static void ReopenSockets(bool byUser)", StringComparison.Ordinal);
        var end = source.IndexOf("internal static string GetMyStorageDir()", start, StringComparison.Ordinal);
        var reopen = source.Substring(start, end - start).Replace("\r\n", "\n");
        StringAssert.Contains(reopen, "Sk = !string.IsNullOrWhiteSpace(Encryption.MyKey)\n                    ? new SocketStuff(tcpPort, byUser)\n                    : null;");
        var close = reopen.IndexOf("tmpSk.Close(byUser);", StringComparison.Ordinal);
        var construct = reopen.IndexOf("new SocketStuff(tcpPort, byUser)", StringComparison.Ordinal);
        Assert.IsTrue(close >= 0 && close < construct);
    }

    [TestMethod]
    public void SettingsReloadSourceContractPublishesUnderExistingSettingsLock()
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Class\Setting.cs").Replace("\r\n", "\n");
        StringAssert.Contains(source, "lock (_loadingSettingsLock)\n                    {\n                        PauseInstantSaving = true;\n                        last_properties = _properties;\n                        _settings = settings;\n                        _properties = settings.Properties;\n                    }");
    }

    [TestMethod]
    public void PairingPageSourceWiringDisablesOnlyPairingControlsAndShowsLocalizedErrors()
    {
        var page = XDocument.Parse(ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml"));
        var pageControl = page.Descendants().Single(element => element.Name.LocalName == "SettingsPageControl");
        Assert.IsNull(pageControl.Attribute("IsEnabled"));
        var pairing = page.Descendants().Single(element => (string)element.Attribute("Name") == "MouseWithoutBordersSecurityKey");
        Assert.AreEqual("{x:Bind ViewModel.CanManagePairing, Mode=OneWay}", (string)pairing.Attribute("IsEnabled"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var error = page.Descendants().Single(element => (string)element.Attribute(xaml + "Uid") == "MouseWithoutBorders_PairingUpdateFailed");
        Assert.AreEqual("{x:Bind ViewModel.HasPairingError, Mode=OneWay}", (string)error.Attribute("IsOpen"));
    }

    [TestMethod]
    public void PairingViewModelSourceWiringClearsDisplayWithoutSavingOrDecryptingIt()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        var clearStart = source.IndexOf("public void ResetPairingUiState()", StringComparison.Ordinal);
        var clearEnd = source.IndexOf("public void NotifyPropertyChanged(", clearStart, StringComparison.Ordinal);
        var clear = source.Substring(clearStart, clearEnd - clearStart);
        StringAssert.Contains(clear, "_servicePairingKey = null;");
        StringAssert.Contains(clear, "OnPropertyChanged(nameof(SecurityKey));");
        Assert.IsFalse(clear.Contains("SaveSettings", StringComparison.Ordinal));
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        StringAssert.Contains(reload, "_servicePairingKey = null;");
        StringAssert.Contains(reload, "if (UseService != useService || Settings.Properties.SecurityKey.Value != storedKey)");
        Assert.IsFalse(reload.Contains("ResetPairingUiState();", StringComparison.Ordinal));
        StringAssert.Contains(source.Replace("\r\n", "\n"), "Settings.Properties.UseService = value;\n                    ResetPairingUiState();");
        Assert.IsFalse(source.Contains("FromBase64String", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Unprotect", StringComparison.Ordinal));
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs");
        StringAssert.Contains(page, "Unloaded += (s, e) => ClearPairingUI();");
    }

    [DataTestMethod]
    [DataRow("public Task ConnectToMachine(", "Setting.Values.MyKey = securityKey;")]
    [DataRow("public Task<string> GenerateNewKey()", "Setting.Values.MyKey = key;")]
    public void PairingRequestSourceContractProtectsKeyBeforeChangingOtherState(string signature, string assignment)
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Class\Program.cs");
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        var key = source.IndexOf(assignment, start, StringComparison.Ordinal);
        var matrix = source.IndexOf("MachineStuff.ClearComputerMatrix();", start, StringComparison.Ordinal);
        var runtime = source.IndexOf("Encryption.MyKey =", start, StringComparison.Ordinal);
        Assert.IsTrue(key >= start && key < matrix && key < runtime);
        if (signature.Contains("GenerateNewKey", StringComparison.Ordinal))
        {
            var options = source.IndexOf("Setting.Values.EasyMouse =", start, StringComparison.Ordinal);
            Assert.IsTrue(key < options);
        }
    }

    private static string ReadSource(string relativePath)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, relativePath)))
        {
            root = root.Parent;
        }

        Assert.IsNotNull(root);
        return File.ReadAllText(Path.Combine(root.FullName, relativePath));
    }
}
