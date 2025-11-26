// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class StartAwakeCommand : InvokableCommand
{
    private readonly Func<string> _argumentsProvider;
    private readonly string? _successToast;

    internal StartAwakeCommand(string title, Func<string> argumentsProvider, string? successToast = null)
    {
        ArgumentNullException.ThrowIfNull(argumentsProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        _argumentsProvider = argumentsProvider;
        _successToast = successToast;
        Name = title;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var executablePath = PowerToysPathResolver.TryResolveExecutable("PowerToys.Awake.exe");
            if (string.IsNullOrEmpty(executablePath))
            {
                return CommandResult.ShowToast("Unable to locate PowerToys.Awake.exe.");
            }

            var arguments = _argumentsProvider()?.Trim() ?? string.Empty;

            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = string.IsNullOrWhiteSpace(arguments),
                CreateNoWindow = true,
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
            }

            Process.Start(startInfo);
            return string.IsNullOrWhiteSpace(_successToast) ? CommandResult.Hide() : CommandResult.ShowToast(_successToast);
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Launching Awake failed: {ex.Message}");
        }
    }
}
