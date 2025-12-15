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
        return ImageResizerCliExecutor.RunStandalone(args);
    }
}
