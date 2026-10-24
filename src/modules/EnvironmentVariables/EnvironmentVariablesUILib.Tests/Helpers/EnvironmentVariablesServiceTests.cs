// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Converters;
using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using Microsoft.UI.Xaml;

namespace EnvironmentVariablesUILib.UnitTests.Helpers
{
    [TestClass]
    [DoNotParallelize]
    public class EnvironmentVariablesServiceTests
    {
        private static readonly string[] ProfileProperties = { "Id", "Name", "Variables", "Valid", "IsEnabled" };
        private static readonly string[] VariableProperties = { "Name", "Values", "ApplyToSystem" };
        private static readonly string[] ProfileNames = { "Empty", "Enabled" };
        private static readonly bool[] ProfileEnabled = { false, true };
        private static readonly string[] VariableNames = { "PATH", "SECOND" };

        [TestMethod]
        public async Task WriteRead_UsesCurrentSchemaAndRebuildsOrderedRawValues()
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var empty = new ProfileVariablesSet(Guid.NewGuid(), "Empty");
            var enabled = new ProfileVariablesSet(Guid.NewGuid(), "Enabled") { IsEnabled = true };
            enabled.Variables.Add(new Variable("PATH", @"%ROOT%\bin;;C:\Tools;C:\Tools;", VariablesSetType.System)
            {
                IsAppliedFromProfile = true,
                ApplyToSystem = true,
            });
            enabled.Variables.Add(new Variable("SECOND", "raw%UNEXPANDED%", VariablesSetType.Profile));
            var profiles = new List<ProfileVariablesSet> { empty, enabled };

            await service.WriteAsync(profiles);

            var write = files.Writes.Single();
            Assert.AreEqual(service.ProfilesJsonFilePath, write.Path);
            Assert.AreEqual(CancellationToken.None, write.CancellationToken);
            Assert.IsTrue(write.Contents!.Contains('\n'));
            using (var json = JsonDocument.Parse(write.Contents))
            {
                var saved = json.RootElement;
                Assert.AreEqual(JsonValueKind.Array, saved.ValueKind);
                Assert.AreEqual(2, saved.GetArrayLength());
                CollectionAssert.AreEquivalent(
                    ProfileProperties,
                    saved[1].EnumerateObject().Select(property => property.Name).ToArray());
                Assert.IsTrue(saved[1].GetProperty("Valid").GetBoolean());
                CollectionAssert.AreEquivalent(
                    VariableProperties,
                    saved[1].GetProperty("Variables")[0].EnumerateObject().Select(property => property.Name).ToArray());
                Assert.AreEqual(enabled.Variables[0].Values, saved[1].GetProperty("Variables")[0].GetProperty("Values").GetString());
            }

            var loaded = service.ReadProfiles();

            CollectionAssert.AreEqual(profiles.Select(profile => profile.Id).ToArray(), loaded.Select(profile => profile.Id).ToArray());
            CollectionAssert.AreEqual(ProfileNames, loaded.Select(profile => profile.Name).ToArray());
            CollectionAssert.AreEqual(ProfileEnabled, loaded.Select(profile => profile.IsEnabled).ToArray());
            Assert.AreEqual(0, loaded[0].Variables.Count);
            Assert.AreEqual(VariablesSetType.Profile, loaded[1].Type);
            CollectionAssert.AreEqual(VariableNames, loaded[1].Variables.Select(variable => variable.Name).ToArray());
            CollectionAssert.AreEqual(enabled.Variables.Select(variable => variable.Values).ToArray(), loaded[1].Variables.Select(variable => variable.Values).ToArray());
            CollectionAssert.AreEqual(
                new[] { @"%ROOT%\bin", string.Empty, @"C:\Tools", @"C:\Tools", string.Empty },
                loaded[1].Variables[0].ValuesList.Select(item => item.Text).ToArray());
            Assert.IsTrue(loaded[1].Variables[0].ApplyToSystem);
            Assert.IsFalse(loaded[1].Variables[0].IsAppliedFromProfile);
            Assert.AreNotEqual(VariablesSetType.System, loaded[1].Variables[0].ParentType);
            CollectionAssert.AreEqual(
                new[] { ("WriteAllTextAsync", (string?)service.ProfilesJsonFilePath), ("Exists", service.ProfilesJsonFilePath), ("ReadAllText", service.ProfilesJsonFilePath) },
                files.Calls.ToArray());

