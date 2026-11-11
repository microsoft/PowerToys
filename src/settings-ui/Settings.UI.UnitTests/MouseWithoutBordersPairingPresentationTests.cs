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

[OSCondition(OperatingSystems.Windows)]
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
        Assert.Contains(
            """
            ClearPairingUI();
                            ViewModel.OnPageLoaded();
                            if (ViewModel.LoadUpdatedSettings())
                            {
                                ViewModel.NotifyUpdatedSettings();
                            }

                            await ViewModel.LoadPairingKeyAsync();
            """.Replace("\r\n", "\n"),
            page.Substring(start, end - start));
    }

    [TestMethod]
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
        Assert.IsEmpty(sentModes);
        Assert.IsFalse(model.CanManagePairing);
        Assert.IsFalse(model.CanBeEnabled);
        Assert.IsFalse(model.CanToggleUseService);
        Assert.IsFalse(model.CanReconnect);
        shutdown.SetResult();
        await restartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.AreSequenceEqual(new[] { useService }, sentModes);
        Assert.AreEqual(useService, settings.Properties.UseService);
        Assert.IsFalse(model.CanReconnect);
        restart.SetResult();
        await transition.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.IsTrue(model.CanReconnect);
        Assert.IsTrue(model.CanBeEnabled);
        Assert.IsTrue(model.CanManagePairing);
        Assert.IsTrue(model.UseServiceSettingIsEnabled);
        Assert.IsTrue(model.ShowPairingSetupRequired);
        Assert.AreEqual(string.Empty, model.SecurityKey);
        Assert.IsFalse(model.HasServiceModeError);
    }

    [TestMethod]
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

        await transition.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.IsTrue(model.HasServiceModeError);
        Assert.IsFalse(model.CanManagePairing);
        Assert.IsFalse(model.CanReconnect);
        Assert.IsTrue(model.UseServiceSettingIsEnabled);
        Assert.AreEqual(shutdownFails, settings.Properties.UseService);
        Assert.HasCount(shutdownFails ? 0 : 1, sentModes);
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
        await initial.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Assert.AreEqual(2, reads, "The latest generation needs exactly one replacement read.");
        Assert.AreEqual("current-key", model.SecurityKey);
        Assert.IsFalse(model.HasPairingError);
    }

    [TestMethod]
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

    [TestMethod]
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
        Assert.Contains("LinkButton.Enabled = Encryption.IsKeyValid(Regex.Replace(SecurityCodeField.Text, @\"\\s+\", string.Empty), out _) && ComputerNameField.Text.Trim().Length > 0;", setup);
        Assert.Contains("var key = Regex.Replace(SecurityCodeField.Text, @\"\\s+\", string.Empty);", setup);
        Assert.IsLessThan(setup.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal), setup.IndexOf("Setting.Values.MyKey = key;", StringComparison.Ordinal));
        var matrix = ReadSource(@"src\modules\MouseWithoutBorders\App\Form\frmMatrix.cs");
        Assert.IsFalse(matrix.Contains("Class.Settings.IsProtectedMode", StringComparison.Ordinal));
        Assert.Contains("textBoxEnc.Text = Setting.Values.PairingKeyDisplayText;", matrix);
        Assert.Contains("if (!Encryption.IsKeyValid(newKey, out string rv))", matrix);
        Assert.Contains("Setting.Values.MyKey = newKey;", matrix);
        Assert.Contains("Encryption.MyKey = newKey;", matrix);
        Assert.IsLessThan(matrix.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal), matrix.IndexOf("Setting.Values.MyKey = key;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void OriginalSettingsSourceContractPreservesServiceModeEntryAndProtectsBeforePublishingKeys()
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Core\MachineStuff.cs");
        var start = source.IndexOf("internal static void ShowMachineMatrix()", StringComparison.Ordinal);
        var end = source.IndexOf("private static string[] mcMatrix;", start, StringComparison.Ordinal);
        var show = source.Substring(start, end - start);
        Assert.IsFalse(show.Contains("if (Class.Settings.IsProtectedMode)", StringComparison.Ordinal));
        Assert.Contains("if (Setting.Values.FirstRun && !Setting.Values.IsPairingKeyModeMismatch && !Common.AtLeastOneSocketConnected())", show);
        Assert.Contains("Common.MatrixForm = new FrmMatrix();", show);
        Assert.Contains("Common.MatrixForm.Show();", show);
        Assert.Contains("Common.MatrixForm.Activate();", show);

        var setupStart = source.IndexOf("internal static void ShowSetupForm(", StringComparison.Ordinal);
        var setup = source.Substring(setupStart, start - setupStart);
        Assert.IsFalse(setup.Contains("Class.Settings.IsProtectedMode", StringComparison.Ordinal));
        Assert.IsLessThan(
            setup.IndexOf("Encryption.MyKey = key;", StringComparison.Ordinal),
            setup.IndexOf("Setting.Values.MyKey = key;", StringComparison.Ordinal));
        var settings = ReadSource(@"src\modules\MouseWithoutBorders\App\Class\Setting.cs");
        var displayStart = settings.IndexOf("internal string PairingKeyDisplayText", StringComparison.Ordinal);
        var displayEnd = settings.IndexOf("internal string MyKey", displayStart, StringComparison.Ordinal);
        var display = settings.Substring(displayStart, displayEnd - displayStart);
        Assert.Contains("if (IsProtectedMode)", display);
        Assert.Contains("return Encryption.MyKey ?? string.Empty;", display);
        Assert.IsFalse(display.Contains("Decrypt(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PairingViewModelSourceWiringKeepsUiStateOutOfSettings()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        Assert.Contains("PairingKeyCodec.IsEncryptedValue(Settings.Properties.SecurityKey.Value)", source);
        Assert.Contains("public bool CanManagePairing => !_isPairingPending && !_isServiceModeChanging && !_hasServiceModeError && (!UseService || IsElevated);", source);
        Assert.Contains("var generatedKey = await endpoint.GenerateNewKey().WaitAsync(SettingsSyncRequests.Timeout);", source);
        Assert.Contains("_servicePairingKey = useService ? generatedKey : null;", source);
        Assert.Contains("await endpoint.ConnectToMachine(pcName, securityKey).WaitAsync(SettingsSyncRequests.Timeout);", source);
        Assert.Contains("_servicePairingKey = useService ? securityKey : null;\n                    OnPropertyChanged(nameof(SecurityKey));", source.Replace("\r\n", "\n"));
        Assert.Contains("public async Task<bool> SubmitConnectionRequestAsync(string pcName, string securityKey)", source);
        Assert.Contains("pcName = pcName?.Trim();", source);
        Assert.Contains("securityKey = securityKey?.Trim();", source);
        foreach (var removedInput in new[] { "_connectSecurityKey", "_connectPCName", "ConnectSecurityKey", "ConnectPCName" })
        {
            Assert.IsFalse(source.Contains(removedInput, StringComparison.Ordinal));
        }

        Assert.Contains("if (_disposed || pairingUiVersion != _pairingUiVersion || UseService != useService || (useService && !IsElevated))", source);
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
        Assert.Contains("ViewModel.ResetPairingUiState();", clear);
        Assert.Contains("ConnectPCNameTextBox.Text = string.Empty;", clear);
        Assert.Contains("ConnectSecurityKeyTextBox.Text = string.Empty;", clear);
        Assert.Contains("Loaded += async (s, e) =>\n            {\n                ClearPairingUI();", page);
        Assert.Contains("Unloaded += (s, e) => ClearPairingUI();", page);
        Assert.Contains("ClearPairingUI();", page.Substring(end, clearStart - end));
        var xaml = XDocument.Parse(ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml"));
        var serviceToggle = xaml.Descendants().Single(element => (string)element.Attribute("IsOn") == "{x:Bind ViewModel.UseService, Mode=TwoWay}");
        Assert.AreEqual("ServiceMode_Toggled", (string)serviceToggle.Attribute("Toggled"));

        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs");
        start = source.IndexOf("public async Task<bool> SubmitConnectionRequestAsync(", StringComparison.Ordinal);
        end = source.IndexOf("private async Task<ISettingsSyncHelper.MachineSocketState[]>", start, StringComparison.Ordinal);
        var request = source.Substring(start, end - start);
        Assert.Contains("if (!CanConnectPairing)", request);
        Assert.Contains("_isPairingPending = true;", request);
        Assert.Contains("_hasPairingError = true;", request);
        Assert.Contains("catch (Exception ex)", request);
        Assert.Contains("return false;", request);
        Assert.Contains("return true;", request);
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
        var semaphore = request.IndexOf("using (await SettingsSyncRequests.Semaphore.EnterAsync())", StringComparison.Ordinal);
        var roleGuard = request.IndexOf("if (_disposed || _isServiceModeChanging || !IsEnabled || !UseService || !IsElevated || PairingSetupRequired)", semaphore, StringComparison.Ordinal);
        var read = request.IndexOf("var key = await readPairingKey();", StringComparison.Ordinal);
        var resultGuard = request.IndexOf("if (_disposed || !IsEnabled || UseService != useService || !IsElevated)", StringComparison.Ordinal);
        var display = request.IndexOf("_servicePairingKey = key;", StringComparison.Ordinal);
        Assert.IsTrue(initialGuard >= 0 && initialGuard < semaphore && semaphore < roleGuard && roleGuard < read && read < resultGuard && resultGuard < display);
        Assert.Contains("catch (Exception ex)", request);
        Assert.Contains("_hasPairingError = true;", request);
        Assert.Contains("OnPropertyChanged(nameof(HasPairingError));", request);
        Assert.Contains("if (_servicePairingKey != key)", request);
        Assert.Contains(
            """
            if (_servicePairingKey != key)
                                {
                                    _servicePairingKey = key;
                                    OnPropertyChanged(nameof(SecurityKey));
                                }
            """.Replace("\r\n", "\n"),
            request);
        var failure = request.Substring(request.IndexOf("catch (Exception ex)", StringComparison.Ordinal));
        Assert.Contains(
            """
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
            """.Replace("\r\n", "\n"),
            failure);
        Assert.IsFalse(request.Contains("SaveSettings", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("NotifyPropertyChanged(", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("Settings.Properties.SecurityKey.Value", StringComparison.Ordinal));
        var getterStart = source.IndexOf("public string SecurityKey =>", StringComparison.Ordinal);
        var getter = source.Substring(getterStart, source.IndexOf("public bool WrapMouse", getterStart, StringComparison.Ordinal) - getterStart);
        Assert.Contains("UseService\n            ? IsElevated\n                ? _servicePairingKey ?? string.Empty", getter);
        Assert.Contains(": PairingSetupRequired ? string.Empty : Settings.Properties.SecurityKey.Value;", getter);
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs").Replace("\r\n", "\n");
        Assert.Contains("await ViewModel.LoadPairingKeyAsync();", page);
        Assert.Contains("ViewModel.NotifyUpdatedSettings();\n                            await ViewModel.LoadPairingKeyAsync();", page);
        var resources = XDocument.Parse(ReadSource(@"src\settings-ui\Settings.UI\Strings\en-us\Resources.resw"));
        Assert.AreEqual("Pairing could not be loaded or updated. Try again.", resources.Root.Elements("data").Single(element => (string)element.Attribute("name") == "MouseWithoutBorders_PairingUpdateFailed.Message").Element("value").Value);
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        Assert.Contains("if (UseService != useService || PairingSetupRequired || !IsEnabled)", reload);
        Assert.Contains("if (SecurityKey != displayedKey)", reload);
    }

    [TestMethod]
    public void SocketReopenSourceContractSkipsConstructionQuietlyWithoutRuntimeKey()
    {
        var source = ReadSource(@"src\modules\MouseWithoutBorders\App\Core\Common.cs");
        var start = source.IndexOf("internal static void ReopenSockets(bool byUser)", StringComparison.Ordinal);
        var end = source.IndexOf("internal static string GetMyStorageDir()", start, StringComparison.Ordinal);
        var reopen = source.Substring(start, end - start).Replace("\r\n", "\n");
        Assert.Contains("Sk = !string.IsNullOrWhiteSpace(Encryption.MyKey)\n                    ? new SocketStuff(tcpPort, byUser)\n                    : null;", reopen);
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
        Assert.Contains("ApplyLoadedSettings(\n                        settings,", reload);
        Assert.Contains("_settings = settings;\n            _properties = settings.Properties;", reload);
        Assert.Contains("var shouldReopenSockets = UpdateRuntimeKey();", reload);
        Assert.Contains("SocketStuff.InvalidKeyFound = false;", reload);
        Assert.Contains("InitAndCleanup.ReopenSocketDueToReadError = true;", reload);
        Assert.Contains("Common.ReopenSockets(true);", reload);
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
        Assert.Contains("_servicePairingKey = null;", clear);
        Assert.Contains("OnPropertyChanged(nameof(SecurityKey));", clear);
        Assert.IsFalse(clear.Contains("SaveSettings", StringComparison.Ordinal));
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        Assert.Contains("_servicePairingKey = null;", reload);
        Assert.Contains("if (UseService != useService || PairingSetupRequired || !IsEnabled)", reload);
        Assert.IsFalse(reload.Contains("ResetPairingUiState();", StringComparison.Ordinal));
        var transition = source.Substring(source.IndexOf("private async Task ChangeServiceModeAsync(", StringComparison.Ordinal));
        Assert.IsLessThan(transition.IndexOf("Settings.Properties.UseService = useService;", StringComparison.Ordinal), transition.IndexOf("await shutdown();", StringComparison.Ordinal));
        Assert.Contains("await waitForRestart();", transition);
        Assert.IsFalse(source.Contains("FromBase64String", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Unprotect", StringComparison.Ordinal));
        var page = ReadSource(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs");
        Assert.Contains("Unloaded += (s, e) => ClearPairingUI();", page);
    }

    [TestMethod]
    public void PairingRefreshSourceContractRetainsGeneratedKeyUntilAuthoritativeComparison()
    {
        var source = ReadSource(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs").Replace("\r\n", "\n");
        var generateStart = source.IndexOf("public async Task SubmitNewKeyRequestAsync()", StringComparison.Ordinal);
        var loadStart = source.IndexOf("public async Task LoadPairingKeyAsync()", generateStart, StringComparison.Ordinal);
        var generate = source.Substring(generateStart, loadStart - generateStart);
        Assert.Contains("var generatedKey = await endpoint.GenerateNewKey().WaitAsync(SettingsSyncRequests.Timeout);", generate);
        Assert.Contains("_servicePairingKey = useService ? generatedKey : null;\n                    OnPropertyChanged(nameof(SecurityKey));", generate);
        var reloadStart = source.IndexOf("public bool LoadUpdatedSettings()", StringComparison.Ordinal);
        var reloadEnd = source.IndexOf("private void SendCustomAction(", reloadStart, StringComparison.Ordinal);
        var reload = source.Substring(reloadStart, reloadEnd - reloadStart);
        Assert.Contains(
            """
            LoadViewModelFromSettings(SettingsUtils.GetSettings<MouseWithoutBordersSettings>("MouseWithoutBorders"));
                            if (UseService != useService || PairingSetupRequired || !IsEnabled)
                            {
                                _servicePairingKey = null;
                            }

                            if (SecurityKey != displayedKey)
                            {
                                OnPropertyChanged(nameof(SecurityKey));
                            }
            """.Replace("\r\n", "\n"),
            reload);
        Assert.IsFalse(reload.Contains("Settings.Properties.SecurityKey.Value", StringComparison.Ordinal));
        Assert.IsFalse(reload.Contains("storedKey", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("public Task ConnectToMachine(", "Setting.Values.MyKey = securityKey;")]
    [DataRow("public async Task<string> GenerateNewKey()", "Setting.Values.MyKey = key;")]
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
            Assert.IsLessThan(options, key);
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

    public TestContext TestContext { get; set; } = null!;
}
