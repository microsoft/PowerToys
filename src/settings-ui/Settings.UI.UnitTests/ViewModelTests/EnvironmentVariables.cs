// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

extern alias SettingsApp;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GpoRuleConfigured = SettingsApp::PowerToys.GPOWrapper.GpoRuleConfigured;

namespace ViewModelTests
{
    [TestClass]
    public class EnvironmentVariables
    {
        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, true)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        public void LaunchAndActualCommandUseCurrentPreference(bool elevated, bool preference, bool expected)
        {
            using var fixture = new Fixture(elevated: elevated);
            fixture.Module.Properties.LaunchAdministrator = preference;
            fixture.ViewModel.Launch();
            CollectionAssert.AreEqual(new[] { expected }, fixture.LaunchRequests);
            fixture.LaunchRequests.Clear();
            fixture.Module.Properties.LaunchAdministrator = !preference;
            var command = fixture.ViewModel.LaunchEventHandler;
            var secondCommand = fixture.ViewModel.LaunchEventHandler;
            Assert.AreNotSame(command, secondCommand);
            fixture.Module.Properties.LaunchAdministrator = preference;
            Assert.IsTrue(command.CanExecute(null));
            command.Execute(null);
            CollectionAssert.AreEqual(new[] { expected }, fixture.LaunchRequests);
            secondCommand.Execute(null);
            CollectionAssert.AreEqual(new[] { expected, expected }, fixture.LaunchRequests);
            Assert.AreEqual(preference, fixture.Module.Properties.LaunchAdministrator);
            Assert.AreEqual(0, fixture.Notifications.Count);
            fixture.AssertNoSaveOrIpc();
            Assert.AreEqual(1, fixture.PolicyReads);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DisabledCommandCanExecuteWithoutEffectsAndStillRequestsLaunch(bool configured)
        {
            using var fixture = new Fixture(policy: configured ? GpoRuleConfigured.Disabled : GpoRuleConfigured.NotConfigured);
            Assert.IsFalse(fixture.ViewModel.IsEnabled);
            var command = fixture.ViewModel.LaunchEventHandler;
            fixture.Order.Clear();
            Assert.IsTrue(command.CanExecute(null));
            Assert.AreEqual(0, fixture.Order.Count);
            Assert.AreEqual(0, fixture.LaunchRequests.Count);
            fixture.AssertNoSaveOrIpc();
            Assert.AreEqual(1, fixture.PolicyReads);
            command.Execute(null);
            var expectedLaunchRequests = new[] { true };
            CollectionAssert.AreEqual(expectedLaunchRequests, fixture.LaunchRequests);
            var expectedOrder = new[] { "launch" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void LaunchShortCircuitsPreferenceOnlyWhenRunnerIsElevated(bool elevated)
        {
            using var fixture = new Fixture(elevated: elevated);
            fixture.Module.Properties = null!;
            if (elevated)
            {
                fixture.ViewModel.Launch();
                var expectedLaunchRequests = new[] { false };
                CollectionAssert.AreEqual(expectedLaunchRequests, fixture.LaunchRequests);
            }
            else
            {
                Assert.ThrowsException<NullReferenceException>(() => fixture.ViewModel.Launch());
                Assert.AreEqual(0, fixture.LaunchRequests.Count);
            }

            fixture.AssertNoSaveOrIpc();
            Assert.AreEqual(1, fixture.PolicyReads);
        }

        [TestMethod]
        public void LaunchRequestFailurePropagatesWithoutRetryOrPreferenceRollback()
        {
            using var fixture = new Fixture();
            var failure = new InvalidOperationException("launch boundary");
            fixture.OnLaunchRequest = _ => throw failure;
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.LaunchEventHandler.Execute(null)));
            var expectedLaunchRequests = new[] { true };
            CollectionAssert.AreEqual(expectedLaunchRequests, fixture.LaunchRequests);
            Assert.IsTrue(fixture.Module.Properties.LaunchAdministrator);
            fixture.AssertNoSaveOrIpc();
            Assert.AreEqual(1, fixture.PolicyReads);
        }

        [TestMethod]
        public void PreferenceNotifiesBeforeRealSerializationAndRecreatesLaunchIntent()
        {
            using var fixture = new Fixture();
            var originalGeneral = fixture.General.ToJsonString();
            fixture.OnNotification = name =>
            {
                Assert.AreEqual(nameof(EnvironmentVariablesViewModel.LaunchAdministrator), name);
                Assert.AreEqual(fixture.Notifications.Count == 2, fixture.ViewModel.LaunchAdministrator);
                Assert.AreEqual(fixture.Module.Properties.LaunchAdministrator, fixture.ViewModel.LaunchAdministrator);
                Assert.AreEqual(fixture.Notifications.Count - 1, fixture.Saves.Count);
            };
            fixture.Order.Clear();
            fixture.ViewModel.LaunchAdministrator = false;
            fixture.ViewModel.LaunchAdministrator = false;
            fixture.ViewModel.LaunchAdministrator = true;
            var expectedOrder = new[] { "notify:LaunchAdministrator", "save", "notify:LaunchAdministrator", "save" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(2, fixture.Saves.Count);
            AssertPreferenceJson(fixture.Saves[0], false);
            AssertPreferenceJson(fixture.Saves[1], true);
            foreach (var index in new[] { 0, 1 })
            {
                var restored = JsonSerializer.Deserialize(fixture.Saves[index], SettingsSerializationContext.Default.EnvironmentVariablesSettings);
                Assert.IsNotNull(restored);
                using var reopened = new Fixture(module: restored);
                reopened.ViewModel.LaunchEventHandler.Execute(null);
                CollectionAssert.AreEqual(new[] { index == 1 }, reopened.LaunchRequests);
                reopened.AssertNoSaveOrIpc();
            }

            AssertPreferenceJson(fixture.Saves[0], false);
            Assert.AreEqual(originalGeneral, fixture.General.ToJsonString());
            Assert.AreEqual(0, fixture.Messages.Count);
            Assert.AreEqual(1, fixture.PolicyReads);
            fixture.VerifySaveCount(2);
        }

        [TestMethod]
        public void PreferenceSaveSerializesModelAfterNotificationSubscriberMutation()
        {
            using var fixture = new Fixture();
            fixture.OnNotification = _ =>
            {
                Assert.IsFalse(fixture.ViewModel.LaunchAdministrator);
                Assert.AreEqual(0, fixture.Saves.Count);
                fixture.Module.Properties.LaunchAdministrator = true;
                fixture.OnNotification = null;
            };
            fixture.Order.Clear();
            fixture.ViewModel.LaunchAdministrator = false;
            Assert.IsTrue(fixture.ViewModel.LaunchAdministrator);
            Assert.AreEqual(1, fixture.Saves.Count);
            AssertPreferenceJson(fixture.Saves[0], true);
            var expectedOrder = new[] { "notify:LaunchAdministrator", "save" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            fixture.VerifySaveCount(1);
            Assert.AreEqual(0, fixture.Messages.Count);
        }

        [TestMethod]
        public void PreferenceSubscriberFailureRetainsChangedStateAndSameValueDoesNotRetry()
        {
            using var fixture = new Fixture();
            var failure = new InvalidOperationException("subscriber boundary");
            fixture.OnNotification = _ => throw failure;
            fixture.Order.Clear();
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.LaunchAdministrator = false));
            Assert.IsFalse(fixture.Module.Properties.LaunchAdministrator);
            fixture.ViewModel.LaunchAdministrator = false;
            var expectedOrder = new[] { "notify:LaunchAdministrator" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        public void UnregisteredSettingsUsesActualSerializerFailureAfterNotification()
        {
            using var fixture = new Fixture(module: new UnregisteredSettings());
            fixture.Order.Clear();
            var failure = Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.LaunchAdministrator = false);
            StringAssert.Contains(failure.Message, nameof(UnregisteredSettings));
            StringAssert.Contains(failure.Message, "not registered");
            Assert.IsFalse(fixture.Module.Properties.LaunchAdministrator);
            fixture.ViewModel.LaunchAdministrator = false;
            var expectedOrder = new[] { "notify:LaunchAdministrator" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        public void PreferenceVirtualSaveFailurePropagatesAfterSerializationWithoutRetry()
        {
            using var fixture = new Fixture();
            var failure = new InvalidOperationException("virtual save boundary");
            fixture.OnSave = _ => throw failure;
            fixture.Order.Clear();
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.LaunchAdministrator = false));
            Assert.IsFalse(fixture.Module.Properties.LaunchAdministrator);
            fixture.ViewModel.LaunchAdministrator = false;
            var expectedOrder = new[] { "notify:LaunchAdministrator", "save" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(1, fixture.Saves.Count);
            AssertPreferenceJson(fixture.Saves[0], false);
            fixture.VerifySaveCount(1);
            Assert.AreEqual(0, fixture.Messages.Count);
        }

        [TestMethod]
        [DataRow(GpoRuleConfigured.Enabled, false, true)]
        [DataRow(GpoRuleConfigured.Disabled, true, false)]
        public void ConfiguredPolicyOverridesGeneralAndIgnoresEnabledAssignments(GpoRuleConfigured policy, bool generalEnabled, bool expected)
        {
            using var fixture = new Fixture(policy: policy, enabled: generalEnabled);
            var expectedOrder = new[] { "general", "module", "policy" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(expected, fixture.ViewModel.IsEnabled);
            Assert.IsTrue(fixture.ViewModel.IsEnabledGpoConfigured);
            fixture.ViewModel.IsEnabled = !expected;
            Assert.AreEqual(expected, fixture.ViewModel.IsEnabled);
            Assert.AreEqual(generalEnabled, fixture.General.Enabled.EnvironmentVariables);
            Assert.AreEqual(0, fixture.Notifications.Count);
            fixture.AssertNoSaveOrIpc();
            Assert.AreEqual(1, fixture.PolicyReads);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void UnconfiguredRefreshRereadsGeneralAndOnlyNotifiesEnabled(bool elevated)
        {
            using var fixture = new Fixture(elevated: elevated);
            fixture.OnNotification = name =>
            {
                Assert.AreEqual(nameof(EnvironmentVariablesViewModel.IsEnabled), name);
                Assert.AreEqual(fixture.General.Enabled.EnvironmentVariables, fixture.ViewModel.IsEnabled);
            };
            fixture.Order.Clear();
            fixture.General.Enabled.EnvironmentVariables = true;
            fixture.ViewModel.RefreshEnabledState();
            Assert.AreEqual(!elevated, fixture.ViewModel.LaunchAdministratorEnabled);
            fixture.General.Enabled.EnvironmentVariables = false;
            fixture.ViewModel.RefreshEnabledState();
            Assert.IsFalse(fixture.ViewModel.LaunchAdministratorEnabled);
            fixture.ViewModel.RefreshEnabledState();
            var expectedOrder = new[] { "policy", "notify:IsEnabled", "policy", "notify:IsEnabled", "policy", "notify:IsEnabled" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(4, fixture.PolicyReads);
            Assert.IsFalse(fixture.ViewModel.IsEnabledGpoConfigured);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        [DataRow(GpoRuleConfigured.Enabled, false)]
        [DataRow(GpoRuleConfigured.Disabled, true)]
        public void ReturningToUnconfiguredPolicyPreservesStickyConfiguredGuard(GpoRuleConfigured initialPolicy, bool generalEnabled)
        {
            using var fixture = new Fixture(policy: initialPolicy, enabled: generalEnabled);
            fixture.Policy = GpoRuleConfigured.NotConfigured;
            fixture.Order.Clear();
            fixture.ViewModel.RefreshEnabledState();
            Assert.AreEqual(generalEnabled, fixture.ViewModel.IsEnabled);
            Assert.IsTrue(fixture.ViewModel.IsEnabledGpoConfigured);
            Assert.AreEqual(generalEnabled, fixture.ViewModel.LaunchAdministratorEnabled);
            fixture.ViewModel.IsEnabled = !generalEnabled;
            Assert.AreEqual(generalEnabled, fixture.ViewModel.IsEnabled);
            Assert.AreEqual(generalEnabled, fixture.General.Enabled.EnvironmentVariables);
            var expectedOrder = new[] { "policy", "notify:IsEnabled" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(2, fixture.PolicyReads);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        public void RefreshRequeriesConfiguredPolicyWithoutUpdatingGeneral()
        {
            using var fixture = new Fixture(policy: GpoRuleConfigured.Enabled, enabled: true);
            fixture.Policy = GpoRuleConfigured.Disabled;
            fixture.Order.Clear();
            fixture.ViewModel.RefreshEnabledState();
            Assert.IsFalse(fixture.ViewModel.IsEnabled);
            Assert.IsFalse(fixture.ViewModel.LaunchAdministratorEnabled);
            Assert.IsTrue(fixture.General.Enabled.EnvironmentVariables);
            Assert.IsTrue(fixture.ViewModel.IsEnabledGpoConfigured);
            var expectedOrder = new[] { "policy", "notify:IsEnabled" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(2, fixture.PolicyReads);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        [DataRow(GpoRuleConfigured.Enabled, false)]
        [DataRow(GpoRuleConfigured.NotConfigured, true)]
        public void RefreshPolicyFailurePreservesStateAndStopsBeforeNotification(GpoRuleConfigured initialPolicy, bool generalEnabled)
        {
            using var fixture = new Fixture(policy: initialPolicy, enabled: generalEnabled);
            var enabled = fixture.ViewModel.IsEnabled;
            var configured = fixture.ViewModel.IsEnabledGpoConfigured;
            var failure = new InvalidOperationException("policy boundary");
            fixture.ReadPolicy = () => throw failure;
            fixture.General.Enabled.EnvironmentVariables = !generalEnabled;
            fixture.Order.Clear();
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.RefreshEnabledState()));
            Assert.AreEqual(enabled, fixture.ViewModel.IsEnabled);
            Assert.AreEqual(configured, fixture.ViewModel.IsEnabledGpoConfigured);
            var expectedOrder = new[] { "policy" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(0, fixture.Notifications.Count);
            Assert.AreEqual(2, fixture.PolicyReads);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        [DataRow("general")]
        [DataRow("module")]
        [DataRow("policy")]
        public void ConstructorFailureStopsAtFailingRepositoryOrPolicyBoundary(string boundary)
        {
            using var fixture = new Fixture(construct: false);
            var failure = new InvalidOperationException(boundary);
            if (boundary == "general")
            {
                fixture.GeneralRepository.SetupGet(repository => repository.SettingsConfig)
                    .Returns(() =>
                    {
                        fixture.Order.Add("general");
                        throw failure;
                    });
            }
            else if (boundary == "module")
            {
                fixture.ModuleRepository.SetupGet(repository => repository.SettingsConfig)
                    .Returns(() =>
                    {
                        fixture.Order.Add("module");
                        throw failure;
                    });
            }
            else
            {
                fixture.ReadPolicy = () => throw failure;
            }

            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.CreateViewModel()));
            var expected = boundary == "general" ? new[] { "general" }
                : boundary == "module" ? new[] { "general", "module" }
                : new[] { "general", "module", "policy" };
            CollectionAssert.AreEqual(expected, fixture.Order);
            Assert.AreEqual(boundary == "policy" ? 1 : 0, fixture.PolicyReads);
            Assert.AreEqual(0, fixture.Notifications.Count);
            Assert.AreEqual(0, fixture.LaunchRequests.Count);
            fixture.AssertNoSaveOrIpc();
        }

        [TestMethod]
        [DataRow(false, 0)]
        [DataRow(true, 73)]
        public void EnabledChangesSendActualGeneralJsonBeforeNotificationAndIgnoreReturn(bool initial, int callbackResult)
        {
            using var fixture = new Fixture(enabled: initial);
            fixture.General.Enabled.FancyZones = false;
            fixture.OnIpc = json =>
            {
                Assert.AreEqual(!initial, fixture.ViewModel.IsEnabled);
                Assert.AreEqual(!initial, fixture.General.Enabled.EnvironmentVariables);
                Assert.AreEqual(0, fixture.Notifications.Count);
                AssertEnabledJson(json, !initial);
                return callbackResult;
            };
            fixture.OnNotification = name =>
            {
                Assert.AreEqual(nameof(EnvironmentVariablesViewModel.IsEnabled), name);
                Assert.AreEqual(1, fixture.Messages.Count);
            };
            fixture.Order.Clear();
            fixture.ViewModel.IsEnabled = !initial;
            fixture.ViewModel.IsEnabled = !initial;
            var expectedOrder = new[] { "ipc", "notify:IsEnabled" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(1, fixture.Messages.Count);
            AssertEnabledJson(fixture.Messages[0], !initial);
            Assert.AreEqual(1, fixture.PolicyReads);
            Assert.AreEqual(0, fixture.Saves.Count);
            Assert.AreEqual(0, fixture.LaunchRequests.Count);
            Assert.IsTrue(fixture.Module.Properties.LaunchAdministrator);
            fixture.VerifySaveCount(0);
        }

        [TestMethod]
        public void EnabledIpcFailureRetainsStateAndDoesNotNotifyRollbackOrRetry()
        {
            using var fixture = new Fixture();
            fixture.General.Enabled.FancyZones = false;
            var failure = new InvalidOperationException("ipc boundary");
            fixture.OnIpc = _ => throw failure;
            fixture.Order.Clear();
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.IsEnabled = true));
            Assert.IsTrue(fixture.ViewModel.IsEnabled);
            Assert.IsTrue(fixture.General.Enabled.EnvironmentVariables);
            fixture.ViewModel.IsEnabled = true;
            var expectedOrder = new[] { "ipc" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(1, fixture.Messages.Count);
            AssertEnabledJson(fixture.Messages[0], true);
            Assert.AreEqual(0, fixture.Notifications.Count);
            Assert.AreEqual(1, fixture.PolicyReads);
            fixture.VerifySaveCount(0);
        }

        [TestMethod]
        public void EnabledSubscriberFailureOccursAfterIpcAndDoesNotRetry()
        {
            using var fixture = new Fixture();
            var failure = new InvalidOperationException("enabled subscriber");
            fixture.OnNotification = _ => throw failure;
            fixture.Order.Clear();
            Assert.AreSame(failure, Assert.ThrowsException<InvalidOperationException>(() => fixture.ViewModel.IsEnabled = true));
            fixture.ViewModel.IsEnabled = true;
            Assert.IsTrue(fixture.ViewModel.IsEnabled);
            Assert.IsTrue(fixture.General.Enabled.EnvironmentVariables);
            var expectedOrder = new[] { "ipc", "notify:IsEnabled" };
            CollectionAssert.AreEqual(expectedOrder, fixture.Order);
            Assert.AreEqual(1, fixture.Messages.Count);
            Assert.AreEqual(1, fixture.PolicyReads);
            fixture.VerifySaveCount(0);
        }

        private static void AssertPreferenceJson(string json, bool expected)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.AreEqual("EnvironmentVariables", root.GetProperty("name").GetString());
            Assert.AreEqual("1.0", root.GetProperty("version").GetString());
            Assert.AreEqual(expected, root.GetProperty("properties").GetProperty("LaunchAdministrator").GetProperty("value").GetBoolean());
        }

        private static void AssertEnabledJson(string json, bool expected)
        {
            using var document = JsonDocument.Parse(json);
            var enabled = document.RootElement.GetProperty("general").GetProperty("enabled");
            Assert.AreEqual(expected, enabled.GetProperty("EnvironmentVariables").GetBoolean());
            Assert.IsFalse(enabled.GetProperty("FancyZones").GetBoolean());
        }

        private sealed class UnregisteredSettings : EnvironmentVariablesSettings
        {
        }

        private sealed class Fixture : IDisposable
        {
            private readonly bool _elevated;
            private PropertyChangedEventHandler? _notificationHandler;
            private EnvironmentVariablesViewModel? _viewModel;

            internal Fixture(
                bool elevated = false,
                bool enabled = false,
                GpoRuleConfigured policy = GpoRuleConfigured.NotConfigured,
                EnvironmentVariablesSettings? module = null,
                bool construct = true)
            {
                _elevated = elevated;
                General = new GeneralSettings();
                General.Enabled.EnvironmentVariables = enabled;
                Module = module ?? new EnvironmentVariablesSettings();
                Policy = policy;
                GeneralRepository.SetupGet(repository => repository.SettingsConfig)
                    .Returns(() =>
                    {
                        Order.Add("general");
                        return General;
                    });
                ModuleRepository.SetupGet(repository => repository.SettingsConfig)
                    .Returns(() =>
                    {
                        Order.Add("module");
                        return Module;
                    });
                SettingsUtils.Setup(utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string>((json, moduleName, fileName) =>
                    {
                        Assert.AreEqual(EnvironmentVariablesSettings.ModuleName, moduleName);
                        Assert.AreEqual(Microsoft.PowerToys.Settings.UI.Library.SettingsUtils.DefaultFileName, fileName);
                        Order.Add("save");
                        Saves.Add(json);
                        OnSave?.Invoke(json);
                    });
                if (construct)
                {
                    CreateViewModel();
                }
            }

            internal GeneralSettings General { get; }

            internal EnvironmentVariablesSettings Module { get; }

            internal Mock<ISettingsRepository<GeneralSettings>> GeneralRepository { get; } = new Mock<ISettingsRepository<GeneralSettings>>(MockBehavior.Strict);

            internal Mock<ISettingsRepository<EnvironmentVariablesSettings>> ModuleRepository { get; } = new Mock<ISettingsRepository<EnvironmentVariablesSettings>>(MockBehavior.Strict);

            internal Mock<SettingsUtils> SettingsUtils { get; } = new Mock<SettingsUtils>(MockBehavior.Strict, new MockFileSystem(), null) { CallBase = false };

            internal EnvironmentVariablesViewModel ViewModel => _viewModel ?? throw new InvalidOperationException("View model has not been constructed.");

            internal List<string> Order { get; } = new List<string>();

            internal List<string> Saves { get; } = new List<string>();

            internal List<string> Messages { get; } = new List<string>();

            internal List<string> Notifications { get; } = new List<string>();

            internal List<bool> LaunchRequests { get; } = new List<bool>();

            internal int PolicyReads { get; private set; }

            internal GpoRuleConfigured Policy { get; set; }

            internal Func<GpoRuleConfigured>? ReadPolicy { get; set; }

            internal Action<string>? OnNotification { get; set; }

            internal Action<string>? OnSave { get; set; }

            internal Func<string, int>? OnIpc { get; set; }

            internal Action<bool>? OnLaunchRequest { get; set; }

            internal void CreateViewModel()
            {
                _viewModel = new EnvironmentVariablesViewModel(
                    SettingsUtils.Object,
                    GeneralRepository.Object,
                    ModuleRepository.Object,
                    json =>
                    {
                        Order.Add("ipc");
                        Messages.Add(json);
                        return OnIpc?.Invoke(json) ?? 0;
                    },
                    _elevated,
                    () =>
                    {
                        Order.Add("policy");
                        PolicyReads++;
                        return ReadPolicy?.Invoke() ?? Policy;
                    },
                    administrator =>
                    {
                        Order.Add("launch");
                        LaunchRequests.Add(administrator);
                        OnLaunchRequest?.Invoke(administrator);
                    });
                _notificationHandler = (_, args) =>
                {
                    var name = args.PropertyName ?? throw new InvalidOperationException("Expected a named property notification.");
                    Order.Add("notify:" + name);
                    Notifications.Add(name);
                    OnNotification?.Invoke(name);
                };
                _viewModel.PropertyChanged += _notificationHandler;
            }

            internal void AssertNoSaveOrIpc()
            {
                Assert.AreEqual(0, Saves.Count);
                Assert.AreEqual(0, Messages.Count);
                VerifySaveCount(0);
            }

            internal void VerifySaveCount(int count)
            {
                SettingsUtils.Verify(
                    utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(count));
            }

            public void Dispose()
            {
                if (_viewModel != null && _notificationHandler != null)
                {
                    _viewModel.PropertyChanged -= _notificationHandler;
                }
            }
        }
    }
}
