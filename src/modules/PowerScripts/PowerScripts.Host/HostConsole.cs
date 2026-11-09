// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerScripts.Host;

internal static class HostConsole
{
    private static readonly AsyncLocal<string?> Input = new();

    internal static TextWriter Out => System.Console.Out;

    internal static TextWriter Error => System.Console.Error;

    internal static Encoding OutputEncoding
    {
        set
        {
            if (Input.Value is null)
            {
                System.Console.OutputEncoding = value;
            }
        }
    }

    internal static TextReader CreateInputReader() =>
        Input.Value is string input
            ? new StringReader(input)
            : new StreamReader(System.Console.OpenStandardInput(), Encoding.UTF8);

    internal static void WriteLine(string? value = null) => Out.WriteLine(value);

    internal static IDisposable Capture(string input, TextWriter output, TextWriter error)
    {
        var scope = new Scope(Input.Value, System.Console.Out, System.Console.Error);
        Input.Value = input;
        System.Console.SetOut(output);
        System.Console.SetError(error);
        return scope;
    }

    private sealed class Scope(string? input, TextWriter output, TextWriter error) : IDisposable
    {
        public void Dispose()
        {
            Input.Value = input;
            System.Console.SetOut(output);
            System.Console.SetError(error);
        }
    }
}
