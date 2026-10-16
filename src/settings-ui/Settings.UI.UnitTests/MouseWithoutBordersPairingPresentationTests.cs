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
    public void PairingViewModelSourceWiringKeepsUiStateOutOfSettings()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        StringAssert.Contains(source, "PairingKeyCodec.IsEncryptedValue(Settings.Properties.SecurityKey.Value)");
        StringAssert.Contains(source, "public bool CanManagePairing => !_isPairingPending && (!UseService || IsElevated);");
        StringAssert.Contains(source, "var generatedKey = await endpoint.GenerateNewKey();");
        StringAssert.Contains(source, "_transientSecurityKey = useService ? generatedKey : null;");
        StringAssert.Contains(source, "await endpoint.ConnectToMachine(pcName, securityKey);");
        StringAssert.Contains(source.Replace("\r\n", "\n"), "_transientSecurityKey = null;\n                    OnPropertyChanged(nameof(SecurityKey));");
        StringAssert.Contains(source, "var pcName = ConnectPCName?.Trim();");
        StringAssert.Contains(source, "var securityKey = ConnectSecurityKey?.Trim();");
        StringAssert.Contains(source, "if (_disposed || UseService != useService)");
        Assert.IsFalse(source.Contains("TryUpdateAsync", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Settings.Properties.SecurityKey.Value = ", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("CanEditSettings", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void PairingViewModelSourceWiringInvalidatesPendingDisplayOnClear()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs").Replace("\r\n", "\n");
        var start = source.IndexOf("public async Task SubmitNewKeyRequestAsync()", StringComparison.Ordinal);
        var end = source.IndexOf("public bool HasPairingError", start, StringComparison.Ordinal);
        var request = source.Substring(start, end - start);
        var capture = request.IndexOf("var displayGeneration = _pairingDisplayGeneration;", StringComparison.Ordinal);
        var awaitResult = request.IndexOf("var generatedKey = await endpoint.GenerateNewKey();", StringComparison.Ordinal);
        var guard = request.IndexOf("if (_disposed || UseService != useService || _pairingDisplayGeneration != displayGeneration)\n                    {\n                        return;\n                    }", StringComparison.Ordinal);
        var display = request.IndexOf("_transientSecurityKey = useService ? generatedKey : null;", StringComparison.Ordinal);
        Assert.IsTrue(capture >= 0 && capture < awaitResult && awaitResult < guard && guard < display);
        StringAssert.Contains(request, "finally\n            {\n                _isPairingPending = false;");
        var clearStart = source.IndexOf("public void ClearTransientPairingState()", StringComparison.Ordinal);
        var clearEnd = source.IndexOf("public void NotifyPropertyChanged(", clearStart, StringComparison.Ordinal);
        var clear = source.Substring(clearStart, clearEnd - clearStart);
        StringAssert.Contains(clear, "_pairingDisplayGeneration++;");
        Assert.IsFalse(clear.Contains("_isPairingPending =", StringComparison.Ordinal));
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
        var clearStart = source.IndexOf("public void ClearTransientPairingState()", StringComparison.Ordinal);
        var clearEnd = source.IndexOf("public void NotifyPropertyChanged(", clearStart, StringComparison.Ordinal);
        var clear = source.Substring(clearStart, clearEnd - clearStart);
        StringAssert.Contains(clear, "_transientSecurityKey = null;");
        StringAssert.Contains(clear, "OnPropertyChanged(nameof(SecurityKey));");
        Assert.IsFalse(clear.Contains("SaveSettings", StringComparison.Ordinal));
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        StringAssert.Contains(reload, "_transientSecurityKey = null;");
        StringAssert.Contains(reload, "if (UseService != useService)");
        Assert.IsFalse(reload.Contains("ClearTransientPairingState();", StringComparison.Ordinal));
        StringAssert.Contains(source.Replace("\r\n", "\n"), "Settings.Properties.UseService = value;\n                    ClearTransientPairingState();");
        StringAssert.Contains(source, "_transientSecurityKey != null && UseService && IsElevated");
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
