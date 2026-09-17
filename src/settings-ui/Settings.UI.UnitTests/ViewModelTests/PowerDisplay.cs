// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PowerDisplay.Models;

namespace ViewModelTests;

// SA1649: file is named PowerDisplay.cs to match the module naming convention used throughout
// ViewModelTests (FancyZones.cs, ColorPicker.cs, etc.).
[TestClass]
public class PowerDisplay
{
    [TestMethod]
    public void MouseWheelMode_DefaultsToDisabled()
    {
        using var viewModel = CreateViewModel(out _);

        Assert.AreEqual(
            (int)MouseWheelControlMode.Disabled,
            viewModel.MouseWheelControlModeIndex);
    }

    [TestMethod]
    public void MouseWheelMode_SetPrimaryDisplay_PersistsAndRoundTrips()
    {
        using var viewModel = CreateViewModel(out var settings);

        viewModel.MouseWheelControlModeIndex = (int)MouseWheelControlMode.PrimaryDisplay;

        Assert.AreEqual(
            MouseWheelControlMode.PrimaryDisplay,
            settings.Properties.MouseWheelControlMode);
        Assert.AreEqual(
            (int)MouseWheelControlMode.PrimaryDisplay,
            viewModel.MouseWheelControlModeIndex);
    }

    [TestMethod]
    public void MouseWheelMode_SetAllDisplays_PersistsAndRoundTrips()
    {
        using var viewModel = CreateViewModel(out var settings);

        viewModel.MouseWheelControlModeIndex = (int)MouseWheelControlMode.AllDisplays;

        Assert.AreEqual(
            MouseWheelControlMode.AllDisplays,
            settings.Properties.MouseWheelControlMode);
        Assert.AreEqual(
            (int)MouseWheelControlMode.AllDisplays,
            viewModel.MouseWheelControlModeIndex);
    }

    // The ComboBox in PowerDisplayPage.xaml binds SelectedIndex straight to the enum value, so the
    // declared item order (Off, Primary display, All displays) is load-bearing. Pin the numbering
    // here: inserting a new mode anywhere but at the end would silently remap existing settings.
    [TestMethod]
    public void MouseWheelMode_EnumValues_MatchComboBoxItemOrder()
    {
        Assert.AreEqual(0, (int)MouseWheelControlMode.Disabled);
        Assert.AreEqual(1, (int)MouseWheelControlMode.PrimaryDisplay);
        Assert.AreEqual(2, (int)MouseWheelControlMode.AllDisplays);
    }

    [TestMethod]
    public void MouseWheelMode_UnsupportedIndex_IsIgnored()
    {
        using var viewModel = CreateViewModel(out var settings);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.MouseWheelControlModeIndex = 99;

        Assert.AreEqual(
            MouseWheelControlMode.Disabled,
            settings.Properties.MouseWheelControlMode);
        CollectionAssert.Contains(
            changedProperties,
            nameof(PowerDisplayViewModel.MouseWheelControlModeIndex));
    }

