// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using EnvironmentVariablesUILib.ViewModels;

namespace EnvironmentVariablesUILib.UnitTests.Adapters
{
    [TestClass]
    [DoNotParallelize]
    public class ProfileFileAdapterTests
    {
        private static readonly string[] ActiveProfileDeleteOrder = { "TMP", "TMP_PowerToys_Test_profile_1", "PATH", "PATH_PowerToys_Test_profile_1", "profile_1_variable_1" };

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("Adapter")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CapturedTaskRun_DisabledRemovalsJoinOwnersAndPublishOwnedFile(bool olderPublishesLast)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestContext.ResultsDirectory));
            Assert.IsTrue(Path.IsPathFullyQualified(TestContext.ResultsDirectory));
            var resultsDirectory = Path.GetFullPath(TestContext.ResultsDirectory);
            var ownedDirectory = Path.Combine(resultsDirectory, $"EV-A-TASKRUN-{Guid.NewGuid():N}");
            var ownedFile = Path.Combine(ownedDirectory, "profiles.json");
            Assert.AreEqual(resultsDirectory.TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(ownedDirectory)!.TrimEnd(Path.DirectorySeparatorChar));
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var canonicalPath = service.ProfilesJsonFilePath;
            string MapCanonicalRequest(string? path) => string.Equals(path, canonicalPath, StringComparison.Ordinal)
                ? ownedFile : throw new InvalidOperationException("Rejected noncanonical physical IO.");
            files.ExistsCallback = path => File.Exists(MapCanonicalRequest(path));
            files.ReadAllTextCallback = path => File.ReadAllText(MapCanonicalRequest(path));
            files.WriteAllTextAsyncCallback = (path, text, token) => File.WriteAllTextAsync(MapCanonicalRequest(path), text, token);
            var asyncOwners = new ConcurrentQueue<Task>();
            var actionOwners = new ConcurrentQueue<Task>();
            var publicationTasks = new ConcurrentQueue<Task>();
            var refreshes = new ConcurrentQueue<Action>();
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
            var first = new ProfileVariablesSet(Guid.NewGuid(), "First removed");
            var second = new ProfileVariablesSet(Guid.NewGuid(), "Second removed");
            const string rawPath = @"%ROOT%;;C:\Tools;";
            second.Variables.Add(new Variable("PATH", rawPath, VariablesSetType.Profile));
            var ownsDirectory = false;
            var fixture = new EnvironmentVariablesFixture();
            try
            {
                Assert.IsFalse(string.Equals(canonicalPath, ownedFile, StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(Directory.Exists(ownedDirectory));
                Directory.CreateDirectory(ownedDirectory);
                ownsDirectory = true;
                await service.WriteAsync(new[] { first, second });
                var initialJson = File.ReadAllText(ownedFile);
                var callbackCount = 0;
                files.WriteAllTextAsyncCallback = (path, text, token) =>
                {
                    var index = Interlocked.Increment(ref callbackCount) - 1;
                    var publication = PublishAsync(index, path, text!, token);
                    publicationTasks.Enqueue(publication);
                    return publication;
                };

                async Task PublishAsync(int index, string path, string text, CancellationToken token)
                {
                    var physicalPath = MapCanonicalRequest(path);
                    entered[index].SetResult(text);
                    await release[index].Task.ConfigureAwait(false);
                    await File.WriteAllTextAsync(physicalPath, text, token).ConfigureAwait(false);
                }

                var vm = new MainViewModel(
                    new EnvironmentVariablesFixture.FixedElevation(false),
                    service,
                    fixture.Logger,
                    fixture.Telemetry,
                    refresh =>
                    {
                        refreshes.Enqueue(refresh);
                        return false;
                    },
                    new DefaultVariablesSet(VariablesSet.UserGuid, "User", VariablesSetType.User),
                    new DefaultVariablesSet(VariablesSet.SystemGuid, "System", VariablesSetType.System),
                    new DefaultVariablesSet(Guid.NewGuid(), "Defaults", VariablesSetType.User),
                    fixture.Environment,
                    action =>
                    {
                        var owner = Task.Run(action);
                        actionOwners.Enqueue(owner);
                        return owner;
                    },
                    work =>
                    {
                        var owner = Task.Run(work);
                        asyncOwners.Enqueue(owner);
                        return owner;
                    })
                {
                    Profiles = new ObservableCollection<ProfileVariablesSet> { first, second },
                };
                Assert.IsTrue(vm.Profiles.All(profile => !profile.IsEnabled));
                Assert.IsNull(vm.AppliedProfile);

                vm.RemoveProfile(first);
                var olderJson = await entered[0].Task.WaitAsync(TimeSpan.FromSeconds(5));
                vm.RemoveProfile(second);
                var newerJson = await entered[1].Task.WaitAsync(TimeSpan.FromSeconds(5));
                var owners = asyncOwners.ToArray();
                Assert.AreEqual(2, owners.Length);
                Assert.AreEqual(0, actionOwners.Count);
                Assert.IsTrue(owners.All(owner => !owner.IsCompleted));
                Assert.AreEqual(initialJson, File.ReadAllText(ownedFile));
                Assert.AreEqual(0, vm.Profiles.Count);
                using (var older = JsonDocument.Parse(olderJson))
                using (var newer = JsonDocument.Parse(newerJson))
                {
                    Assert.AreEqual(1, older.RootElement.GetArrayLength());
                    Assert.AreEqual(second.Id, older.RootElement[0].GetProperty("Id").GetGuid());
                    Assert.AreEqual(rawPath, older.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
                    Assert.AreEqual(0, newer.RootElement.GetArrayLength());
                }

                var firstPublication = olderPublishesLast ? 1 : 0;
                var lastPublication = 1 - firstPublication;
                release[firstPublication].SetResult(true);
                await owners[firstPublication];
                var intermediateJson = File.ReadAllText(ownedFile);
                using (var intermediate = JsonDocument.Parse(intermediateJson))
                {
                    TestContext.WriteLine($"Task.Run intermediate: olderPublishesLast={olderPublishesLast}; count={intermediate.RootElement.GetArrayLength()}; JSON={intermediateJson}");
                    Assert.AreEqual(olderPublishesLast ? 0 : 1, intermediate.RootElement.GetArrayLength());
                }

                Assert.IsFalse(owners[lastPublication].IsCompleted);
                release[lastPublication].SetResult(true);
                await Task.WhenAll(owners.Concat(actionOwners.ToArray()));
                var completedPublications = publicationTasks.ToArray();
                await Task.WhenAll(completedPublications);
                var finalJson = File.ReadAllText(ownedFile);
                using (var final = JsonDocument.Parse(finalJson))
                {
                    TestContext.WriteLine($"Task.Run final: olderPublishesLast={olderPublishesLast}; count={final.RootElement.GetArrayLength()}; JSON={finalJson}");
                    Assert.AreEqual(olderPublishesLast ? 1 : 0, final.RootElement.GetArrayLength(), "Controlled publication characterization, not required-result acceptance.");
                    if (olderPublishesLast)
                    {
                        var restored = final.RootElement[0];
                        Assert.AreEqual(second.Id, restored.GetProperty("Id").GetGuid());
                        Assert.AreEqual(second.Name, restored.GetProperty("Name").GetString());
                        Assert.IsFalse(restored.GetProperty("IsEnabled").GetBoolean());
                        Assert.AreEqual(1, restored.GetProperty("Variables").GetArrayLength());
                        Assert.AreEqual("PATH", restored.GetProperty("Variables")[0].GetProperty("Name").GetString());
                        Assert.AreEqual(rawPath, restored.GetProperty("Variables")[0].GetProperty("Values").GetString());
                        Assert.IsFalse(restored.GetProperty("Variables")[0].GetProperty("ApplyToSystem").GetBoolean());
                    }
                }

                Assert.AreEqual(2, callbackCount);
                Assert.AreEqual(2, completedPublications.Length);
                Assert.IsTrue(owners.All(owner => owner.IsCompletedSuccessfully));
                Assert.IsTrue(completedPublications.All(publication => publication.IsCompletedSuccessfully));
                Assert.AreEqual(0, refreshes.Count);
                Assert.AreEqual(0, fixture.Environment.Operations.Count);
                Assert.AreEqual(0, fixture.Environment.Keys.Count);
                Assert.AreEqual(0, fixture.Environment.Notifications.Count);
                Assert.AreEqual(0, fixture.Environment.User.Count);
                Assert.AreEqual(0, fixture.Environment.Machine.Count);
                Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
                Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
                Assert.AreEqual(0, fixture.Logger.Entries.Count);
                Assert.AreEqual(0, files.Files.Count);
                Assert.AreEqual(3, files.Writes.Count);
                Assert.AreEqual(olderJson, files.Writes[1].Contents);
                Assert.AreEqual(newerJson, files.Writes[2].Contents);
                Assert.IsTrue(files.Writes.All(write => write.Path == canonicalPath && write.CancellationToken == CancellationToken.None));
            }
            finally
            {
                release[0].TrySetResult(true);
                release[1].TrySetResult(true);
                var owners = asyncOwners.Concat(actionOwners).ToArray();
                try
                {
                    await Task.WhenAll(owners);
                }
                finally
                {
                    var publications = publicationTasks.ToArray();
                    try
                    {
                        await Task.WhenAll(publications);
                    }
                    finally
                    {
                        var fullyJoined = owners.All(owner => owner.IsCompleted)
                            && publications.All(publication => publication.IsCompleted)
                            && owners.Length == asyncOwners.Count + actionOwners.Count
                            && publications.Length == publicationTasks.Count;
                        Assert.IsTrue(fullyJoined, "Unjoined work: retain physical artifacts and do not restore static holders.");
                        if (fullyJoined)
                        {
                            try
                            {
                                if (ownsDirectory)
                                {
                                    File.Delete(ownedFile);
                                    Directory.Delete(ownedDirectory, recursive: false);
                                    TestContext.WriteLine($"Task.Run cleanup completed after owner/IO joins: {ownedDirectory}");
                                }
                            }
                            finally
                            {
                                fixture.Dispose();
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("Adapter")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PhysicalProfiles_FreshServiceReconstructsOwnershipAndEditability(bool elevated)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var canonicalPath = service.ProfilesJsonFilePath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestContext.ResultsDirectory));
            Assert.IsTrue(Path.IsPathFullyQualified(TestContext.ResultsDirectory));
            var ownedDirectory = Path.Combine(TestContext.ResultsDirectory, $"EV-A-FRESH-{Guid.NewGuid():N}");
            var ownedFile = Path.Combine(ownedDirectory, "profiles.json");
            string MapCanonicalRequest(string? path) => string.Equals(path, canonicalPath, StringComparison.Ordinal)
                ? ownedFile : throw new InvalidOperationException("Rejected noncanonical physical IO.");
            files.ExistsCallback = path => File.Exists(MapCanonicalRequest(path));
            files.ReadAllTextCallback = path => File.ReadAllText(MapCanonicalRequest(path));
            files.WriteAllTextAsyncCallback = (path, text, token) => File.WriteAllTextAsync(MapCanonicalRequest(path), text, token);
            var authored = new ProfileVariablesSet(Guid.NewGuid(), "Persisted") { IsEnabled = true };
            const string rawPath = @"%ROOT%\bin;;C:\Tools;C:\Tools;";
            authored.Variables.Add(new Variable("PATH", rawPath, VariablesSetType.Profile));
            authored.Variables.Add(new Variable("TMP", "%OVERRIDE%;;", VariablesSetType.Profile));
            authored.Variables.Add(new Variable("CREATED", "profile-created", VariablesSetType.Profile));
            var inactive = new ProfileVariablesSet(Guid.NewGuid(), "RetainedDisabled");
            inactive.Variables.Add(new Variable("DISABLED_ONE", "not-applied-one", VariablesSetType.Profile));
            inactive.Variables.Add(new Variable("DISABLED_TWO", "not-applied-two", VariablesSetType.Profile));
            var expectedUser = new[]
            {
                ("PATH", rawPath),
                ("PATH_PowerToys_Persisted", "%ORIGINAL_PATH%;"),
                ("TMP", "%OVERRIDE%;;"),
                ("TMP_PowerToys_Persisted", "%ORIGINAL%;;"),
                ("TMP_PowerToys_Other", "%LOOKALIKE%;"),
                ("UNRELATED", "%USER%;"),
                ("CREATED", "profile-created"),
            };
            foreach (var (name, value) in expectedUser)
            {
                fixture.Environment.User[name] = value;
            }

            fixture.Environment.Machine["MACHINE_ONLY"] = "%MACHINE%;";
            var ownsDirectory = false;
            try
            {
                Assert.IsFalse(Directory.Exists(ownedDirectory));
                Directory.CreateDirectory(ownedDirectory);
                ownsDirectory = true;
                await service.WriteAsync(new[] { authored, inactive });
                var originalBytes = File.ReadAllBytes(ownedFile);
                var previous = fixture.CreateViewModel(elevated, service);
                previous.LoadEnvironmentVariables();
                var previousProfile = previous.Profiles.Single(profile => profile.Id == authored.Id);
                service.Dispose();
                var freshFiles = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
                using var freshService = new EnvironmentVariablesService(freshFiles);
                Assert.AreEqual(canonicalPath, freshService.ProfilesJsonFilePath);
                freshFiles.ExistsCallback = path => File.Exists(MapCanonicalRequest(path));
                freshFiles.ReadAllTextCallback = path => File.ReadAllText(MapCanonicalRequest(path));
                freshFiles.WriteAllTextAsyncCallback = (path, text, token) => File.WriteAllTextAsync(MapCanonicalRequest(path), text, token);
                var operationsBeforeLoad = fixture.Environment.Operations.Count;
                var fresh = fixture.CreateViewModel(elevated, freshService);

                fresh.LoadEnvironmentVariables();

                Assert.IsNotNull(fresh.AppliedProfile, "The physical saved active profile must be reconstructed.");
                Assert.AreEqual(2, fresh.Profiles.Count);
                var restored = fresh.Profiles.Single(profile => profile.Id == authored.Id);
                Assert.AreSame(restored, fresh.AppliedProfile);
                Assert.AreEqual((authored.Id, authored.Name, true), (restored.Id, restored.Name, restored.IsEnabled));
                Assert.AreNotSame(previousProfile, restored);
                Assert.AreNotSame(previous.Profiles, fresh.Profiles);
                Assert.AreNotSame(previousProfile.Variables, restored.Variables);
                CollectionAssert.AreEqual(authored.Variables.Select(item => item.Name).ToArray(), restored.Variables.Select(item => item.Name).ToArray());
                CollectionAssert.AreEqual(authored.Variables.Select(item => item.Values).ToArray(), restored.Variables.Select(item => item.Values).ToArray());
                var restoredInactive = fresh.Profiles.Single(profile => profile.Id == inactive.Id);
                Assert.AreEqual(inactive.Name, restoredInactive.Name);
                Assert.IsFalse(restoredInactive.IsEnabled);
                CollectionAssert.AreEqual(
                    inactive.Variables.Select(item => (item.Name, item.Values)).ToArray(),
                    restoredInactive.Variables.Select(item => (item.Name, item.Values)).ToArray());
                for (var index = 0; index < restored.Variables.Count; index++)
                {
                    var child = restored.Variables[index];
                    Assert.AreNotSame(previousProfile.Variables[index], child);
                    Assert.AreNotSame(previousProfile.Variables[index].ValuesList, child.ValuesList);
                    Assert.AreNotSame(previousProfile.Variables[index].ValuesList[0], child.ValuesList[0]);
                    Assert.AreEqual(VariablesSetType.Profile, child.ParentType);
                    Assert.IsFalse(child.IsAppliedFromProfile);
                    Assert.IsTrue(child.IsEditable);
                }

                CollectionAssert.AreEqual(new[] { @"%ROOT%\bin", string.Empty, @"C:\Tools", @"C:\Tools", string.Empty }, restored.Variables[0].ValuesList.Select(item => item.Text).ToArray());
                CollectionAssert.AreEqual(new[] { "%OVERRIDE%", string.Empty, string.Empty }, restored.Variables[1].ValuesList.Select(item => item.Text).ToArray());
                CollectionAssert.AreEquivalent(expectedUser, fresh.UserDefaultSet.Variables.Select(item => (item.Name, item.Values)).ToArray());
                foreach (var variable in fresh.UserDefaultSet.Variables)
                {
                    var owned = variable.Name is "PATH" or "TMP" or "CREATED" or "PATH_PowerToys_Persisted" or "TMP_PowerToys_Persisted";
                    Assert.AreEqual(owned, variable.IsAppliedFromProfile, variable.Name);
                    Assert.AreEqual(!owned, variable.IsEditable, variable.Name);
                    Assert.AreEqual(VariablesSetType.User, variable.ParentType);
                }

                var machine = fresh.SystemDefaultSet.Variables.Single();
                Assert.AreEqual(("MACHINE_ONLY", "%MACHINE%;", VariablesSetType.System), (machine.Name, machine.Values, machine.ParentType));
                Assert.IsFalse(machine.IsAppliedFromProfile);
                Assert.AreEqual(elevated, machine.IsEditable);
                Assert.AreEqual(elevated, fresh.IsElevated);
                Assert.AreEqual(EnvironmentState.Unchanged, fresh.EnvironmentState);
                CollectionAssert.AreEqual(
                    previous.AppliedVariables.Select(item => (item.Name, item.Values, item.ParentType, item.IsAppliedFromProfile, item.IsEditable)).ToArray(),
                    fresh.AppliedVariables.Select(item => (item.Name, item.Values, item.ParentType, item.IsAppliedFromProfile, item.IsEditable)).ToArray());
                Assert.AreEqual(rawPath, fresh.AppliedVariables.Single(item => item.Name == "PATH" && item.ParentType == VariablesSetType.Profile).Values);
                Assert.AreEqual("%OVERRIDE%;;", fresh.AppliedVariables.Single(item => item.Name == "TMP").Values);
                Assert.AreEqual("profile-created", fresh.AppliedVariables.Single(item => item.Name == "CREATED").Values);
                Assert.IsFalse(fresh.AppliedVariables.Any(item => item.Name is "DISABLED_ONE" or "DISABLED_TWO"));
                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(ownedFile));
                CollectionAssert.AreEqual(new[] { ("Exists", (string?)canonicalPath), ("ReadAllText", canonicalPath) }, freshFiles.Calls.ToArray());
                Assert.AreEqual(1, files.Writes.Count);
                Assert.AreEqual(0, freshFiles.Writes.Count);
                Assert.AreEqual(0, files.Files.Count);
                Assert.AreEqual(0, freshFiles.Files.Count);
                CollectionAssert.AreEquivalent(expectedUser, fixture.Environment.User.Select(item => (item.Key, (string)item.Value!)).ToArray());
                Assert.AreEqual(1, fixture.Environment.Machine.Count);
                Assert.AreEqual("%MACHINE%;", fixture.Environment.Machine["MACHINE_ONLY"]);
                Assert.IsFalse(fixture.Environment.Operations.Skip(operationsBeforeLoad).Any(item => item.Operation is "Write" or "Delete"));
                Assert.IsFalse(fixture.Environment.Operations.Any(item => item.Operation is "Write" or "Delete"));
                Assert.AreEqual(0, fixture.Environment.Notifications.Count);
                Assert.AreEqual(0, fixture.Work.ActionCount);
                Assert.AreEqual(0, fixture.Work.SaveCount);
                Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
                Assert.AreEqual(0, fixture.Service.Writes.Count);
                Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
                Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
                Assert.IsTrue(fixture.Work.IsQuiescent);
                TestContext.WriteLine($"Fresh physical reconstruction: elevated={elevated}; count={fresh.Profiles.Count}; JSON={File.ReadAllText(ownedFile)}");
            }
            finally
            {
                await fixture.Work.SettleAsync();
                Assert.IsTrue(fixture.Work.IsQuiescent, "Owned work is still pending; physical artifacts are retained.");

                if (ownsDirectory)
                {
                    File.Delete(ownedFile);
                    Directory.Delete(ownedDirectory, recursive: false);
                    TestContext.WriteLine($"Cleanup completed: {ownedDirectory}");
                }
            }
        }

        [TestMethod]
        [TestCategory("Adapter")]
        public async Task ControlledStalePublication_PhysicalFileRestoresRemovedProfileAfterBothSaves()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var canonicalPath = service.ProfilesJsonFilePath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestContext.ResultsDirectory));
            Assert.IsTrue(Path.IsPathFullyQualified(TestContext.ResultsDirectory));
            var ownedDirectory = Path.Combine(TestContext.ResultsDirectory, $"EV-A-STALE-{Guid.NewGuid():N}");
            var ownedFile = Path.Combine(ownedDirectory, "profiles.json");
            string MapCanonicalRequest(string? path) => string.Equals(path, canonicalPath, StringComparison.Ordinal)
                ? ownedFile : throw new InvalidOperationException("Rejected noncanonical physical IO.");
            files.ExistsCallback = path => File.Exists(MapCanonicalRequest(path));
            files.ReadAllTextCallback = path => File.ReadAllText(MapCanonicalRequest(path));
            files.WriteAllTextAsyncCallback = (path, text, token) => File.WriteAllTextAsync(MapCanonicalRequest(path), text, token);
            var first = new ProfileVariablesSet(Guid.NewGuid(), "First removed");
            var second = new ProfileVariablesSet(Guid.NewGuid(), "Second removed");
            const string rawPath = @"%ROOT%;;C:\Tools;";
            second.Variables.Add(new Variable("PATH", rawPath, VariablesSetType.Profile));
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
            var publicationTasks = new System.Collections.Generic.List<Task>();
            Task? olderSave = null;
            Task? newerSave = null;
            var ownsDirectory = false;
            try
            {
                Assert.IsFalse(Directory.Exists(ownedDirectory));
                Directory.CreateDirectory(ownedDirectory);
                ownsDirectory = true;
                await service.WriteAsync(new[] { first, second });
                var initialJson = File.ReadAllText(ownedFile);
                var vm = fixture.CreateViewModel(service: service);
                vm.LoadEnvironmentVariables();
                var removedSecond = vm.Profiles[1];
                var callbackCount = 0;
                files.WriteAllTextAsyncCallback = (path, text, token) =>
                {
                    var index = callbackCount++;
                    var task = PublishAsync(index, path, text!, token);
                    publicationTasks.Add(task);
                    return task;
                };

                async Task PublishAsync(int index, string path, string text, CancellationToken token)
                {
                    var physicalPath = MapCanonicalRequest(path);
                    entered[index].SetResult(text);
                    await release[index].Task.ConfigureAwait(false);
                    await File.WriteAllTextAsync(physicalPath, text, token).ConfigureAwait(false);
                }

                vm.RemoveProfile(vm.Profiles[0]);
                olderSave = fixture.Work.CompleteSaveAsync(0);
                var olderJson = await entered[0].Task.WaitAsync(TimeSpan.FromSeconds(5));
                vm.RemoveProfile(vm.Profiles.Single());
                newerSave = fixture.Work.CompleteSaveAsync(1);
                var newerJson = await entered[1].Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(initialJson, File.ReadAllText(ownedFile));
                Assert.IsFalse(olderSave.IsCompleted);
                Assert.IsFalse(newerSave.IsCompleted);
                using (var older = JsonDocument.Parse(olderJson))
                using (var newer = JsonDocument.Parse(newerJson))
                {
                    Assert.AreEqual(1, older.RootElement.GetArrayLength());
                    Assert.AreEqual(second.Id, older.RootElement[0].GetProperty("Id").GetGuid());
                    Assert.AreEqual(0, newer.RootElement.GetArrayLength());
                }

                release[1].SetResult(true);
                await newerSave;
                var newerPhysicalJson = File.ReadAllText(ownedFile);
                using (var newerPhysical = JsonDocument.Parse(newerPhysicalJson))
                {
                    Assert.AreEqual(0, newerPhysical.RootElement.GetArrayLength());
                    TestContext.WriteLine($"Newer publication: count={newerPhysical.RootElement.GetArrayLength()}; JSON={newerPhysicalJson}");
                }

                Assert.IsFalse(olderSave.IsCompleted);
                release[0].SetResult(true);
                await Task.WhenAll(olderSave, newerSave);
                var finalJson = File.ReadAllText(ownedFile);
                using (var final = JsonDocument.Parse(finalJson))
                {
                    TestContext.WriteLine($"Both saves completed: count={final.RootElement.GetArrayLength()}; JSON={finalJson}");
                    Assert.AreEqual(1, final.RootElement.GetArrayLength(), "Characterization: the older publication restores the removed second profile.");
                    Assert.AreEqual(second.Id, final.RootElement[0].GetProperty("Id").GetGuid());
                    Assert.AreEqual(second.Name, final.RootElement[0].GetProperty("Name").GetString());
                    Assert.IsFalse(final.RootElement[0].GetProperty("IsEnabled").GetBoolean());
                    Assert.AreEqual(rawPath, final.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
                }

                Assert.AreEqual(0, vm.Profiles.Count);
                using var freshService = new EnvironmentVariablesService(files);
                var fresh = fixture.CreateViewModel(service: freshService);
                fresh.LoadEnvironmentVariables();
                Assert.AreEqual(1, fresh.Profiles.Count, "Characterization expects one restored removed profile, not required-result zero.");
                var restored = fresh.Profiles.Single();
                Assert.AreEqual((second.Id, second.Name, false), (restored.Id, restored.Name, restored.IsEnabled));
                Assert.AreNotSame(removedSecond, restored);
                Assert.AreEqual(("PATH", rawPath, VariablesSetType.Profile), (restored.Variables.Single().Name, restored.Variables.Single().Values, restored.Variables.Single().ParentType));
                CollectionAssert.AreEqual(new[] { "%ROOT%", string.Empty, @"C:\Tools", string.Empty }, restored.Variables.Single().ValuesList.Select(item => item.Text).ToArray());
                Assert.IsNull(fresh.AppliedProfile);
                Assert.AreEqual(EnvironmentState.Unchanged, fresh.EnvironmentState);
                Assert.AreEqual(finalJson, File.ReadAllText(ownedFile));
                Assert.AreEqual(3, files.Writes.Count);
                Assert.AreEqual(olderJson, files.Writes[1].Contents);
                Assert.AreEqual(newerJson, files.Writes[2].Contents);
                Assert.IsTrue(files.Writes.All(item => item.CancellationToken == CancellationToken.None));
                Assert.AreEqual(0, files.Files.Count);
                Assert.AreEqual(2, fixture.Work.SaveCount);
                Assert.AreEqual(0, fixture.Work.ActionCount);
                Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
                Assert.AreEqual(0, fixture.Service.Writes.Count);
                Assert.AreEqual(0, fixture.Environment.Notifications.Count);
                Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
                Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
                Assert.IsTrue(fixture.Work.IsQuiescent);
            }
            finally
            {
                release[0].TrySetResult(true);
                release[1].TrySetResult(true);
                try
                {
                    try
                    {
                        await Task.WhenAll(publicationTasks.Concat(new[] { olderSave, newerSave }.OfType<Task>()));
                    }
                    finally
                    {
                        await fixture.Work.SettleAsync();
                    }
                }
                finally
                {
                    Assert.IsTrue(fixture.Work.IsQuiescent && publicationTasks.All(task => task.IsCompleted), "Publication work is still pending; physical artifacts are retained.");

                    if (ownsDirectory)
                    {
                        File.Delete(ownedFile);
                        Directory.Delete(ownedDirectory, recursive: false);
                        TestContext.WriteLine($"Cleanup completed: {ownedDirectory}");
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("Adapter")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task LockedProfileFile_PropagatesRealIoFailureAndFreshServiceRecovers(bool write)
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var canonicalPath = service.ProfilesJsonFilePath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestContext.ResultsDirectory));
            Assert.IsTrue(Path.IsPathFullyQualified(TestContext.ResultsDirectory));
            var resultsDirectory = Path.GetFullPath(TestContext.ResultsDirectory);
            var ownedDirectory = Path.Combine(resultsDirectory, $"EV-A-FILE-LOCK-{Guid.NewGuid():N}");
            var ownedFile = Path.Combine(ownedDirectory, "profiles.json");
            Assert.AreEqual(resultsDirectory.TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(ownedDirectory)!.TrimEnd(Path.DirectorySeparatorChar));
            Assert.IsFalse(string.Equals(canonicalPath, ownedFile, StringComparison.OrdinalIgnoreCase));
            IOException? physicalFailure = null;
            var physicalReads = 0;
            var physicalWrites = 0;

            string MapCanonicalRequest(string? requestedPath)
            {
                if (!string.Equals(canonicalPath, requestedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Rejected a noncanonical request before physical IO.");
                }

                return ownedFile;
            }

            files.ExistsCallback = path => File.Exists(MapCanonicalRequest(path));
            files.ReadAllTextCallback = path =>
            {
                physicalReads++;
                try
                {
                    return File.ReadAllText(MapCanonicalRequest(path));
                }
                catch (IOException exception)
                {
                    physicalFailure = exception;
                    throw;
                }
            };
            files.WriteAllTextAsyncCallback = async (path, contents, token) =>
            {
                physicalWrites++;
                try
                {
                    await File.WriteAllTextAsync(MapCanonicalRequest(path), contents, token);
                }
                catch (IOException exception)
                {
                    physicalFailure = exception;
                    throw;
                }
            };

            var original = new ProfileVariablesSet(Guid.NewGuid(), "Owned original") { IsEnabled = true };
            const string rawPath = @"%ROOT%\bin;;C:\Tools;";
            original.Variables.Add(new Variable("PATH", rawPath, VariablesSetType.Profile));
            var replacement = new ProfileVariablesSet(Guid.NewGuid(), "Rejected replacement");
            var ownsDirectory = false;
            try
            {
                Assert.IsFalse(Directory.Exists(ownedDirectory));
                Directory.CreateDirectory(ownedDirectory);
                ownsDirectory = true;
                await service.WriteAsync(new[] { original });
                var originalJson = File.ReadAllText(ownedFile);

                using (var heldFile = new FileStream(ownedFile, FileMode.Open, FileAccess.Read, write ? FileShare.Read : FileShare.None))
                {
                    IOException failure;
                    if (write)
                    {
                        failure = await Assert.ThrowsExactlyAsync<IOException>(() =>
                            service.WriteAsync(new[] { replacement }));
                    }
                    else
                    {
                        failure = Assert.ThrowsExactly<IOException>(() => service.ReadProfiles());
                    }

                    Assert.AreSame(physicalFailure, failure, "The service must propagate the actual backend exception.");
                    Assert.AreEqual(write ? 2 : 1, physicalWrites);
                    Assert.AreEqual(write ? 0 : 1, physicalReads);
                    TestContext.WriteLine($"Real sharing failure: write={write}; HResult={failure.HResult}; owned file={ownedFile}");
                }

                Assert.AreEqual(originalJson, File.ReadAllText(ownedFile));
                using var freshService = new EnvironmentVariablesService(files);
                var restored = freshService.ReadProfiles().Single();
                Assert.AreNotSame(original, restored);
                Assert.AreEqual(original.Id, restored.Id);
                Assert.AreEqual(original.Name, restored.Name);
                Assert.IsTrue(restored.IsEnabled);
                Assert.AreEqual("PATH", restored.Variables.Single().Name);
                Assert.AreEqual(rawPath, restored.Variables.Single().Values);
                Assert.AreEqual(write ? 2 : 1, physicalWrites, "Recovery must not retry the failed operation.");
                Assert.AreEqual(write ? 1 : 2, physicalReads);
                Assert.AreEqual(0, files.Files.Count, "Neither failure nor recovery may use in-memory data.");
                Assert.IsTrue(fixture.Work.IsQuiescent);
            }
            finally
            {
                if (ownsDirectory)
                {
                    File.Delete(ownedFile);
                    Directory.Delete(ownedDirectory, recursive: false);
                    TestContext.WriteLine($"Cleanup completed: {ownedDirectory}");
                }
            }
        }

        [TestMethod]
        [TestCategory("Adapter")]
        public async Task WriteReadAndRemoveProfiles_UsesOnlyOwnedArtifact()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var canonicalPath = service.ProfilesJsonFilePath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(canonicalPath));
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestContext.ResultsDirectory));
            Assert.IsTrue(Path.IsPathFullyQualified(TestContext.ResultsDirectory));

            var resultsDirectory = Path.GetFullPath(TestContext.ResultsDirectory);
            var ownedDirectory = Path.Combine(resultsDirectory, $"EV-A-PROFILE-FILE-{Guid.NewGuid():N}");
            var ownedFile = Path.Combine(ownedDirectory, "profiles.json");
            Assert.AreEqual(resultsDirectory.TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(ownedDirectory)!.TrimEnd(Path.DirectorySeparatorChar));
            Assert.AreEqual(ownedDirectory, Path.GetDirectoryName(ownedFile));
            Assert.IsFalse(string.Equals(canonicalPath, ownedFile, StringComparison.OrdinalIgnoreCase));

            void RequireQuiescentCleanup()
            {
                if (!fixture.Work.IsQuiescent)
                {
                    throw new InvalidOperationException("Owned work did not quiesce; artifact retained and shared holders must not be restored.");
                }
            }

            string MapCanonicalRequest(string? requestedPath)
            {
                if (requestedPath == null || !string.Equals(canonicalPath, requestedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Rejected a noncanonical profile path before filesystem IO.");
                }

                return ownedFile;
            }

            files.ExistsCallback = requestedPath => File.Exists(MapCanonicalRequest(requestedPath));
            files.ReadAllTextCallback = requestedPath => File.ReadAllText(MapCanonicalRequest(requestedPath));
            files.WriteAllTextAsyncCallback = (requestedPath, contents, cancellationToken) =>
                File.WriteAllTextAsync(MapCanonicalRequest(requestedPath), contents, cancellationToken);

            var profileId = Guid.NewGuid();
            const string rawPath = @"%ROOT%\bin;;C:\Tools;C:\Tools;";
            var profile = new ProfileVariablesSet(profileId, "Owned file adapter") { IsEnabled = true };
            profile.Variables.Add(new Variable("PATH", rawPath, VariablesSetType.Profile));
            Task? startedWrite = null;
            var ownsDirectory = false;
            try
            {
                MapCanonicalRequest(canonicalPath);
                Assert.IsFalse(Directory.Exists(ownedDirectory), "The GUID-owned directory must not already exist.");
                Directory.CreateDirectory(ownedDirectory);
                ownsDirectory = true;
                TestContext.WriteLine($"Canonical request: {canonicalPath}");
                TestContext.WriteLine($"Owned artifact mapping: {ownedFile}");

                startedWrite = service.WriteAsync(new[] { profile });
                await startedWrite;

                var write = files.Writes.Single();
                Assert.AreEqual(canonicalPath, write.Path);
                Assert.AreEqual(CancellationToken.None, write.CancellationToken);
                var physicalJson = File.ReadAllText(ownedFile);
                TestContext.WriteLine($"Independent physical JSON readback: {physicalJson}");
                using (var json = JsonDocument.Parse(physicalJson))
                {
                    Assert.AreEqual(1, json.RootElement.GetArrayLength());
                    var saved = json.RootElement[0];
                    Assert.AreEqual(profileId, saved.GetProperty("Id").GetGuid());
                    Assert.AreEqual(profile.Name, saved.GetProperty("Name").GetString());
                    Assert.IsTrue(saved.GetProperty("IsEnabled").GetBoolean());
                    Assert.AreEqual(1, saved.GetProperty("Variables").GetArrayLength());
                    Assert.AreEqual("PATH", saved.GetProperty("Variables")[0].GetProperty("Name").GetString());
                    Assert.AreEqual(rawPath, saved.GetProperty("Variables")[0].GetProperty("Values").GetString());
                }

                var loaded = service.ReadProfiles().Single();
                Assert.AreEqual(profileId, loaded.Id);
                Assert.AreEqual(profile.Name, loaded.Name);
                Assert.IsTrue(loaded.IsEnabled);
                var loadedPath = loaded.Variables.Single();
                Assert.AreEqual("PATH", loadedPath.Name);
                Assert.AreEqual(rawPath, loadedPath.Values);
                CollectionAssert.AreEqual(
                    new[] { @"%ROOT%\bin", string.Empty, @"C:\Tools", @"C:\Tools", string.Empty },
                    loadedPath.ValuesList.Select(item => item.Text).ToArray());
                CollectionAssert.AreEqual(
                    new[] { ("WriteAllTextAsync", (string?)canonicalPath), ("Exists", canonicalPath), ("ReadAllText", canonicalPath) },
                    files.Calls.ToArray());
                Assert.AreEqual(0, files.Files.Count, "Adapter readback must not come from the in-memory map.");
                TestContext.WriteLine($"Service readback: Id={loaded.Id}; IsEnabled={loaded.IsEnabled}; PATH={loadedPath.Values}");

                var first = new ProfileVariablesSet(Guid.NewGuid(), "First removed");
                var second = new ProfileVariablesSet(Guid.NewGuid(), "Second removed");
                var removedIds = new[] { first.Id, second.Id };
                var removedNames = new[] { first.Name, second.Name };
                startedWrite = service.WriteAsync(new[] { first, second });
                await startedWrite;

                var vm = fixture.CreateViewModel(service: service);
                vm.LoadEnvironmentVariables();
                CollectionAssert.AreEqual(removedIds, vm.Profiles.Select(item => item.Id).ToArray());
                CollectionAssert.AreEqual(removedNames, vm.Profiles.Select(item => item.Name).ToArray());
                Assert.IsTrue(vm.Profiles.All(item => !item.IsEnabled));
                Assert.IsNull(vm.AppliedProfile);
                Assert.AreEqual(0, fixture.Work.ActionCount);
                Assert.AreEqual(0, fixture.Work.SaveCount);
                var writesBeforeRemovals = files.Writes.Count;

                vm.RemoveProfile(vm.Profiles[0]);

                Assert.AreEqual(second.Id, vm.Profiles.Single().Id);
                Assert.AreEqual(1, fixture.Work.SaveCount);
                Assert.AreEqual(writesBeforeRemovals, files.Writes.Count);
                await fixture.Work.CompleteSaveAsync(0);
                Assert.AreEqual(writesBeforeRemovals + 1, files.Writes.Count);
                using (var intermediate = JsonDocument.Parse(File.ReadAllText(ownedFile)))
                {
                    Assert.AreEqual(1, intermediate.RootElement.GetArrayLength());
                    Assert.AreEqual(second.Id, intermediate.RootElement[0].GetProperty("Id").GetGuid());
                    Assert.AreEqual(second.Name, intermediate.RootElement[0].GetProperty("Name").GetString());
                }

                vm.RemoveProfile(vm.Profiles.Single());

                Assert.AreEqual(0, vm.Profiles.Count);
                Assert.AreEqual(2, fixture.Work.SaveCount);
                Assert.AreEqual(writesBeforeRemovals + 1, files.Writes.Count);
                await fixture.Work.CompleteSaveAsync(1);
                Assert.AreEqual(writesBeforeRemovals + 2, files.Writes.Count);
                var finalJson = File.ReadAllText(ownedFile);
                using (var final = JsonDocument.Parse(finalJson))
                {
                    Assert.AreEqual(JsonValueKind.Array, final.RootElement.ValueKind);
                    Assert.AreEqual(0, final.RootElement.GetArrayLength());
                }

                foreach (var id in removedIds)
                {
                    Assert.IsFalse(finalJson.Contains(id.ToString("D"), StringComparison.Ordinal));
                }

                foreach (var name in removedNames)
                {
                    Assert.IsFalse(finalJson.Contains(name, StringComparison.Ordinal));
                }

                CollectionAssert.AreEqual(
                    new[]
                    {
                        ("WriteAllTextAsync", (string?)canonicalPath),
                        ("Exists", canonicalPath),
                        ("ReadAllText", canonicalPath),
                        ("WriteAllTextAsync", canonicalPath),
                        ("WriteAllTextAsync", canonicalPath),
                    },
                    files.Calls.Skip(3).ToArray());
                Assert.IsTrue(files.Writes.All(item => item.CancellationToken == CancellationToken.None));
                Assert.AreEqual(0, files.Files.Count);
                Assert.AreEqual(0, fixture.Service.Writes.Count, "Scheduled saves must use the real service, not its recording default.");
                Assert.IsFalse(fixture.Logger.Entries.Any(item => item.Level == "Error"));
                Assert.AreEqual(0, fixture.Work.ActionCount);
                Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
                Assert.IsTrue(fixture.Work.IsQuiescent);
                TestContext.WriteLine($"Removed profile IDs: {string.Join(", ", removedIds)}; names: {string.Join(", ", removedNames)}");
                TestContext.WriteLine($"Two released MainViewModel saves; independent final physical JSON: {finalJson}");
            }
            finally
            {
                try
                {
                    if (startedWrite != null)
                    {
                        await startedWrite;
                    }
                }
                finally
                {
                    try
                    {
                        await fixture.Work.SettleAsync();
                    }
                    finally
                    {
                        RequireQuiescentCleanup();

                        if (ownsDirectory)
                        {
                            File.Delete(ownedFile);
                            Directory.Delete(ownedDirectory, recursive: false);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("Adapter")]
        public async Task ActiveRemoveAndSaveProfiles_RestoresEnvironmentAndUsesOnlyOwnedArtifact()
        {
            using var fixture = new EnvironmentVariablesFixture();
            var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
            using var service = new EnvironmentVariablesService(files);
            var canonicalPath = service.ProfilesJsonFilePath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(canonicalPath));
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestContext.ResultsDirectory));
            Assert.IsTrue(Path.IsPathFullyQualified(TestContext.ResultsDirectory));

            var resultsDirectory = Path.GetFullPath(TestContext.ResultsDirectory);
            var ownedDirectory = Path.Combine(resultsDirectory, $"EV-A-PROFILE-FILE-{Guid.NewGuid():N}");
            var ownedFile = Path.Combine(ownedDirectory, "profiles.json");
            Assert.AreEqual(resultsDirectory.TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(ownedDirectory)!.TrimEnd(Path.DirectorySeparatorChar));
            Assert.AreEqual(ownedDirectory, Path.GetDirectoryName(ownedFile));
            Assert.IsFalse(string.Equals(canonicalPath, ownedFile, StringComparison.OrdinalIgnoreCase));

            void RequireQuiescentCleanup()
            {
                if (!fixture.Work.IsQuiescent)
                {
                    throw new InvalidOperationException("Owned work did not quiesce; artifact retained and shared holders must not be restored.");
                }
            }

            string MapCanonicalRequest(string? requestedPath)
            {
                if (requestedPath == null || !string.Equals(canonicalPath, requestedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Rejected a noncanonical profile path before filesystem IO.");
                }

                return ownedFile;
            }

            files.ExistsCallback = requestedPath => File.Exists(MapCanonicalRequest(requestedPath));
            files.ReadAllTextCallback = requestedPath => File.ReadAllText(MapCanonicalRequest(requestedPath));
            files.WriteAllTextAsyncCallback = (requestedPath, contents, cancellationToken) =>
                File.WriteAllTextAsync(MapCanonicalRequest(requestedPath), contents, cancellationToken);

            var first = new ProfileVariablesSet(Guid.NewGuid(), "Test_profile_1") { IsEnabled = true };
            first.Variables.Add(new Variable("TMP", "test_TMP", VariablesSetType.Profile));
            first.Variables.Add(new Variable("PATH", "path1;path2;path3", VariablesSetType.Profile));
            first.Variables.Add(new Variable("profile_1_variable_1", "profile_1_value_1", VariablesSetType.Profile));
            var second = new ProfileVariablesSet(Guid.NewGuid(), "Test_profile_2");
            fixture.Environment.User["TMP"] = "test_TMP";
            fixture.Environment.User["TMP_PowerToys_Test_profile_1"] = "original_TMP";
            fixture.Environment.User["PATH"] = "path1;path2;path3";
            fixture.Environment.User["PATH_PowerToys_Test_profile_1"] = "original_user_path";
            fixture.Environment.User["profile_1_variable_1"] = "profile_1_value_1";
            fixture.Environment.User["KEEP_USER"] = "raw%USER_SENTINEL%";
            fixture.Environment.Machine["PATH"] = "machine_path";
            fixture.Environment.Machine["KEEP_MACHINE"] = "raw%MACHINE_SENTINEL%";
            var initialUser = fixture.Environment.User.ToArray();
            var initialMachine = fixture.Environment.Machine.ToArray();
            Task? startedWrite = null;
            var ownsDirectory = false;
            try
            {
                MapCanonicalRequest(canonicalPath);
                Assert.IsFalse(Directory.Exists(ownedDirectory), "The GUID-owned directory must not already exist.");
                Directory.CreateDirectory(ownedDirectory);
                ownsDirectory = true;
                TestContext.WriteLine($"Canonical request: {canonicalPath}");
                TestContext.WriteLine($"Owned artifact mapping: {ownedFile}");

                startedWrite = service.WriteAsync(new[] { first, second });
                await startedWrite;
                var initialJson = File.ReadAllText(ownedFile);
                var vm = fixture.CreateViewModel(service: service);
                vm.LoadEnvironmentVariables();
                var active = vm.Profiles.Single(item => item.Id == first.Id);
                var inactive = vm.Profiles.Single(item => item.Id == second.Id);
                Assert.AreSame(active, vm.AppliedProfile);
                Assert.AreEqual(EnvironmentState.Unchanged, vm.EnvironmentState);
                Assert.IsTrue(active.IsEnabled);
                Assert.IsFalse(inactive.IsEnabled);
                Assert.AreEqual("machine_path;path1;path2;path3", vm.AppliedVariables.Single(item => item.ParentType == VariablesSetType.Path).Values);
                Assert.AreEqual(0, fixture.Work.ActionCount);
                Assert.AreEqual(0, fixture.Work.SaveCount);
                Assert.AreEqual(1, files.Writes.Count);
                fixture.Environment.Operations.Clear();

                vm.RemoveProfile(active);

                Assert.AreSame(inactive, vm.Profiles.Single());
                Assert.IsNull(vm.AppliedProfile);
                Assert.IsFalse(active.IsEnabled);
                Assert.AreEqual(1, fixture.Work.ActionCount);
                Assert.AreEqual(1, fixture.Work.SaveCount);
                Assert.AreEqual(0, fixture.Environment.Operations.Count);
                Assert.AreEqual(0, fixture.Environment.Notifications.Count);
                CollectionAssert.AreEquivalent(initialUser, fixture.Environment.User.ToArray());
                CollectionAssert.AreEquivalent(initialMachine, fixture.Environment.Machine.ToArray());
                Assert.AreEqual(initialJson, File.ReadAllText(ownedFile));
                Assert.AreEqual(1, files.Writes.Count);

                await fixture.Work.CompleteActionAsync(0);
                Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
                fixture.Work.DrainRefreshes();

                Assert.AreEqual("original_TMP", fixture.Environment.User["TMP"]);
                Assert.AreEqual("original_user_path", fixture.Environment.User["PATH"]);
                Assert.IsFalse(fixture.Environment.User.ContainsKey("TMP_PowerToys_Test_profile_1"));
                Assert.IsFalse(fixture.Environment.User.ContainsKey("PATH_PowerToys_Test_profile_1"));
                Assert.IsFalse(fixture.Environment.User.ContainsKey("profile_1_variable_1"));
                Assert.AreEqual(3, fixture.Environment.User.Count);
                Assert.AreEqual("raw%USER_SENTINEL%", fixture.Environment.User["KEEP_USER"]);
                CollectionAssert.AreEquivalent(initialMachine, fixture.Environment.Machine.ToArray());
                CollectionAssert.AreEqual(
                    ActiveProfileDeleteOrder,
                    fixture.Environment.Operations.Where(item => item.Operation == "Delete").Select(item => item.Name).ToArray());
                Assert.AreEqual(1, fixture.Environment.Notifications.Count);
                Assert.AreEqual("machine_path;original_user_path", vm.AppliedVariables.Single(item => item.ParentType == VariablesSetType.Path).Values);
                Assert.AreEqual("original_TMP", vm.AppliedVariables.Single(item => item.Name == "TMP").Values);
                Assert.IsFalse(vm.AppliedVariables.Any(item => item.Name == "profile_1_variable_1"));
                Assert.IsFalse(vm.AppliedVariables.Any(item => item.ParentType == VariablesSetType.Profile || item.IsAppliedFromProfile || item.Name.EndsWith("_PowerToys_Test_profile_1", StringComparison.Ordinal)));
                Assert.AreEqual(initialJson, File.ReadAllText(ownedFile));
                Assert.AreEqual(1, files.Writes.Count);
                TestContext.WriteLine("Released active UnApply and drained its refresh: TMP/PATH originals restored; exact backups removed; Machine and unrelated sentinels unchanged.");

                await fixture.Work.CompleteSaveAsync(0);

                Assert.AreEqual(2, files.Writes.Count);
                var intermediateJson = File.ReadAllText(ownedFile);
                using (var intermediate = JsonDocument.Parse(intermediateJson))
                {
                    Assert.AreEqual(1, intermediate.RootElement.GetArrayLength());
                    var survivor = intermediate.RootElement[0];
                    Assert.AreEqual(second.Id, survivor.GetProperty("Id").GetGuid());
                    Assert.AreEqual(second.Name, survivor.GetProperty("Name").GetString());
                    Assert.IsFalse(survivor.GetProperty("IsEnabled").GetBoolean());
                }

                TestContext.WriteLine($"First VM-scheduled real-service save, independent physical JSON: {intermediateJson}");
                var restoredUser = fixture.Environment.User.ToArray();
                var restoredOperations = fixture.Environment.Operations.ToArray();

                vm.RemoveProfile(inactive);

                Assert.AreEqual(0, vm.Profiles.Count);
                Assert.AreEqual(1, fixture.Work.ActionCount);
                Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
                Assert.AreEqual(2, fixture.Work.SaveCount);
                Assert.AreEqual(2, files.Writes.Count);
                Assert.AreEqual(intermediateJson, File.ReadAllText(ownedFile));
                await fixture.Work.CompleteSaveAsync(1);

                Assert.AreEqual(3, files.Writes.Count);
                var finalJson = File.ReadAllText(ownedFile);
                using (var final = JsonDocument.Parse(finalJson))
                {
                    Assert.AreEqual(JsonValueKind.Array, final.RootElement.ValueKind);
                    Assert.AreEqual(0, final.RootElement.GetArrayLength());
                }

                foreach (var removed in new[] { first, second })
                {
                    Assert.IsFalse(finalJson.Contains(removed.Id.ToString("D"), StringComparison.Ordinal));
                    Assert.IsFalse(finalJson.Contains(removed.Name, StringComparison.Ordinal));
                }

                CollectionAssert.AreEquivalent(restoredUser, fixture.Environment.User.ToArray());
                CollectionAssert.AreEquivalent(initialMachine, fixture.Environment.Machine.ToArray());
                CollectionAssert.AreEqual(restoredOperations, fixture.Environment.Operations.ToArray());
                Assert.AreEqual(1, fixture.Environment.Notifications.Count);
                Assert.AreEqual(1, fixture.Work.ActionCount);
                Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        ("WriteAllTextAsync", (string?)canonicalPath),
                        ("Exists", canonicalPath),
                        ("ReadAllText", canonicalPath),
                        ("WriteAllTextAsync", canonicalPath),
                        ("WriteAllTextAsync", canonicalPath),
                    },
                    files.Calls.ToArray());
                Assert.IsTrue(files.Writes.All(item => item.CancellationToken == CancellationToken.None));
                Assert.AreEqual(0, files.Files.Count);
                Assert.AreEqual(0, fixture.Service.Writes.Count);
                Assert.IsFalse(fixture.Logger.Entries.Any(item => item.Level == "Error"));
                Assert.IsTrue(fixture.Work.IsQuiescent);
                TestContext.WriteLine($"Removed IDs: {first.Id}, {second.Id}; names: {first.Name}, {second.Name}; independent final physical JSON: {finalJson}");
            }
            finally
            {
                try
                {
                    if (startedWrite != null)
                    {
                        await startedWrite;
                    }
                }
                finally
                {
                    try
                    {
                        await fixture.Work.SettleAsync();
                    }
                    finally
                    {
                        RequireQuiescentCleanup();
                        if (ownsDirectory)
                        {
                            File.Delete(ownedFile);
                            Directory.Delete(ownedDirectory, recursive: false);
                            TestContext.WriteLine($"Cleanup completed: exact owned file and empty nonrecursive directory removed: {ownedDirectory}");
                        }
                    }
                }
            }
        }
    }
}
