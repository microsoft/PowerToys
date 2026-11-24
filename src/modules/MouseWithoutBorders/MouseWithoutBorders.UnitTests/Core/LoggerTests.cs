// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

public static class LoggerTests
{
    [TestClass]
    public sealed class MaskTests
    {
        [TestMethod]
        [DataRow("SampleMachineA", "SxxxxxMxxxxxxA")]
        [DataRow("ExampleMachineA", "ExxxxxxMxxxxxxA")]
        [DataRow("DEVBOX123", "DxxxOxxx3")]
        [DataRow("ABCD", "ABxD")]
        [DataRow("1234", "12x4")]
        [DataRow("\U0001F600ABCD\U0001F600", "\U0001F600xBxx\U0001F600")]
        [DataRow("A\U0001F600B\U0001F600C", "AxxBxxC")]
        public void MasksNamesKeepingFirstMiddleAndLastCharacters(string original, string expected)
        {
            Assert.AreEqual(expected, Logger.MaskName(original));
        }

        [TestMethod]
        [DataRow("A", "x")]
        [DataRow("AB", "xx")]
        [DataRow("ABC", "xxx")]
        [DataRow("", "")]
        [DataRow(null, null)]
        public void DoesNotRevealShortNamesAndPreservesAbsentValues(string? original, string? expected)
        {
            Assert.AreEqual(expected, Logger.MaskName(original));
        }

        [TestMethod]
        [DataRow("192.16.10.42", "xxx.xx.xx.42")]
        [DataRow("192.168.0.67", "xxx.xxx.x.67")]
        [DataRow("10.20.30.255", "xx.xx.xx.255")]
        [DataRow("0.1.255.0", "x.x.xxx.0")]
        [DataRow("::ffff:192.16.10.42", "xxx.xx.xx.42")]
        public void MasksIpv4AddressesKeepingOnlyLastOctet(string original, string expected)
        {
            Assert.AreEqual(expected, Logger.MaskAddress(IPAddress.Parse(original)));
            Assert.AreEqual(expected, Logger.MaskAddress(original));
        }

        [TestMethod]
        [DataRow("2001:db8:1234:5678::42")]
        [DataRow("::1")]
        public void MasksIpv6AddressesWithoutExposingTheirPrefix(string original)
        {
            Assert.AreEqual("xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx", Logger.MaskAddress(IPAddress.Parse(original)));
            Assert.AreEqual("xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx", Logger.MaskAddress(original));
        }

        [TestMethod]
        [DataRow("192.16.10.42:15100", "xxx.xx.xx.42:15100")]
        [DataRow("[2001:db8::42]:15100", "[xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx]:15100")]
        [DataRow("[::ffff:192.16.10.42]:15100", "xxx.xx.xx.42:15100")]
        public void MasksSocketAddressesAndPreservesTheirPorts(string original, string expected)
        {
            Assert.AreEqual(expected, Logger.MaskEndpoint(IPEndPoint.Parse(original)));
        }

        [TestMethod]
        [DataRow(null, null)]
        [DataRow("", "")]
        [DataRow("SampleMachineA", "<invalid IP address>")]
        public void AddressTextDoesNotFallBackToNameMasking(string? original, string? expected)
        {
            Assert.AreEqual(expected, Logger.MaskAddress(original));
        }

        [TestMethod]
        public void AbsentSocketValuesStayAbsent()
        {
            Assert.IsNull(Logger.MaskAddress((IPAddress?)null));
            Assert.IsNull(Logger.MaskEndpoint(null));
        }
    }

    [TestClass]
    public sealed class FormatExceptionTests
    {
        [TestMethod]
        public void SocketErrorsKeepTheirCodesWithoutTheirEndpoints()
        {
            var actual = Logger.FormatException(new EndpointSocketException());

            Assert.IsTrue(actual.Contains(nameof(SocketError.ConnectionRefused), StringComparison.Ordinal));
            Assert.IsFalse(actual.Contains("192.0.2.42", StringComparison.Ordinal));
        }

        [TestMethod]
        public void NestedSocketErrorsAreMaskedInBothExceptionMessages()
        {
            var inner = new EndpointSocketException();
            var exception = new IOException($"Connection failed: {inner.Message}", inner);

            var actual = Logger.FormatException(exception);

            Assert.IsTrue(actual.Contains("Connection failed:", StringComparison.Ordinal));
            Assert.IsTrue(actual.Contains(nameof(SocketError.ConnectionRefused), StringComparison.Ordinal));
            Assert.IsFalse(actual.Contains("192.0.2.42", StringComparison.Ordinal));
        }

