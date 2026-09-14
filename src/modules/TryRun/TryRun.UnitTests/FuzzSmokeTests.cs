// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;
using PowerToys.TryRun.FuzzTests;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class FuzzSmokeTests
{
    [TestMethod]
    public void MultiBackendRequestsHandleMutatedInputs()
    {
        var random = new Random(97);
        foreach (var kind in Enum.GetValues<WorkloadKind>())
        {
            var request = new ExecutionRequest("echo hello", "C:\\work", "C:\\temp", 30)
            {
                Kind = kind,
                ApplicationPath = kind == WorkloadKind.WindowsApplication ? "C:\\app.exe" : null,
                FileRelativePath = kind == WorkloadKind.LinuxApplication ? "program" : null,
                WorkingSubdirectory = "package\\nested",
                Arguments = ["a'b\"c", "path with spaces"],
            };
            var seed = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
            RequestFuzzer.FuzzTarget(seed);
            for (var iteration = 0; iteration < 250; iteration++)
            {
                var mutation = seed.ToArray();
                mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
                RequestFuzzer.FuzzTarget(mutation);
            }
        }
    }

    [TestMethod]
    public void WorkspaceNamesPreviewsAndFileRoundTripsHandleFuzzInputs()
    {
        var random = new Random(73);
        foreach (var seed in new[] { "..\\escape", "CON.txt", "file.txt:stream", "folder/你好.txt", "\\\\?\\C:\\file", "result.txt", "a\0b" })
        {
            WorkspaceFuzzer.FuzzTarget(Encoding.UTF8.GetBytes(seed));
        }

        for (var index = 0; index < 1000; index++)
        {
            var data = new byte[random.Next(1, 10000)];
            random.NextBytes(data);
            WorkspaceFuzzer.FuzzTarget(data);
        }

        foreach (var name in new[] { "result.txt", "nested/你好.txt", "binary.bin" })
        {
            var encoded = Encoding.UTF8.GetBytes(name);
            for (var index = 0; index < 16; index++)
            {
                var data = new byte[1 + encoded.Length + random.Next(0, 512)];
                random.NextBytes(data);
                data[0] = (byte)encoded.Length;
                encoded.CopyTo(data, 1);
                WorkspaceFuzzer.FileRoundTrip(data);
            }
        }
    }

    [TestMethod]
    public void MalformedProtocolAndDiagnosticInputsDoNotEscapeValidation()
    {
        var random = new Random(42);
        foreach (var seed in new[] { "null", "{}", "{\"Script\":\"test\"}", "#< CLIXML\n<Objs><S S='Error'>error</S></Objs>" })
        {
            var bytes = Encoding.UTF8.GetBytes(seed);
            RequestFuzzer.FuzzTarget(bytes);
            for (var iteration = 0; iteration < 250; iteration++)
            {
                var mutation = bytes.ToArray();
                mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
                RequestFuzzer.FuzzTarget(mutation);
            }
        }

        for (var iteration = 0; iteration < 1000; iteration++)
        {
            var data = new byte[random.Next(2048)];
            random.NextBytes(data);
            RequestFuzzer.FuzzTarget(data);
        }
    }
}
