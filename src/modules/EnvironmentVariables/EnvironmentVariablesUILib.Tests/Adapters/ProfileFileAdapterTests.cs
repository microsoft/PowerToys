// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;

namespace EnvironmentVariablesUILib.UnitTests.Adapters
{
    [TestClass]
    [DoNotParallelize]
    public class ProfileFileAdapterTests
    {
        public TestContext TestContext { get; set; } = null!;

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
    }
}
