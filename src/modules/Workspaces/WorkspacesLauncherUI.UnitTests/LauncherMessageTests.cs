// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.IPC;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LauncherMessageTests
    {
        public static IEnumerable<object[]> MalformedMessages()
        {
            foreach (var json in new[]
            {
                string.Empty,
                "{",
                "null",
                "[]",
                "{}",
                """{"protocolVersion":2,"type":"shutdown"}""",
                """{"protocolVersion":"1","type":"shutdown"}""",
                """{"protocolVersion":1.5,"type":"shutdown"}""",
                """{"protocolVersion":2147483648,"type":"shutdown"}""",
                """{"protocolVersion":1,"type":null}""",
                """{"protocolVersion":1,"type":"future"}""",
                """{"protocolVersion":1,"type":"Shutdown"}""",
                """{"protocolVersion":1,"type":"shutdown","type":"shutdown"}""",
                """{"protocolVersion":1,"type":"shutdown","\u0074ype":"shutdown"}""",
                """{"protocolVersion":1,"type":"shutdown","extra":{"x":1,"x":2}}""",
                """{"protocolVersion":1,"type":"shutdown","extra":[{"x":1,"x":2}]}""",
                """{"protocolVersion":1,"type":"shutdown","extra":"\u0000"}""",
                """{"protocolVersion":1,"type":"shutdown","\u0000":"x"}""",
                """{"protocolVersion":1,"type":"shutdown","extra":"\uD800"}""",
                """{"protocolVersion":1,"type":"shutdown","extra":"\uDC00"}""",
                LauncherProtocolTestData.Shutdown + "\0",
                LauncherProtocolTestData.Shutdown + "{}",
                """{"protocolVersion":1,"type":"dismiss-warning"}""",
                """{"protocolVersion":1,"type":"dismiss-warning","requestId":5}""",
                """{"protocolVersion":1,"type":"dismiss-warning","requestId":"not-a-guid"}""",
                """{"protocolVersion":1,"type":"dismiss-warning","requestId":""}""",
            })
            {
                yield return new object[] { json };
            }

            foreach (var field in new[] { "requestId", "appName", "path", "arguments", "reason", "status" })
            {
                var missing = JsonNode.Parse(LauncherProtocolTestData.Warning).AsObject();
                missing.Remove(field);
                yield return new object[] { missing.ToJsonString() };
                foreach (var invalid in new[] { "null", "42", "false", "{}", "[]" })
                {
                    var wrongType = JsonNode.Parse(LauncherProtocolTestData.Warning);
                    wrongType[field] = JsonNode.Parse(invalid);
                    yield return new object[] { wrongType.ToJsonString() };
                }
            }
        }

        [TestMethod]
        public void ParsesEveryMessageKindUsingRealDataWrappers()
        {
            var warning = Parse(LauncherProtocolTestData.Warning);
            Assert.AreEqual("elevation-warning", warning.Type);
            Assert.AreEqual(Guid.Parse(LauncherProtocolTestData.RequestId), warning.RequestId);
            Assert.AreEqual(LauncherProtocolTestData.RequestId, warning.Warning.RequestId);
            Assert.AreEqual("Example", warning.Warning.AppName);
            Assert.AreEqual(@"C:\Example\app.exe", warning.Warning.Path);
            Assert.AreEqual("--example", warning.Warning.Arguments);
            Assert.AreEqual("unsigned", warning.Warning.Reason);
            Assert.AreEqual("untrusted", warning.Warning.Status);
            Assert.AreEqual(warning.RequestId, Parse(LauncherProtocolTestData.Dismiss).RequestId);
            Assert.AreEqual("shutdown", Parse(LauncherProtocolTestData.Shutdown).Type);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        public void ParsesAllKnownLaunchStatesAndNestedFields(int state)
        {
            var message = Parse(LauncherProtocolTestData.LaunchStatus(123, state));
            Assert.AreEqual("launch-status", message.Type);
            Assert.AreEqual(123, message.LaunchStatus.LauncherProcessID);
            var infos = message.LaunchStatus.AppLaunchInfos.AppLaunchInfoList;
            Assert.HasCount(1, infos);
            Assert.AreEqual((LaunchingState)state, infos[0].State);
            var app = infos[0].Application;
            Assert.AreEqual("Example", app.Application);
            Assert.AreEqual(@"C:\Example\app.exe", app.ApplicationPath);
            Assert.AreEqual("Example title", app.Title);
            Assert.AreEqual(string.Empty, app.PackageFullName);
            Assert.AreEqual(string.Empty, app.AppUserModelId);
            Assert.AreEqual(string.Empty, app.PwaAppId);
            Assert.AreEqual("--example", app.CommandLineArguments);
            Assert.IsFalse(app.IsElevated);
            Assert.IsTrue(app.CanLaunchElevated);
            Assert.IsFalse(app.Minimized);
            Assert.IsTrue(app.Maximized);
            Assert.AreEqual(2, app.Monitor);
            Assert.AreEqual(new PositionWrapper { X = -100, Y = 200, Width = 800, Height = 600 }, app.Position);
        }

        [DataTestMethod]
        [DynamicData(nameof(MalformedMessages), DynamicDataSourceType.Method)]
        public void RejectsMalformedJsonAndInvalidFields(string json)
        {
            try
            {
                Parse(json);
                Assert.Fail("Invalid message was accepted.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException or EncoderFallbackException or InvalidOperationException)
            {
            }
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(6)]
        [DataRow(int.MaxValue)]
        public void RejectsUnknownLaunchState(int state)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => Parse(LauncherProtocolTestData.LaunchStatus(123, state)));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(124)]
        public void RejectsMismatchedLaunchStatusProcess(int processId)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => Parse(LauncherProtocolTestData.LaunchStatus(processId)));
        }

        [DataTestMethod]
        [DataRow("application-path", "null")]
        [DataRow("is-elevated", "1")]
        [DataRow("can-launch-elevated", "\"true\"")]
        [DataRow("minimized", "null")]
        [DataRow("maximized", "{}")]
        [DataRow("monitor", "1.5")]
        [DataRow("monitor", "2147483648")]
        [DataRow("position", "[]")]
        public void RejectsIncorrectNestedApplicationFieldTypes(string field, string value)
        {
            var root = JsonNode.Parse(LauncherProtocolTestData.LaunchStatus(123));
            root["apps"]["appLaunchInfos"][0]["application"][field] = JsonNode.Parse(value);
            Assert.ThrowsExactly<InvalidDataException>(() => Parse(root.ToJsonString()));
        }

        [DataTestMethod]
        [DataRow("processId", "null")]
        [DataRow("processId", "\"123\"")]
        [DataRow("processId", "123.5")]
        [DataRow("processId", "2147483648")]
        [DataRow("apps", "null")]
        [DataRow("apps", "[]")]
        [DataRow("apps", "{}")]
        [DataRow("apps", "{\"appLaunchInfos\":{}}")]
        [DataRow("apps", "{\"appLaunchInfos\":[null]}")]
        [DataRow("apps", "{\"appLaunchInfos\":[{\"state\":0}]}")]
        public void RejectsInvalidLaunchStatusStructure(string field, string json)
        {
            var root = JsonNode.Parse(LauncherProtocolTestData.LaunchStatus(123));
            root[field] = JsonNode.Parse(json);
            Assert.ThrowsExactly<InvalidDataException>(() => Parse(root.ToJsonString()));
        }

        [TestMethod]
        public void RejectsMissingNestedApplicationAndPositionFields()
        {
            var valid = JsonNode.Parse(LauncherProtocolTestData.LaunchStatus(123));
            var application = valid["apps"]["appLaunchInfos"][0]["application"].AsObject();
            foreach (var field in application)
            {
                var root = valid.DeepClone();
                root["apps"]["appLaunchInfos"][0]["application"].AsObject().Remove(field.Key);
                Assert.ThrowsExactly<InvalidDataException>(() => Parse(root.ToJsonString()), field.Key);
            }

            foreach (var field in new[] { "X", "Y", "width", "height" })
            {
                var root = valid.DeepClone();
                var position = root["apps"]["appLaunchInfos"][0]["application"]["position"].AsObject();
                position.Remove(field);
                Assert.ThrowsExactly<InvalidDataException>(() => Parse(root.ToJsonString()), field);
                position[field] = "0";
                Assert.ThrowsExactly<InvalidDataException>(() => Parse(root.ToJsonString()), field);
            }
        }

        [TestMethod]
        public void AcceptsEmptyLaunchStatusAndIgnoresNonDuplicateExtensionFields()
        {
            var root = JsonNode.Parse(LauncherProtocolTestData.LaunchStatus(123));
            root["apps"]["appLaunchInfos"] = new JsonArray();
            root["extension"] = new JsonObject { ["text"] = "valid" };
            Assert.HasCount(0, Parse(root.ToJsonString()).LaunchStatus.AppLaunchInfos.AppLaunchInfoList);
        }

        [TestMethod]
        public void RejectsMalformedUtf8InsteadOfReplacingBytes()
        {
            foreach (var invalid in new[] { new byte[] { 0xff }, new byte[] { 0xc0, 0x80 }, new byte[] { 0xed, 0xa0, 0x80 }, new byte[] { 0xf0, 0x9f } })
            {
                Assert.ThrowsExactly<DecoderFallbackException>(() => LauncherMessage.Parse(invalid, 123));
            }
        }

        [TestMethod]
        public void PreservesValidUnicodeAndEscapedRawWarningValues()
        {
            const string raw = "Résumé 😀 \u202Ehidden\u202C\r\n--value";
            var root = JsonNode.Parse(LauncherProtocolTestData.Warning);
            root["arguments"] = raw;
            Assert.AreEqual(raw, Parse(root.ToJsonString()).Warning.Arguments);
        }

        [TestMethod]
        public void DecodedWarningEscapesOnlyDisplayPropertiesAndKeepsCopyDetailsRaw()
        {
            const string json = """
                {"protocolVersion":1,"type":"elevation-warning","requestId":"{01234567-89ab-cdef-0123-456789abcdef}",
                 "appName":"App\r\n\u202Ename","path":"C:\\Example\\\u202Eapp.exe",
                 "arguments":"--one\r\n--two\u202E","reason":"unsigned","status":"untrusted"}
                """;
            const string appName = "App\r\n\u202Ename";
            const string path = "C:\\Example\\\u202Eapp.exe";
            const string arguments = "--one\r\n--two\u202E";

            var warning = Parse(json).Warning;
            Assert.AreEqual(@"App\r\n\u202Ename", warning.DisplayAppName);
            Assert.AreEqual(@"C:\Example\\u202Eapp.exe", warning.DisplayPath);
            Assert.AreEqual(@"--one\r\n--two\u202E", warning.DisplayArguments);
            Assert.IsTrue(warning.HasEscapedDisplayValues);
            Assert.AreEqual(appName, warning.AppName);
            Assert.AreEqual(path, warning.Path);
            Assert.AreEqual(arguments, warning.Arguments);
            StringAssert.Contains(warning.DetailsText, appName);
            StringAssert.Contains(warning.DetailsText, path);
            StringAssert.Contains(warning.DetailsText, arguments);
        }

        private static LauncherMessage Parse(string json)
        {
            return LauncherMessage.Parse(Encoding.UTF8.GetBytes(json), 123);
        }
    }
}
