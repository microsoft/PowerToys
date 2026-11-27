// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.Models
{
    [TestClass]
    [DoNotParallelize]
    public class ProfileVariablesSetTests
    {
        private static readonly string[] BackupAndVariableNames = { "TMP_PowerToys_Test_profile_1", "TMP" };
        private static readonly string[] VariableNames = { "TMP" };

        [TestMethod]
        public void Clone_SeparatesProfileAndCollectionButRetainsSharedVariableItems()
        {
            var variable = new Variable("TMP", "original", VariablesSetType.Profile);
            var original = CreateProfile(variable);
            original.IsEnabled = true;
            var originalName = original.Name;

            var clone = original.Clone();

            Assert.AreEqual(original.Id, clone.Id);
            Assert.AreEqual(original.Name, clone.Name);
            Assert.IsTrue(clone.IsEnabled);
            Assert.AreNotSame(original.Variables, clone.Variables);
            Assert.AreSame(variable, clone.Variables.Single());

            clone.Name = "Changed";
            clone.IsEnabled = false;
            clone.Variables.Add(new Variable("OTHER", "added", VariablesSetType.Profile));
            clone.Variables[0].Values = "shared-change";

            Assert.AreEqual(originalName, original.Name);
            Assert.IsTrue(original.IsEnabled);
            Assert.AreEqual(1, original.Variables.Count);
            Assert.AreEqual("shared-change", original.Variables[0].Values);
        }

        [TestMethod]
        public async Task Apply_OverwritesPriorBackupBeforeUserOverrideEvenForSystemOrigin()
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["TMP"] = "original%ROOT%";
            fixture.Environment.User["TMP_PowerToys_Test_profile_1"] = "older-backup";
            fixture.Environment.Machine["TMP"] = "machine";
            var profile = CreateProfile(new Variable("TMP", "test_TMP", VariablesSetType.System));

            await profile.Apply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            var writes = fixture.Environment.Operations.Where(operation => operation.Operation == "Write").ToArray();
            CollectionAssert.AreEqual(BackupAndVariableNames, writes.Select(write => write.Name).ToArray());
            Assert.AreEqual(((object)"original%ROOT%", RegistryValueKind.ExpandString), writes[0].Value);
            Assert.AreEqual(((object)"test_TMP", RegistryValueKind.String), writes[1].Value);
            Assert.IsTrue(writes.All(write => write.Hive == RegistryHive.CurrentUser));
            Assert.AreEqual("original%ROOT%", fixture.Environment.User["TMP_PowerToys_Test_profile_1"]);
            Assert.AreEqual("machine", fixture.Environment.Machine["TMP"]);
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.IsTrue(fixture.Environment.Keys.All(key => key.IsDisposed));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Apply_AbsentOrMachineOnlyCollisionDoesNotCreateUserBackup(bool machineCollision)
        {
            using var fixture = new EnvironmentVariablesFixture();
            if (machineCollision)
            {
                fixture.Environment.Machine["TMP"] = "machine";
            }

            var profile = CreateProfile(new Variable("TMP", "profile", VariablesSetType.Profile));

            await profile.Apply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            CollectionAssert.AreEqual(VariableNames, fixture.Environment.Operations.Where(operation => operation.Operation == "Write").Select(operation => operation.Name).ToArray());
            Assert.AreEqual("profile", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Test_profile_1"));
            Assert.AreEqual(machineCollision, fixture.Environment.Machine.ContainsKey("TMP"));
            if (machineCollision)
            {
                Assert.AreEqual("machine", fixture.Environment.Machine["TMP"]);
            }
        }

        [TestMethod]
        [DataRow("TMP_PowerToys_Test_profile_1", "Failed to set backup variable.")]
        [DataRow("TMP", "Failed to set profile variable.")]
        public async Task Apply_FailedConstituentLogsContinuesAndNotifiesOnce(string failingName, string expectedLog)
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["TMP"] = "original";
            fixture.Environment.BeforeOperation = (operation, _, name) =>
            {
                if (operation == "Write" && name == failingName)
                {
                    throw new IOException("scripted write failure");
                }
            };
            var profile = CreateProfile(
                new Variable("TMP", "override", VariablesSetType.Profile),
                new Variable("NEXT", "next", VariablesSetType.System));

            await profile.Apply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            Assert.AreEqual("next", fixture.Environment.User["NEXT"]);
            Assert.AreEqual(failingName == "TMP" ? "original" : "override", fixture.Environment.User["TMP"]);
            Assert.IsTrue(fixture.Logger.Entries.Any(entry => entry.Message == expectedLog));
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.IsTrue(fixture.Environment.Keys.All(key => key.IsDisposed));
        }

        [TestMethod]
        public async Task EmptyBatch_ApplyAndUnapplyEachNotifyOnce()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var profile = CreateProfile();

            await profile.Apply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);
            await profile.UnApply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            Assert.AreEqual(0, fixture.Environment.Opens.Count);
            Assert.AreEqual(2, fixture.Environment.Notifications.Count);
        }

        [TestMethod]
        public async Task Apply_AllWritesRejectedByValidationStillContinuesAndNotifiesOnce()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var profile = CreateProfile(
                new Variable("FIRST", "invalid\0value", VariablesSetType.Profile),
                new Variable("SECOND", "invalid\0value", VariablesSetType.Profile));

            await profile.Apply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            Assert.AreEqual(0, fixture.Environment.User.Count);
            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation == "Write"));
            Assert.AreEqual(2, fixture.Logger.Entries.Count(entry => entry.Message == "Failed to set profile variable."));
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.IsTrue(fixture.Environment.Keys.All(key => key.IsDisposed));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Unapply_DeletesOverrideThenBackupThenRestoresRawText(bool singleVariable)
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["TMP"] = "override";
            fixture.Environment.User["TMP_PowerToys_Test_profile_1"] = "original%ROOT%";
            var profile = CreateProfile(new Variable("TMP", "override", VariablesSetType.Profile));

            if (singleVariable)
            {
                profile.UnapplyVariable(profile.Variables[0], fixture.Environment);
            }
            else
            {
                await profile.UnApply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);
            }

            var mutations = fixture.Environment.Operations.Where(operation => operation.Operation is "Delete" or "Write").ToArray();
            CollectionAssert.AreEqual(new[] { ("Delete", "TMP"), ("Delete", "TMP_PowerToys_Test_profile_1"), ("Write", "TMP") }, mutations.Select(operation => (operation.Operation, operation.Name)).ToArray());
            Assert.IsTrue(mutations.All(operation => operation.Hive == RegistryHive.CurrentUser));
            var deleteOverride = fixture.Environment.Operations.FindIndex(operation => operation.Operation == "Delete" && operation.Name == "TMP");
            var readBackup = fixture.Environment.Operations.FindIndex(operation => operation.Operation == "Read" && operation.Name == "TMP_PowerToys_Test_profile_1");
            var deleteBackup = fixture.Environment.Operations.FindIndex(operation => operation.Operation == "Delete" && operation.Name == "TMP_PowerToys_Test_profile_1");
            Assert.IsTrue(deleteOverride < readBackup && readBackup < deleteBackup);
            Assert.AreEqual(((object)"original%ROOT%", RegistryValueKind.ExpandString), mutations[2].Value);
            Assert.AreEqual("original%ROOT%", fixture.Environment.User["TMP"]);
            Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Test_profile_1"));
            Assert.AreEqual(singleVariable ? 0 : 1, fixture.Environment.Notifications.Count);
        }

        [TestMethod]
        [DataRow("Delete", "TMP", "Failed to unset variable.")]
        [DataRow("Delete", "TMP_PowerToys_Test_profile_1", "Failed to unset backup variable.")]
        [DataRow("Write", "TMP", "Failed to restore backup variable.")]
        public async Task Unapply_FailedConstituentDoesNotAbortRestorationOrNextVariable(string failingOperation, string failingName, string expectedLog)
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["TMP"] = "override";
            fixture.Environment.User["TMP_PowerToys_Test_profile_1"] = "original";
            fixture.Environment.User["NEXT"] = "next";
            fixture.Environment.BeforeOperation = (operation, _, name) =>
            {
                if (operation == failingOperation && name == failingName)
                {
                    throw new IOException("scripted constituent failure");
                }
            };
            var profile = CreateProfile(new Variable("TMP", "override", VariablesSetType.Profile), new Variable("NEXT", "next", VariablesSetType.Profile));

            await profile.UnApply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            Assert.IsFalse(fixture.Environment.User.ContainsKey("NEXT"));
            Assert.IsTrue(fixture.Environment.Operations.Any(operation => operation.Operation == "Write" && operation.Name == "TMP"));
            Assert.AreEqual(failingOperation == "Delete" && failingName.Contains("_PowerToys_", StringComparison.Ordinal), fixture.Environment.User.ContainsKey("TMP_PowerToys_Test_profile_1"));
            if (failingOperation == "Write")
            {
                Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP"));
            }
            else
            {
                Assert.AreEqual("original", fixture.Environment.User["TMP"]);
            }

            Assert.IsTrue(fixture.Logger.Entries.Any(entry => entry.Message == expectedLog));
            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
            Assert.IsTrue(fixture.Environment.Keys.All(key => key.IsDisposed));
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task Unapply_MissingBackupDoesNotRestoreAndMachineBackupStillRestoresIntoUser(bool machineBackup, bool overridePresent)
        {
            using var fixture = new EnvironmentVariablesFixture();
            if (overridePresent)
            {
                fixture.Environment.User["TMP"] = "override";
            }

            if (machineBackup)
            {
                fixture.Environment.Machine["TMP_PowerToys_Test_profile_1"] = "machine-original";
            }

            var profile = CreateProfile(new Variable("TMP", "override", VariablesSetType.System));

            await profile.UnApply(fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

            Assert.AreEqual(machineBackup, fixture.Environment.User.ContainsKey("TMP"));
            Assert.IsTrue(fixture.Environment.Operations.Where(operation => operation.Operation is "Write" or "Delete").All(operation => operation.Hive == RegistryHive.CurrentUser));
            if (machineBackup)
            {
                Assert.AreEqual("machine-original", fixture.Environment.User["TMP"]);
                Assert.AreEqual("machine-original", fixture.Environment.Machine["TMP_PowerToys_Test_profile_1"]);
            }

            Assert.AreEqual(1, fixture.Environment.Notifications.Count);
        }

        [TestMethod]
        [DataRow(true, false, "exact", true)]
        [DataRow(true, false, "EXACT", false)]
        [DataRow(true, true, "exact", false)]
        [DataRow(false, false, "exact", false)]
        public void IsCorrectlyApplied_RequiresEnabledExactRawUserValue(bool enabled, bool machineOnly, string stored, bool expected)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var values = machineOnly ? fixture.Environment.Machine : fixture.Environment.User;
            values["TMP"] = stored;
            var profile = CreateProfile(new Variable("TMP", "exact", VariablesSetType.Profile));
            profile.IsEnabled = enabled;

            Assert.AreEqual(expected, profile.IsCorrectlyApplied(fixture.Environment));

            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation is "Write" or "Delete"));
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
        }

        [TestMethod]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void IsApplicable_OnlyUserCollisionRequiresProspectiveBackupBudget(bool userCollision, bool expected)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var values = userCollision ? fixture.Environment.User : fixture.Environment.Machine;
            values["TMP"] = new string('v', 32762);
            var profile = CreateProfile(new Variable("TMP", "small", VariablesSetType.Profile));

            Assert.AreEqual(expected, profile.IsApplicable(fixture.Environment));

            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation is "Write" or "Delete"));
            Assert.AreEqual(userCollision, fixture.Logger.Entries.Any(entry => entry.Level == "Error"));
        }

        private static ProfileVariablesSet CreateProfile(params Variable[] variables)
        {
            var profile = new ProfileVariablesSet(Guid.NewGuid(), "Test_profile_1");
            foreach (var variable in variables)
            {
                profile.Variables.Add(variable);
            }

            return profile;
        }
    }
}
