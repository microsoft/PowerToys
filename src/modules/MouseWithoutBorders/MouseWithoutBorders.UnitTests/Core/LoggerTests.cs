// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

public static class LoggerTests
{
    [TestClass]
    public sealed class PrivateDumpTests
    {
        /*
        [TestMethod]
        public void PrivateDumpShouldGenerateExpectedOutput()
        {
            // this was used to create the contents of "Logger.PrivateDump.original.txt"
            // when the "Core.Logger" class was "Common" in "Common.Log.cs"

            // PrivateDump throws an ArgumentNullException if this is null
            Common.BinaryName = "MyBinary.dll";

            // magic number from Settings.cs
            var dumpObjectsLevel = 6;

            // copied from DumpObjects in Common.Log.cs
            var sb = new StringBuilder(1000000);
            var result = Common.PrivateDump(sb, new Common(), "[Other Logs]\r\n===============\r\n", 0, dumpObjectsLevel, false);
            var output = sb.ToString();
        }
        */

        [TestMethod]
        [Ignore(
            "This test relies on internal details of the dotnet platform and is sensitive to " +
            "the specific version of dotnet being used. As a result it's likely to fail if the " +
            "\"expected\" result was generated with a different version to the version used to " +
            "run the test, so we're going to ignore it in the CI build process.")]
        public void PrivateDumpShouldGenerateExpectedOutput()
        {
            static string NormalizeLog(string log)
            {
                var lines = log.Split("\r\n");

                // some parts of the PrivateDump output are impossible to reproduce -
                // e.g. random numbers, system timestamps, thread ids, etc, so we'll mask them
                var maskPrefixes = new string[]
                {
                    "----_s0 = ",
                    "----_s1 = ",
                    "----_s2 = ",
                    "----_s3 = ",
                    "<LastResumeSuspendTime>k__BackingField = ",
                    "--_dateData = ",
                    "lastJump = ",
                    "lastStartServiceTime = ",
                    "InitialIV = ",
                    "--_budget = ",
                };
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (var maskPrefix in maskPrefixes)
                    {
                        if (lines[i].StartsWith(maskPrefix, StringComparison.InvariantCulture))
                        {
                            // replace the trailing text with "?" characters
                            lines[i] = string.Concat(
                                lines[i].AsSpan(0, maskPrefix.Length),
                                new string('?', 12));
                        }
                    }
                }

                // hide some of the internals of concurrent dictionary lock tables
                // as the size can vary across machines
                var removeLines = new string[]
                {
                    "------[8] = 0",
                    "------[9] = 0",
                    "------[10] = 0",
                    "------[11] = 0",
                    "------[12] = 0",
                    "------[13] = 0",
                    "------[14] = 0",
                    "------[15] = 0",
                    "------[16] = 0",
                    "------[17] = 0",
                    "------[18] = 0",
                    "------[19] = 0",
                };
                lines = lines.Where(line => !removeLines.Contains(line)).ToArray();

                return string.Join("\r\n", lines);
            }

            // PrivateDump throws an ArgumentNullException if this is null
            Common.BinaryName = "MyBinary.dll";

            // default magic number from Settings.cs
            var settingsDumpObjectsLevel = 6;

            // get the expected test result from an embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{typeof(LoggerTests).Namespace}.Logger.PrivateDump.expected.txt";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException();
            using var streamReader = new StreamReader(stream);
            var expected = streamReader.ReadToEnd();

            // copied from DumpObjects in Common.Log.cs
            var sb = new StringBuilder(1000000);
            _ = Logger.PrivateDump(sb, Logger.AllLogs, "[Program logs]\r\n===============\r\n", 0, settingsDumpObjectsLevel, false);
            _ = Logger.PrivateDump(sb, new Common(), "[Other Logs]\r\n===============\r\n", 0, settingsDumpObjectsLevel, false);
            sb.AppendLine("[Logger]\r\n===============");
            Logger.DumpType(sb, typeof(Logger), 0, settingsDumpObjectsLevel);
            sb.AppendLine("[DragDrop]\r\n===============");
            Logger.DumpType(sb, typeof(DragDrop), 0, settingsDumpObjectsLevel);
            sb.AppendLine("[MachineStuff]\r\n===============");
            Logger.DumpType(sb, typeof(MachineStuff), 0, settingsDumpObjectsLevel);
            sb.AppendLine("[Receiver]\r\n===============");
            Logger.DumpType(sb, typeof(Receiver), 0, settingsDumpObjectsLevel);
            var actual = sb.ToString();

            expected = NormalizeLog(expected);
            actual = NormalizeLog(actual);

            // Azure DevOps truncates debug output which makes it hard to see where
            // the expected and actual differ, so we need to write a custom error message
            // so we can just focus on the differences between expected and actual
            var expectedLines = expected.Split("\r\n");
            var actualLines = actual.Split("\r\n");
            for (var i = 0; i < Math.Min(expectedLines.Length, actualLines.Length); i++)
            {
                if (actualLines[i] != expectedLines[i])
                {
                    var message = new StringBuilder();
                    message.AppendLine(CultureInfo.InvariantCulture, $"{nameof(actual)} and {nameof(expected)} differ at line {i}:");

                    message.AppendLine();
                    message.AppendLine($"{nameof(actual)}:");
                    for (var j = i; j < Math.Min(i + 5, actualLines.Length); j++)
                    {
                        message.AppendLine(CultureInfo.InvariantCulture, $"[{j}]: {actualLines[j]}:");
                    }

                    message.AppendLine();
                    message.AppendLine($"{nameof(expected)}:");
                    for (var j = i; j < Math.Min(i + 5, expectedLines.Length); j++)
                    {
                        message.AppendLine(CultureInfo.InvariantCulture, $"[{j}]: {expectedLines[j]}:");
                    }

                    Assert.Fail(message.ToString());
                }
            }

            // finally, throw an exception if the two don't match
            // just in case the above doesn't spot a difference
            // (e.g. different number of lines in the output)
            Assert.AreEqual(expected, actual);
        }
    }
}
