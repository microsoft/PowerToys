// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

if (args.Length < 1)
{
    Console.WriteLine("Usage: PowerToys.Peek.CLI.exe <path>");
    return;
}

using var pipe = new NamedPipeClientStream(".", "PeekPipe", PipeDirection.Out);
pipe.Connect();

using var writer = new StreamWriter(pipe);

writer.WriteLine(args[0]);
writer.Flush();
writer.Close();
pipe.Close();
