// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AdvancedPaste.Core;

namespace AdvancedPaste.LocalCli;

internal static class Program
{
    private const string Usage = "Usage: PowerToys.AdvancedPaste.LocalCLI --format plain-text|markdown|json < input";
    private const int MaximumInputCharacters = 16 * 1024 * 1024;

    public static int Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            return Run(args, Console.In, Console.Out, Console.Error, static () =>
                PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredAdvancedPasteEnabledValue() != PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled);
        }
        catch (Exception ex) when (ex is COMException or TypeInitializationException or DllNotFoundException)
        {
            Console.Error.WriteLine($"Unable to read Advanced Paste Group Policy: {ex.Message}");
            return 1;
        }
    }

    internal static int Run(string[] args, TextReader input, TextWriter output, TextWriter error, Func<bool> isEnabled)
    {
        if (args is ["--help"] or ["-h"])
        {
            output.WriteLine(Usage);
            return 0;
        }

        HeadlessTransformFormat? format = args is ["--format", var name] ? name switch
        {
            "plain-text" => HeadlessTransformFormat.PlainText,
            "markdown" => HeadlessTransformFormat.Markdown,
            "json" => HeadlessTransformFormat.Json,
            _ => null,
        }
        : null;
        if (format is null)
        {
            error.WriteLine(Usage);
            return 2;
        }

        if (!isEnabled())
        {
            error.WriteLine("Advanced Paste is disabled by Group Policy.");
            return 1;
        }

        try
        {
            var text = new StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length > MaximumInputCharacters - count)
                {
                    error.WriteLine("Text input exceeds the 16,777,216 character limit.");
                    return 1;
                }

                text.Append(buffer, 0, count);
            }

            var result = HeadlessTransformService.Transform(format.Value, text.ToString());
            output.Write(result);
            return 0;
        }
        catch (IOException ex)
        {
            error.WriteLine($"Local transformation I/O failed: {ex.Message}");
            return 1;
        }
    }
}