            profiles.Clear();
            await service.WriteAsync(profiles);

            using var deleted = JsonDocument.Parse(files.Writes.Last().Contents!);
            Assert.AreEqual(0, deleted.RootElement.GetArrayLength());
            Assert.AreEqual(0, service.ReadProfiles().Count);
        }

        [TestMethod]
        public void ReadProfiles_IgnoresUnknownAndIgnoredFieldsWithoutRepairingMissingCollections()
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var id = Guid.NewGuid();
            files.Files[service.ProfilesJsonFilePath] = $$"""
                [{"Id":"{{id}}","Name":"Loaded","IsEnabled":true,"Future":{"flag":true},
                  "Type":4,"Valid":false,"Variables":[
                    {"Name":"PATH","Values":"first;;last;","ApplyToSystem":true,
                     "ParentType":4,"IsAppliedFromProfile":true,"ValuesList":[{"Text":"stale"}],"Future":42}
                  ]}]
                """;

            var profile = service.ReadProfiles().Single();

            Assert.AreEqual(id, profile.Id);
            Assert.AreEqual("Loaded", profile.Name);
            Assert.IsTrue(profile.IsEnabled);
            Assert.IsTrue(profile.Valid);
            Assert.AreEqual(VariablesSetType.Profile, profile.Type);
            Assert.IsFalse(profile.Variables.Single().IsAppliedFromProfile);
            Assert.AreNotEqual(VariablesSetType.System, profile.Variables.Single().ParentType);
            Assert.IsTrue(profile.Variables.Single().ApplyToSystem);
            CollectionAssert.AreEqual(new[] { "first", string.Empty, "last", string.Empty }, profile.Variables.Single().ValuesList.Select(item => item.Text).ToArray());