        [TestMethod]
        public void OtherExceptionDetailsRemainUnchanged()
        {
            var exception = new InvalidOperationException("The selected operation is not available.");

            Assert.AreEqual(exception.ToString(), Logger.FormatException(exception));
        }

        private sealed class EndpointSocketException : SocketException
        {
            public EndpointSocketException()
                : base((int)SocketError.ConnectionRefused)
            {
            }

            public override string Message => "Connection refused by 192.0.2.42:15100";
        }
    }

    [TestClass]
    public sealed class GetStackTraceTests
    {
        [TestMethod]
        public void GetStackTraceShouldReturnCorrectValue()
        {
            // if we get the stack trace from the current test method with
            // "new StackTrace()" it's incredibly deep as it contains the
            // full MSTest call stack so we'll create a Task to capture a
            // much shallower stack trace from inside the task
            var stackTrace = default(StackTrace);
            Task.Run(() =>
            {
                void MyMethod1()
                {
                    MyMethod2();
                }

                void MyMethod2()
                {
                    stackTrace = new StackTrace();
                }

                MyMethod1();
            }).Wait();

            /*
            // even with the above, the stack trace can still vary depending on the test runner host
            // being used (e.g. visual studio vs dotnet test) and the version of dotnet being used -
            // e.g.
            //
            //   Void<GetStackTraceShouldReturnCorrectValue> g__MyMethod2| 2()
            //   <= Void <GetStackTraceShouldReturnCorrectValue> g__MyMethod1 | 1()
            //   <= Void <GetStackTraceShouldReturnCorrectValue> b__0()
            //   <= Void RunFromThreadPoolDispatchLoop(System.Threading.Thread, System.Threading.ExecutionContext, System.Threading.ContextCallback, System.Object)
            //           ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
            //   <= Void ExecuteWithThreadLocal(System.Threading.Tasks.Task ByRef, System.Threading.Thread)
            //   ... etc ...
            //
            // versus
            //
            //   Void<GetStackTraceShouldReturnCorrectValue> g__MyMethod2| 2()
            //   <= Void <GetStackTraceShouldReturnCorrectValue> g__MyMethod1 | 1()
            //   <= Void <GetStackTraceShouldReturnCorrectValue> b__0()
            //   <= Void RunInternal(System.Threading.ExecutionContext, System.Threading.ContextCallback, System.Object)
            //           ^^^^^^^^^^^
            //   <= Void ExecuteWithThreadLocal(System.Threading.Tasks.Task ByRef, System.Threading.Thread)
            //   ... etc ...
            //
            // so we're going to ignore everything after our task code.
            //
            // however, this is *still* inherently unstable due to compiler-generated uniqueness keys - e.g.
            //
            //   Void <GetStackTraceShouldReturnCorrectValue>g__MyMethod1|2()
            //                                                            ^
            //
            // versus
            //
            //   Void <GetStackTraceShouldReturnCorrectValue>g__MyMethod1|1()
            //                                                            ^
            // but we'll try to suppress the differences with a normalization regex
            // (which is giving diminishing returns on the value of this test, but
            // we'll persevere...)
             */

            string NormalizeStackTrace(string stackTrace, [CallerMemberName] string caller = "")
            {
                return string.Join(
                    " <= ",
                    stackTrace
                        .Split(" <= ")
                        .TakeWhile(line => line.Contains(caller, StringComparison.InvariantCulture))
                        .Select(f => f.Split('|')[0])
                        .ToList());
            }

            var expected = NormalizeStackTrace(
                "Void <GetStackTraceShouldReturnCorrectValue>g__MyMethod2|2()" +
                " <= Void <GetStackTraceShouldReturnCorrectValue>g__MyMethod1|1()" +
                " <= Void <GetStackTraceShouldReturnCorrectValue>b__0()");

            var actual = NormalizeStackTrace(
                Logger.GetStackTrace(stackTrace));

            Assert.AreEqual(expected, actual);
        }
    }

    [TestClass]
    [DoNotParallelize]
    public sealed class PrivateDumpTests
    {
        [TestMethod]
        [DataRow("myKey")]
        [DataRow("MYKEY")]
        [DataRow("SecurityKey")]
        [DataRow("securitykey")]
        [DataRow("<MyKey>k__BackingField")]
        [DataRow("<SecurityKey>k__BackingField")]
        [DataRow("_securityCode")]
        [DataRow("_SECURITYCODE")]
        [DataRow("SecurityCodeField")]
        [DataRow("securitycodefield")]
        public void KnownKeyFieldsAreOmittedBeforeFormatting(string fieldName)
        {
            var output = new StringBuilder();
            var method = typeof(Logger).GetMethod("PrivateDump", BindingFlags.NonPublic | BindingFlags.Static)!;

            var included = (bool)method.Invoke(null, new object[] { output, new UnformattableDumpValue(), fieldName, 0, 6, true })!;

            Assert.IsFalse(included);
            Assert.AreEqual(0, output.Length);
        }

