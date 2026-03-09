// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.RaycastStore;

internal static class NodeJsDetector
{
    private static bool? _isInstalled;

    private static string? _version;

    public static bool IsInstalled => _isInstalled == true;

    public static string? Version => _version;

    public static async Task<bool> DetectAsync()
    {
        if (_isInstalled.HasValue)
        {
            return _isInstalled.Value;
        }

        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                _version = output.Trim();
                _isInstalled = true;
                Logger.LogDebug("Node.js detected: " + _version);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Node.js not found: " + ex.Message);
        }

        _isInstalled = false;
        return false;
    }

    public static void Reset()
    {
        _isInstalled = null;
        _version = null;
    }
}