            files.Files[service.ProfilesJsonFilePath] = """[{"Name":"Missing"}]""";
            Assert.IsNull(service.ReadProfiles().Single().Variables);
            files.Files[service.ProfilesJsonFilePath] = """[{"Name":"Null","Variables":null}]""";
            Assert.IsNull(service.ReadProfiles().Single().Variables);
            files.Files[service.ProfilesJsonFilePath] = "[null]";
            Assert.IsNull(service.ReadProfiles().Single());
        }

        [TestMethod]
        public void ReadProfiles_MissingAndNullAreDistinctSuccessfulResults()
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);

            Assert.AreEqual(0, service.ReadProfiles().Count);
            CollectionAssert.AreEqual(new[] { ("Exists", (string?)service.ProfilesJsonFilePath) }, files.Calls.ToArray());

            files.Files[service.ProfilesJsonFilePath] = "null";
            Assert.IsNull(service.ReadProfiles());
            files.Files[service.ProfilesJsonFilePath] = "[]";
            Assert.AreEqual(0, service.ReadProfiles().Count);
        }

        [TestMethod]
        [DataRow("{")]
        [DataRow("{}")]
        [DataRow("42")]
        [DataRow("""[{"Name":"Wrong","Variables":{}}]""")]
        public void ReadProfiles_MalformedOrWrongShapePropagatesJsonException(string text)
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            files.Files[service.ProfilesJsonFilePath] = text;

            Assert.ThrowsExactly<JsonException>(() => service.ReadProfiles());
        }

        [TestMethod]
        [DataRow("""[{"Name":"MissingValue","Variables":[{"Name":"PATH"}]}]""")]
        [DataRow("""[{"Name":"NullValue","Variables":[{"Name":"PATH","Values":null}]}]""")]
        public void ReadProfiles_MissingOrNullRawValuesPropagatesDeserializationCallbackFailure(string text)
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            files.Files[service.ProfilesJsonFilePath] = text;

            Assert.ThrowsExactly<NullReferenceException>(() => service.ReadProfiles());
        }

        [TestMethod]
        public async Task ScheduledInactiveRemoval_FailedSaveThenReloadRestoresDeletedVariable()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            const string original = """
                [{"Id":"031782d2-f6c5-4e62-8454-9f729fbdc5f1","Name":"Inactive","IsEnabled":false,
                  "Variables":[{"Name":"KEEP","Values":"%RAW%;;"},{"Name":"REMOVE","Values":"gone"}]}]
                """;
            files.Files[service.ProfilesJsonFilePath] = original;
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var profile = vm.Profiles.Single();
            var kept = profile.Variables[0];
            var removed = profile.Variables[1];
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var failure = new IOException("scripted scheduled write failure");
            files.WriteFailure = failure;

            vm.DeleteVariable(removed, profile);

            Assert.AreSame(kept, profile.Variables.Single());
            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, files.Writes.Count);
            Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);

            await fixture.Work.CompleteSaveAsync(0);

            var attempted = files.Writes.Single();
            var attemptedText = attempted.Contents!;
            Assert.AreEqual(service.ProfilesJsonFilePath, attempted.Path);
            Assert.AreEqual(CancellationToken.None, attempted.CancellationToken);
            using (var json = JsonDocument.Parse(attemptedText))
            {
                Assert.AreEqual(JsonValueKind.Array, json.RootElement.ValueKind);
                Assert.AreEqual(1, json.RootElement.GetArrayLength());
                var savedProfile = json.RootElement.EnumerateArray().Single();
                Assert.AreEqual(profile.Id, savedProfile.GetProperty("Id").GetGuid());
                Assert.AreEqual("Inactive", savedProfile.GetProperty("Name").GetString());
                Assert.IsFalse(savedProfile.GetProperty("IsEnabled").GetBoolean());
                var savedVariables = savedProfile.GetProperty("Variables");
                Assert.AreEqual(JsonValueKind.Array, savedVariables.ValueKind);
                Assert.AreEqual(1, savedVariables.GetArrayLength());
                var savedVariable = savedVariables.EnumerateArray().Single();
                Assert.AreEqual("KEEP", savedVariable.GetProperty("Name").GetString());
                Assert.AreEqual("%RAW%;;", savedVariable.GetProperty("Values").GetString());
            }

            var error = fixture.Logger.Entries.Single();
            Assert.AreEqual("Error", error.Level);
            Assert.AreEqual("Failed to save to profiles.json file", error.Message);
            Assert.AreSame(failure, error.Exception);
            Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);
            Assert.AreSame(profile, vm.Profiles.Single());
            Assert.AreSame(kept, profile.Variables.Single());
            Assert.IsFalse(profile.IsEnabled);
            Assert.IsNull(vm.AppliedProfile);
            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);

            await fixture.Work.SettleAsync();

            Assert.AreEqual(1, fixture.Work.SaveCount);
            Assert.AreEqual(1, files.Writes.Count);
            Assert.AreEqual(attemptedText, files.Writes.Single().Contents);
            Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);
            Assert.IsTrue(fixture.Work.IsQuiescent);

            var profilesBeforeReload = vm.Profiles;
            var callsBeforeReload = files.Calls.Count;
            var savesBeforeReload = fixture.Work.SaveCount;
            var actionsBeforeReload = fixture.Work.ActionCount;
            var telemetryBeforeReload = fixture.Telemetry.Changed.Count;

            vm.LoadEnvironmentVariables();

            Assert.AreEqual(1, vm.Profiles.Count);
            var reloaded = vm.Profiles[0];
            Assert.AreEqual(2, reloaded.Variables.Count);
            Assert.AreNotSame(profilesBeforeReload, vm.Profiles);
            Assert.AreNotSame(profile, reloaded);
            Assert.AreSame(profile, profilesBeforeReload.Single());
            Assert.AreEqual((profile.Id, "Inactive", false), (reloaded.Id, reloaded.Name, reloaded.IsEnabled));
            Assert.AreNotSame(profile.Variables, reloaded.Variables);
            Assert.AreSame(kept, profile.Variables.Single());
            Assert.AreNotSame(kept, reloaded.Variables[0]);
            Assert.AreNotSame(removed, reloaded.Variables[1]);
            Assert.AreEqual(
                ("KEEP", "%RAW%;;", VariablesSetType.Profile),
                (reloaded.Variables[0].Name, reloaded.Variables[0].Values, reloaded.Variables[0].ParentType));
            Assert.AreEqual(
                ("REMOVE", "gone", VariablesSetType.Profile),
                (reloaded.Variables[1].Name, reloaded.Variables[1].Values, reloaded.Variables[1].ParentType));
            CollectionAssert.AreEqual(
                new[] { ("Exists", (string?)service.ProfilesJsonFilePath), ("ReadAllText", service.ProfilesJsonFilePath) },
                files.Calls.Skip(callsBeforeReload).ToArray());
            Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);
            Assert.AreEqual(attemptedText, files.Writes.Single().Contents);
            Assert.AreSame(failure, files.WriteFailure);
            Assert.AreEqual(error, fixture.Logger.Entries.Single());
            Assert.AreEqual(savesBeforeReload, fixture.Work.SaveCount);
            Assert.AreEqual(actionsBeforeReload, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(telemetryBeforeReload, fixture.Telemetry.Changed.Count);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation == "Write" || operation.Operation == "Delete"));
            Assert.AreEqual(0, fixture.Environment.User.Count);
            Assert.AreEqual(0, fixture.Environment.Machine.Count);
            Assert.IsNull(vm.AppliedProfile);
            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task ScheduledDisabledRemovals_StaleWriteThenReloadRestoresRemovedProfile()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            const string original = """
                [{"Id":"131782d2-f6c5-4e62-8454-9f729fbdc5f1","Name":"First","IsEnabled":false,
                  "Variables":[{"Name":"PATH","Values":"%ROOT%;;C:\\Tools;"}]},
                 {"Id":"231782d2-f6c5-4e62-8454-9f729fbdc5f1","Name":"Middle","IsEnabled":false,"Variables":[]},
                 {"Id":"331782d2-f6c5-4e62-8454-9f729fbdc5f1","Name":"Last","IsEnabled":false,
                  "Variables":[{"Name":"FINAL","Values":"raw%LAST%"}]}]
                """;
            files.Files[service.ProfilesJsonFilePath] = original;
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var first = vm.Profiles[0];
            var middle = vm.Profiles[1];
            var last = vm.Profiles[2];
            var timeout = TimeSpan.FromSeconds(5);
            var entered = new[]
            {
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
            };
            var release = new[]
            {
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            };
            var ioTasks = new List<Task>();
            var callbackCount = 0;
            Task? olderSave = null;
            Task? newerSave = null;
            files.WriteAllTextAsyncCallback = (path, contents, _) =>
            {
                var index = callbackCount++;
                var task = PublishAsync(index, path, contents!);
                ioTasks.Add(task);
                return task;
            };

            async Task PublishAsync(int index, string path, string contents)
            {
                entered[index].SetResult(contents);
                await release[index].Task.WaitAsync(timeout).ConfigureAwait(false);
                files.Files[path] = contents;
            }

            try
            {
                vm.RemoveProfile(middle);
                Assert.AreEqual(1, fixture.Work.SaveCount);
                olderSave = fixture.Work.CompleteSaveAsync(0);
                var olderText = await entered[0].Task.WaitAsync(timeout);
                Assert.IsFalse(olderSave.IsCompleted);
                Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);

                vm.RemoveProfile(first);
                Assert.AreEqual(2, fixture.Work.SaveCount);
                newerSave = fixture.Work.CompleteSaveAsync(1);
                var newerText = await entered[1].Task.WaitAsync(timeout);
                Assert.IsFalse(newerSave.IsCompleted);
                Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);
                Assert.AreEqual(2, callbackCount);
                Assert.AreEqual(2, files.Writes.Count);
                Assert.AreEqual(olderText, files.Writes[0].Contents);
                Assert.AreEqual(newerText, files.Writes[1].Contents);
                Assert.IsTrue(files.Writes.All(write => write.Path == service.ProfilesJsonFilePath && write.CancellationToken == CancellationToken.None));
                using (var olderJson = JsonDocument.Parse(olderText))
                using (var newerJson = JsonDocument.Parse(newerText))
                {
                    Assert.AreEqual(JsonValueKind.Array, olderJson.RootElement.ValueKind);
                    Assert.AreEqual(2, olderJson.RootElement.GetArrayLength());
                    Assert.AreEqual(JsonValueKind.Array, newerJson.RootElement.ValueKind);
                    Assert.AreEqual(1, newerJson.RootElement.GetArrayLength());
                    var olderProfiles = olderJson.RootElement.EnumerateArray().ToArray();
                    var newerProfile = newerJson.RootElement.EnumerateArray().Single();
                    var expectedOlderIds = new[] { first.Id, last.Id };
                    CollectionAssert.AreEqual(expectedOlderIds, olderProfiles.Select(profile => profile.GetProperty("Id").GetGuid()).ToArray());
                    Assert.AreEqual("First", olderProfiles[0].GetProperty("Name").GetString());
                    Assert.AreEqual("Last", olderProfiles[1].GetProperty("Name").GetString());
                    Assert.IsTrue(olderProfiles.All(profile => !profile.GetProperty("IsEnabled").GetBoolean()));
                    var olderVariables = olderProfiles[0].GetProperty("Variables");
                    Assert.AreEqual(JsonValueKind.Array, olderVariables.ValueKind);
                    Assert.AreEqual(1, olderVariables.GetArrayLength());
                    Assert.AreEqual(@"%ROOT%;;C:\Tools;", olderVariables[0].GetProperty("Values").GetString());
                    Assert.AreEqual(last.Id, newerProfile.GetProperty("Id").GetGuid());
                    Assert.AreEqual("Last", newerProfile.GetProperty("Name").GetString());
                    Assert.IsFalse(newerProfile.GetProperty("IsEnabled").GetBoolean());
                    var newerVariables = newerProfile.GetProperty("Variables");
                    Assert.AreEqual(JsonValueKind.Array, newerVariables.ValueKind);
                    Assert.AreEqual(1, newerVariables.GetArrayLength());
                    Assert.AreEqual("FINAL", newerVariables[0].GetProperty("Name").GetString());
                    Assert.AreEqual("raw%LAST%", newerVariables[0].GetProperty("Values").GetString());
                }

                release[1].SetResult(true);
                await newerSave.WaitAsync(timeout);
                Assert.AreEqual(newerText, files.Files[service.ProfilesJsonFilePath]);
                Assert.IsFalse(olderSave.IsCompleted);

                release[0].SetResult(true);
                await olderSave.WaitAsync(timeout);
                Assert.AreEqual(olderText, files.Files[service.ProfilesJsonFilePath]);
                Assert.AreNotEqual(newerText, files.Files[service.ProfilesJsonFilePath]);
                Assert.AreSame(last, vm.Profiles.Single());
                Assert.AreEqual(olderText, files.Writes[0].Contents);
                Assert.AreEqual(newerText, files.Writes[1].Contents);
                Assert.AreEqual(0, fixture.Logger.Entries.Count);
                Assert.AreEqual(0, fixture.Work.ActionCount);
                Assert.AreEqual(0, fixture.Environment.Notifications.Count);
                Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
                Assert.IsNull(vm.AppliedProfile);
                Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
                Assert.IsTrue(fixture.Work.IsQuiescent);
            }
            finally
            {
                release[0].TrySetResult(true);
                release[1].TrySetResult(true);
                try
                {
                    var pending = ioTasks.Concat(new[] { olderSave, newerSave }.OfType<Task>()).ToArray();
                    await Task.WhenAll(pending).WaitAsync(timeout).ConfigureAwait(false);
                }
                finally
                {
                    await fixture.Work.SettleAsync().ConfigureAwait(false);
                }
            }

            Assert.AreEqual(2, fixture.Work.SaveCount);
            Assert.AreEqual(2, files.Writes.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);

            var profilesBeforeReload = vm.Profiles;
            var backingBeforeReload = files.Files[service.ProfilesJsonFilePath];
            var callsBeforeReload = files.Calls.Count;
            var savesBeforeReload = fixture.Work.SaveCount;
            var actionsBeforeReload = fixture.Work.ActionCount;
            var telemetryBeforeReload = fixture.Telemetry.Changed.Count;

            vm.LoadEnvironmentVariables();

            Assert.AreEqual(2, vm.Profiles.Count);
            Assert.AreNotSame(profilesBeforeReload, vm.Profiles);
            CollectionAssert.AreEqual(new[] { first.Id, last.Id }, vm.Profiles.Select(profile => profile.Id).ToArray());
            var reloadedFirst = vm.Profiles[0];
            var reloadedLast = vm.Profiles[1];
            Assert.AreNotSame(first, reloadedFirst);
            Assert.AreNotSame(last, reloadedLast);
            Assert.AreSame(last, profilesBeforeReload.Single());
            Assert.AreEqual(("First", false), (reloadedFirst.Name, reloadedFirst.IsEnabled));
            Assert.AreEqual(("Last", false), (reloadedLast.Name, reloadedLast.IsEnabled));
            Assert.AreEqual(1, reloadedFirst.Variables.Count);
            Assert.AreEqual(1, reloadedLast.Variables.Count);
            Assert.AreEqual(
                ("PATH", @"%ROOT%;;C:\Tools;", VariablesSetType.Profile),
                (reloadedFirst.Variables[0].Name, reloadedFirst.Variables[0].Values, reloadedFirst.Variables[0].ParentType));
            Assert.AreEqual(
                ("FINAL", "raw%LAST%", VariablesSetType.Profile),
                (reloadedLast.Variables[0].Name, reloadedLast.Variables[0].Values, reloadedLast.Variables[0].ParentType));
            CollectionAssert.AreEqual(
                new[] { ("Exists", (string?)service.ProfilesJsonFilePath), ("ReadAllText", service.ProfilesJsonFilePath) },
                files.Calls.Skip(callsBeforeReload).ToArray());
            Assert.AreEqual(backingBeforeReload, files.Files[service.ProfilesJsonFilePath]);
            Assert.AreEqual(files.Writes[0].Contents, backingBeforeReload);
            Assert.AreNotEqual(files.Writes[1].Contents, backingBeforeReload);
            Assert.AreEqual(2, files.Writes.Count);
            Assert.AreEqual(2, callbackCount);
            Assert.AreEqual(savesBeforeReload, fixture.Work.SaveCount);
            Assert.AreEqual(actionsBeforeReload, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(telemetryBeforeReload, fixture.Telemetry.Changed.Count);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation == "Write" || operation.Operation == "Delete"));
            Assert.AreEqual(0, fixture.Environment.User.Count);
            Assert.AreEqual(0, fixture.Environment.Machine.Count);
            Assert.IsNull(vm.AppliedProfile);
            Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
            Assert.AreEqual(0, fixture.Logger.Entries.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task Reload_FileReadFailureThenRetryReconcilesActiveProfileAndWarning()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var authored = new ProfileVariablesSet(Guid.NewGuid(), "Recovered") { IsEnabled = true };
            authored.Variables.Add(new Variable("TMP", "%ACTIVE%;;", VariablesSetType.Profile));
            var expectedUser = new[]
            {
                ("TMP", "%ACTIVE%;;"),
                ("TMP_PowerToys_Recovered", "%ORIGINAL%;;"),
                ("Unrelated", "%USER%;"),
            };
            foreach (var (name, value) in expectedUser)
            {
                fixture.Environment.User[name] = value;
            }

            fixture.Environment.Machine["MachineOnly"] = "%MACHINE%;";
            await service.WriteAsync(new[] { authored });
            var payload = files.Files[service.ProfilesJsonFilePath];
            var vm = fixture.CreateViewModel(service: service);
            vm.LoadEnvironmentVariables();
            var originalProfiles = vm.Profiles;
            var originalActive = vm.Profiles.Single();
            var originalChild = originalActive.Variables.Single();
            Assert.AreSame(originalActive, vm.AppliedProfile);
            Assert.IsTrue(originalActive.IsEnabled);
            vm.EnvironmentState = EnvironmentState.EnvironmentMessageReceived;
            var failure = new IOException("scripted file read failure before retry");
            files.ReadFailure = failure;
            var callsBeforeFault = files.Calls.Count;
            Assert.AreEqual(3, callsBeforeFault);

            vm.LoadEnvironmentVariables();

            Assert.AreEqual(EnvironmentState.EnvironmentMessageReceived, vm.EnvironmentState);
            Assert.AreSame(originalActive, vm.AppliedProfile);
            Assert.IsTrue(originalActive.IsEnabled);
            Assert.AreSame(originalChild, originalActive.Variables.Single());
            Assert.AreEqual(0, vm.Profiles.Count);
            Assert.AreNotSame(originalProfiles, vm.Profiles);
            var failedProfiles = vm.Profiles;
            var error = fixture.Logger.Entries.Single();
            Assert.AreEqual(("Error", "Failed to load profiles.json file"), (error.Level, error.Message));
            Assert.AreSame(failure, error.Exception);
            Assert.AreSame(failure, files.ReadFailure);
            CollectionAssert.AreEqual(
                new[] { ("Exists", (string?)service.ProfilesJsonFilePath), ("ReadAllText", service.ProfilesJsonFilePath) },
                files.Calls.Skip(callsBeforeFault).ToArray());
            Assert.AreEqual(1, files.Files.Count);
            Assert.AreEqual(payload, files.Files[service.ProfilesJsonFilePath]);
            Assert.AreEqual(1, files.Writes.Count);
            CollectionAssert.AreEquivalent(expectedUser, fixture.Environment.User.Select(entry => (entry.Key, (string)entry.Value!)).ToArray());
            Assert.AreEqual(1, fixture.Environment.Machine.Count);
            Assert.AreEqual("%MACHINE%;", fixture.Environment.Machine["MachineOnly"]);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation is "Write" or "Delete"));
            Assert.IsTrue(fixture.Work.IsQuiescent);
            var boolean = new EnvironmentStateToBoolConverter();
            var visibility = new EnvironmentStateToVisibilityConverter();
            var messageKeys = new List<string>();
            var titleKeys = new List<string>();
            Func<string, string> messageResource = key =>
            {
                messageKeys.Add(key);
                return "message:" + key;
            };
            Func<string, string> titleResource = key =>
            {
                titleKeys.Add(key);
                return "title:" + key;
            };
            Assert.AreEqual(true, boolean.Convert(vm.EnvironmentState, typeof(bool), null, string.Empty));
            Assert.AreEqual(Visibility.Visible, visibility.Convert(vm.EnvironmentState, typeof(Visibility), null, string.Empty));
            Assert.AreEqual("message:StateNotUpToDateEnvironmentMessageReceivedMsg", EnvironmentStateToMessageConverter.ConvertState(vm.EnvironmentState, messageResource));
            Assert.AreEqual("StateNotUpToDateEnvironmentMessageReceivedMsg", messageKeys.Single());
            Assert.AreEqual("title:StateNotUpToDateTitle", EnvironmentStateToTitleConverter.ConvertState(vm.EnvironmentState, titleResource));
            Assert.AreEqual("StateNotUpToDateTitle", titleKeys.Single());
            Assert.IsTrue(vm.IsInfoBarButtonVisible);
            messageKeys.Clear();
            titleKeys.Clear();
            var callsBeforeRetry = files.Calls.Count;
            files.ReadFailure = null;

            vm.LoadEnvironmentVariables();

            Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
            var recovered = vm.Profiles.Single();
            Assert.AreSame(recovered, vm.AppliedProfile);
            Assert.AreNotSame(originalActive, recovered);
            Assert.AreNotSame(authored, recovered);
            Assert.AreNotSame(originalProfiles, vm.Profiles);
            Assert.AreNotSame(failedProfiles, vm.Profiles);
            Assert.AreEqual((authored.Id, "Recovered", true), (recovered.Id, recovered.Name, recovered.IsEnabled));
            Assert.AreNotSame(originalActive.Variables, recovered.Variables);
            var child = recovered.Variables.Single();
            Assert.AreNotSame(originalChild, child);
            Assert.AreEqual(("TMP", "%ACTIVE%;;", VariablesSetType.Profile), (child.Name, child.Values, child.ParentType));
            Assert.AreSame(originalChild, originalActive.Variables.Single());
            Assert.IsTrue(originalActive.IsEnabled);
            Assert.IsNull(files.ReadFailure);
            Assert.AreEqual(false, boolean.Convert(vm.EnvironmentState, typeof(bool), null, string.Empty));
            Assert.AreEqual(Visibility.Collapsed, visibility.Convert(vm.EnvironmentState, typeof(Visibility), null, string.Empty));
            Assert.AreEqual(string.Empty, EnvironmentStateToMessageConverter.ConvertState(vm.EnvironmentState, messageResource));
            Assert.AreEqual(0, messageKeys.Count);
            Assert.AreEqual("title:StateNotUpToDateTitle", EnvironmentStateToTitleConverter.ConvertState(vm.EnvironmentState, titleResource));
            Assert.AreEqual("StateNotUpToDateTitle", titleKeys.Single());
            Assert.IsFalse(vm.IsInfoBarButtonVisible);
            CollectionAssert.AreEqual(
                new[] { ("Exists", (string?)service.ProfilesJsonFilePath), ("ReadAllText", service.ProfilesJsonFilePath) },
                files.Calls.Skip(callsBeforeRetry).ToArray());
            Assert.AreEqual(1, files.Files.Count);
            Assert.AreEqual(payload, files.Files[service.ProfilesJsonFilePath]);
            Assert.AreEqual(payload, files.Writes.Single().Contents);
            CollectionAssert.AreEquivalent(expectedUser, fixture.Environment.User.Select(entry => (entry.Key, (string)entry.Value!)).ToArray());
            Assert.AreEqual(1, fixture.Environment.Machine.Count);
            Assert.AreEqual("%MACHINE%;", fixture.Environment.Machine["MachineOnly"]);
            Assert.AreEqual(error, fixture.Logger.Entries.Single());
            Assert.AreSame(failure, fixture.Logger.Entries.Single().Exception);
            Assert.AreEqual(0, fixture.Work.ActionCount);
            Assert.AreEqual(0, fixture.Work.SaveCount);
            Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
            Assert.AreEqual(0, fixture.Environment.Notifications.Count);
            Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation is "Write" or "Delete"));
            Assert.AreEqual(0, fixture.Service.Writes.Count);
            Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
            Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
            Assert.IsTrue(fixture.Work.IsQuiescent);
        }

        [TestMethod]
        public async Task WriteAsync_EnumerationFailurePropagatesWithoutFileAccess()
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            const string original = "[]";
            files.Files[service.ProfilesJsonFilePath] = original;
            var failure = new InvalidOperationException("scripted profile enumeration failure");

            var observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.WriteAsync(ProfilesWithEnumerationFailure()));

            Assert.AreSame(failure, observed);
            Assert.AreEqual(0, files.Calls.Count);
            Assert.AreEqual(0, files.Writes.Count);
            Assert.AreEqual(1, files.Files.Count);
            Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);

            IEnumerable<ProfileVariablesSet> ProfilesWithEnumerationFailure()
            {
                yield return new ProfileVariablesSet(Guid.NewGuid(), "Unsaved");
                throw failure;
            }
        }

        [TestMethod]
        public async Task ReadWrite_FileFaultsPropagateOriginalExceptionWithoutRetryOrReplacement()
        {
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            const string original = "[]";
            files.Files[service.ProfilesJsonFilePath] = original;
            var readFailure = new IOException("scripted read failure");
            files.ReadFailure = readFailure;

            Assert.AreSame(readFailure, Assert.ThrowsExactly<IOException>(() => service.ReadProfiles()));
            CollectionAssert.AreEqual(
                new[] { ("Exists", (string?)service.ProfilesJsonFilePath), ("ReadAllText", service.ProfilesJsonFilePath) },
                files.Calls.ToArray());

            var writeFailure = new IOException("scripted write failure");
            files.WriteFailure = writeFailure;
            var observed = await Assert.ThrowsExactlyAsync<IOException>(() => service.WriteAsync(new[] { new ProfileVariablesSet(Guid.NewGuid(), "Unsaved") }));

            Assert.AreSame(writeFailure, observed);
            Assert.AreEqual(1, files.Writes.Count);
            Assert.AreEqual("WriteAllTextAsync", files.Calls.Last().Operation);
            Assert.AreEqual(3, files.Calls.Count);
            Assert.AreEqual(original, files.Files[service.ProfilesJsonFilePath]);
        }
    }
}
