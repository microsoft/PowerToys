// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Explorer;

[ComVisible(true)]
[Guid(ShellInterop.CommandClassId)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class ExplorerCommand(Action<string[]> launch, Action finished) : ShellInterop.IExecuteCommand, ShellInterop.IObjectWithSelection
{
    private ShellInterop.IShellItemArray? selection;
    private bool queued;

    public int SetSelection(ShellInterop.IShellItemArray? selection)
    {
        if (queued)
        {
            return unchecked((int)0x8000FFFF);
        }

        this.selection = selection;
        return 0;
    }

    public int GetSelection(ref Guid interfaceId, out IntPtr result)
    {
        result = IntPtr.Zero;
        return selection is null ? unchecked((int)0x80004005) : ShellInterop.QueryInterface(selection, ref interfaceId, out result);
    }

    public int Execute()
    {
        if (queued || selection is null)
        {
            return unchecked((int)0x80070057);
        }

        queued = true;

        // Return to Explorer before resolving names or starting the UI. This
        // callback runs in this local server, never in the Explorer process.
        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            try
            {
                launch(ReadSelection(selection));
            }
            catch (Exception exception)
            {
                MessageBox.Show("Could not open this selection in Try Run.\n\n" + exception.Message, "Try Run", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                selection = null;
                finished();
            }
        });
        return 0;
    }

    public static string[] ReadSelection(ShellInterop.IShellItemArray items)
    {
        ArgumentNullException.ThrowIfNull(items);
        items.GetCount(out var count);
        if (count is 0 or > WorkspacePath.MaximumEntries)
        {
            throw new ArgumentException("Select between 1 and 1,000 files and folders.");
        }

        var paths = new string[count];
        long characters = 0;
        for (uint index = 0; index < count; index++)
        {
            items.GetItemAt(index, out var item);
            item.GetDisplayName(0x80058000, out var pointer); // SIGDN_FILESYSPATH
            try
            {
                paths[index] = Marshal.PtrToStringUni(pointer) ?? throw new ArgumentException("Select local files or folders.");
                characters += paths[index].Length;
                if (characters > 32768)
                {
                    throw new ArgumentException("The selection is too large. Select a containing folder instead.");
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }

        return TaskBundle.ParseLaunchArguments(paths);
    }

    // These values are deliberately ignored. The shell cannot supply a command,
    // execution arguments, a working directory, or request silent execution.
    public int SetKeyState(uint keyState) => 0;

    public int SetParameters(string parameters) => 0;

    public int SetPosition(ShellInterop.CommandPosition position) => 0;

    public int SetShowWindow(int show) => 0;

    public int SetNoShowUI(bool noShow) => 0;

    public int SetDirectory(string directory) => 0;
}
