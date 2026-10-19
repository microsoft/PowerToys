// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public sealed class MouseWithoutBordersPairingPresentationTests
{
    [TestMethod]
    public void PairingPageLoadedSourceContractReloadsSettingsBeforeNotificationsAndCurrentKeyRead()
    {
        // This checks page wiring, not a WinUI Loaded event.
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs").Replace("\r\n", "\n");
        var start = page.IndexOf("Loaded += async (s, e) =>", StringComparison.Ordinal);
        var end = page.IndexOf("Unloaded +=", start, StringComparison.Ordinal);
        StringAssert.Contains(page.Substring(start, end - start), """
            ClearPairingUI();
                            ViewModel.OnPageLoaded();
                            if (ViewModel.LoadUpdatedSettings())
                            {
                                ViewModel.NotifyUpdatedSettings();
                            }

                            await ViewModel.LoadPairingKeyAsync();
            """.Replace("\r\n", "\n"));
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task ServiceModeChangeWaitsForExitAndKeepsControlsDisabledUntilRestart(bool useService, bool elevated)
    {
        var model = CreateServiceModeViewModel(!useService, out var settings, out var sentModes, elevated);
        settings.Properties.SecurityKey.Value = useService ? "unencrypted-key" : "mwb-dpapi:v1:TEST";
        var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restartEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var change = GetServiceModeChangeMethod();
        var transition = (Task)change.Invoke(model, new object[]
        {
            useService,
            (Func<Task>)(() => shutdown.Task),
            (Func<Task>)(() =>
            {
                restartEntered.SetResult();
                return restart.Task;
            }),
        })!;

        Assert.AreEqual(!useService, settings.Properties.UseService);
        Assert.AreEqual(0, sentModes.Count);
        Assert.IsFalse(model.CanManagePairing);
        Assert.IsFalse(model.CanBeEnabled);
        Assert.IsFalse(model.CanToggleUseService);
        Assert.IsFalse(model.CanReconnect);
        shutdown.SetResult();
        await restartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { useService }, sentModes);
        Assert.AreEqual(useService, settings.Properties.UseService);
        Assert.IsFalse(model.CanReconnect);
        restart.SetResult();
        await transition.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(model.CanReconnect);
        Assert.IsTrue(model.CanBeEnabled);
        Assert.IsTrue(model.CanManagePairing);
        Assert.IsTrue(model.UseServiceSettingIsEnabled);
        Assert.IsTrue(model.ShowPairingSetupRequired);
        Assert.AreEqual(string.Empty, model.SecurityKey);
        Assert.IsFalse(model.HasServiceModeError);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ServiceModeChangeReportsFailureWithoutPublishingBeforeShutdown(bool shutdownFails)
    {
        var model = CreateServiceModeViewModel(true, out var settings, out var sentModes);
        var restartCalls = 0;
        var transition = (Task)GetServiceModeChangeMethod().Invoke(model, new object[]
        {
            false,
            (Func<Task>)(() => shutdownFails ? Task.FromException(new TimeoutException("Synthetic shutdown timeout.")) : Task.CompletedTask),
            (Func<Task>)(() =>
            {
                restartCalls++;
                return Task.FromException(new TimeoutException("Synthetic restart timeout."));
            }),
        })!;

        await transition.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(model.HasServiceModeError);
        Assert.IsFalse(model.CanManagePairing);
        Assert.IsFalse(model.CanReconnect);
        Assert.IsTrue(model.UseServiceSettingIsEnabled);
        Assert.AreEqual(shutdownFails, settings.Properties.UseService);
        Assert.AreEqual(shutdownFails ? 0 : 1, sentModes.Count);
        Assert.AreEqual(shutdownFails ? 0 : 1, restartCalls);
    }

    [TestMethod]
    public async Task ResetDuringKeyReadDrainsOneLatestRefreshWithoutConcurrentRequests()
    {
        var model = CreateUninitializedViewModel();
        var settings = new MouseWithoutBordersSettings();
        settings.Properties.UseService = true;
        settings.Properties.SecurityKey.Value = "mwb-dpapi:v1:TEST";
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MouseWithoutBordersViewModel).GetProperty("Settings", flags)!.SetValue(model, settings);
        typeof(MouseWithoutBordersViewModel).GetProperty("GeneralSettingsConfig", flags)!.SetValue(model, new GeneralSettings { IsElevated = true });
        typeof(MouseWithoutBordersViewModel).GetField("_isEnabled", flags)!.SetValue(model, true);
        var firstRead = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        Func<Task<string>> read = () => ++reads == 1 ? firstRead.Task : Task.FromResult("current-key");
        var load = typeof(MouseWithoutBordersViewModel).GetMethod("LoadPairingKeyAsync", flags, null, new[] { typeof(Func<Task<string>>) }, null)!;
        var initial = (Task)load.Invoke(model, new object[] { read })!;
        Assert.AreEqual(1, reads);
        model.ResetPairingUiState();
        for (var notification = 0; notification < 20; notification++)
        {
            await (Task)load.Invoke(model, new object[] { read })!;
        }

        Assert.AreEqual(1, reads, "Notifications must not start an overlapping RPC.");
        firstRead.SetResult("obsolete-key");
        await initial.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(2, reads, "The latest generation needs exactly one replacement read.");
        Assert.AreEqual("current-key", model.SecurityKey);
        Assert.IsFalse(model.HasPairingError);
    }

    [DataTestMethod]
    [DataRow(false, false, "unencrypted-key", "unencrypted-key", true, false, false)]
    [DataRow(false, false, "mwb-dpapi:v1:TEST", "", true, false, true)]
    [DataRow(true, false, "mwb-dpapi:v1:TEST", "********", false, true, false)]
    [DataRow(false, true, "mwb-dpapi:v1:TEST", "", true, false, true)]
    [DataRow(true, true, "mwb-dpapi:v1:TEST", "current-key", true, false, false)]
    [DataRow(false, false, "", "", true, false, true)]
    public void PairingPresentationRequiresAKeyUsableInTheSelectedMode(bool service, bool elevated, string storedKey, string displayedKey, bool canManage, bool requiresAdmin, bool setupRequired)
    {
        var model = CreateUninitializedViewModel();
        var settings = new MouseWithoutBordersSettings();
        settings.Properties.UseService = service;
        settings.Properties.SecurityKey.Value = storedKey;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MouseWithoutBordersViewModel).GetProperty("Settings", flags)!.SetValue(model, settings);
        typeof(MouseWithoutBordersViewModel).GetProperty("GeneralSettingsConfig", flags)!.SetValue(model, new GeneralSettings { IsElevated = elevated });
        typeof(MouseWithoutBordersViewModel).GetField("_isEnabled", flags)!.SetValue(model, true);
        typeof(MouseWithoutBordersViewModel).GetField("_servicePairingKey", flags)!.SetValue(model, "current-key");
        Assert.AreEqual(displayedKey, model.SecurityKey);
        Assert.AreEqual(canManage, model.CanManagePairing);
        Assert.AreEqual(requiresAdmin, model.ShowPairingRequiresAdministrator);
        Assert.AreEqual(setupRequired, model.PairingSetupRequired);
    }

    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:TEST", true)]
    [DataRow("mwb-dpapi:v1:", true)]
    [DataRow("mwb-dpapi:v1:!", true)]
    [DataRow("mwb-dpapi:v2:TEST", true)]
    [DataRow("MWB-DPAPI:v1:TEST", true)]
    [DataRow("mwb-dpapi:v1:A QID", true)]
    [DataRow("unencrypted-key", false)]
    [DataRow(null, false)]
    public void ClassifierRecognizesReservedFamilyWithoutValidatingPayload(string value, bool reserved)
    {
        Assert.AreEqual(reserved, PairingKeyCodec.IsEncryptedValue(value));
    }

    [TestMethod]
    public void OriginalPairingSourceWiringPreservesKeyEditingInTheWorkerProcess()
    {
        var setup = ReadSource(@"src\modules\MouseWithoutBorders\App\Form\Settings\SetupPage2a.cs");
        Assert.IsFalse(setup.Contains("Class.Settings.IsProtectedMode", StringComparison.Ordinal));
        StringAssert.Contains(setup, "LinkButton.Enabled = Encryption.IsKeyValid(Regex.Replace(SecurityCodeField.Text, @\"\\s+\", string.Empty), out _) && ComputerNameField.Text.Trim().Length > 0;");
        StringAssert.Contains(setup, "var key = Regex.Replace(SecurityCodeField.Text, @\"\\s+\", string.Empty);");
        Assert.IsTrue(setup.IndexOf("Setting.Values.MyKey = key;", StringComparison.Ordinal) < setup.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal));
        var matrix = ReadSource(@"src\modules\MouseWithoutBorders\App\Form\frmMatrix.cs");
        Assert.IsFalse(matrix.Contains("Class.Settings.IsProtectedMode", StringComparison.Ordinal));
        StringAssert.Contains(matrix, "textBoxEnc.Text = Setting.Values.PairingKeyDisplayText;");
        StringAssert.Contains(matrix, "if (!Encryption.IsKeyValid(newKey, out string rv))");
        StringAssert.Contains(matrix, "Setting.Values.MyKey = newKey;");
        StringAssert.Contains(matrix, "Encryption.MyKey = newKey;");
        Assert.IsTrue(matrix.IndexOf("Setting.Values.MyKey = key;", StringComparison.Ordinal) < matrix.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void OriginalSettingsSourceContractPreservesServiceModeEntryAndProtectsBeforePublishingKeys()
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Core\MachineStuff.cs");
        var start = source.IndexOf("internal static void ShowMachineMatrix()", StringComparison.Ordinal);
        var end = source.IndexOf("private static string[] mcMatrix;", start, StringComparison.Ordinal);
        var show = source.Substring(start, end - start);
        Assert.IsFalse(show.Contains("if (Class.Settings.IsProtectedMode)", StringComparison.Ordinal));
        StringAssert.Contains(show, "if (Setting.Values.FirstRun && !Setting.Values.IsPairingKeyModeMismatch && !Common.AtLeastOneSocketConnected())");
        StringAssert.Contains(show, "Common.MatrixForm = new FrmMatrix();");
        StringAssert.Contains(show, "Common.MatrixForm.Show();");
        StringAssert.Contains(show, "Common.MatrixForm.Activate();");

        var setupStart = source.IndexOf("internal static void ShowSetupForm(", StringComparison.Ordinal);
        var setup = source.Substring(setupStart, start - setupStart);
        Assert.IsFalse(setup.Contains("Class.Settings.IsProtectedMode", StringComparison.Ordinal));
        Assert.IsTrue(
            setup.IndexOf("Setting.Values.MyKey = key;", StringComparison.Ordinal) <
            setup.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal));
        var settings = ReadSource(@"src\modules\MouseWithoutBorders\App\Class\Setting.cs");
        var displayStart = settings.IndexOf("internal string PairingKeyDisplayText", StringComparison.Ordinal);
        var displayEnd = settings.IndexOf("internal string MyKey", displayStart, StringComparison.Ordinal);
        var display = settings.Substring(displayStart, displayEnd - displayStart);
        StringAssert.Contains(display, "if (IsProtectedMode)");
        StringAssert.Contains(display, "return Encryption.MyKey ?? string.Empty;");
        Assert.IsFalse(display.Contains("Decrypt(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PairingViewModelSourceWiringKeepsUiStateOutOfSettings()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        StringAssert.Contains(source, "PairingKeyCodec.IsEncryptedValue(Settings.Properties.SecurityKey.Value)");
        StringAssert.Contains(source, "public bool CanManagePairing => !_isPairingPending && !_isServiceModeChanging && !_hasServiceModeError && (!UseService || IsElevated);");
        StringAssert.Contains(source, "var generatedKey = await endpoint.GenerateNewKey().WaitAsync(TimeSpan.FromSeconds(10));");
        StringAssert.Contains(source, "_servicePairingKey = useService ? generatedKey : null;");
        StringAssert.Contains(source, "await endpoint.ConnectToMachine(pcName, securityKey).WaitAsync(TimeSpan.FromSeconds(10));");
        StringAssert.Contains(source.Replace("\r\n", "\n"), "_servicePairingKey = useService ? securityKey : null;\n                    OnPropertyChanged(nameof(SecurityKey));");
        StringAssert.Contains(source, "public async Task<bool> SubmitConnectionRequestAsync(string pcName, string securityKey)");
        StringAssert.Contains(source, "pcName = pcName?.Trim();");
        StringAssert.Contains(source, "securityKey = securityKey?.Trim();");
        foreach (var removedInput in new[] { "_connectSecurityKey", "_connectPCName", "ConnectSecurityKey", "ConnectPCName" })
        {
            Assert.IsFalse(source.Contains(removedInput, StringComparison.Ordinal));
        }

        StringAssert.Contains(source, "if (_disposed || pairingUiVersion != _pairingUiVersion || UseService != useService || (useService && !IsElevated))");
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
        var initialGuard = request.IndexOf("if (_disposed || _isServiceModeChanging || !IsEnabled || !UseService || !IsElevated || PairingSetupRequired)", StringComparison.Ordinal);
        var semaphore = request.IndexOf("using (await _ipcSemaphore.EnterAsync())", StringComparison.Ordinal);
        var roleGuard = request.IndexOf("if (_disposed || _isServiceModeChanging || !IsEnabled || !UseService || !IsElevated || PairingSetupRequired)", semaphore, StringComparison.Ordinal);
        var read = request.IndexOf("var key = await readPairingKey();", StringComparison.Ordinal);
        var resultGuard = request.IndexOf("if (_disposed || !IsEnabled || UseService != useService || !IsElevated)", StringComparison.Ordinal);
        var display = request.IndexOf("_servicePairingKey = key;", StringComparison.Ordinal);
        Assert.IsTrue(initialGuard >= 0 && initialGuard < semaphore && semaphore < roleGuard && roleGuard < read && read < resultGuard && resultGuard < display);
        StringAssert.Contains(request, "catch (Exception ex)");
        StringAssert.Contains(request, "_hasPairingError = true;");
        StringAssert.Contains(request, "OnPropertyChanged(nameof(HasPairingError));");
        StringAssert.Contains(request, "if (_servicePairingKey != key)");
        StringAssert.Contains(request, """
            if (_servicePairingKey != key)
                                {
                                    _servicePairingKey = key;
                                    OnPropertyChanged(nameof(SecurityKey));
                                }
            """.Replace("\r\n", "\n"));
        var failure = request.Substring(request.IndexOf("catch (Exception ex)", StringComparison.Ordinal));
        StringAssert.Contains(failure, """
            if (!_disposed && IsEnabled && UseService == useService && IsElevated)
                            {
                                if (_servicePairingKey != null)
                                {
                                    _servicePairingKey = null;
                                    OnPropertyChanged(nameof(SecurityKey));
                                }

                                _hasPairingError = true;
                                OnPropertyChanged(nameof(HasPairingError));
                            }
            """.Replace("\r\n", "\n"));
        Assert.IsFalse(request.Contains("SaveSettings", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("NotifyPropertyChanged(", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("Settings.Properties.SecurityKey.Value", StringComparison.Ordinal));
        var getterStart = source.IndexOf("public string SecurityKey =>", StringComparison.Ordinal);
        var getter = source.Substring(getterStart, source.IndexOf("public bool WrapMouse", getterStart, StringComparison.Ordinal) - getterStart);
        StringAssert.Contains(getter, "UseService\n            ? IsElevated\n                ? _servicePairingKey ?? string.Empty");
        StringAssert.Contains(getter, ": PairingSetupRequired ? string.Empty : Settings.Properties.SecurityKey.Value;");
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs").Replace("\r\n", "\n");
        StringAssert.Contains(page, "await ViewModel.LoadPairingKeyAsync();");
        StringAssert.Contains(page, "ViewModel.NotifyUpdatedSettings();\n                            await ViewModel.LoadPairingKeyAsync();");
        var resources = XDocument.Parse(ReadSource(@"src\settings-ui\Settings.UI\Strings\en-us\Resources.resw"));
        Assert.AreEqual("Pairing could not be loaded or updated. Try again.", resources.Root.Elements("data").Single(element => (string)element.Attribute("name") == "MouseWithoutBorders_PairingUpdateFailed.Message").Element("value").Value);
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        StringAssert.Contains(reload, "if (UseService != useService || PairingSetupRequired || !IsEnabled)");
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
    public void SettingsReloadSourceContractUsesConvertedKeyInTheReloadOwner()
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Class\Setting.cs").Replace("\r\n", "\n");
        var start = source.IndexOf("private void UpdateSettingsFromJson()", StringComparison.Ordinal);
        var end = source.IndexOf("public void SaveSettings()", start, StringComparison.Ordinal);
        var reload = source.Substring(start, end - start);
        StringAssert.Contains(reload, "ApplyLoadedSettings(\n                        settings,");
        StringAssert.Contains(reload, "_settings = settings;\n            _properties = settings.Properties;");
        StringAssert.Contains(reload, "var shouldReopenSockets = UpdateRuntimeKey();");
        StringAssert.Contains(reload, "SocketStuff.InvalidKeyFound = false;");
        StringAssert.Contains(reload, "InitAndCleanup.ReopenSocketDueToReadError = true;");
        StringAssert.Contains(reload, "Common.ReopenSockets(true);");
        Assert.IsFalse(reload.Contains("lock (_loadingSettingsLock)", StringComparison.Ordinal));
        Assert.IsFalse(reload.Contains("Encryption.MyKey =", StringComparison.Ordinal));
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
        StringAssert.Contains(reload, "if (UseService != useService || PairingSetupRequired || !IsEnabled)");
        Assert.IsFalse(reload.Contains("ResetPairingUiState();", StringComparison.Ordinal));
        var transition = source.Substring(source.IndexOf("private async Task ChangeServiceModeAsync(", StringComparison.Ordinal));
        Assert.IsTrue(transition.IndexOf("await shutdown();", StringComparison.Ordinal) < transition.IndexOf("Settings.Properties.UseService = useService;", StringComparison.Ordinal));
        StringAssert.Contains(transition, "await waitForRestart();");
        Assert.IsFalse(source.Contains("FromBase64String", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Unprotect", StringComparison.Ordinal));
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs");
        StringAssert.Contains(page, "Unloaded += (s, e) => ClearPairingUI();");
    }

    [TestMethod]
    public void PairingRefreshSourceContractRetainsGeneratedKeyUntilAuthoritativeComparison()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs").Replace("\r\n", "\n");
        var generateStart = source.IndexOf("public async Task SubmitNewKeyRequestAsync()", StringComparison.Ordinal);
        var loadStart = source.IndexOf("public async Task LoadPairingKeyAsync()", generateStart, StringComparison.Ordinal);
        var generate = source.Substring(generateStart, loadStart - generateStart);
        StringAssert.Contains(generate, "var generatedKey = await endpoint.GenerateNewKey().WaitAsync(TimeSpan.FromSeconds(10));");
        StringAssert.Contains(generate, "_servicePairingKey = useService ? generatedKey : null;\n                    OnPropertyChanged(nameof(SecurityKey));");
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        StringAssert.Contains(reload, """
            LoadViewModelFromSettings(SettingsUtils.GetSettings<MouseWithoutBordersSettings>("MouseWithoutBorders"));
                            if (UseService != useService || PairingSetupRequired || !IsEnabled)
                            {
                                _servicePairingKey = null;
                            }

                            if (SecurityKey != displayedKey)
                            {
                                OnPropertyChanged(nameof(SecurityKey));
                            }
            """.Replace("\r\n", "\n"));
        Assert.IsFalse(reload.Contains("Settings.Properties.SecurityKey.Value", StringComparison.Ordinal));
        Assert.IsFalse(reload.Contains("storedKey", StringComparison.Ordinal));
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

    [SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "The constructor is intentionally not run; skip finalization of the uninitialized reflection test instance.")]
    private static MouseWithoutBordersViewModel CreateUninitializedViewModel()
    {
        var model = (MouseWithoutBordersViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MouseWithoutBordersViewModel));
        var servicePolicy = typeof(MouseWithoutBordersViewModel).GetField("_allowServiceModeGpoConfiguration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        servicePolicy.SetValue(model, Enum.Parse(servicePolicy.FieldType, "NotConfigured"));
        GC.SuppressFinalize(model);
        return model;
    }

    private static MethodInfo GetServiceModeChangeMethod() =>
        typeof(MouseWithoutBordersViewModel).GetMethod("ChangeServiceModeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static MouseWithoutBordersViewModel CreateServiceModeViewModel(bool useService, out MouseWithoutBordersSettings settings, out List<bool> sentModes, bool elevated = true)
    {
        var model = CreateUninitializedViewModel();
        var moduleSettings = new MouseWithoutBordersSettings();
        moduleSettings.Properties.UseService = useService;
        moduleSettings.Properties.SecurityKey.Value = "unencrypted-key";
        var modes = new List<bool>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MouseWithoutBordersViewModel).GetProperty("Settings", flags)!.SetValue(model, moduleSettings);
        typeof(MouseWithoutBordersViewModel).GetProperty("SettingsUtils", flags)!.SetValue(model, new SettingsUtils(new MockFileSystem()));
        typeof(MouseWithoutBordersViewModel).GetProperty("GeneralSettingsConfig", flags)!.SetValue(model, new GeneralSettings { IsElevated = elevated });
        typeof(MouseWithoutBordersViewModel).GetField("_isEnabled", flags)!.SetValue(model, true);
        Func<string, int> sendConfig = _ =>
        {
            modes.Add(moduleSettings.Properties.UseService);
            return 0;
        };
        typeof(MouseWithoutBordersViewModel).GetField("<SendConfigMSG>k__BackingField", flags)!.SetValue(model, sendConfig);
        settings = moduleSettings;
        sentModes = modes;
        return model;
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
