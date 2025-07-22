// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;

if (args.Length < 1)
{
    Console.WriteLine("Usage: PowerToys.Peek.CLI.exe <path>");
    return 1; // Return error code
}

try
{
    using var pipe = new NamedPipeClientStream(".", "PeekPipe", PipeDirection.Out);
    pipe.Connect(5000); // 5 second timeout

    using var writer = new StreamWriter(pipe) { AutoFlush = true };
    writer.WriteLine(args[0]);
    return 0;
}
catch (TimeoutException)
{
    Console.WriteLine("Error: Could not connect to PowerToys. Make sure PowerToys Peek is running.");
    return 2;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 3;
}
