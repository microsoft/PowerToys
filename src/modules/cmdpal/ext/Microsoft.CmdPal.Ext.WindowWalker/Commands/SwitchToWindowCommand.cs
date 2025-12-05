// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class SwitchToWindowCommand : InvokableCommand
{
    private readonly Window? _window;

    public SwitchToWindowCommand(Window? window)
    {
        Icon = Icons.GenericAppIcon; // Fallback to default icon
        Name = Resources.switch_to_command_title;
        _window = window;
        if (_window is not null)
        {
            // Use window icon
            if (SettingsManager.Instance.UseWindowIcon)
            {
                if (_window.TryGetWindowIcon(out var icon) && icon is not null)
                {
                    try
                    {
                        using var bitmap = icon.ToBitmap();
                        using var memoryStream = new MemoryStream();
                        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                        var raStream = new InMemoryRandomAccessStream();
                        using var outputStream = raStream.GetOutputStreamAt(0);
                        using var dataWriter = new DataWriter(outputStream);
                        dataWriter.WriteBytes(memoryStream.ToArray());
                        dataWriter.StoreAsync().AsTask().Wait();
                        dataWriter.FlushAsync().AsTask().Wait();
                        Icon = IconInfo.FromStream(raStream);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        icon.Dispose();
                    }
                }
            }

            // Use process icon
            else
            {
                var p = Process.GetProcessById((int)_window.Process.ProcessID);
                if (p is not null)
                {
                    try
                    {
                        var processFileName = p.MainModule?.FileName;
                        Icon = new IconInfo(processFileName);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    public override ICommandResult Invoke()
    {
        if (_window is null)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = "Cannot switch to the window, because it doesn't exist." });
            return CommandResult.Dismiss();
        }

        _window.SwitchToWindow();

        return CommandResult.Dismiss();
    }
}