    [TestMethod]
    [DataRow(nameof(MonitorInfo.EnableContrast))]
    [DataRow(nameof(MonitorInfo.EnableVolume))]
    [DataRow(nameof(MonitorInfo.EnableInputSource))]
    [DataRow(nameof(MonitorInfo.EnableRotation))]
    [DataRow(nameof(MonitorInfo.EnableColorTemperature))]
    [DataRow(nameof(MonitorInfo.EnablePowerState))]
    [DataRow(nameof(MonitorInfo.IsHidden))]
    public void MonitorUserPreference_ChangesSaveAndSendOnce(string propertyName)
    {
        var monitor = CreateMonitor();
        var namedEvents = new List<string>();
        var operationOrder = new List<string>();
        using var viewModel = CreateViewModel(out _, out var settingsUtils, out var ipcMessages, monitor, namedEvents: namedEvents, operationOrder: operationOrder);
        var property = typeof(MonitorInfo).GetProperty(propertyName);

        property.SetValue(monitor, true);

        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), PowerDisplaySettings.ModuleName, SettingsUtils.DefaultFileName),
            Times.Once);
        Assert.AreEqual(1, ipcMessages.Count);
        Assert.AreEqual(1, namedEvents.Count);
        Assert.AreEqual("save,ipc,signal", string.Join(',', operationOrder));
        Assert.IsTrue((bool)property.GetValue(GetSavedSettings(settingsUtils).Properties.Monitors.Single()));
    }

    [TestMethod]
    public void MonitorRuntimeAndDerivedProperties_DoNotSaveOrSend()
    {
        var monitor = CreateMonitor();
        var namedEvents = new List<string>();
        using var viewModel = CreateViewModel(out _, out var settingsUtils, out var ipcMessages, monitor, namedEvents: namedEvents);

        monitor.Name = "Updated monitor";
        monitor.CurrentBrightness = 75;
        monitor.ColorTemperatureVcp = 0x08;
        monitor.SupportsColorTemperature = false;
        monitor.CapabilitiesRaw = "(vcp(10))";
        monitor.VcpCodesFormatted = new List<VcpCodeDisplayInfo>();

        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(0, ipcMessages.Count);
        Assert.AreEqual(0, namedEvents.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReloadMonitors_ChangedUserPreferencesDoNotSaveOrSend(bool onPageLoaded)
    {
        var monitor = CreateMonitor();
        Action reloadMonitors = null;
        var namedEvents = new List<string>();
        using var viewModel = CreateViewModel(
            out _,
            out var settingsUtils,
            out var ipcMessages,
            monitor,
            (_, callback) => reloadMonitors = callback,
            namedEvents);
        var updatedSettings = new PowerDisplaySettings();
        var updatedMonitor = CreateMonitor();
        updatedMonitor.EnableColorTemperature = true;
        updatedMonitor.EnableInputSource = true;
        updatedMonitor.CapabilitiesRaw = "(vcp(10 14(05 08) 60(11)))";
        updatedSettings.Properties.Monitors.Add(updatedMonitor);
        settingsUtils.Setup(utils => utils.GetSettingsOrDefault<PowerDisplaySettings>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(updatedSettings);

        if (onPageLoaded)
        {
            viewModel.OnPageLoaded();
        }
        else
        {
            reloadMonitors();
        }

        Assert.AreSame(monitor, viewModel.Monitors.Single());
        Assert.IsTrue(monitor.EnableColorTemperature);
        Assert.IsTrue(monitor.EnableInputSource);
        Assert.AreEqual(updatedMonitor.CapabilitiesRaw, monitor.CapabilitiesRaw);
        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(0, ipcMessages.Count);
        Assert.AreEqual(0, namedEvents.Count);
    }

    [TestMethod]
    public void Dispose_ReleasesRegistrationAndAllCollectionSubscriptions()
    {
        var monitor = CreateMonitor();
        var registration = new Mock<IDisposable>();
        using var viewModel = CreateViewModel(
            out _,
            out var settingsUtils,
            out var ipcMessages,
            monitor,
            registerEvent: (_, _) => registration.Object);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Dispose();
        viewModel.Dispose();
        monitor.EnableContrast = true;
        viewModel.Monitors.Add(CreateMonitor());
        viewModel.Profiles.Add(new PowerDisplayProfile());
        viewModel.CustomVcpMappings.Add(new CustomVcpValueMapping());

        registration.Verify(item => item.Dispose(), Times.Once);
        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(0, ipcMessages.Count);
        Assert.AreEqual(0, changedProperties.Count);
        Assert.IsFalse(viewModel.CanUseProfiles);
    }

    [TestMethod]
    public void Dispose_QueuedRefreshOnlyUpdatesTheCurrentViewModel()
    {
        var previousRegistration = new TestRegistration();
        using var previous = CreateViewModel(
            out _,
            out var oldSettingsUtils,
            out _,
            registerEvent: previousRegistration.Register);
        Action queuedRefresh = previousRegistration.Invoke;
        previous.Dispose();
        oldSettingsUtils.Invocations.Clear();

        var currentRegistration = new TestRegistration();
        using var current = CreateViewModel(
            out _,
            out var currentSettingsUtils,
            out _,
            registerEvent: currentRegistration.Register);
        currentSettingsUtils.Invocations.Clear();
        queuedRefresh();
        currentRegistration.Invoke();

        oldSettingsUtils.Verify(utils => utils.GetSettingsOrDefault<PowerDisplaySettings>(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        currentSettingsUtils.Verify(utils => utils.GetSettingsOrDefault<PowerDisplaySettings>(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void FailedRegistration_ReleasesConstructorSubscriptions()
    {
        var monitor = CreateMonitor();
        Mock<SettingsUtils> settingsUtils = null;
        List<string> ipcMessages = null;

        Assert.ThrowsExactly<InvalidOperationException>(() => CreateViewModel(
            out _,
            out settingsUtils,
            out ipcMessages,
            monitor,
            registerEvent: (_, _) => throw new InvalidOperationException("Cannot register the event.")));
        monitor.EnableContrast = true;

        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(0, ipcMessages.Count);
    }

    [TestMethod]
    public async Task Dispose_CancelsPendingProfileLoadWithoutPublishingItsResult()
    {
        var pending = new TaskCompletionSource<PowerDisplayProfiles>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken loadToken = default;
        using var viewModel = CreateViewModel(
            out _,
            out _,
            out _,
            loadProfilesAsync: token =>
            {
                loadToken = token;
                return pending.Task;
            });
        var load = viewModel.InitializeProfilesAsync();
        var changes = 0;
        viewModel.PropertyChanged += (_, _) => changes++;

        viewModel.Dispose();
        await load.WaitAsync(TimeSpan.FromSeconds(10));
        pending.SetResult(new PowerDisplayProfiles { Profiles = new() { new PowerDisplayProfile { Id = 1 } } });

        Assert.IsTrue(loadToken.IsCancellationRequested);
        Assert.IsFalse(viewModel.HasProfiles);
        Assert.IsFalse(viewModel.IsProfileReorderError);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    public async Task CanceledProfileLoad_AllowsAnActiveViewModelToRetry()
    {
        var pending = new TaskCompletionSource<PowerDisplayProfiles>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using var viewModel = CreateViewModel(out _, out _, out _, loadProfilesAsync: _ => pending.Task);
        var load = viewModel.InitializeProfilesAsync(cancellation.Token);

        cancellation.Cancel();
        await load.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.IsFalse(viewModel.IsProfileReorderError);

        pending.SetResult(new PowerDisplayProfiles { Profiles = new() { new PowerDisplayProfile { Id = 1 } } });
        await viewModel.InitializeProfilesAsync();
        Assert.AreEqual(1, viewModel.Profiles.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_AfterLoadCompletionWasQueued_DoesNotChangeOldProfiles(bool failRead)
    {
        var pending = new TaskCompletionSource<PowerDisplayProfiles>();
        using var viewModel = CreateViewModel(out _, out _, out _, loadProfilesAsync: _ => pending.Task);
        var existingProfile = new PowerDisplayProfile { Id = 1 };
        viewModel.Profiles.Add(existingProfile);
        var context = new QueuedSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        Task load;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            load = viewModel.InitializeProfilesAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        if (failRead)
        {
            pending.SetException(new IOException("A read failed before navigation."));
        }
        else
        {
            pending.SetResult(new PowerDisplayProfiles());
        }

        Assert.IsTrue(context.HasCallbacks);
        var changes = 0;
        viewModel.PropertyChanged += (_, _) => changes++;
        viewModel.Dispose();
        context.Drain();
        await load.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreSame(existingProfile, viewModel.Profiles.Single());
        Assert.IsFalse(viewModel.IsProfileReorderError);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    [DataRow(PowerDisplayWarningKind.EnableModule)]
    [DataRow(PowerDisplayWarningKind.MaxCompatibility)]
    public void Dispose_AfterConfirmationWasQueued_DoesNotEnableOrNotify(PowerDisplayWarningKind kind)
    {
        var signals = new List<string>();
        using var viewModel = CreateViewModel(out var settings, out _, out var messages, namedEvents: signals);
        viewModel.IsEnabled = false;
        messages.Clear();
        var pending = new TaskCompletionSource<bool>();
        viewModel.ConfirmDangerousFeatureAsync = requestedKind =>
        {
            Assert.AreEqual(kind, requestedKind);
            return pending.Task;
        };
        var context = new QueuedSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            if (kind == PowerDisplayWarningKind.EnableModule)
            {
                viewModel.IsEnabled = true;
            }
            else
            {
                viewModel.MaxCompatibilityMode = true;
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        pending.SetResult(true);
        Assert.IsTrue(context.HasCallbacks);
        var changes = 0;
        viewModel.PropertyChanged += (_, _) => changes++;
        viewModel.Dispose();
        context.Drain();

        Assert.IsFalse(viewModel.IsEnabled);
        Assert.IsFalse(settings.Properties.MaxCompatibilityMode);
        Assert.AreEqual(0, messages.Count);
        Assert.AreEqual(0, signals.Count);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    public void Dispose_DetachesPageConfirmationCallbackAndAllowsCollection()
    {
        var monitor = CreateMonitor();
        var registration = new TestRegistration();
        var references = CreateDisposedViewModel(monitor, registration);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(references.ViewModel.IsAlive, "The monitor or registration retained the disposed viewmodel.");
        Assert.IsFalse(references.Page.IsAlive, "The disposed viewmodel retained the page confirmation callback.");
        GC.KeepAlive(monitor);
        GC.KeepAlive(registration);
    }

    [TestMethod]
    public async Task Dispose_ReleasesConfirmationTargetEvenIfViewModelIsRetained()
    {
        using var viewModel = CreateViewModel(out _);
        var page = AttachConfirmationOwner(viewModel);
        viewModel.Dispose();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(page.IsAlive);
        Assert.IsFalse(await viewModel.ConfirmDangerousFeatureAsync(PowerDisplayWarningKind.EnableModule));
        GC.KeepAlive(viewModel);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachConfirmationOwner(PowerDisplayViewModel viewModel)
    {
        var owner = new ConfirmationOwner();
        viewModel.ConfirmDangerousFeatureAsync = owner.ConfirmAsync;
        return new WeakReference(owner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference ViewModel, WeakReference Page) CreateDisposedViewModel(MonitorInfo monitor, TestRegistration registration)
    {
        var viewModel = CreateViewModel(out _, out _, out _, monitor, registerEvent: registration.Register);
        var page = new ConfirmationOwner();
        viewModel.ConfirmDangerousFeatureAsync = page.ConfirmAsync;
        viewModel.Dispose();
        return (new WeakReference(viewModel), new WeakReference(page));
    }

    private static MonitorInfo CreateMonitor()
    {
        return new MonitorInfo
        {
            Id = @"\\?\DISPLAY#TEST001#1",
            SupportsColorTemperature = true,
            ColorTemperatureVcp = 0x05,
            VcpCodesFormatted = new List<VcpCodeDisplayInfo>
            {
                new()
                {
                    Code = "0x14",
                    ValueList = new()
                    {
                        new() { Value = "0x05", Name = "6500 K" },
                        new() { Value = "0x08", Name = "9300 K" },
                    },
                },
            },
        };
    }

    private static PowerDisplaySettings GetSavedSettings(Mock<SettingsUtils> settingsUtils)
    {
        var save = settingsUtils.Invocations.Single(invocation => invocation.Method.Name == nameof(SettingsUtils.SaveSettings));
        return JsonSerializer.Deserialize((string)save.Arguments[0], SettingsSerializationContext.Default.PowerDisplaySettings);
    }

    private static PowerDisplayViewModel CreateViewModel(out PowerDisplaySettings settings)
        => CreateViewModel(out settings, out _, out _);

    private static PowerDisplayViewModel CreateViewModel(
        out PowerDisplaySettings settings,
        out Mock<SettingsUtils> settingsUtils,
        out List<string> ipcMessages,
        MonitorInfo monitor = null,
        Action<string, Action> waitForEventLoop = null,
        List<string> namedEvents = null,
        List<string> operationOrder = null,
        Func<string, Action, IDisposable> registerEvent = null,
        Func<CancellationToken, Task<PowerDisplayProfiles>> loadProfilesAsync = null)
    {
        var powerDisplaySettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<PowerDisplaySettings>();
        var generalSettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();

        settings = powerDisplaySettingsUtils.Object.GetSettingsOrDefault<PowerDisplaySettings>(
            PowerDisplaySettings.ModuleName);
        if (monitor != null)
        {
            settings.Properties.Monitors.Add(monitor);
        }

        settingsUtils = powerDisplaySettingsUtils;
        var messages = new List<string>();
        ipcMessages = messages;
        var events = namedEvents ?? new List<string>();
        var operations = operationOrder ?? new List<string>();
        powerDisplaySettingsUtils.Setup(utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, _) => operations.Add("save"));
        registerEvent ??= (eventName, callback) =>
        {
            waitForEventLoop?.Invoke(eventName, callback);
            return Mock.Of<IDisposable>();
        };

        return new PowerDisplayViewModel(
            powerDisplaySettingsUtils.Object,
            new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                generalSettingsUtils.Object),
            new BackCompatTestProperties.MockSettingsRepository<PowerDisplaySettings>(
                powerDisplaySettingsUtils.Object),
            message =>
            {
                messages.Add(message);
                operations.Add("ipc");
                return 0;
            },
            registerEvent,
            eventName =>
            {
                events.Add(eventName);
                operations.Add("signal");
            },
            loadProfilesAsync);
    }

    private sealed class TestRegistration : IDisposable
    {
        private Action _callback;

        public IDisposable Register(string eventName, Action callback)
        {
            _callback = callback;
            return this;
        }

        public void Invoke() => _callback?.Invoke();

        public void Dispose() => _callback = null;
    }

    private sealed class ConfirmationOwner
    {
        public Task<bool> ConfirmAsync(PowerDisplayWarningKind kind) => Task.FromResult(false);
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> _callbacks = new();

        public bool HasCallbacks => !_callbacks.IsEmpty;

        public override void Post(SendOrPostCallback callback, object state) => _callbacks.Enqueue((callback, state));

        public void Drain()
        {
            var previous = Current;
            try
            {
                SetSynchronizationContext(this);
                while (_callbacks.TryDequeue(out var item))
                {
                    item.Callback(item.State);
                }
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
