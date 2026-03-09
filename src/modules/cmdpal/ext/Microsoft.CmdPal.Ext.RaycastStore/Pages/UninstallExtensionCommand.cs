// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CmdPal.Ext.RaycastStore.Properties;

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class UninstallExtensionCommand : InvokableCommand
{
    private readonly RaycastExtensionInfo _extension;
    private readonly InstalledExtensionTracker _tracker;
    private readonly Action? _onUninstallComplete;
    private readonly StatusMessage _statusMessage = new();

    private Task? _uninstallTask;

    public UninstallExtensionCommand(RaycastExtensionInfo extension, InstalledExtensionTracker tracker, Action? onUninstallComplete = null)
    {
        _extension = extension;
        _tracker = tracker;
        _onUninstallComplete = onUninstallComplete;
        Name = Resources.extension_uninstall;
    }

    public override ICommandResult Invoke()
    {
        if (_uninstallTask != null)
        {
            return CommandResult.KeepOpen();
        }

        _statusMessage.State = MessageState.Info;
        _statusMessage.Message = $"{Resources.extension_uninstalling} {_extension.Title}...";
        _statusMessage.Progress = new ProgressState { IsIndeterminate = true };
        RaycastStoreExtensionHost.Instance.ShowStatus(_statusMessage, StatusContext.Extension);

        _uninstallTask = Task.Run(() => RunUninstallAsync());
        return CommandResult.KeepOpen();
    }

    private async Task RunUninstallAsync()
    {
        try
        {
            PipelineResult result = await PipelineLauncher.UninstallAsync(
                _extension.DirectoryName,
                (stage, message) => { _statusMessage.Message = message; },
                CancellationToken.None);

            if (result.Success)
            {
                _statusMessage.Progress = null;
                _statusMessage.State = MessageState.Success;
                _statusMessage.Message = $"{_extension.Title} {Resources.extension_uninstalled}.";
                _tracker.Refresh();
                _onUninstallComplete?.Invoke();
            }
            else
            {
                _statusMessage.Progress = null;
                _statusMessage.State = MessageState.Error;
                _statusMessage.Message = $"{Resources.extension_failed_to_uninstall} {_extension.Title}: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            _statusMessage.Progress = null;
            _statusMessage.State = MessageState.Error;
            _statusMessage.Message = $"{Resources.extension_error_uninstalling} {_extension.Title}: {ex.Message}";
        }
        finally
        {
            _uninstallTask = null;
            await Task.Delay(5000);
            RaycastStoreExtensionHost.Instance.HideStatus(_statusMessage);
        }
    }
}
