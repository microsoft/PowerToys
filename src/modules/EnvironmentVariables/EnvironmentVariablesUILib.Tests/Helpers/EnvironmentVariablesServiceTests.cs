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

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;

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
