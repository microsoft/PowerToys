// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class InstallExtensionCommand : InvokableCommand
{
    private readonly RaycastExtensionInfo _extension;
    private readonly InstalledExtensionTracker _tracker;
    private readonly Action? _onInstallComplete;
    private readonly StatusMessage _statusMessage = new();

    private Task? _installTask;

    public InstallExtensionCommand(RaycastExtensionInfo extension, InstalledExtensionTracker tracker, Action? onInstallComplete = null)
    {
        _extension = extension;
        _tracker = tracker;
        _onInstallComplete = onInstallComplete;
        Name = Properties.Resources.extension_install;
    }

    public override ICommandResult Invoke()
    {
        if (_installTask != null)
        {
            return CommandResult.KeepOpen();
        }

        _statusMessage.State = MessageState.Info;
        _statusMessage.Message = "Installing " + _extension.Title + "...";
        _statusMessage.Progress = new ProgressState { IsIndeterminate = true };
        RaycastStoreExtensionHost.Instance.ShowStatus(_statusMessage, StatusContext.Extension);

        _installTask = Task.Run(() => RunInstallAsync());
        return CommandResult.KeepOpen();
    }

    private async Task RunInstallAsync()
    {
        try
        {
            PipelineResult result = await PipelineLauncher.InstallAsync(
                _extension.DirectoryName,
                (stage, message) => { _statusMessage.Message = "[" + stage + "] " + message; },
                CancellationToken.None);

            if (result.Success)
            {
                _statusMessage.Progress = null;
                _statusMessage.State = MessageState.Success;
                _statusMessage.Message = "✓ " + _extension.Title + " installed successfully!";
                _tracker.Refresh();
                _onInstallComplete?.Invoke();
            }
            else
            {
                _statusMessage.Progress = null;
                _statusMessage.State = MessageState.Error;
                _statusMessage.Message = FormatErrorMessage(result.Error);
            }
        }
        catch (Exception ex)
        {
            _statusMessage.Progress = null;
            _statusMessage.State = MessageState.Error;
            _statusMessage.Message = "Error installing " + _extension.Title + ": " + ex.Message;
        }
        finally
        {
            _installTask = null;
            await Task.Delay(5000);
            RaycastStoreExtensionHost.Instance.HideStatus(_statusMessage);
        }
    }

    private string FormatErrorMessage(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return "Failed to install " + _extension.Title + ".";
        }

        if (error.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))
                ? "GitHub API rate limit exceeded. Please wait a few minutes and try again."
                : "GitHub API rate limit exceeded. Set a GITHUB_TOKEN environment variable to increase the limit.";
        }

        if (error.Contains("404", StringComparison.Ordinal) || error.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Extension \"" + _extension.DirectoryName + "\" was not found on GitHub.";
        }

        var pipelineIdx = error.IndexOf("Pipeline failed with exit code", StringComparison.Ordinal);
        if (pipelineIdx >= 0)
        {
            var colonIdx = error.IndexOf(':', pipelineIdx + "Pipeline failed with exit code".Length);
            if (colonIdx >= 0 && colonIdx + 1 < error.Length)
            {
                error = error.Substring(colonIdx + 1).Trim();
            }
        }

        return "Failed to install " + _extension.Title + ": " + error;
    }
}
