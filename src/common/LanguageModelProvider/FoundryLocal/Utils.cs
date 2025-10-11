// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace LanguageModelProvider.FoundryLocal;

internal static class Utils
{
    public static async Task<(string? Output, string? Error, int ExitCode)> RunFoundryWithArguments(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "foundry";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string? output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            string? error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);

            return (output, error, process.ExitCode);
        }
        catch
        {
            return (null, null, -1);
        }
    }
}
