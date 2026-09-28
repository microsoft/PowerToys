// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    // The page's context menu: the real menu Windows builds for each kind of right-click, captured by
    // the MenuCapture helper process, with every item linked back to the entry that adds it so it
    // can be turned off from the menu itself. Captures are cached per target until the next refresh.
    public partial class ContextMenuManagerViewModel
    {
        // Explorer's folder view adds these itself at display time, so the captured menu lacks them.
        // By resw key suffix; a trailing '>' marks a submenu, "-" a separator.
        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> ViewItems = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "View>", "SortBy>", "Refresh", "-", "Paste", "PasteShortcut", "Undo" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "View>", "SortBy>", "GroupBy>", "Refresh", "-", "Paste", "PasteShortcut", "Undo" },
        };

        // Registry roots Explorer merges for each target. The sample file is a .txt, matching the capture helper.
        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> TargetRoots = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "DesktopBackground", "Directory\\Background" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "Directory\\Background" },
            [ContextMenuPreviewTarget.Folder] = new[] { "Directory", "AllFilesystemObjects" },
            [ContextMenuPreviewTarget.File] = new[] { "*", "AllFilesystemObjects", ".txt" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Drive", "AllFilesystemObjects" },
        };

        // Present when the user restored the Windows 10 menu as the default right-click menu.
        private const string ClassicMenuRestoreKey = "Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32";

        private const string CaptureHelperExe = "PowerToys.ContextMenuManager.MenuCapture.exe";

        private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

        // Modern is Windows 11's first menu; null when the classic menu is the default.
        private sealed record CapturedMenu(List<ContextMenuPreviewItem> Modern, List<ContextMenuPreviewItem> Classic);

        // Tasks rather than results, so flipping back to a target that is still capturing reuses it.
        private readonly Dictionary<ContextMenuPreviewTarget, Task<CapturedMenu>> _captures = new();

        private ContextMenuPreviewTarget _previewTarget = ContextMenuPreviewTarget.Desktop;
        private bool _isClassicMenuDefault;
        private bool _showClassicLayer;
        private bool _isCapturing;
        private string _captureError;
        private ContextMenuPreviewItem _selectedPreviewItem;

        public ObservableCollection<ContextMenuPreviewItem> PreviewItems { get; } = new ObservableCollection<ContextMenuPreviewItem>();

        public int PreviewTargetIndex
        {
            get => (int)_previewTarget;
            set
            {
                if (value >= 0 && value != (int)_previewTarget)
                {
                    _previewTarget = (ContextMenuPreviewTarget)value;
                    OnPropertyChanged(nameof(PreviewTargetIndex));
                    SelectedPreviewItem = null;
                    _ = LoadPreviewAsync();
                }
            }
        }

        public bool IsCapturing
        {
            get => _isCapturing;
            private set
            {
                if (value != _isCapturing)
                {
                    _isCapturing = value;
                    OnPropertyChanged(nameof(IsCapturing));
                }
            }
        }

        public string CaptureError
        {
            get => _captureError;
            private set
            {
                if (value != _captureError)
                {
                    _captureError = value;
                    OnPropertyChanged(nameof(CaptureError));
                    OnPropertyChanged(nameof(HasCaptureError));
                }
            }
        }

        public bool HasCaptureError => !string.IsNullOrEmpty(CaptureError);

        public bool CanGoBackToModernMenu => _showClassicLayer && !_isClassicMenuDefault;

        public ButtonClickCommand BackToModernMenuCommand => new ButtonClickCommand(() =>
        {
            _showClassicLayer = false;
            RenderPreview();
        });

        public ContextMenuPreviewItem SelectedPreviewItem
        {
            get => _selectedPreviewItem;
            set
            {
                if (value != _selectedPreviewItem)
                {
                    _selectedPreviewItem = value;
                    OnPropertyChanged(nameof(SelectedPreviewItem));
                    OnPropertyChanged(nameof(SelectedEntry));
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(HasNoSelection));
                    OnPropertyChanged(nameof(HasSelectedEntry));
                    OnPropertyChanged(nameof(SelectedDescription));
                    OnPropertyChanged(nameof(SelectedLocation));
                    OnPropertyChanged(nameof(IsSelectedExtension));
                }
            }
        }

        public ContextMenuEntry SelectedEntry => _selectedPreviewItem?.Entry;

        public bool HasSelection => _selectedPreviewItem != null;

        public bool HasNoSelection => !HasSelection;

        public bool HasSelectedEntry => SelectedEntry != null;

        public string SelectedDescription => SelectedEntry is { } entry
            ? $"{entry.DisplayName} · {entry.ScopeAndSourceDisplayName}"
            : ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_DetailsUnmanaged");

        public string SelectedLocation
        {
            get
            {
                if (SelectedEntry is not { } entry)
                {
                    return null;
                }

                if (entry.Kind == ContextMenuEntryKind.BlockedClsid)
                {
                    return string.Join(Environment.NewLine, entry.Clsids.Prepend(entry.HandlerKeyName).Distinct(StringComparer.OrdinalIgnoreCase));
                }

                string hive = entry.Scope == ContextMenuEntryScope.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                return string.Join(Environment.NewLine, entry.KeyPaths.Select(p => $"{hive}\\Software\\Classes\\{p}"));
            }
        }

        // A shell extension can add several items; turning it off removes all of them.
        public bool IsSelectedExtension => SelectedEntry is { Source: ContextMenuEntrySource.Classic, Kind: not ContextMenuEntryKind.Verb };

        // Windows 11's "Show more options": swaps the preview to the classic menu, like Explorer does.
        public void ShowClassicLayer()
        {
            _showClassicLayer = true;
            RenderPreview();
        }

        // Entries were re-enumerated, so every cached item points at an old entry object.
        private void ResetPreview()
        {
            _captures.Clear();
            _isClassicMenuDefault = IsClassicMenuDefault();
            SelectedPreviewItem = null;
            _ = LoadPreviewAsync();
        }

        // Re-adds the cached rows so their on/off look and late-loaded icons are read again.
        private void RenderPreview()
        {
            PreviewItems.Clear();
            if (_captures.TryGetValue(_previewTarget, out var task) && task.IsCompletedSuccessfully)
            {
                foreach (var item in task.Result.Modern == null || _showClassicLayer ? task.Result.Classic : task.Result.Modern)
                {
                    PreviewItems.Add(item);
                }
            }

            OnPropertyChanged(nameof(CanGoBackToModernMenu));
        }

        // Never throws: the page discards the task.
        private async Task LoadPreviewAsync()
        {
            var target = _previewTarget;
            CaptureError = null;
            if (!_captures.TryGetValue(target, out var task))
            {
                task = CaptureAsync(target);
                _captures[target] = task;
            }

            RenderPreview();
            IsCapturing = !task.IsCompleted;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ContextMenuManager: menu capture failed: {ex}");
                if (_captures.TryGetValue(target, out var current) && current == task)
                {
                    // Not cached, so picking the target again retries.
                    _captures.Remove(target);
                    if (target == _previewTarget)
                    {
                        CaptureError = ex is TimeoutException
                            ? ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_CaptureTimeout")
                            : ex.Message;
                    }
                }
            }

            RenderPreview();
            IsCapturing = _captures.TryGetValue(_previewTarget, out var shown) && !shown.IsCompleted;
        }

        // The capture misses what Explorer adds itself at display time - the view's own View/Sort
        // by/Refresh block on backgrounds, and Windows 11 packaged verbs - so those are added here.
        private async Task<CapturedMenu> CaptureAsync(ContextMenuPreviewTarget target)
        {
            var targetEntries = Entries.Where(e => IsForTarget(e, target)).ToList();

            // Enabled shell extensions, probed one by one so their items can be traced back to them.
            var extensions = new Dictionary<string, ContextMenuEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in targetEntries.Where(e => e.IsEnabled && e.Parent == null && e.Source == ContextMenuEntrySource.Classic && e.Kind != ContextMenuEntryKind.Verb))
            {
                string clsid = entry.Kind == ContextMenuEntryKind.HandlerValue ? entry.OriginalClsidValue : entry.Clsids.FirstOrDefault();
                if (Guid.TryParse(clsid, out Guid guid))
                {
                    extensions.TryAdd(guid.ToString("B"), entry);
                }
            }

            string json = await RunCaptureHelperAsync(target, extensions.Keys);
            using var document = JsonDocument.Parse(json);
            var classic = ParseCapturedItems(document.RootElement.GetProperty("items"));

            var extensionByText = new Dictionary<string, ContextMenuEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var handler in document.RootElement.GetProperty("handlers").EnumerateObject())
            {
                foreach (var text in handler.Value.EnumerateArray())
                {
                    extensionByText.TryAdd(text.GetString() ?? string.Empty, extensions[handler.Name]);
                }
            }

            var verbs = targetEntries.Where(e => e.Kind == ContextMenuEntryKind.Verb).ToList();
            foreach (var item in classic.Where(i => i.IsItem))
            {
                item.Entry = MatchEntry(item, verbs, extensionByText);
                LinkChildren(item, verbs);
            }

            // Turned-off entries aren't in the real menu at all; list them after it so they can come back.
            var shown = Flatten(classic).Select(i => i.Entry).Where(e => e != null).ToHashSet();
            var off = targetEntries
                .Where(e => !e.IsEnabled && e.Source == ContextMenuEntrySource.Classic && !shown.Contains(e))
                .OrderBy(e => e.SortKey ?? e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(e => new ContextMenuPreviewItem { Text = e.DisplayName, Entry = e })
                .ToList();
            if (off.Count > 0)
            {
                classic.Add(ContextMenuPreviewItem.Separator());
                classic.Add(new ContextMenuPreviewItem { IsHeader = true, Text = ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_PreviewTurnedOffHeader") });
                classic.AddRange(off);
            }

            if (ViewItems.TryGetValue(target, out var viewKeys))
            {
                var viewItems = viewKeys.Select(BuiltIn).ToList();
                var viewTexts = viewItems.Where(i => !i.IsSeparator).Select(i => i.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
                classic.RemoveAll(i => i.IsItem && i.Entry == null && viewTexts.Contains(i.Text));
                classic.InsertRange(0, viewItems.Append(ContextMenuPreviewItem.Separator()));
            }

            var modern = targetEntries
                .Where(e => e.Source == ContextMenuEntrySource.Modern)
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(e => new ContextMenuPreviewItem { Text = e.DisplayName, Entry = e })
                .ToList();

            if (_isClassicMenuDefault)
            {
                int firstSeparator = classic.FindIndex(i => i.IsSeparator);
                classic.InsertRange(firstSeparator < 0 ? classic.Count : firstSeparator, modern);
                return new CapturedMenu(null, TidySeparators(classic));
            }

            modern.Add(ContextMenuPreviewItem.Separator());
            modern.Add(new ContextMenuPreviewItem { Text = ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_BuiltIn_ShowMoreOptions"), IsShowMoreOptions = true });
            return new CapturedMenu(TidySeparators(modern), TidySeparators(classic));
        }

        // Static verbs report their key name as the verb; extensions are known from the probe; a
        // cascading verb has no verb string, so its label is the last resort.
        private static ContextMenuEntry MatchEntry(ContextMenuPreviewItem item, List<ContextMenuEntry> verbs, Dictionary<string, ContextMenuEntry> extensionByText)
        {
            var topVerbs = verbs.Where(e => e.Parent == null);
            return (item.Verb != null ? topVerbs.FirstOrDefault(e => string.Equals(e.HandlerKeyName, item.Verb, StringComparison.OrdinalIgnoreCase)) : null)
                ?? extensionByText.GetValueOrDefault(item.Text)
                ?? topVerbs.FirstOrDefault(e => string.Equals(e.DisplayName, item.Text, StringComparison.OrdinalIgnoreCase));
        }

        // A cascading verb's items are entries of their own; an extension's submenu belongs to the extension.
        private static void LinkChildren(ContextMenuPreviewItem parent, List<ContextMenuEntry> verbs)
        {
            foreach (var child in parent.Children.Where(c => c.IsItem))
            {
                child.Entry = parent.Entry is { Kind: ContextMenuEntryKind.Verb } verb
                    ? verbs.FirstOrDefault(e => e.Parent == verb && string.Equals(e.DisplayName, $"{verb.DisplayName} > {child.Text}", StringComparison.OrdinalIgnoreCase))
                    : parent.Entry;
                LinkChildren(child, verbs);
            }
        }

        private static IEnumerable<ContextMenuPreviewItem> Flatten(IEnumerable<ContextMenuPreviewItem> items) =>
            items.SelectMany(i => Flatten(i.Children).Prepend(i));

        // Registered for the target; submenu items go by their top-level parent.
        private static bool IsForTarget(ContextMenuEntry entry, ContextMenuPreviewTarget target)
        {
            while (entry.Parent != null)
            {
                entry = entry.Parent;
            }

            var roots = TargetRoots[target];
            return entry.Roots.Any(r => roots.Contains(r, StringComparer.OrdinalIgnoreCase))
                || entry.ItemTypes.Any(t => roots.Contains(t, StringComparer.OrdinalIgnoreCase));
        }

        private static async Task<string> RunCaptureHelperAsync(ContextMenuPreviewTarget target, IEnumerable<string> probeClsids)
        {
            string argument = target switch
            {
                ContextMenuPreviewTarget.Desktop => "desktop",
                ContextMenuPreviewTarget.FolderBackground => "background",
                ContextMenuPreviewTarget.Folder => "folder",
                ContextMenuPreviewTarget.File => "file",
                _ => "drive",
            };

            string probe = string.Join(",", probeClsids);
            var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, CaptureHelperExe), $"--target {argument}" + (probe.Length > 0 ? $" --probe {probe}" : string.Empty))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {CaptureHelperExe}.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();

            // A shell extension can hang (network drives, licensing prompts); never wait on it forever.
            using var timeout = new CancellationTokenSource(CaptureTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException();
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException((await error).Trim());
            }

            return await output;
        }

        private static List<ContextMenuPreviewItem> ParseCapturedItems(JsonElement array)
        {
            var items = new List<ContextMenuPreviewItem>();
            foreach (var element in array.EnumerateArray())
            {
                if (element.TryGetProperty("separator", out _))
                {
                    items.Add(ContextMenuPreviewItem.Separator());
                    continue;
                }

                var item = new ContextMenuPreviewItem
                {
                    Text = element.TryGetProperty("text", out var text) ? text.GetString() : string.Empty,
                    Shortcut = element.TryGetProperty("shortcut", out var shortcut) ? shortcut.GetString() : null,
                    Verb = element.TryGetProperty("verb", out var verb) ? verb.GetString() : null,
                    IsDisabled = element.TryGetProperty("disabled", out _),
                };

                if (element.TryGetProperty("icon", out var icon))
                {
                    item.Icon = CreateBgraImage(icon.GetProperty("w").GetInt32(), icon.GetProperty("h").GetInt32(), icon.GetProperty("bgra").GetBytesFromBase64());
                }

                if (element.TryGetProperty("children", out var children))
                {
                    item.Children.AddRange(ParseCapturedItems(children));
                }

                items.Add(item);
            }

            return items;
        }

        // The helper sends premultiplied 32bpp top-down pixels, which is WriteableBitmap's own layout.
        private static WriteableBitmap CreateBgraImage(int width, int height, byte[] pixels)
        {
            if (width <= 0 || height <= 0 || pixels.Length != width * height * 4)
            {
                return null;
            }

            var bitmap = new WriteableBitmap(width, height);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                stream.Write(pixels, 0, pixels.Length);
            }

            bitmap.Invalidate();
            return bitmap;
        }

        private static ContextMenuPreviewItem BuiltIn(string key)
        {
            if (key == "-")
            {
                return ContextMenuPreviewItem.Separator();
            }

            var item = new ContextMenuPreviewItem { Text = ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_BuiltIn_" + key.TrimEnd('>')) };
            if (key.EndsWith('>'))
            {
                // Placeholder so the row shows a chevron; Explorer fills these at runtime.
                item.Children.Add(new ContextMenuPreviewItem { Text = "…", IsDisabled = true });
            }

            return item;
        }

        // Drops leading, trailing and doubled separators left behind by empty sections.
        private static List<ContextMenuPreviewItem> TidySeparators(IEnumerable<ContextMenuPreviewItem> items)
        {
            var result = new List<ContextMenuPreviewItem>();
            foreach (var item in items)
            {
                if (item.IsSeparator && (result.Count == 0 || result[^1].IsSeparator))
                {
                    continue;
                }

                result.Add(item);
            }

            if (result.Count > 0 && result[^1].IsSeparator)
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private static bool IsClassicMenuDefault()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ClassicMenuRestoreKey, writable: false);
                return key != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
