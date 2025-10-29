// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Resources;
using PowerToys.DSC.UnitTests.Models;

namespace PowerToys.DSC.UnitTests;

public class BaseDscTest
{
    private readonly ResourceManager _resourceManager;

    public BaseDscTest()
    {
        _resourceManager = new ResourceManager("PowerToys.DSC.Properties.Resources", typeof(PowerToys.DSC.Program).Assembly);
    }

    /// <summary>
    /// Returns the string resource for the given name, formatted with the provided arguments.
    /// </summary>
    /// <param name="name">The name of the resource string.</param>
    /// <param name="args">The arguments to format the resource string with.</param>
    /// <returns></returns>
    public string GetResourceString(string name, params string[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, _resourceManager.GetString(name, CultureInfo.InvariantCulture), args);
    }

    /// <summary>
    /// Execute a dsc command with the provided arguments.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="args"></param>
    /// <returns></returns>
    protected DscExecuteResult ExecuteDscCommand<T>(params string[] args)
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
