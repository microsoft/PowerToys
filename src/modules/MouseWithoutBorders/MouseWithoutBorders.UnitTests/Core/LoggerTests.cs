// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            // this was used to create the contents of "logger.privatedump.original.txt"
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
        public void PrivateDumpShouldGenerateExpectedOutput()
        {
            // some parts of the PrivateDump output are impossible to reproduce -
            // e.g. random numbers, system timestamps, thread ids, etc, so we'll need
            // to normalize the output before we can compare it with the expected value
            static string NormalizeLog(string log)
            {
                var lines = log.Split("\r\n");
                var prefixes = new string[]
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
                };
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (var prefix in prefixes)
                    {
                        if (lines[i].StartsWith(prefix, StringComparison.InvariantCulture))
                        {
                            // replace the trailing text with "?" characters
                            lines[i] = string.Concat(
                                lines[i].AsSpan(0, prefix.Length),
                                new string('?', 12));
                        }
                    }
                }

                return string.Join("\r\n", lines);
            }

            // PrivateDump throws an ArgumentNullException if this is null
            Common.BinaryName = "MyBinary.dll";

            // default magic number from Settings.cs
            var settingsDumpObjectsLevel = 6;

            // get the expected test result from an embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{typeof(LoggerTests).Namespace}.logger.privatedump.expected.txt";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException();
            using var streamReader = new StreamReader(stream);
            var expected = streamReader.ReadToEnd();

            // copied from DumpObjects in Common.Log.cs
            var sb = new StringBuilder(1000000);
            _ = Logger.PrivateDump(sb, Logger.AllLogs, "[Program logs]\r\n===============\r\n", 0, settingsDumpObjectsLevel, false);
            _ = Logger.PrivateDump(sb, new Common(), "[Other Logs]\r\n===============\r\n", 0, settingsDumpObjectsLevel, false);
            sb.AppendLine("[Logger Logs]\r\n===============");
            Logger.DumpType(sb, typeof(Logger), 0, settingsDumpObjectsLevel);
            var actual = sb.ToString();

            expected = NormalizeLog(expected);
            actual = NormalizeLog(actual);
            Assert.AreEqual(expected, actual);
        }
    }
}
