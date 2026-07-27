// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using SHDocVw;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>File Explorer selection helpers backed by the Shell COM view used by Explorer itself.</summary>
public static class ExplorerShell
{
    private static readonly Guid ShellApplicationClassId = new("13709620-C279-11CE-A49E-444553540000");
    private const int ShellViewSelect = 0x1;
    private const int ShellViewDeselectOthers = 0x4;
    private const int ShellViewEnsureVisible = 0x8;
    private const int ShellViewFocused = 0x10;

    public sealed record SelectionSnapshot(IReadOnlySet<string> SelectedPaths, string? FocusedPath);

    private sealed record ReadinessSnapshot(bool IsForeground, SelectionSnapshot? Selection);

    /// <summary>
    /// Set the exact selected path set and focused item, then require both selection and foreground
    /// ownership to remain stable across consecutive Shell snapshots.
    /// </summary>
    public static WaitHelper.StableWaitResult<SelectionSnapshot> SetSelectionAndWaitForStable(
        IntPtr explorerWindow,
        IReadOnlyCollection<string> selectedPaths,
        string focusedPath,
        int timeoutMS = 30_000,
        int requiredConsecutiveMatches = 4,
        int pollIntervalMS = 250)
    {
        ArgumentNullException.ThrowIfNull(selectedPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(focusedPath);
        if (explorerWindow == IntPtr.Zero)
        {
            throw new ArgumentException("Explorer HWND must not be zero.", nameof(explorerWindow));
        }

        var normalizedPaths = selectedPaths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedFocusedPath = NormalizePath(focusedPath);
        if (normalizedPaths.Count == 0 || !normalizedPaths.Contains(normalizedFocusedPath))
        {
            throw new ArgumentException("The selected paths must contain the focused path.", nameof(selectedPaths));
        }

        var result = WaitHelper.WaitForStable(
            observe: () => new ReadinessSnapshot(
                WindowControl.GetForegroundWindowHandle() == explorerWindow,
                TryGetSelection(explorerWindow)),
            isMatch: snapshot => snapshot is not null &&
                                 snapshot.IsForeground &&
                                 snapshot.Selection is not null &&
                                 snapshot.Selection.SelectedPaths.SetEquals(normalizedPaths) &&
                                 string.Equals(snapshot.Selection.FocusedPath, normalizedFocusedPath, StringComparison.OrdinalIgnoreCase),
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: requiredConsecutiveMatches,
            pollIntervalMS: pollIntervalMS,
            recover: snapshot =>
            {
                if (snapshot?.IsForeground != true)
                {
                    WindowControl.TryBringToForeground(explorerWindow);
                }
                else
                {
                    TrySetSelection(explorerWindow, normalizedPaths, normalizedFocusedPath);
                }
            });

        return new WaitHelper.StableWaitResult<SelectionSnapshot>(
            result.Succeeded,
            result.LastObservation?.Selection,
            result.ConsecutiveMatches,
            result.LastException);
    }

    /// <summary>Read the current selected path set and focused path from an Explorer window.</summary>
    public static SelectionSnapshot? TryGetSelection(IntPtr explorerWindow)
    {
        object? shellObject = null;
        ShellWindows? shellWindows = null;

        try
        {
            var shellType = Type.GetTypeFromCLSID(ShellApplicationClassId, throwOnError: true)!;
            shellObject = Activator.CreateInstance(shellType);
            var shell = (Shell32.IShellDispatch2)shellObject!;
            shellWindows = shell.Windows();
            foreach (IWebBrowserApp browser in shellWindows)
            {
                try
                {
                    if (browser.HWND != explorerWindow.ToInt64() || browser.Document is not Shell32.IShellFolderViewDual2 folderView)
                    {
                        continue;
                    }

                    var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var selectedItems = folderView.SelectedItems();
                    try
                    {
                        for (var index = 0; index < selectedItems.Count; index++)
                        {
                            var item = selectedItems.Item(index);
                            try
                            {
                                selectedPaths.Add(NormalizePath(item.Path));
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(item);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(selectedItems);
                    }

                    var focusedItem = folderView.FocusedItem;
                    if (focusedItem is null)
                    {
                        return new SelectionSnapshot(selectedPaths, null);
                    }

                    try
                    {
                        return new SelectionSnapshot(selectedPaths, NormalizePath(focusedItem.Path));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(focusedItem);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(browser);
                }
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            ReleaseComObject(shellWindows);
            ReleaseComObject(shellObject);
        }

        return null;
    }

    private static bool TrySetSelection(IntPtr explorerWindow, IReadOnlySet<string> selectedPaths, string focusedPath)
    {
        object? shellObject = null;
        ShellWindows? shellWindows = null;

        try
        {
            var shellType = Type.GetTypeFromCLSID(ShellApplicationClassId, throwOnError: true)!;
            shellObject = Activator.CreateInstance(shellType);
            var shell = (Shell32.IShellDispatch2)shellObject!;
            shellWindows = shell.Windows();
            foreach (IWebBrowserApp browser in shellWindows)
            {
                try
                {
                    if (browser.HWND != explorerWindow.ToInt64() || browser.Document is not Shell32.IShellFolderViewDual2 folderView)
                    {
                        continue;
                    }

                    var folder = folderView.Folder;
                    var folderItems = folder.Items();
                    var retainedItems = new List<Shell32.FolderItem>();
                    try
                    {
                        var itemsByPath = new Dictionary<string, Shell32.FolderItem>(StringComparer.OrdinalIgnoreCase);
                        for (var index = 0; index < folderItems.Count; index++)
                        {
                            var item = folderItems.Item(index);
                            var normalizedPath = NormalizePath(item.Path);
                            if (selectedPaths.Contains(normalizedPath))
                            {
                                itemsByPath[normalizedPath] = item;
                                retainedItems.Add(item);
                            }
                            else
                            {
                                Marshal.ReleaseComObject(item);
                            }
                        }

                        if (itemsByPath.Count != selectedPaths.Count)
                        {
                            return false;
                        }

                        var orderedPaths = selectedPaths
                            .Where(path => !string.Equals(path, focusedPath, StringComparison.OrdinalIgnoreCase))
                            .Append(focusedPath)
                            .ToList();

                        for (var index = 0; index < orderedPaths.Count; index++)
                        {
                            var path = orderedPaths[index];
                            var flags = ShellViewSelect | ShellViewEnsureVisible;
                            if (index == 0)
                            {
                                flags |= ShellViewDeselectOthers;
                            }

                            if (string.Equals(path, focusedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                flags |= ShellViewFocused;
                            }

                            folderView.SelectItem(itemsByPath[path], flags);
                        }

                        return true;
                    }
                    finally
                    {
                        foreach (var item in retainedItems)
                        {
                            Marshal.ReleaseComObject(item);
                        }

                        Marshal.ReleaseComObject(folderItems);
                        Marshal.ReleaseComObject(folder);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(browser);
                }
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            ReleaseComObject(shellWindows);
            ReleaseComObject(shellObject);
        }

        return false;
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }
}