        [TestMethod]
        public void StoredKeyIsOmittedWithoutChangingOtherSettings()
        {
            var previousBinaryName = Common.BinaryName;
            try
            {
                Common.BinaryName = "DumpTestBinary";
                var settings = new MouseWithoutBordersProperties
                {
                    SecurityKey = new StringProperty("synthetic-stored-key"),
                    ShareClipboard = true,
                };
                var output = new StringBuilder();
                var method = typeof(Logger).GetMethod("PrivateDump", BindingFlags.NonPublic | BindingFlags.Static)!;

                method.Invoke(null, new object[] { output, settings, "settings", 0, 6, true });

                var dump = output.ToString();
                Assert.IsFalse(dump.Contains("SecurityKey", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(dump.Contains("synthetic-stored-key", StringComparison.Ordinal));
                StringAssert.Contains(dump, "<ShareClipboard>k__BackingField = True");
                Assert.AreEqual("synthetic-stored-key", settings.SecurityKey.Value);
            }
            finally
            {
                Common.BinaryName = previousBinaryName;
            }
        }

        [TestMethod]
        public void RuntimeKeyIsOmittedWithoutChangingOtherEncryptionDiagnostics()
        {
            var previousBinaryName = Common.BinaryName;
            var previousKey = Encryption.MyKey;
            try
            {
                Common.BinaryName = "DumpTestBinary";
                Encryption.MyKey = "synthetic-runtime-key";
                var output = new StringBuilder();
                var method = typeof(Logger).GetMethod("DumpType", BindingFlags.NonPublic | BindingFlags.Static)!;

                method.Invoke(null, new object[] { output, typeof(Encryption), 0, 2 });

                var dump = output.ToString();
                Assert.IsFalse(dump.Contains("myKey", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(dump.Contains("synthetic-runtime-key", StringComparison.Ordinal));
                StringAssert.Contains(dump, "SymAlBlockSize = ");
                Assert.AreEqual("synthetic-runtime-key", Encryption.MyKey);
            }
            finally
            {
                Encryption.MyKey = previousKey;
                Common.BinaryName = previousBinaryName;
            }
        }

        [TestMethod]
        public void PendingSetupKeyIsOmittedWithoutChangingActiveKeyOrOtherSetupDiagnostics()
        {
            var codeField = typeof(SetupPage2a).GetField("_securityCode", BindingFlags.NonPublic | BindingFlags.Static)!;
            var nameField = typeof(SetupPage2a).GetField("_computerName", BindingFlags.NonPublic | BindingFlags.Static)!;
            var previousCode = codeField.GetValue(null);
            var previousName = nameField.GetValue(null);
            var previousBinaryName = Common.BinaryName;
            var previousKey = Encryption.MyKey;
            try
            {
                Common.BinaryName = "DumpTestBinary";
                Encryption.MyKey = "synthetic-active-key";
                codeField.SetValue(null, "synthetic-pending-key");
                nameField.SetValue(null, "SetupTestMachine");
                var output = new StringBuilder();
                var method = typeof(Logger).GetMethod("PrivateDump", BindingFlags.NonPublic | BindingFlags.Static)!;

                foreach (var field in new[] { codeField, nameField })
                {
                    method.Invoke(null, new object[] { output, field.GetValue(null)!, field.Name, 0, 2, true });
                }

                var dump = output.ToString();
                Assert.IsFalse(dump.Contains("_securityCode", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(dump.Contains("synthetic-pending-key", StringComparison.Ordinal));
                StringAssert.Contains(dump, "_computerName = SetupTestMachine");
                Assert.AreEqual("synthetic-active-key", Encryption.MyKey);
                Assert.AreEqual("synthetic-pending-key", codeField.GetValue(null));
            }
            finally
            {
                codeField.SetValue(null, previousCode);
                nameField.SetValue(null, previousName);
                Encryption.MyKey = previousKey;
                Common.BinaryName = previousBinaryName;
            }
        }

        private sealed class UnformattableDumpValue
        {
            public override string ToString()
            {
                throw new InvalidOperationException("Known key fields must be skipped before formatting.");
            }
        }

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
                // e.g. random numbers, system timestamps, thread ids, etc., so we'll mask them
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

            // copied from DumpObjects in Logger.cs
            var sb = new StringBuilder(1000000);
            Logger.DumpProgramLogs(sb, settingsDumpObjectsLevel);
            Logger.DumpStaticTypes(sb, settingsDumpObjectsLevel);
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
