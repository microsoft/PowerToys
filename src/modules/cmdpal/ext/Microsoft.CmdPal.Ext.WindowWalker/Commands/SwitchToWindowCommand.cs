// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CommandPalette.Extensions;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class SwitchToWindowCommand : InvokableCommand
{
    private readonly Lock _iconLock = new();
    private Window? _window;
    private bool _iconLoaded;
    private bool _iconLoading;
    private bool _useWindowIcon;

    internal bool NeedsIconLoad
    {
        get
        {
            if (Volatile.Read(ref _window) is null)
            {
                return false;
            }

            var useWindowIcon = SettingsManager.Instance.UseWindowIcon;
            lock (_iconLock)
            {
                return !_iconLoading && (!_iconLoaded || _useWindowIcon != useWindowIcon);
            }
        }
    }

    public SwitchToWindowCommand(Window? window)
    {
        Icon = Icons.GenericAppIcon; // Fallback to default icon
        Name = Resources.switch_to_command_title;
        _window = window;
    }

    internal void UpdateWindow(Window window)
    {
        Volatile.Write(ref _window, window);
    }

    internal void LoadIcon()
    {
        var window = Volatile.Read(ref _window);
        if (window is null)
        {
            return;
        }

        var useWindowIcon = SettingsManager.Instance.UseWindowIcon;
        lock (_iconLock)
        {
            if (_iconLoading || (_iconLoaded && _useWindowIcon == useWindowIcon))
            {
                return;
            }

            _iconLoading = true;
        }

        IconInfo? icon = null;
        try
        {
            icon = useWindowIcon ? GetWindowIcon(window) : GetProcessIcon(window);
        }
        catch
        {
        }

        lock (_iconLock)
        {
            _useWindowIcon = useWindowIcon;
            _iconLoaded = true;
            _iconLoading = false;
        }

        Icon = icon ?? Icons.GenericAppIcon;
    }

    private static IconInfo? GetWindowIcon(Window window)
    {
        if (!window.TryGetWindowIcon(out var icon) || icon is null)
        {
            return null;
        }

        using (icon)
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
                dataWriter.StoreAsync().AsTask().GetAwaiter().GetResult();
                dataWriter.FlushAsync().AsTask().GetAwaiter().GetResult();
                return IconInfo.FromStream(raStream);
            }
            catch
            {
                return null;
            }
        }
    }

    private static IconInfo? GetProcessIcon(Window window)
    {
        try
        {
            using var process = Process.GetProcessById((int)window.Process.ProcessID);
            var processFileName = process.MainModule?.FileName;
            return string.IsNullOrEmpty(processFileName) ? null : new IconInfo(processFileName);
        }
        catch
        {
            return null;
        }
    }

    public override ICommandResult Invoke()
    {
        var window = Volatile.Read(ref _window);
        if (window is null)
        {
            ExtensionHost.LogMessage(new LogMessage { Message = "Cannot switch to the window, because it doesn't exist." });
            return CommandResult.Dismiss();
        }

        window.SwitchToWindow();

        return CommandResult.Dismiss();
    }
}
