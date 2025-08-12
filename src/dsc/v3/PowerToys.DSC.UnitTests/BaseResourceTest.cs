// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using PowerToys.DSC.Commands;
using PowerToys.DSC.UnitTests.Models;

namespace PowerToys.DSC.UnitTests;

public class BaseResourceTest
{
    protected DscExecuteResult ExecuteCommand<T>(params string[] args)
        where T : Command, new()
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        var outSw = new StringWriter();
        var errSw = new StringWriter();

        try
        {
            Console.SetOut(outSw);
            Console.SetError(errSw);

            var executeResult = new T().Invoke(args);
            var output = outSw.ToString();
            var errorOutput = errSw.ToString();
            return new(executeResult == 0, output, errorOutput);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            outSw.Dispose();
            errSw.Dispose();
        }
    }
}
