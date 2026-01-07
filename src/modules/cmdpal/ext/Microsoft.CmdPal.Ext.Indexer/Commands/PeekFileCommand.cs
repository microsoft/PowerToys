// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

/// <summary>
/// Command to preview a file using PowerToys Peek.
/// </summary>
public sealed partial class PeekFileCommand : InvokableCommand
{
    private const string PeekExecutable = @"WinUI3Apps\PowerToys.Peek.UI.exe";

    private static readonly Lazy<string> _peekPath = new(GetPeekExecutablePath);

    private readonly string _fullPath;

    public PeekFileCommand(string fullPath)
    {
        _fullPath = fullPath;
        Name = Resources.Indexer_Command_Peek;
        Icon = IconHelpers.FromRelativePath("Assets\\Peek.png");
    }

    /// <summary>
    /// Gets a value indicating whether Peek is available on this system.
    /// </summary>
    public static bool IsPeekAvailable => !string.IsNullOrEmpty(_peekPath.Value);

    public override CommandResult Invoke()
    {
        var peekExe = _peekPath.Value;
        if (string.IsNullOrEmpty(peekExe))
        {
            return CommandResult.ShowToast(Resources.Indexer_Command_Peek_NotAvailable);
        }

        try
        {
            using var process = new Process();
            process.StartInfo.FileName = peekExe;
            process.StartInfo.Arguments = $"\"{_fullPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.Start();
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Unable to launch Peek for {_fullPath}\n{ex}");
            return CommandResult.ShowToast(Resources.Indexer_Command_Peek_Failed);
        }

        return CommandResult.Dismiss();
    }

    private static string GetPeekExecutablePath()
    {
        var installPath = PowerToysPathResolver.GetPowerToysInstallPath();
        if (string.IsNullOrEmpty(installPath))
        {
            return string.Empty;
        }

        var peekPath = Path.Combine(installPath, PeekExecutable);
        return File.Exists(peekPath) ? peekPath : string.Empty;
    }
}
