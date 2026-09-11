// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.FuzzTests;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class FuzzSmokeTests
{
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
