// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.Helpers
{
    [TestClass]
    [DoNotParallelize]
    public class EnvironmentVariablesHelperContractTests
    {
        private static readonly string[] SortedNames = { "Alpha", "Zed" };

        [TestMethod]
        [DataRow(EnvironmentVariableTarget.User, VariablesSetType.User, RegistryHive.CurrentUser, "Environment")]
        [DataRow(EnvironmentVariableTarget.Machine, VariablesSetType.System, RegistryHive.LocalMachine, @"System\CurrentControlSet\Control\Session Manager\Environment")]
        public void GetVariables_UsesReadOnlyRawScopeAndSorts(EnvironmentVariableTarget target, VariablesSetType type, RegistryHive hive, string subKey)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var environment = fixture.Environment;
            var values = hive == RegistryHive.CurrentUser ? environment.User : environment.Machine;
            values["Zed"] = "%ROOT%";
            values["Alpha"] = "raw";
            var set = new DefaultVariablesSet(Guid.NewGuid(), "Scope", type);

            EnvironmentVariablesHelper.GetVariables(target, set, environment);

            CollectionAssert.AreEqual(new[] { (hive, subKey, false) }, environment.Opens.ToArray());
            CollectionAssert.AreEqual(SortedNames, set.Variables.Select(variable => variable.Name).ToArray());
            Assert.AreEqual("%ROOT%", set.Variables[1].Values);
            Assert.IsTrue(set.Variables.All(variable => variable.ParentType == type));
            foreach (var read in environment.Operations.Where(operation => operation.Operation == "Read"))
            {
                Assert.AreEqual(((object)string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames), read.Value);
            }

            Assert.IsTrue(environment.Keys.Single().IsDisposed);
            Assert.AreEqual(0, environment.Notifications.Count);
        }

        [TestMethod]
        public void GetExisting_PrefersUserIgnoringNameCaseWithoutReadingMachine()
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["Pilot"] = "%RAW%";
            fixture.Environment.Machine["PILOT"] = "machine";

            var result = EnvironmentVariablesHelper.GetExisting("pilot", fixture.Environment);

            Assert.IsNotNull(result);
            Assert.AreEqual("Pilot", result.Name);
            Assert.AreEqual("%RAW%", result.Values);
            Assert.AreEqual(VariablesSetType.User, result.ParentType);
            Assert.IsTrue(fixture.Environment.Opens.All(open => open.Hive == RegistryHive.CurrentUser));
        }

        [TestMethod]
        [DataRow(VariablesSetType.User, RegistryHive.CurrentUser, "Environment")]
        [DataRow(VariablesSetType.Profile, RegistryHive.CurrentUser, "Environment")]
        [DataRow(VariablesSetType.System, RegistryHive.LocalMachine, @"System\CurrentControlSet\Control\Session Manager\Environment")]
        public void SetVariable_UsesWritableScopeAndNotifiesAfterDisposal(VariablesSetType type, RegistryHive hive, string subKey)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var environment = fixture.Environment;
            environment.NotifyResult = -1;
            environment.BeforeNotify = () => Assert.IsTrue(environment.Keys.All(key => key.IsDisposed));

            Assert.IsTrue(EnvironmentVariablesHelper.SetVariable(new Variable("Pilot", "raw", type), environment));

            CollectionAssert.AreEqual(new[] { (hive, subKey, true) }, environment.Opens.ToArray());
            Assert.AreEqual(hive, environment.Operations.Single(operation => operation.Operation == "Write").Hive);
            Assert.AreEqual((new IntPtr(0xffff), 0x001a, new IntPtr(0x12345), "Environment"), environment.Notifications.Single());
        }

        [TestMethod]
        [DataRow("", RegistryValueKind.String)]
        [DataRow("plain", RegistryValueKind.String)]
        [DataRow("%ROOT%", RegistryValueKind.ExpandString)]
        [DataRow("unmatched%", RegistryValueKind.ExpandString)]
        public void SetVariable_EmptyWritesAndAnyPercentInfersExpandableKind(string value, RegistryValueKind expectedKind)
        {
            using var fixture = new EnvironmentVariablesFixture();

            Assert.IsTrue(EnvironmentVariablesHelper.SetVariable(new Variable("Pilot", value, VariablesSetType.User), fixture.Environment));

            var write = fixture.Environment.Operations.Single(operation => operation.Operation == "Write");
            Assert.AreEqual(((object)value, expectedKind), write.Value);
            Assert.AreEqual(value, fixture.Environment.User["Pilot"]);
            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation == "Delete"));
        }

        [TestMethod]
        public void NullWriteAndUnset_DeleteInvalidLegacyNameWithoutValidation()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var variable = new Variable(" legacy=bad ", "old", VariablesSetType.User);
            fixture.Environment.User[variable.Name] = "old";
            variable.Values = null!;

            Assert.IsTrue(EnvironmentVariablesHelper.SetVariable(variable, fixture.Environment));
            Assert.IsTrue(EnvironmentVariablesHelper.UnsetVariable(variable, fixture.Environment));

            var deletes = fixture.Environment.Operations.Where(operation => operation.Operation == "Delete").ToArray();
            Assert.AreEqual(2, deletes.Length);
            Assert.IsTrue(deletes.All(operation => operation.Name == " legacy=bad " && Equals(false, operation.Value)));
            Assert.AreEqual(2, fixture.Environment.Notifications.Count);
            Assert.AreEqual(0, fixture.Logger.Entries.Count);
        }

        [TestMethod]
        public void InvalidNonNullWrite_DoesNotOpenKeyOrNotify()
        {
            using var fixture = new EnvironmentVariablesFixture();

            Assert.IsFalse(EnvironmentVariablesHelper.SetVariable(new Variable("bad=name", "value", VariablesSetType.User), fixture.Environment));

            Assert.AreEqual(0, fixture.Environment.Opens.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(1, fixture.Logger.Entries.Count);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void WriteFailure_ReturnsFalseLogsAndDoesNotNotify(bool missingKey)
        {
            using var fixture = new EnvironmentVariablesFixture();
            if (missingKey)
            {
                fixture.Environment.MissingHives.Add(RegistryHive.CurrentUser);
            }
            else
            {
                fixture.Environment.BeforeOperation = (operation, _, _) =>
                {
                    if (operation == "Write")
                    {
                        throw new IOException("scripted write failure");
                    }
                };
            }

            Assert.IsFalse(EnvironmentVariablesHelper.SetVariable(new Variable("Pilot", "value", VariablesSetType.User), fixture.Environment));

            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(1, fixture.Logger.Entries.Count);
            Assert.IsTrue(fixture.Environment.Keys.All(key => key.IsDisposed));
            Assert.IsFalse(fixture.Environment.User.ContainsKey("Pilot"));
        }

        [TestMethod]
        public void GetVariables_MissingKeyReturnsEmptyButReadExceptionPropagatesAndDisposes()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var set = new DefaultVariablesSet(Guid.NewGuid(), "User", VariablesSetType.User);
            fixture.Environment.MissingHives.Add(RegistryHive.CurrentUser);
            EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.User, set, fixture.Environment);
            Assert.AreEqual(0, set.Variables.Count);
            fixture.Environment.MissingHives.Clear();
            fixture.Environment.User["Pilot"] = "raw";
            var failure = new IOException("scripted read failure");
            fixture.Environment.BeforeOperation = (operation, _, _) =>
            {
                if (operation == "Read")
                {
                    throw failure;
                }
            };

            var actual = Assert.ThrowsExactly<IOException>(() => EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.User, set, fixture.Environment));

            Assert.AreSame(failure, actual);
            Assert.IsTrue(fixture.Environment.Keys.Single().IsDisposed);
        }

        [TestMethod]
        public void GetVariables_UnexpectedNullReadStillThrowsAndDisposes()
        {
            using var fixture = new EnvironmentVariablesFixture();
            fixture.Environment.User["Pilot"] = null;
            var set = new DefaultVariablesSet(Guid.NewGuid(), "User", VariablesSetType.User);

            Assert.ThrowsExactly<NullReferenceException>(() => EnvironmentVariablesHelper.GetVariables(EnvironmentVariableTarget.User, set, fixture.Environment));

            Assert.IsTrue(fixture.Environment.Keys.Single().IsDisposed);
        }

        [TestMethod]
        [DataRow(VariablesSetType.Path)]
        [DataRow(VariablesSetType.Duplicate)]
        public void UnsupportedIndividualScope_ThrowsWithoutEffects(VariablesSetType type)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var variable = new Variable("Pilot", "raw", type);

            Assert.ThrowsExactly<NotImplementedException>(() => EnvironmentVariablesHelper.SetVariable(variable, fixture.Environment));
            Assert.ThrowsExactly<NotImplementedException>(() => EnvironmentVariablesHelper.UnsetVariable(variable, fixture.Environment));

            Assert.AreEqual(0, fixture.Environment.Opens.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
        }

        [TestMethod]
        public void BackupWrite_ExemptsAuthoringLengthAndAlwaysUsesUserWithoutNotify()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var backup = new Variable(new string('B', 260), "%RAW%", VariablesSetType.System);

            Assert.IsTrue(EnvironmentVariablesHelper.SetBackupVariableWithoutNotify(backup, fixture.Environment));
            Assert.IsTrue(EnvironmentVariablesHelper.UnsetBackupVariableWithoutNotify(backup, fixture.Environment));

            Assert.IsTrue(fixture.Environment.Opens.All(open => open.Hive == RegistryHive.CurrentUser && open.Writable));
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.IsFalse(fixture.Environment.User.ContainsKey(backup.Name));
        }
    }
}
