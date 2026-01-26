// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;

using ImageResizer.Cli;
using ManagedCommon;

namespace ImageResizerCLI;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
            }
        }
        catch (CultureNotFoundException)
        {
            // Ignore invalid culture and fall back to default.
        }

        Console.InputEncoding = Encoding.Unicode;

        // Initialize logger to file (same as other modules)
        CliLogger.Initialize("\\Image Resizer\\CLI");
        CliLogger.Info($"ImageResizerCLI started with {args.Length} argument(s)");

        try
        {
            var executor = new ImageResizerCliExecutor();
            return executor.Run(args);
        }
        catch (Exception ex)
        {
            CliLogger.Error($"Unhandled exception: {ex.Message}");
            CliLogger.Error($"Stack trace: {ex.StackTrace}");
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }
}
