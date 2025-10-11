// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LanguageModelProvider.FoundryLocal;

internal sealed class FoundryServiceManager
{
    public static FoundryServiceManager? TryCreate()
    {
        return IsAvailable() ? new FoundryServiceManager() : null;
    }

    private static bool IsAvailable()
    {
        using var process = new Process();
        process.StartInfo.FileName = "where";
        process.StartInfo.Arguments = "foundry";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static string? GetUrl(string output)
    {
        var match = Regex.Match(output, @"https?:\/\/[^\/]+:\d+");
        return match.Success ? match.Value : null;
    }

    public async Task<string?> GetServiceUrl()
    {
        var status = await Utils.RunFoundryWithArguments("service status").ConfigureAwait(false);

        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return null;
        }

        return GetUrl(status.Output);
    }

    public async Task<bool> IsRunning()
    {
        var url = await GetServiceUrl().ConfigureAwait(false);
        return url is not null;
    }

    public async Task<bool> StartService()
    {
        if (await IsRunning().ConfigureAwait(false))
        {
            return true;
        }

        var status = await Utils.RunFoundryWithArguments("service start").ConfigureAwait(false);
        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return false;
        }

        return GetUrl(status.Output) is not null;
    }
}
