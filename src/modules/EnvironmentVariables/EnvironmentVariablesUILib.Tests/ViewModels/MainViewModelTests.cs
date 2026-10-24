// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using EnvironmentVariablesUILib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.ViewModels
{
    [TestClass]
    [DoNotParallelize]
    public class MainViewModelTests
    {
        private static readonly bool[] EnabledTelemetry = { true };
        private static readonly bool[] DisabledTelemetry = { false };
        private static readonly string[] ExpansionInputs = { "%ROOT%", "user", "system" };
        private static readonly string[] ComposedPathExpansionInputs = { "%DRAFT%", "%USER%", "%BACKUP%", "%SYSTEM%" };
        private static readonly string[] AddedDefaultNames = { "Added", "Zed" };
        private static readonly string[] EditedListEntries = { "%ROOT%", "tail" };
        private static readonly string[] ActiveProfileDeletes = { "TMP", "TMP_PowerToys_Active", "PATH", "PATH_PowerToys_Active" };
        private static readonly string[] AuthoredPathEntries = { "path1", "path2", "path3" };
        private static readonly (string Name, string Values, VariablesSetType ParentType)[] RetainedOldProjection =
        {
            ("TMP", "override", VariablesSetType.Duplicate),
            ("TMP_PowerToys_Old", "original", VariablesSetType.User),
        };

        [DataTestMethod]
        [DataRow(VariablesSetType.User, false, false, true)]
        [DataRow(VariablesSetType.User, false, true, false)]
        [DataRow(VariablesSetType.User, true, false, true)]
        [DataRow(VariablesSetType.User, true, true, false)]
        [DataRow(VariablesSetType.System, false, false, false)]
        [DataRow(VariablesSetType.System, false, true, false)]
        [DataRow(VariablesSetType.System, true, false, true)]
        [DataRow(VariablesSetType.System, true, true, false)]
        [DataRow(VariablesSetType.Profile, false, false, true)]
        [DataRow(VariablesSetType.Profile, false, true, false)]
        [DataRow(VariablesSetType.Profile, true, false, true)]
        [DataRow(VariablesSetType.Profile, true, true, false)]
        public void Editability_UsesCurrentConstructedElevationAndProfileOwnership(VariablesSetType type, bool elevated, bool owned, bool expected)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var vm = fixture.CreateViewModel(elevated);
            var variable = new Variable("TMP", "value", type);
            variable.IsAppliedFromProfile = owned;

            Assert.AreEqual(expected, variable.IsEditable);
            Assert.AreEqual(elevated, vm.IsElevated);
            Assert.AreEqual(0, fixture.Environment.Opens.Count);
        }

        [DataTestMethod]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void ElevationSnapshot_ExistingVmKeepsCaptureWhileVariableReadsLatestHolder(bool firstElevation, bool secondElevation)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var first = fixture.CreateViewModel(firstElevation);
            var systemVariable = new Variable("TMP", "value", VariablesSetType.System);
            Assert.AreEqual(firstElevation, systemVariable.IsEditable);

            var second = fixture.CreateViewModel(secondElevation);

            Assert.AreEqual(firstElevation, first.IsElevated);
            Assert.AreEqual(secondElevation, second.IsElevated);
            Assert.AreEqual(secondElevation, systemVariable.IsEditable);
            Assert.AreEqual(0, fixture.Environment.Opens.Count);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
        }

        [DataTestMethod]
        [DataRow(EnvironmentState.Unchanged, false)]
        [DataRow(EnvironmentState.ChangedOnStartup, false)]
        [DataRow(EnvironmentState.EnvironmentMessageReceived, true)]
        [DataRow(EnvironmentState.ProfileNotApplicable, false)]
        [DataRow(EnvironmentState.ProfileNameInvalid, false)]
        [DataRow((EnvironmentState)999, false)]
        public void InfoBarButton_OnlyEnvironmentMessageReceivedIsVisible(EnvironmentState state, bool expected)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var vm = fixture.CreateViewModel();
            vm.EnvironmentState = state;

            Assert.AreEqual(expected, vm.IsInfoBarButtonVisible);
        }

        [DataTestMethod]
        [DataRow("list", false)]
        [DataRow("member", false)]
        [DataRow("variables", true)]
        [DataRow("variable-member", true)]
        public async Task ReloadNullShape_LogsRetainsOldStateAndPreservesOnlyReachedSubscription(string shape, bool subscribed)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = LoadActive(fixture, out var vm);
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var partial = Profile("Partial", true, "NEW", "new");
            if (shape == "variables")
            {
                partial.Variables = null!;
            }
            else if (shape == "variable-member")
            {
                partial.Variables.Add(null!);
            }

            fixture.Service.Read = () => shape switch
            {
                "list" => null!,
                "member" => new() { null! },
                _ => new() { partial },
            };

            vm.LoadEnvironmentVariables();

            Assert.AreSame(old, vm.AppliedProfile);
            Assert.IsTrue(old.IsEnabled);
            Assert.IsTrue(partial.IsEnabled);
            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.AreEqual(0, vm.Profiles.Count);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            var error = fixture.Logger.Entries.Single();
            Assert.AreEqual("Failed to load profiles.json file", error.Message);
            Assert.IsInstanceOfType<NullReferenceException>(error.Exception);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            Assert.AreEqual("original", fixture.Environment.User["TMP_PowerToys_Old"]);

            partial.IsEnabled = false;

            Assert.AreEqual(subscribed ? 1 : 0, fixture.Work.ActionCount);
            Assert.AreEqual(subscribed ? 1 : 0, fixture.Work.SaveCount);
            if (subscribed)
            {
                Assert.IsNull(vm.AppliedProfile);
                Assert.IsFalse(old.IsEnabled);
                CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
                await fixture.Work.CompleteActionAsync(0);
                Assert.AreEqual("original", fixture.Environment.User["TMP"]);
                Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Old"));
                fixture.Work.DrainRefreshes();
                Assert.AreEqual("original", vm.AppliedVariables.Single(x => x.Name == "TMP").Values);
                await fixture.Work.CompleteSaveAsync(0);
                Assert.AreEqual(0, fixture.Service.Writes.Single().Length);
            }
            else
            {
                Assert.AreSame(old, vm.AppliedProfile);
                Assert.IsTrue(old.IsEnabled);
                Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            }

            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.AreEqual(1, fixture.Logger.Entries.Count);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DefaultReadFailure_PropagatesOutsideLoadCatchAndRetainsAlreadySubmittedRejectionWork(bool failFinalRead)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = LoadActive(fixture, out var vm);
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var previousProfiles = vm.Profiles;
            var previousProjection = vm.AppliedVariables;
            var rejected = Profile(string.Empty, true, "NEW", "new");
            var serviceReads = 0;
            fixture.Service.Read = () =>
            {
                serviceReads++;
                return new() { rejected };
            };
            var machineOpens = 0;
            var failure = new InvalidOperationException("default read failed");
            fixture.Environment.BeforeOperation = (operation, hive, name) =>
            {
                if (operation == "Open" && hive == RegistryHive.LocalMachine)
                {
                    machineOpens++;
                    if (machineOpens == (failFinalRead ? 2 : 1))
                    {
                        throw failure;
                    }
                }
            };

            var observed = Assert.ThrowsException<InvalidOperationException>(() => vm.LoadEnvironmentVariables());
            fixture.Environment.BeforeOperation = null;

            Assert.AreSame(failure, observed);
            Assert.AreEqual(failFinalRead ? 2 : 1, machineOpens);
            Assert.AreEqual(failFinalRead ? 1 : 0, serviceReads);
            Assert.AreEqual(0, fixture.Logger.Entries.Count);
            Assert.AreSame(previousProjection, vm.AppliedVariables);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            Assert.AreEqual(failFinalRead ? 1 : 0, fixture.Work.ActionCount);
            Assert.AreEqual(failFinalRead ? 1 : 0, fixture.Work.SaveCount);
            if (failFinalRead)
            {
                Assert.IsNull(vm.AppliedProfile);
                Assert.IsFalse(old.IsEnabled);
                Assert.IsFalse(rejected.IsEnabled);
                Assert.AreSame(rejected, vm.Profiles.Single());
                Assert.AreEqual(EnvironmentState.ProfileNameInvalid, vm.EnvironmentState);
                CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
                await fixture.Work.CompleteActionAsync(0);
                Assert.AreEqual("original", fixture.Environment.User["TMP"]);
                Assert.AreSame(previousProjection, vm.AppliedVariables);
                fixture.Work.DrainRefreshes();
                Assert.AreEqual("original", vm.AppliedVariables.Single(x => x.Name == "TMP").Values);
                Assert.AreEqual(EnvironmentState.ProfileNameInvalid, vm.EnvironmentState);
                Assert.AreEqual(1, serviceReads);
                await fixture.Work.CompleteSaveAsync(0);
                Assert.AreSame(rejected, fixture.Service.Writes.Single().Single());
            }
            else
            {
                Assert.AreSame(old, vm.AppliedProfile);
                Assert.IsTrue(old.IsEnabled);
                Assert.IsTrue(rejected.IsEnabled);
                Assert.AreSame(previousProfiles, vm.Profiles);
                Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
                Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            }
        }

        [DataTestMethod]
        [DataRow(VariablesSetType.User, false)]
        [DataRow(VariablesSetType.User, true)]
        [DataRow(VariablesSetType.System, false)]
        [DataRow(VariablesSetType.System, true)]
        public void AddDefault_WritesRequestedScopeBeforeAddingAndRefreshesOnlyOnSuccess(VariablesSetType type, bool failWrite)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var target = type == VariablesSetType.User ? fixture.Environment.User : fixture.Environment.Machine;
            var other = type == VariablesSetType.User ? fixture.Environment.Machine : fixture.Environment.User;
            target["Zed"] = "existing";
            other["Other"] = "untouched";
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            fixture.Environment.Expand = value => value == "%ROOT%" ? @"C:\Expanded" : value;
            var set = type == VariablesSetType.User ? vm.UserDefaultSet : vm.SystemDefaultSet;
            var previousProjection = vm.AppliedVariables;
            var draft = new Variable("Added", "%ROOT%", type);
            var writeSawUnchangedModel = false;
            fixture.Environment.BeforeOperation = (operation, hive, name) =>
            {
                if (operation == "Write" && name == "Added")
                {
                    writeSawUnchangedModel = set.Variables.Count == 1
                        && set.Variables[0].Name == "Zed"
                        && ReferenceEquals(previousProjection, vm.AppliedVariables);
                    if (failWrite)
                    {
                        throw new InvalidOperationException("write failed");
                    }
                }
            };

            vm.AddDefaultVariable(draft, type);

            Assert.IsTrue(writeSawUnchangedModel);
            var write = fixture.Environment.Operations.Single(x => x.Operation == "Write");
            Assert.AreEqual(type == VariablesSetType.User ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, write.Hive);
            Assert.AreEqual(((object)"%ROOT%", RegistryValueKind.ExpandString), write.Value);
            Assert.AreEqual(!failWrite, target.ContainsKey("Added"));
            Assert.AreEqual("untouched", other["Other"]);
            Assert.IsFalse(other.ContainsKey("Added"));
            Assert.AreEqual(failWrite ? 0 : 1, fixture.Environment.Notifications.Count);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            if (failWrite)
            {
                Assert.AreSame(previousProjection, vm.AppliedVariables);
                Assert.AreEqual(1, set.Variables.Count);
                Assert.IsFalse(vm.AppliedVariables.Any(x => x.Name == "Added"));
                Assert.AreEqual(1, fixture.Logger.Entries.Count);
            }
            else
            {
                Assert.AreNotSame(previousProjection, vm.AppliedVariables);
                CollectionAssert.AreEqual(AddedDefaultNames, set.Variables.Select(x => x.Name).ToArray());
                Assert.AreEqual("%ROOT%", set.Variables[0].Values);
                Assert.AreNotSame(draft, set.Variables[0]);
                Assert.AreEqual(@"C:\Expanded", vm.AppliedVariables.Single(x => x.Name == "Added").Values);
                Assert.AreEqual(0, fixture.Logger.Entries.Count);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void EditUnchanged_DefaultAndInactiveProfileSubmitNoWorkTelemetryOrSave(bool inProfile)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var profile = Profile("Inactive", false, "TMP", "old");
            fixture.Environment.User["TMP"] = "old";
            fixture.Service.Read = () => new() { profile };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var original = inProfile ? profile.Variables[0] : vm.UserDefaultSet.Variables.Single();
            var originalList = original.ValuesList;
            var previousProjection = vm.AppliedVariables;
            fixture.Environment.Operations.Clear();

            vm.EditVariable(original, original.Clone(), inProfile ? profile : null!);

            Assert.AreSame(originalList, original.ValuesList);
            Assert.AreSame(previousProjection, vm.AppliedVariables);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
        }

        [DataTestMethod]
        [DataRow(VariablesSetType.User, "NEW")]
        [DataRow(VariablesSetType.User, "tmp")]
        [DataRow(VariablesSetType.System, "NEW")]
        [DataRow(VariablesSetType.System, "tmp")]
        public async Task EditDefault_RenameMutatesModelBeforeWorkAndRefreshesExpandedReadback(VariablesSetType type, string newName)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var target = type == VariablesSetType.User ? fixture.Environment.User : fixture.Environment.Machine;
            var other = type == VariablesSetType.User ? fixture.Environment.Machine : fixture.Environment.User;
            target["TMP"] = "old";
            other["OTHER"] = "untouched";
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            fixture.Environment.Expand = value => value == "%ROOT%;tail" ? @"C:\Expanded;tail" : value;
            var set = type == VariablesSetType.User ? vm.UserDefaultSet : vm.SystemDefaultSet;
            var original = set.Variables.Single();
            var previousProjection = vm.AppliedVariables;
            var edited = new Variable(newName, "%ROOT%;tail", type);
            fixture.Environment.Operations.Clear();

            vm.EditVariable(original, edited, null!);

            Assert.AreEqual((newName, "%ROOT%;tail"), (original.Name, original.Values));
            CollectionAssert.AreEqual(EditedListEntries, original.ValuesList.Select(x => x.Text).ToArray());
            Assert.AreEqual("old", target["TMP"]);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreSame(previousProjection, vm.AppliedVariables);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            CollectionAssert.AreEqual(new[] { type }, fixture.Telemetry.Changed.ToArray());

            await fixture.Work.CompleteActionAsync(0);

            CollectionAssert.AreEqual(
                new[] { ("Delete", "TMP"), ("Write", newName) },
                fixture.Environment.Operations.Where(x => x.Operation is "Delete" or "Write").Select(x => (x.Operation, x.Name)).ToArray());
            Assert.IsTrue(fixture.Environment.Operations.Where(x => x.Operation is "Delete" or "Write").All(x => x.Hive == (type == VariablesSetType.User ? RegistryHive.CurrentUser : RegistryHive.LocalMachine)));
            Assert.AreEqual(newName, target.Keys.Single());
            Assert.AreEqual("%ROOT%;tail", target[newName]);
            Assert.AreEqual("untouched", other["OTHER"]);
            Assert.AreEqual(2, fixture.Environment.Notifications.Count);
            Assert.AreSame(previousProjection, vm.AppliedVariables);
            Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
            fixture.Work.DrainRefreshes();
            Assert.AreNotSame(previousProjection, vm.AppliedVariables);
            Assert.AreEqual(@"C:\Expanded;tail", vm.AppliedVariables.Single(x => x.Name == newName).Values);
            Assert.AreEqual("%ROOT%;tail", set.Variables.Single().Values);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual(0, fixture.Service.Writes.Single().Length);
        }

        [TestMethod]
        public async Task EditInactiveProfile_ChangesOwnedItemButDoesNotPropagateEnvironment()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var profile = Profile("Inactive", false, "TMP", "old");
            fixture.Environment.User["TMP"] = "user";
            fixture.Service.Read = () => new() { profile };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var original = profile.Variables[0];
            fixture.Environment.Operations.Clear();

            vm.EditVariable(original, new Variable("NEW", "new;tail", VariablesSetType.Profile), profile);

            Assert.AreSame(original, profile.Variables.Single());
            Assert.AreEqual(("NEW", "new;tail"), (original.Name, original.Values));
            Assert.AreEqual("user", fixture.Environment.User["TMP"]);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            CollectionAssert.AreEqual(new[] { VariablesSetType.Profile }, fixture.Telemetry.Changed.ToArray());
            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            fixture.Work.DrainRefreshes();
            Assert.AreEqual(("TMP", "user"), (vm.AppliedVariables.Single().Name, vm.AppliedVariables.Single().Values));
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreSame(profile, fixture.Service.Writes.Single().Single());
        }

        [TestMethod]
        public async Task AddDisabledEmptyProfile_PersistsThenRealEnableCallbackAppliesSubscribedObject()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var profile = new ProfileVariablesSet(Guid.NewGuid(), "Empty");
            fixture.Environment.Operations.Clear();

            vm.AddProfile(profile);

            Assert.AreSame(profile, vm.Profiles.Single());
            Assert.IsTrue(profile.Valid);
            Assert.IsNull(vm.AppliedProfile);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreSame(profile, fixture.Service.Writes.Single().Single());

            profile.IsEnabled = true;

            Assert.AreSame(profile, vm.AppliedProfile);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(2, fixture.Work.SaveCount);
            CollectionAssert.AreEqual(EnabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.IsFalse(fixture.Environment.Operations.Any(x => x.Operation is "Write" or "Delete"));
            fixture.Work.DrainRefreshes();
        }

        [DataTestMethod]
        [DataRow("valid", EnvironmentState.EnvironmentMessageReceived)]
        [DataRow("name", EnvironmentState.ProfileNameInvalid)]
        [DataRow("variable", EnvironmentState.ProfileNotApplicable)]
        public async Task AddEnabledProfile_UnsetsPreviousBeforeValidatingAndKeepsRejectedMember(string validity, EnvironmentState expected)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = LoadActive(fixture, out var vm);
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var added = Profile(validity == "name" ? string.Empty : "Added", true, "NEW", validity == "variable" ? "bad\0" : "new");

            vm.AddProfile(added);

            CollectionAssert.AreEqual(new[] { old, added }, vm.Profiles.ToArray());
            Assert.IsFalse(old.IsEnabled);
            Assert.AreEqual(validity == "valid", added.IsEnabled);
            Assert.AreSame(validity == "valid" ? added : null, vm.AppliedProfile);
            Assert.AreEqual(expected, vm.EnvironmentState);
            Assert.AreEqual(validity == "valid" ? 2 : 1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual("original", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Old"));
            if (validity == "valid")
            {
                await fixture.Work.CompleteActionAsync(1);
                Assert.AreEqual("new", fixture.Environment.User["NEW"]);
            }
            else
            {
                Assert.IsFalse(fixture.Environment.User.ContainsKey("NEW"));
            }

            fixture.Work.DrainRefreshes();
            await fixture.Work.CompleteSaveAsync(0);
            CollectionAssert.AreEqual(new[] { old, added }, fixture.Service.Writes.Single());
        }

        [DataTestMethod]
        [DataRow("active")]
        [DataRow("disabled")]
        [DataRow("enabled-noncurrent")]
        public async Task RemoveProfile_ChosenMembershipAndEnabledFlagDetermineCurrentUnapply(string selection)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var active = Profile("Active", true, "TMP", "override");
            active.Variables.Add(new Variable("PATH", "profile-path", VariablesSetType.Profile));
            var other = Profile("Other", selection == "enabled-noncurrent", "OTHER", "other-value");
            fixture.Environment.User["TMP"] = "override";
            fixture.Environment.User["TMP_PowerToys_Active"] = "original";
            fixture.Environment.User["PATH"] = "profile-path";
            fixture.Environment.User["PATH_PowerToys_Active"] = "user-path";
            fixture.Environment.User["OTHER"] = "other-value";
            fixture.Environment.Machine["Path"] = "system-path";
            fixture.Service.Read = () => new() { active, other };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            Assert.AreSame(active, vm.AppliedProfile);
            var selected = selection == "active" ? active : other;
            var removesCurrentState = selection != "disabled";
            fixture.Environment.Operations.Clear();

            vm.RemoveProfile(selected);

            Assert.AreSame(selection == "active" ? other : active, vm.Profiles.Single());
            Assert.AreSame(removesCurrentState ? null : active, vm.AppliedProfile);
            Assert.AreEqual(!removesCurrentState, active.IsEnabled);
            Assert.AreEqual(selection == "enabled-noncurrent", other.IsEnabled);
            Assert.AreEqual(removesCurrentState ? 1 : 0, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            Assert.AreEqual("profile-path", fixture.Environment.User["PATH"]);
            if (removesCurrentState)
            {
                await fixture.Work.CompleteActionAsync(0);
                Assert.AreEqual("original", fixture.Environment.User["TMP"]);
                Assert.AreEqual("user-path", fixture.Environment.User["PATH"]);
                Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Active"));
                Assert.IsFalse(fixture.Environment.User.ContainsKey("PATH_PowerToys_Active"));
                CollectionAssert.AreEqual(
                    ActiveProfileDeletes,
                    fixture.Environment.Operations.Where(x => x.Operation == "Delete").Select(x => x.Name).ToArray());
                fixture.Work.DrainRefreshes();
                Assert.AreEqual("system-path;user-path", vm.AppliedVariables.Single(x => x.ParentType == VariablesSetType.Path).Values);
                Assert.AreEqual("original", vm.AppliedVariables.Single(x => x.Name == "TMP").Values);
                Assert.IsFalse(vm.AppliedVariables.Any(x => x.ParentType == VariablesSetType.Profile || x.Name.EndsWith("_PowerToys_Active", StringComparison.Ordinal)));
            }
            else
            {
                Assert.AreEqual(0, fixture.Environment.Operations.Count);
                Assert.AreEqual("original", fixture.Environment.User["TMP_PowerToys_Active"]);
                Assert.AreEqual("system-path;profile-path", vm.AppliedVariables.Single(x => x.ParentType == VariablesSetType.Path).Values);
            }

            Assert.AreEqual("other-value", fixture.Environment.User["OTHER"]);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreSame(selection == "active" ? other : active, fixture.Service.Writes.Single().Single());
        }

        [DataTestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task EnabledUpdate_ValidatesOldCollectionButDeferredApplyReadsReplacement(bool oldValid, bool newValid)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var existing = Profile("Test", false, "OLD", oldValid ? "old" : "bad\0");
            fixture.Service.Read = () => new() { existing };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var edited = Profile("Test", true, "NEW", newValid ? "new" : "bad\0");
            edited.Id = existing.Id;

            vm.UpdateProfile(edited);

            Assert.AreSame(edited.Variables, existing.Variables);
            Assert.AreEqual(oldValid, existing.IsEnabled);
            Assert.AreEqual(oldValid ? 1 : 0, fixture.Work.ActionCount);
            Assert.AreSame(oldValid ? existing : null, vm.AppliedProfile);
            Assert.AreEqual(oldValid ? EnvironmentState.EnvironmentMessageReceived : EnvironmentState.ProfileNotApplicable, vm.EnvironmentState);
            CollectionAssert.AreEqual(EnabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);

            if (oldValid)
            {
                await fixture.Work.CompleteActionAsync(0);
                Assert.IsFalse(fixture.Environment.User.ContainsKey("OLD"));
                Assert.AreEqual(newValid, fixture.Environment.User.ContainsKey("NEW"));
                if (newValid)
                {
                    Assert.AreEqual("new", fixture.Environment.User["NEW"]);
                }
                else
                {
                    Assert.IsTrue(fixture.Logger.Entries.Any(x => x.Message == "Failed to set profile variable."));
                }

                Assert.AreEqual(1, fixture.Environment.Notifications.Count);
                Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
                fixture.Work.DrainRefreshes();
            }
        }

        [DataTestMethod]
        [DataRow(false, false, "new", null)]
        [DataRow(true, false, null, null)]
        [DataRow(false, true, "new", "after-original")]
        [DataRow(true, true, "after-original", null)]
        public async Task ActiveUpdate_DeferredUnapplyUsesNewNameAndVariables(
            bool applyFirst,
            bool hasNewBackup,
            string? expectedNewValue,
            string? expectedNewBackup)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = Profile("Before", true, "OLD", "override");
            fixture.Environment.User["OLD"] = "override";
            fixture.Environment.User["OLD_PowerToys_Before"] = "original";
            fixture.Service.Read = () => new() { old };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var edited = Profile("After", true, "NEW", "new");
            edited.Id = old.Id;
            if (hasNewBackup)
            {
                fixture.Environment.User["NEW_PowerToys_After"] = "after-original";
            }

            vm.UpdateProfile(edited);

            Assert.AreEqual(2, fixture.Work.ActionCount);
            Assert.AreSame(old, vm.AppliedProfile);
            await fixture.Work.CompleteActionAsync(applyFirst ? 1 : 0);
            await fixture.Work.CompleteActionAsync(applyFirst ? 0 : 1);
            Assert.AreEqual("override", fixture.Environment.User["OLD"]);
            Assert.AreEqual("original", fixture.Environment.User["OLD_PowerToys_Before"]);
            Assert.AreEqual(expectedNewValue is not null, fixture.Environment.User.ContainsKey("NEW"));
            if (expectedNewValue is not null)
            {
                Assert.AreEqual(expectedNewValue, fixture.Environment.User["NEW"]);
            }

            Assert.AreEqual(expectedNewBackup is not null, fixture.Environment.User.ContainsKey("NEW_PowerToys_After"));
            if (expectedNewBackup is not null)
            {
                Assert.AreEqual(expectedNewBackup, fixture.Environment.User["NEW_PowerToys_After"]);
            }

            Assert.IsFalse(fixture.Environment.Operations.Any(x => x.Operation == "Delete" && x.Name == "OLD"));
            Assert.AreEqual(2, fixture.Work.RefreshReceiptCount);
            fixture.Work.DrainRefreshes();
        }

        [DataTestMethod]
        [DataRow("name", EnvironmentState.ProfileNameInvalid)]
        [DataRow("variable", EnvironmentState.ProfileNotApplicable)]
        [DataRow("mismatch", EnvironmentState.ChangedOnStartup)]
        public async Task ReloadRejectedEnabled_UnsetsPreviousThroughSubscribedFalseCallback(string rejection, EnvironmentState expected)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = LoadActive(fixture, out var vm);
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            var rejected = Profile(rejection == "name" ? string.Empty : "New", true, "TMP", rejection == "variable" ? "bad\0" : "different");
            var later = Profile("Later", true, "LATER", "value");
            fixture.Service.Read = () => new() { rejected, later };

            vm.LoadEnvironmentVariables();

            Assert.IsNull(vm.AppliedProfile);
            Assert.IsFalse(old.IsEnabled);
            Assert.IsFalse(rejected.IsEnabled);
            Assert.IsTrue(later.IsEnabled);
            Assert.AreEqual(expected, vm.EnvironmentState);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            Assert.IsTrue(fixture.Logger.Entries.All(x => x.Message != "Failed to load profiles.json file"));

            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual("original", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Old"));
            fixture.Work.DrainRefreshes();
            Assert.AreEqual("original", vm.AppliedVariables.Single(x => x.Name == "TMP").Values);
            Assert.AreEqual(expected, vm.EnvironmentState);
            await fixture.Work.CompleteSaveAsync(0);
            CollectionAssert.AreEqual(new[] { rejected, later }, fixture.Service.Writes.Single());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ReloadNoEnabledOrReadFailure_RetainsOldReferenceAndWarning(bool fail)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = LoadActive(fixture, out var vm);
            var failure = new InvalidOperationException("read failed");
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            fixture.Service.Read = () => fail ? throw failure : new();

            vm.LoadEnvironmentVariables();

            Assert.AreSame(old, vm.AppliedProfile);
            Assert.IsTrue(old.IsEnabled);
            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.AreEqual(0, vm.Profiles.Count);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual(fail ? 1 : 0, fixture.Logger.Entries.Count);
            if (fail)
            {
                Assert.AreSame(failure, fixture.Logger.Entries.Single().Exception);
                Assert.AreEqual("Failed to load profiles.json file", fixture.Logger.Entries.Single().Message);
            }
        }

        [TestMethod]
        public async Task ReloadAcceptedReplacement_LeavesOldSubscriptionAbleToUnsetNewActive()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = LoadActive(fixture, out var vm);
            var replacement = Profile("New", true, "TMP", "override");
            var later = Profile("Later", true, "OTHER", "not present");
            fixture.Service.Read = () => new() { replacement, later };

            vm.LoadEnvironmentVariables();

            Assert.AreSame(replacement, vm.AppliedProfile);
            Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
            Assert.IsTrue(old.IsEnabled);
            Assert.IsTrue(later.IsEnabled);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            old.IsEnabled = false;
            Assert.IsNull(vm.AppliedProfile);
            Assert.IsFalse(replacement.IsEnabled);
            Assert.IsTrue(later.IsEnabled);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            await fixture.Work.CompleteActionAsync(0);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP"));
            fixture.Work.DrainRefreshes();
        }

        [TestMethod]
        public async Task Save_ReadsLiveProfilesAtExecutionAndWriteFailureOnlyLogs()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var first = Profile("First", false, "A", "1");
            vm.AddProfile(first);
            var replacement = Profile("Replacement", false, "B", "2");
            vm.Profiles = new ObservableCollection<ProfileVariablesSet> { replacement };
            var failure = new InvalidOperationException("write failed");
            fixture.Service.WriteFailure = failure;

            await fixture.Work.CompleteSaveAsync(0);

            CollectionAssert.AreEqual(new[] { replacement }, fixture.Service.Writes.Single());
            Assert.AreSame(failure, fixture.Logger.Entries.Single().Exception);
            Assert.AreEqual("Failed to save to profiles.json file", fixture.Logger.Entries.Single().Message);
            Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
            Assert.AreEqual(1, fixture.Work.SaveCount);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DeleteDefault_RemovesModelImmediatelyButFailedWriteCanReappearOnRefresh(bool failDelete)
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["TMP"] = "user";
            fixture.Environment.Machine["TMP"] = "system";
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var variable = vm.UserDefaultSet.Variables.Single();
            fixture.Environment.BeforeOperation = (operation, hive, name) =>
            {
                if (failDelete && operation == "Delete")
                {
                    throw new InvalidOperationException("delete failed");
                }
            };

            vm.DeleteVariable(variable, null!);

            Assert.AreEqual(0, vm.UserDefaultSet.Variables.Count);
            Assert.AreEqual("user", fixture.Environment.User["TMP"]);
            await fixture.Work.CompleteActionAsync(0);
            fixture.Work.DrainRefreshes();
            Assert.AreEqual(failDelete ? 1 : 0, vm.UserDefaultSet.Variables.Count);
            Assert.AreEqual("system", fixture.Environment.Machine["TMP"]);
            Assert.AreEqual(failDelete ? VariablesSetType.Duplicate : VariablesSetType.System, vm.AppliedVariables.Single().ParentType);
        }

        [TestMethod]
        public async Task FaultedApply_StillAttemptsRejectedRefreshEnqueue()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            fixture.Work.AcceptRefresh = false;
            var failure = new InvalidOperationException("notify failed");
            fixture.Environment.BeforeNotify = () => throw failure;
            vm.AddProfile(Profile("Test", true, "TMP", "new"));

            var observed = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => fixture.Work.CompleteActionAsync(0));

            Assert.AreSame(failure, observed);
            Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(0, vm.AppliedVariables.Count);
            Assert.AreEqual("new", fixture.Environment.User["TMP"]);
            fixture.Work.DrainRefreshes();
            Assert.AreEqual(0, vm.AppliedVariables.Count);
        }

        [TestMethod]
        public void Ownership_LoadMarksDirectAndExactBackupNamesButNotUnrelatedLookalikes()
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["tmp"] = "OVERRIDE";
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            fixture.Environment.User["TMP_PowerToys_Other"] = "unrelated";
            var old = Profile("Old", true, "TMP", "override");
            var vm = fixture.CreateViewModel();
            fixture.Service.Read = () => new();
            vm.AppliedProfile = old;

            vm.LoadEnvironmentVariables();

            Assert.IsFalse(vm.IsElevated);
            Assert.IsTrue(vm.UserDefaultSet.Variables.Single(x => x.Name == "tmp").IsAppliedFromProfile);
            var backup = vm.UserDefaultSet.Variables.Single(x => x.Name == "TMP_PowerToys_Old");
            Assert.IsTrue(backup.IsAppliedFromProfile);
            Assert.IsFalse(backup.IsEditable);
            Assert.IsFalse(vm.UserDefaultSet.Variables.Single(x => x.Name == "TMP_PowerToys_Other").IsAppliedFromProfile);
        }

        [DataTestMethod]
        [DataRow("S", "U", "S;U")]
        [DataRow("S", "", "S;")]
        [DataRow("", "U", ";U")]
        [DataRow("", "", ";")]
        [DataRow("S", null, "S")]
        public void Projection_PathUsesSystemFirstAndPreservesMissingAndEmptyUser(string system, string? user, string expected)
        {
            var systemVariable = new Variable("Path", system, VariablesSetType.System);
            var users = user == null ? Array.Empty<Variable>() : new[] { new Variable("PATH", user, VariablesSetType.User) };
            var result = MainViewModel.BuildAppliedVariables(null!, users, new[] { systemVariable }, value => value);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(("Path", expected, VariablesSetType.Path), (result[0].Name, result[0].Values, result[0].ParentType));
            Assert.AreNotSame(systemVariable, result[0]);
            Assert.AreEqual(system, systemVariable.Values);
            Assert.AreEqual(VariablesSetType.System, systemVariable.ParentType);
            if (user != null)
            {
                Assert.AreEqual(user, users[0].Values);
            }
        }

        [TestMethod]
        public void Projection_MissingSystemLeavesProfileAndUserPathWhileSystemWithoutUserDropsProfilePath()
        {
            var profile = Profile("Test", true, "PATH", "P");
            var user = new Variable("path", "U", VariablesSetType.User);
            var withoutSystem = MainViewModel.BuildAppliedVariables(profile, new[] { user }, Array.Empty<Variable>(), value => value);
            CollectionAssert.AreEqual(new[] { ("PATH", "P", VariablesSetType.Profile), ("path", "U", VariablesSetType.User) }, withoutSystem.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());

            var withoutUser = MainViewModel.BuildAppliedVariables(profile, Array.Empty<Variable>(), new[] { new Variable("Path", "S", VariablesSetType.System) }, value => value);
            Assert.AreEqual(("Path", "S", VariablesSetType.Path), (withoutUser.Single().Name, withoutUser.Single().Values, withoutUser.Single().ParentType));
            Assert.AreEqual("P", profile.Variables.Single().Values);
        }

        [TestMethod]
        public void Projection_ProfilePathUsesEffectiveUserSnapshotAndRetainsBackupWithoutDeduplication()
        {
            var profile = Profile("Test", true, "PATH", "draft");
            var effective = new Variable("path", "P;;P", VariablesSetType.User);
            var backup = new Variable("PATH_PowerToys_Test", "%ROOT%", VariablesSetType.User);
            var system = new Variable("Path", "S;", VariablesSetType.System);

            var result = MainViewModel.BuildAppliedVariables(profile, new[] { effective, backup }, new[] { system }, value => value == "%ROOT%" ? @"C:\Original" : value);

            CollectionAssert.AreEqual(
                new[] { ("Path", "S;;P;;P", VariablesSetType.Path), ("PATH_PowerToys_Test", @"C:\Original", VariablesSetType.User) },
                result.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());
            Assert.AreEqual("draft", profile.Variables.Single().Values);
            Assert.AreEqual("P;;P", effective.Values);
            Assert.AreEqual("%ROOT%", backup.Values);
            Assert.AreEqual("S;", system.Values);
        }

        [TestMethod]
        public void Projection_FirstTwoDuplicatesUseExpandedProfileValueAndUserNameLeavingThirdSystemRow()
        {
            var profile = Profile("Test", true, "TMP", "%ROOT%");
            var user = new Variable("tmp", "user", VariablesSetType.User);
            var system = new Variable("Tmp", "system", VariablesSetType.System);
            var expansions = new System.Collections.Generic.List<string>();

            var result = MainViewModel.BuildAppliedVariables(profile, new[] { user }, new[] { system }, value =>
            {
                expansions.Add(value);
                return value == "%ROOT%" ? @"C:\Controlled" : value;
            });

            CollectionAssert.AreEqual(new[] { ("tmp", @"C:\Controlled", VariablesSetType.Duplicate), ("Tmp", "system", VariablesSetType.System) }, result.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());
            CollectionAssert.AreEqual(ExpansionInputs, expansions);
            Assert.AreEqual("%ROOT%", profile.Variables.Single().Values);
            Assert.AreEqual("user", user.Values);
            Assert.AreEqual(VariablesSetType.User, user.ParentType);
            Assert.AreEqual(VariablesSetType.System, system.ParentType);
            Assert.IsTrue(result.All(x => !ReferenceEquals(x, user) && !ReferenceEquals(x, system) && !ReferenceEquals(x, profile.Variables[0])));
        }

        [TestMethod]
        public void Projection_ComposesExpandedSystemAndEffectiveUserPathWithoutDeduplicating()
        {
            var profile = Profile("Test", true, "PATH", "%DRAFT%");
            var user = new Variable("path", "%USER%", VariablesSetType.User);
            var backup = new Variable("PATH_PowerToys_Test", "%BACKUP%", VariablesSetType.User);
            var system = new Variable("Path", "%SYSTEM%", VariablesSetType.System);
            var inputs = new System.Collections.Generic.List<string>();
            var expansions = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["%DRAFT%"] = @"C:\Ignored",
                ["%USER%"] = @"C:\User;c:/shared;C:\User",
                ["%BACKUP%"] = @"C:\Original;;C:\Original",
                ["%SYSTEM%"] = @"C:\System;C:\Shared\",
            };

            var result = MainViewModel.BuildAppliedVariables(profile, new[] { user, backup }, new[] { system }, value =>
            {
                inputs.Add(value);
                return expansions[value];
            });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(
                @"C:\System;C:\Shared\;C:\User;c:/shared;C:\User",
                result.Single(variable => variable.ParentType == VariablesSetType.Path).Values);
            Assert.AreEqual(
                @"C:\Original;;C:\Original",
                result.Single(variable => variable.Name == "PATH_PowerToys_Test").Values);
            CollectionAssert.AreEqual(ComposedPathExpansionInputs, inputs);
            Assert.AreEqual("%DRAFT%", profile.Variables.Single().Values);
            Assert.AreEqual("%USER%", user.Values);
            Assert.AreEqual("%SYSTEM%", system.Values);
        }

        [DataTestMethod]
        [DataRow(false, "new", "original")]
        [DataRow(true, "original", "old")]
        public async Task SwitchDistinctProfiles_SharedValueAndBackupFollowWholeActionReleaseOrder(
            bool applyFirst,
            string expectedShared,
            string expectedNewBackup)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var old = Profile("Old", true, "Shared", "old");
            old.Variables.Add(new Variable("OldOnly", "old-only", VariablesSetType.Profile));
            var next = Profile("Next", false, "Shared", "new");
            next.Variables.Add(new Variable("NewOnly", "new-only", VariablesSetType.Profile));
            fixture.Environment.User["Shared"] = "old";
            fixture.Environment.User["OldOnly"] = "old-only";
            fixture.Environment.User["Shared_PowerToys_Old"] = "original";
            fixture.Service.Read = () => new() { old, next };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();

            next.IsEnabled = true;

            Assert.IsFalse(old.IsEnabled);
            Assert.IsTrue(next.IsEnabled);
            Assert.AreSame(next, vm.AppliedProfile);
            Assert.AreEqual(2, fixture.Work.ActionCount);
            Assert.AreEqual("old", fixture.Environment.User["Shared"]);

            await fixture.Work.CompleteActionAsync(applyFirst ? 1 : 0);
            await fixture.Work.CompleteActionAsync(applyFirst ? 0 : 1);

            Assert.AreEqual(expectedShared, fixture.Environment.User["Shared"]);
            Assert.AreEqual(expectedNewBackup, fixture.Environment.User["Shared_PowerToys_Next"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("Shared_PowerToys_Old"));
            Assert.IsFalse(fixture.Environment.User.ContainsKey("OldOnly"));
            Assert.AreEqual("new-only", fixture.Environment.User["NewOnly"]);
            Assert.AreEqual(2, fixture.Environment.Notifications.Count);
            Assert.AreEqual(2, fixture.Work.RefreshReceiptCount);
            fixture.Work.DrainRefreshes();

            CollectionAssert.AreEqual(
                new[]
                {
                    ("NewOnly", "new-only", VariablesSetType.Duplicate, false),
                    ("Shared", "new", VariablesSetType.Duplicate, false),
                    ("Shared_PowerToys_Next", expectedNewBackup, VariablesSetType.User, false),
                },
                vm.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType, x.IsAppliedFromProfile)).ToArray());
            CollectionAssert.AreEqual(
                new[] { ("NewOnly", true), ("Shared", !applyFirst), ("Shared_PowerToys_Next", true) },
                vm.UserDefaultSet.Variables.Select(x => (x.Name, x.IsAppliedFromProfile)).ToArray());
            Assert.AreSame(next, vm.AppliedProfile);
            Assert.AreEqual(0, vm.SystemDefaultSet.Variables.Count);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            await fixture.Work.CompleteSaveAsync(0);
            CollectionAssert.AreEqual(new[] { old, next }, fixture.Service.Writes.Single());
        }

        [TestMethod]
        public async Task DisableCreatedProfile_UnappliesWithoutBackupAndRefreshesOnlyUnrelatedRows()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var profile = Profile("CreatedProfile", true, "Created", "profile-value");
            fixture.Environment.User["Created"] = "profile-value";
            fixture.Environment.User["UserUnrelated"] = "user-value";
            fixture.Environment.Machine["SystemUnrelated"] = "system-value";
            fixture.Service.Read = () => new() { profile };
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            Assert.AreSame(profile, vm.AppliedProfile);
            Assert.IsTrue(vm.UserDefaultSet.Variables.Single(x => x.Name == "Created").IsAppliedFromProfile);
            var previousProjection = vm.AppliedVariables;
            fixture.Environment.Operations.Clear();

            profile.IsEnabled = false;

            Assert.IsNull(vm.AppliedProfile);
            Assert.IsFalse(profile.IsEnabled);
            Assert.AreSame(profile, vm.Profiles.Single());
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            Assert.AreEqual("profile-value", fixture.Environment.User["Created"]);
            Assert.AreSame(previousProjection, vm.AppliedVariables);
            profile.IsEnabled = false;
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);

            await fixture.Work.CompleteActionAsync(0);

            Assert.IsFalse(fixture.Environment.User.ContainsKey("Created"));
            Assert.IsFalse(fixture.Environment.User.ContainsKey("Created_PowerToys_CreatedProfile"));
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            var mutation = fixture.Environment.Operations.Single(x => x.Operation is "Delete" or "Write");
            Assert.AreEqual((RegistryHive.CurrentUser, "Delete", "Created"), (mutation.Hive, mutation.Operation, mutation.Name));
            Assert.AreSame(previousProjection, vm.AppliedVariables);
            fixture.Work.DrainRefreshes();
            CollectionAssert.AreEqual(
                new[]
                {
                    ("UserUnrelated", "user-value", VariablesSetType.User, false),
                    ("SystemUnrelated", "system-value", VariablesSetType.System, false),
                },
                vm.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType, x.IsAppliedFromProfile)).ToArray());
            Assert.AreEqual(1, fixture.Environment.User.Count);
            Assert.AreEqual("user-value", fixture.Environment.User["UserUnrelated"]);
            Assert.AreEqual(1, fixture.Environment.Machine.Count);
            Assert.AreEqual("system-value", fixture.Environment.Machine["SystemUnrelated"]);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreSame(profile, fixture.Service.Writes.Single().Single());
            Assert.IsFalse(fixture.Service.Writes.Single().Single().IsEnabled);
        }

        [TestMethod]
        public async Task AuthorEnabledProfile_RetainsBothSpecifiedVariablesThroughApplyAndScheduledSave()
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["Unrelated"] = "kept";
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var profile = new ProfileVariablesSet(Guid.NewGuid(), "Test_profile_2");
            VariableEditorState.AddProfileVariable(profile, true, "profile_2_variable_1", "profile_2_value_1", Array.Empty<object>());
            VariableEditorState.AddProfileVariable(profile, true, "profile_2_variable_2", "profile_2_value_2", Array.Empty<object>());
            profile.IsEnabled = true;
            var expected = new[]
            {
                ("profile_2_variable_1", "profile_2_value_1"),
                ("profile_2_variable_2", "profile_2_value_2"),
            };

            vm.AddProfile(profile);

            Assert.AreSame(profile, vm.Profiles.Single());
            Assert.AreSame(profile, vm.AppliedProfile);
            CollectionAssert.AreEqual(expected, profile.Variables.Select(x => (x.Name, x.Values)).ToArray());
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(1, fixture.Environment.User.Count);
            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual("profile_2_value_1", fixture.Environment.User["profile_2_variable_1"]);
            Assert.AreEqual("profile_2_value_2", fixture.Environment.User["profile_2_variable_2"]);
            Assert.AreEqual("kept", fixture.Environment.User["Unrelated"]);
            fixture.Work.DrainRefreshes();
            CollectionAssert.AreEqual(
                new[]
                {
                    ("profile_2_variable_1", "profile_2_value_1", VariablesSetType.Duplicate),
                    ("profile_2_variable_2", "profile_2_value_2", VariablesSetType.Duplicate),
                    ("Unrelated", "kept", VariablesSetType.User),
                },
                vm.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());
            await fixture.Work.CompleteSaveAsync(0);
            var saved = fixture.Service.Writes.Single().Single();
            Assert.AreSame(profile, saved);
            Assert.AreEqual("Test_profile_2", saved.Name);
            Assert.IsTrue(saved.IsEnabled);
            CollectionAssert.AreEqual(expected, saved.Variables.Select(x => (x.Name, x.Values)).ToArray());
        }

        [TestMethod]
        public async Task EditInactiveWholeProfile_PreservesIdentityAndExistingChildWhileSavingAddedVariableAndExactPath()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var original = Profile("Test_profile_1", false, "Existing", "keep");
            var existingChild = original.Variables.Single();
            existingChild.ApplyToSystem = true;
            var id = original.Id;
            var icon = original.IconPath;
            vm.AddProfile(original);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual(1, fixture.Service.Writes.Single().Single().Variables.Count);
            var draft = original.Clone();
            Assert.AreNotSame(original.Variables, draft.Variables);
            Assert.AreSame(existingChild, draft.Variables.Single());
            VariableEditorState.AddProfileVariable(draft, true, "profile_1_variable_1", "profile_1_value_1", Array.Empty<object>());
            VariableEditorState.AddProfileVariable(draft, true, "PATH", "path1;path2;path3", Array.Empty<object>());
            Assert.AreEqual(1, original.Variables.Count);
            fixture.Environment.Operations.Clear();

            vm.UpdateProfile(draft);

            Assert.AreSame(original, vm.Profiles.Single());
            Assert.AreNotSame(draft, vm.Profiles.Single());
            Assert.AreSame(draft.Variables, original.Variables);
            Assert.AreSame(existingChild, original.Variables[0]);
            Assert.AreEqual((id, "Test_profile_1", false, VariablesSetType.Profile, icon), (original.Id, original.Name, original.IsEnabled, original.Type, original.IconPath));
            Assert.IsTrue(existingChild.ApplyToSystem);
            Assert.AreEqual("keep", existingChild.Values);
            Assert.IsNull(vm.AppliedProfile);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(3, fixture.Work.SaveCount);
            var expected = new[]
            {
                ("Existing", "keep"),
                ("profile_1_variable_1", "profile_1_value_1"),
                ("PATH", "path1;path2;path3"),
            };
            CollectionAssert.AreEqual(expected, original.Variables.Select(x => (x.Name, x.Values)).ToArray());
            CollectionAssert.AreEqual(AuthoredPathEntries, original.Variables[2].ValuesList.Select(x => x.Text).ToArray());
            await fixture.Work.CompleteSaveAsync(1);
            await fixture.Work.CompleteSaveAsync(2);
            Assert.AreEqual(3, fixture.Service.Writes.Count);
            foreach (var write in fixture.Service.Writes.Skip(1))
            {
                var saved = write.Single();
                Assert.AreSame(original, saved);
                Assert.AreEqual((id, "Test_profile_1", false), (saved.Id, saved.Name, saved.IsEnabled));
                CollectionAssert.AreEqual(expected, saved.Variables.Select(x => (x.Name, x.Values)).ToArray());
                CollectionAssert.AreEqual(AuthoredPathEntries, saved.Variables[2].ValuesList.Select(x => x.Text).ToArray());
                Assert.AreSame(existingChild, saved.Variables[0]);
                Assert.IsTrue(saved.Variables[0].ApplyToSystem);
            }
        }

        [TestMethod]
        public async Task UpdateProfile_MissingIdDoesNotUpsertAndSchedulesUnchangedMembershipSave()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var original = Profile("Existing", false, "KEEP", "keep-value");
            original.Id = new Guid("11111111-1111-1111-1111-111111111111");
            var child = original.Variables.Single();
            child.ApplyToSystem = true;
            fixture.Service.Read = () => new() { original };
            fixture.Environment.User["UNRELATED"] = "user-value";
            var vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            var profiles = vm.Profiles;
            var variables = original.Variables;
            var valuesList = child.ValuesList;
            var projection = vm.AppliedVariables;
            var draft = Profile("Missing", false, "REPLACE", "replacement");
            draft.Id = new Guid("22222222-2222-2222-2222-222222222222");
            fixture.Environment.Operations.Clear();

            vm.UpdateProfile(draft);

            Assert.AreSame(profiles, vm.Profiles);
            Assert.AreEqual(1, vm.Profiles.Count);
            Assert.AreSame(original, vm.Profiles.Single());
            Assert.IsFalse(vm.Profiles.Any(x => x.Id == draft.Id));
            Assert.AreEqual((new Guid("11111111-1111-1111-1111-111111111111"), "Existing", false), (original.Id, original.Name, original.IsEnabled));
            Assert.AreSame(variables, original.Variables);
            Assert.AreSame(child, original.Variables.Single());
            Assert.AreEqual(("KEEP", "keep-value", VariablesSetType.Profile, true), (child.Name, child.Values, child.ParentType, child.ApplyToSystem));
            Assert.AreSame(valuesList, child.ValuesList);
            Assert.IsNull(vm.AppliedProfile);
            Assert.AreSame(projection, vm.AppliedVariables);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual("user-value", fixture.Environment.User["UNRELATED"]);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Service.Writes.Count);

            await fixture.Work.CompleteSaveAsync(0);

            var saved = fixture.Service.Writes.Single().Single();
            Assert.AreSame(original, saved);
            Assert.AreEqual((new Guid("11111111-1111-1111-1111-111111111111"), "Existing", false), (saved.Id, saved.Name, saved.IsEnabled));
            Assert.AreSame(variables, saved.Variables);
            Assert.AreSame(child, saved.Variables.Single());
            Assert.AreEqual(("KEEP", "keep-value", VariablesSetType.Profile, true), (saved.Variables[0].Name, saved.Variables[0].Values, saved.Variables[0].ParentType, saved.Variables[0].ApplyToSystem));
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task AuthorEnabledProfile_ScheduledRealJsonReconstructsFreshManagedVmWithoutReapplying()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            files.Files[service.ProfilesJsonFilePath] = "[]";
            fixture.Environment.User["Unrelated"] = "user-sentinel";
            fixture.Environment.Machine["MachineUnrelated"] = "machine-sentinel";
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var authored = new ProfileVariablesSet(Guid.NewGuid(), "Test_profile_2");
            VariableEditorState.AddProfileVariable(authored, true, "profile_2_variable_1", "profile_2_value_1", Array.Empty<object>());
            VariableEditorState.AddProfileVariable(authored, true, "profile_2_variable_2", "profile_2_value_2", Array.Empty<object>());
            authored.IsEnabled = true;

            vm.AddProfile(authored);

            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(0, files.Writes.Count);
            Assert.AreEqual(1, fixture.Environment.User.Count);
            await fixture.Work.CompleteActionAsync(0);
            fixture.Work.DrainRefreshes();
            Assert.AreEqual("profile_2_value_1", fixture.Environment.User["profile_2_variable_1"]);
            Assert.AreEqual("profile_2_value_2", fixture.Environment.User["profile_2_variable_2"]);
            Assert.AreEqual("user-sentinel", fixture.Environment.User["Unrelated"]);
            Assert.AreEqual("machine-sentinel", fixture.Environment.Machine["MachineUnrelated"]);
            var expected = new[]
            {
                ("profile_2_variable_1", "profile_2_value_1"),
                ("profile_2_variable_2", "profile_2_value_2"),
            };
            await fixture.Work.CompleteSaveAsync(0);
            var contents = files.Writes.Single().Contents!;
            using var json = JsonDocument.Parse(contents);
            Assert.AreEqual(1, json.RootElement.GetArrayLength());
            var saved = json.RootElement[0];
            Assert.AreEqual(authored.Id, saved.GetProperty("Id").GetGuid());
            Assert.AreEqual("Test_profile_2", saved.GetProperty("Name").GetString());
            Assert.IsTrue(saved.GetProperty("IsEnabled").GetBoolean());
            CollectionAssert.AreEqual(
                expected,
                saved.GetProperty("Variables").EnumerateArray().Select(x => (x.GetProperty("Name").GetString(), x.GetProperty("Values").GetString())).ToArray());

            var reconstructed = fixture.CreateViewModel(service: service);
            reconstructed.LoadEnvironmentVariables();

            var loaded = reconstructed.Profiles.Single();
            Assert.AreNotSame(authored, loaded);
            Assert.AreNotSame(authored.Variables, loaded.Variables);
            Assert.AreEqual((authored.Id, "Test_profile_2", true), (loaded.Id, loaded.Name, loaded.IsEnabled));
            Assert.AreSame(loaded, reconstructed.AppliedProfile);
            Assert.AreEqual(EnvironmentState.Unchanged, reconstructed.EnvironmentState);
            CollectionAssert.AreEqual(expected, loaded.Variables.Select(x => (x.Name, x.Values)).ToArray());
            Assert.IsTrue(loaded.Variables.All(x => x.ParentType == VariablesSetType.Profile));
            for (var index = 0; index < loaded.Variables.Count; index++)
            {
                Assert.AreNotSame(authored.Variables[index], loaded.Variables[index]);
                Assert.AreNotSame(authored.Variables[index].ValuesList[0], loaded.Variables[index].ValuesList.Single());
                Assert.AreEqual(expected[index].Item2, loaded.Variables[index].ValuesList[0].Text);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    ("profile_2_variable_1", "profile_2_value_1", VariablesSetType.Duplicate),
                    ("profile_2_variable_2", "profile_2_value_2", VariablesSetType.Duplicate),
                    ("Unrelated", "user-sentinel", VariablesSetType.User),
                    ("MachineUnrelated", "machine-sentinel", VariablesSetType.System),
                },
                reconstructed.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());
            CollectionAssert.AreEqual(
                reconstructed.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType)).ToArray(),
                vm.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(1, files.Writes.Count);
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.AreEqual(contents, files.Files[service.ProfilesJsonFilePath]);
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task ExistingTmp_InactiveCloneAndEditPersistImmutableRealJsonWithoutChangingUserSource()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            files.Files[service.ProfilesJsonFilePath] = """
                [{"Id":"33333333-3333-3333-3333-333333333333","Name":"Test_profile_1","IsEnabled":false,
                  "Variables":[{"Name":"KEEP","Values":"keep","ApplyToSystem":true}]}]
                """;
            fixture.Environment.User["TMP"] = "original%ROOT%";
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var original = vm.Profiles.Single();
            var retainedChild = original.Variables.Single();
            var source = vm.UserDefaultSet.Variables.Single();
            var sourceRows = source.ValuesList;
            var draft = original.Clone();
            Assert.AreSame(retainedChild, draft.Variables.Single());
            VariableEditorState.AddProfileVariable(draft, false, string.Empty, string.Empty, new object[] { source });
            Assert.AreEqual(1, original.Variables.Count);
            Assert.AreNotSame(source, draft.Variables[1]);
            Assert.AreNotSame(source.ValuesList[0], draft.Variables[1].ValuesList[0]);

            vm.UpdateProfile(draft);

            Assert.AreSame(original, vm.Profiles.Single());
            Assert.AreSame(draft.Variables, original.Variables);
            Assert.AreSame(retainedChild, original.Variables[0]);
            Assert.AreEqual(2, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, files.Writes.Count);
            await fixture.Work.CompleteSaveAsync(0);
            await fixture.Work.CompleteSaveAsync(1);
            var oldContents = files.Writes[0].Contents!;
            Assert.AreEqual(oldContents, files.Writes[1].Contents);
            using var oldJson = JsonDocument.Parse(oldContents);
            using var secondOldJson = JsonDocument.Parse(files.Writes[1].Contents!);
            Assert.AreEqual(1, oldJson.RootElement.GetArrayLength());
            Assert.AreEqual(1, secondOldJson.RootElement.GetArrayLength());
            var expectedBeforeEdit = new[] { ("KEEP", "keep"), ("TMP", "original%ROOT%") };
            foreach (var previous in new[] { oldJson.RootElement[0], secondOldJson.RootElement[0] })
            {
                Assert.AreEqual(original.Id, previous.GetProperty("Id").GetGuid());
                Assert.AreEqual("Test_profile_1", previous.GetProperty("Name").GetString());
                Assert.IsFalse(previous.GetProperty("IsEnabled").GetBoolean());
                CollectionAssert.AreEqual(
                    expectedBeforeEdit,
                    previous.GetProperty("Variables").EnumerateArray().Select(x => (x.GetProperty("Name").GetString(), x.GetProperty("Values").GetString())).ToArray());
            }

            var profileTmp = original.Variables[1];
            fixture.Environment.Operations.Clear();

            vm.EditVariable(profileTmp, new Variable("TMP", "test_TMP", VariablesSetType.Profile), original);

            Assert.AreEqual("test_TMP", profileTmp.Values);
            Assert.AreEqual("original%ROOT%", source.Values);
            Assert.AreSame(sourceRows, source.ValuesList);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(3, fixture.Work.SaveCount);
            Assert.AreEqual(2, files.Writes.Count);
            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            fixture.Work.DrainRefreshes();
            await fixture.Work.CompleteSaveAsync(2);
            using var latestJson = JsonDocument.Parse(files.Writes[2].Contents!);
            Assert.AreEqual(1, latestJson.RootElement.GetArrayLength());
            var latest = latestJson.RootElement[0];
            Assert.AreEqual(original.Id, latest.GetProperty("Id").GetGuid());
            Assert.AreEqual("Test_profile_1", latest.GetProperty("Name").GetString());
            Assert.IsFalse(latest.GetProperty("IsEnabled").GetBoolean());
            CollectionAssert.AreEqual(
                new[] { ("KEEP", "keep"), ("TMP", "test_TMP") },
                latest.GetProperty("Variables").EnumerateArray().Select(x => (x.GetProperty("Name").GetString(), x.GetProperty("Values").GetString())).ToArray());
            Assert.IsTrue(latest.GetProperty("Variables")[0].GetProperty("ApplyToSystem").GetBoolean());
            Assert.AreEqual("original%ROOT%", oldJson.RootElement[0].GetProperty("Variables")[1].GetProperty("Values").GetString());
            Assert.AreEqual(oldContents, files.Writes[0].Contents);
            Assert.AreEqual(oldContents, files.Writes[1].Contents);
            var readback = service.ReadProfiles().Single();
            Assert.AreNotSame(original, readback);
            Assert.AreNotSame(profileTmp, readback.Variables[1]);
            Assert.AreEqual("test_TMP", readback.Variables[1].Values);
            Assert.AreEqual("test_TMP", readback.Variables[1].ValuesList.Single().Text);
            Assert.IsTrue(readback.Variables[0].ApplyToSystem);
            Assert.AreEqual(VariablesSetType.User, source.ParentType);
            Assert.AreEqual("original%ROOT%", fixture.Environment.User["TMP"]);
            Assert.AreSame(sourceRows, source.ValuesList);
            Assert.IsFalse(fixture.Environment.Operations.Any(x => x.Operation is "Write" or "Delete"));
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task InactivePath_ExplicitRawCommitPersistsImmutableJsonAndRebuildsOrderedReadbackRows()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            files.Files[service.ProfilesJsonFilePath] = """
                [{"Id":"44444444-4444-4444-4444-444444444444","Name":"Test_profile_1","IsEnabled":false,
                  "Variables":[{"Name":"PATH","Values":"before;value","ApplyToSystem":false}]}]
                """;
            fixture.Environment.User["PATH"] = "user-path";
            fixture.Environment.Machine["Path"] = "system-path";
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var profile = vm.Profiles.Single();
            var path = profile.Variables.Single();
            var originalRows = path.ValuesList;
            vm.UpdateProfile(profile.Clone());
            Assert.AreEqual(2, fixture.Work.SaveCount);
            await fixture.Work.CompleteSaveAsync(0);
            await fixture.Work.CompleteSaveAsync(1);
            var beforeContents = files.Writes[0].Contents!;
            using var beforeJson = JsonDocument.Parse(beforeContents);
            using var secondBeforeJson = JsonDocument.Parse(files.Writes[1].Contents!);
            Assert.AreEqual(1, beforeJson.RootElement.GetArrayLength());
            Assert.AreEqual(1, secondBeforeJson.RootElement.GetArrayLength());
            Assert.AreEqual("before;value", beforeJson.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
            Assert.AreEqual("before;value", secondBeforeJson.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
            Assert.AreEqual(profile.Id, beforeJson.RootElement[0].GetProperty("Id").GetGuid());
            Assert.IsFalse(beforeJson.RootElement[0].GetProperty("IsEnabled").GetBoolean());
            var synchronizedDraft = new Variable("PATH", @"%ROOT%\bin;;C:\Tools;C:\Tools;", VariablesSetType.Profile);
            var expectedRows = new[] { @"%ROOT%\bin", string.Empty, @"C:\Tools", @"C:\Tools", string.Empty };
            fixture.Environment.Operations.Clear();

            vm.EditVariable(path, synchronizedDraft, profile);

            Assert.AreEqual(@"%ROOT%\bin;;C:\Tools;C:\Tools;", path.Values);
            Assert.AreNotSame(originalRows, path.ValuesList);
            CollectionAssert.AreEqual(expectedRows, path.ValuesList.Select(x => x.Text).ToArray());
            Assert.AreEqual(5, path.ValuesList.Distinct().Count());
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(3, fixture.Work.SaveCount);
            Assert.AreEqual(2, files.Writes.Count);
            await fixture.Work.CompleteActionAsync(0);
            Assert.AreEqual(0, fixture.Environment.Operations.Count);
            fixture.Work.DrainRefreshes();
            Assert.AreEqual("system-path;user-path", vm.AppliedVariables.Single().Values);
            await fixture.Work.CompleteSaveAsync(2);
            using var committedJson = JsonDocument.Parse(files.Writes[2].Contents!);
            Assert.AreEqual(1, committedJson.RootElement.GetArrayLength());
            var saved = committedJson.RootElement[0];
            Assert.AreEqual(profile.Id, saved.GetProperty("Id").GetGuid());
            Assert.AreEqual("Test_profile_1", saved.GetProperty("Name").GetString());
            Assert.IsFalse(saved.GetProperty("IsEnabled").GetBoolean());
            Assert.AreEqual(1, saved.GetProperty("Variables").GetArrayLength());
            Assert.AreEqual("PATH", saved.GetProperty("Variables")[0].GetProperty("Name").GetString());
            Assert.AreEqual(@"%ROOT%\bin;;C:\Tools;C:\Tools;", saved.GetProperty("Variables")[0].GetProperty("Values").GetString());
            Assert.AreEqual("before;value", beforeJson.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
            Assert.AreEqual(beforeContents, files.Writes[0].Contents);
            Assert.AreEqual(beforeContents, files.Writes[1].Contents);
            var readback = service.ReadProfiles().Single().Variables.Single();
            Assert.AreNotSame(path, readback);
            Assert.AreEqual(@"%ROOT%\bin;;C:\Tools;C:\Tools;", readback.Values);
            Assert.IsTrue(readback.ShowAsList);
            CollectionAssert.AreEqual(expectedRows, readback.ValuesList.Select(x => x.Text).ToArray());
            Assert.AreEqual(5, readback.ValuesList.Distinct().Count());
            for (var index = 0; index < readback.ValuesList.Count; index++)
            {
                Assert.AreNotSame(path.ValuesList[index], readback.ValuesList[index]);
            }

            Assert.AreEqual("user-path", fixture.Environment.User["PATH"]);
            Assert.AreEqual("system-path", fixture.Environment.Machine["Path"]);
            Assert.IsFalse(fixture.Environment.Operations.Any(x => x.Operation is "Write" or "Delete"));
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [DataTestMethod]
        [DataRow("missing", null)]
        [DataRow("[]", null)]
        [DataRow("null", typeof(NullReferenceException))]
        [DataRow("{", typeof(JsonException))]
        [DataRow("[null]", typeof(NullReferenceException))]
        [DataRow("""[{"Name":"Shape","IsEnabled":true,"Variables":null}]""", typeof(NullReferenceException))]
        public async Task RealServiceReload_EmptyAndReadShapeFailuresRetainOldActiveWithoutSaving(string input, Type? expectedException)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var seed = Profile("Old", true, "TMP", "override");
            fixture.Environment.User["TMP"] = "override";
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            await service.WriteAsync(new[] { seed });
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var old = vm.AppliedProfile;
            Assert.AreNotSame(seed, old);
            Assert.AreSame(vm.Profiles.Single(), old);
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            if (input == "missing")
            {
                files.Files.Remove(service.ProfilesJsonFilePath);
            }
            else
            {
                files.Files[service.ProfilesJsonFilePath] = input;
            }

            vm.LoadEnvironmentVariables();

            Assert.AreSame(old, vm.AppliedProfile);
            Assert.IsTrue(old.IsEnabled);
            Assert.AreEqual(0, vm.Profiles.Count);
            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(1, files.Writes.Count);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            Assert.AreEqual("original", fixture.Environment.User["TMP_PowerToys_Old"]);
            CollectionAssert.AreEqual(
                RetainedOldProjection,
                vm.AppliedVariables.Select(x => (x.Name, x.Values, x.ParentType)).ToArray());
            if (expectedException == null)
            {
                Assert.AreEqual(0, fixture.Logger.Entries.Count);
            }
            else
            {
                var error = fixture.Logger.Entries.Single();
                Assert.AreEqual("Failed to load profiles.json file", error.Message);
                Assert.IsInstanceOfType(error.Exception, expectedException);
            }

            Assert.AreEqual(input != "missing", files.Files.ContainsKey(service.ProfilesJsonFilePath));
            if (input != "missing")
            {
                Assert.AreEqual(input, files.Files[service.ProfilesJsonFilePath]);
            }

            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RealServiceReload_RawMismatchRejectsSubscribedProfileAndSerializesDisabledState(bool hasOldActive)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            fixture.Environment.User["TMP"] = "override";
            fixture.Environment.User["Unrelated"] = "kept";
            if (hasOldActive)
            {
                fixture.Environment.User["TMP_PowerToys_Old"] = "original";
                await service.WriteAsync(new[] { Profile("Old", true, "TMP", "override") });
            }
            else
            {
                await service.WriteAsync(Array.Empty<ProfileVariablesSet>());
            }

            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var old = vm.AppliedProfile;
            Assert.AreEqual(hasOldActive, old != null);
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var incoming = Profile("Incoming", true, "TMP", "requested");
            await service.WriteAsync(new[] { incoming });
            var enabledContents = files.Writes[1].Contents!;

            vm.LoadEnvironmentVariables();

            var rejected = vm.Profiles.Single();
            Assert.AreNotSame(incoming, rejected);
            Assert.AreEqual(incoming.Id, rejected.Id);
            Assert.IsFalse(rejected.IsEnabled);
            Assert.IsNull(vm.AppliedProfile);
            if (hasOldActive)
            {
                Assert.IsNotNull(old);
                Assert.IsFalse(old.IsEnabled);
            }

            Assert.AreEqual(EnvironmentState.ChangedOnStartup, vm.EnvironmentState);
            CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            Assert.AreEqual(hasOldActive ? 1 : 0, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(2, files.Writes.Count);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);
            Assert.AreEqual(enabledContents, files.Files[service.ProfilesJsonFilePath]);
            if (hasOldActive)
            {
                await fixture.Work.CompleteActionAsync(0);
            }

            fixture.Work.DrainRefreshes();
            Assert.AreEqual(hasOldActive ? "original" : "override", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Old"));
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Incoming"));
            Assert.AreEqual("kept", fixture.Environment.User["Unrelated"]);
            Assert.AreEqual(hasOldActive ? 1 : 0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(hasOldActive ? 1 : 0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(hasOldActive ? "original" : "override", vm.AppliedVariables.Single(x => x.Name == "TMP").Values);
            await fixture.Work.CompleteSaveAsync(0);
            Assert.AreEqual(3, files.Writes.Count);
            using var savedJson = JsonDocument.Parse(files.Writes[2].Contents!);
            Assert.AreEqual(1, savedJson.RootElement.GetArrayLength());
            var saved = savedJson.RootElement[0];
            Assert.AreEqual(incoming.Id, saved.GetProperty("Id").GetGuid());
            Assert.AreEqual("Incoming", saved.GetProperty("Name").GetString());
            Assert.IsFalse(saved.GetProperty("IsEnabled").GetBoolean());
            Assert.AreEqual(1, saved.GetProperty("Variables").GetArrayLength());
            Assert.AreEqual("TMP", saved.GetProperty("Variables")[0].GetProperty("Name").GetString());
            Assert.AreEqual("requested", saved.GetProperty("Variables")[0].GetProperty("Values").GetString());
            using var enabledJson = JsonDocument.Parse(enabledContents);
            Assert.AreEqual(1, enabledJson.RootElement.GetArrayLength());
            Assert.IsTrue(enabledJson.RootElement[0].GetProperty("IsEnabled").GetBoolean());
            Assert.AreEqual(EnvironmentState.ChangedOnStartup, vm.EnvironmentState);
            Assert.AreEqual(0, fixture.Logger.Entries.Count);
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task RealServiceReload_ProspectiveBackupBudgetRejectsBeforeApplyAndSavesDisabledIncoming()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            fixture.Environment.User["TMP"] = "override";
            fixture.Environment.User["TMP_PowerToys_Old"] = "original";
            await service.WriteAsync(new[] { Profile("Old", true, "TMP", "override") });
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var old = vm.AppliedProfile;
            var rawOriginal = new string('x', 32750);
            fixture.Environment.User["BUDGET"] = rawOriginal;
            var incoming = Profile("IncomingBudget", true, "BUDGET", "requested");
            await service.WriteAsync(new[] { incoming });
            fixture.Environment.Operations.Clear();

            vm.LoadEnvironmentVariables();

            Assert.IsNull(vm.AppliedProfile);
            Assert.IsFalse(old.IsEnabled);
            Assert.AreEqual(incoming.Id, vm.Profiles.Single().Id);
            Assert.IsFalse(vm.Profiles.Single().IsEnabled);
            Assert.AreEqual(EnvironmentState.ProfileNotApplicable, vm.EnvironmentState);
            Assert.AreEqual(1, fixture.Work.ActionCount);
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(2, files.Writes.Count);
            CollectionAssert.AreEqual(DisabledTelemetry, fixture.Telemetry.Enabled.ToArray());
            var error = fixture.Logger.Entries.Single();
            StringAssert.Contains(error.Message, "The variable 'BUDGET' cannot be applied");
            StringAssert.Contains(error.Message, "BUDGET_PowerToys_IncomingBudget");
            StringAssert.Contains(error.Message, "total length");
            Assert.IsNull(error.Exception);
            Assert.AreEqual(rawOriginal, fixture.Environment.User["BUDGET"]);
            Assert.AreEqual("override", fixture.Environment.User["TMP"]);

            await fixture.Work.CompleteActionAsync(0);
            fixture.Work.DrainRefreshes();
            await fixture.Work.CompleteSaveAsync(0);

            Assert.AreEqual("original", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Old"));
            Assert.AreEqual(rawOriginal, fixture.Environment.User["BUDGET"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("BUDGET_PowerToys_IncomingBudget"));
            Assert.IsFalse(fixture.Environment.Operations.Any(x => x.Name == "BUDGET" && x.Operation is "Write" or "Delete"));
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.AreEqual(1, fixture.Logger.Entries.Count);
            Assert.AreEqual(3, files.Writes.Count);
            using var savedJson = JsonDocument.Parse(files.Writes[2].Contents!);
            Assert.AreEqual(1, savedJson.RootElement.GetArrayLength());
            var saved = savedJson.RootElement[0];
            Assert.AreEqual(incoming.Id, saved.GetProperty("Id").GetGuid());
            Assert.AreEqual("IncomingBudget", saved.GetProperty("Name").GetString());
            Assert.IsFalse(saved.GetProperty("IsEnabled").GetBoolean());
            Assert.AreEqual(1, saved.GetProperty("Variables").GetArrayLength());
            Assert.AreEqual("BUDGET", saved.GetProperty("Variables")[0].GetProperty("Name").GetString());
            Assert.AreEqual("requested", saved.GetProperty("Variables")[0].GetProperty("Values").GetString());
            Assert.AreEqual(EnvironmentState.ProfileNotApplicable, vm.EnvironmentState);
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        private static ProfileVariablesSet LoadActive(EnvironmentVariablesFixture fixture, out MainViewModel vm)
        {
            var old = Profile("Old", true, "TMP", "override");
            fixture.Environment.User["TMP"] = "override";
            fixture.Service.Read = () => new() { old };
            vm = fixture.CreateViewModel();
            vm.LoadEnvironmentVariables();
            Assert.AreSame(old, vm.AppliedProfile);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            return old;
        }

        private static ProfileVariablesSet Profile(string name, bool enabled, string variableName, string value)
        {
            var profile = new ProfileVariablesSet(Guid.NewGuid(), name);
            profile.Variables.Add(new Variable(variableName, value, VariablesSetType.Profile));
            profile.IsEnabled = enabled;
            return profile;
        }
    }
}
