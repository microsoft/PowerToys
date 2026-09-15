// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.IPC;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LauncherArgumentsTests
    {
        private const string PipeName = "PowerToys.Workspaces.Launcher.{01234567-89ab-cdef-0123-456789abcdef}";

        public static IEnumerable<object[]> InvalidArguments()
        {
            yield return new object[] { Array.Empty<string>() };
            yield return new object[] { new[] { "--launcher-pid", "1" } };
            yield return new object[] { new[] { "--launcher-pid", "1", "--ipc-name" } };
            yield return new object[] { new[] { "--launcher-pid", "1", "--ipc-name", PipeName, "--extra" } };
            yield return new object[] { new[] { "--launcher-pid", "1", "--launcher-pid", "2" } };
            yield return new object[] { new[] { "--ipc-name", PipeName, "--ipc-name", PipeName } };
            yield return new object[] { new[] { "--unknown", "1", "--ipc-name", PipeName } };
            yield return new object[] { new[] { "--Launcher-pid", "1", "--ipc-name", PipeName } };
            yield return new object[] { new[] { "--launcher-pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "--ipc-name", PipeName } };
            foreach (var pid in new[] { null, string.Empty, "0", "-1", "+1", " 1", "1 ", "1.0", "1e2", "2147483648", "１", "1\0" })
            {
                yield return new object[] { new[] { "--launcher-pid", pid, "--ipc-name", PipeName } };
            }

            foreach (var name in new[]
            {
                null,
                string.Empty,
                "Other.{01234567-89ab-cdef-0123-456789abcdef}",
                PipeName.ToLowerInvariant(),
                PipeName.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal),
                PipeName + " ",
                PipeName + "\\child",
                PipeName + "\0",
                "PowerToys.Workspaces.Launcher.{not-a-guid}",
            })
            {
                yield return new object[] { new[] { "--launcher-pid", "1", "--ipc-name", name } };
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AcceptsBothArgumentOrdersAndPreservesPipeName(bool reversed)
        {
            var name = PipeName.Replace("abcdef", "ABCDEF", StringComparison.Ordinal);
            var args = reversed ? new[] { "--ipc-name", name, "--launcher-pid", "2147483647" }
                : new[] { "--launcher-pid", "2147483647", "--ipc-name", name };

            Assert.IsTrue(LauncherConnection.TryParseArguments(args, out var pid, out var pipe));
            Assert.AreEqual(int.MaxValue, pid);
            Assert.AreEqual(name, pipe);
        }

        [DataTestMethod]
        [DynamicData(nameof(InvalidArguments), DynamicDataSourceType.Method)]
        public void RejectsMalformedOrDuplicateStartupArguments(string[] args)
        {
            Assert.IsFalse(LauncherConnection.TryParseArguments(args, out _, out _));
        }
    }
}
