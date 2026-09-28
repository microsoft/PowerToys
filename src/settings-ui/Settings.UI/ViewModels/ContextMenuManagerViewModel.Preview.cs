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
    // Emulated context menu: a best-effort rebuild of what Explorer shows for each kind of
    // right-click, from the same entries the list toggles. Explorer's own items come from a fixed
    // table (they aren't registry entries this tool edits), and a classic handler shows as one row
    // since the items it adds only exist at runtime - "Capture real menu" shows those.
    public partial class ContextMenuManagerViewModel
    {
        // Explorer's built-in items, by resw key suffix. A trailing '>' marks a submenu, "-" a separator.
        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> BuiltInTop = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "View>", "SortBy>", "Refresh", "-", "Paste", "PasteShortcut", "Undo" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "View>", "SortBy>", "GroupBy>", "Refresh", "-", "Paste", "PasteShortcut", "Undo" },
            [ContextMenuPreviewTarget.Folder] = new[] { "Open", "OpenInNewWindow", "PinToQuickAccess" },
            [ContextMenuPreviewTarget.File] = new[] { "Open", "OpenWith>" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Open", "OpenInNewWindow", "PinToQuickAccess" },
        };

        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> BuiltInMiddle = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "New>" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "New>" },
            [ContextMenuPreviewTarget.Folder] = new[] { "SendTo>", "-", "Cut", "Copy", "-", "CreateShortcut", "Delete", "Rename" },
            [ContextMenuPreviewTarget.File] = new[] { "SendTo>", "-", "Cut", "Copy", "-", "CreateShortcut", "Delete", "Rename" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Format", "-", "Copy", "-", "CreateShortcut", "Rename" },
        };

        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> BuiltInEnd = new()
        {
            [ContextMenuPreviewTarget.Desktop] = Array.Empty<string>(),
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "Properties" },
            [ContextMenuPreviewTarget.Folder] = new[] { "Properties" },
            [ContextMenuPreviewTarget.File] = new[] { "Properties" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Properties" },
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

        private ContextMenuPreviewTarget _previewTarget = ContextMenuPreviewTarget.Desktop;
        private bool _isPreviewCaptured;
        private bool _isCapturing;
        private string _captureError;

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
                    BuildSyntheticPreview();
                }
            }
        }

        public bool IsPreviewCaptured
        {
            get => _isPreviewCaptured;
            private set
            {
                if (value != _isPreviewCaptured)
                {
                    _isPreviewCaptured = value;
                    OnPropertyChanged(nameof(IsPreviewCaptured));
                    OnPropertyChanged(nameof(PreviewSourceText));
                }
            }
        }

        public string PreviewSourceText => ResourceLoaderInstance.ResourceLoader.GetString(
            IsPreviewCaptured ? "ContextMenuManager_PreviewSourceCaptured" : "ContextMenuManager_PreviewSourceSynthetic");

        public bool IsCapturing
        {
            get => _isCapturing;
            private set
            {
                if (value != _isCapturing)
                {
                    _isCapturing = value;
                    OnPropertyChanged(nameof(IsCapturing));
                    OnPropertyChanged(nameof(IsNotCapturing));
                }
            }
        }

        public bool IsNotCapturing => !IsCapturing;

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

        // CaptureRealMenuAsync handles every failure itself, so the discarded task can't fault unobserved.
        public ButtonClickCommand CaptureRealMenuCommand => new ButtonClickCommand(() => _ = CaptureRealMenuAsync());

        public void BuildSyntheticPreview()
        {
            ShowPreview(WrapForMenuStyle(BuildClassicItems(_previewTarget)), captured: false);
        }

        // Windows 11 menu: packaged items on top, everything classic behind "Show more options".
        private List<ContextMenuPreviewItem> WrapForMenuStyle(List<ContextMenuPreviewItem> classic)
        {
            if (IsClassicMenuDefault())
            {
                return classic;
            }

            var items = EnabledEntries(_previewTarget)
                .Where(e => e.Source == ContextMenuEntrySource.Modern)
                .Select(ToPreviewItem)
                .ToList();
            items.Add(ContextMenuPreviewItem.Separator());
            var more = BuiltIn("ShowMoreOptions");
            more.Children.AddRange(TidySeparators(classic));
            items.Add(more);
            return items;
        }

        // Asks Windows for the real menu through the MenuCapture helper process. The capture misses
        // what Explorer adds itself at display time - the view's own View/Sort by/Refresh block on
        // backgrounds, and Windows 11 packaged verbs - so those are merged in from the entry list.
        private async Task CaptureRealMenuAsync()
        {
            var target = _previewTarget;
            IsCapturing = true;
            CaptureError = null;
            try
            {
                string json = await RunCaptureHelperAsync(target);
                if (target != _previewTarget)
                {
                    return;
                }

                using var document = JsonDocument.Parse(json);
                var captured = ParseCapturedItems(document.RootElement);

                var modern = EnabledEntries(target).Where(e => e.Source == ContextMenuEntrySource.Modern).Select(ToPreviewItem).ToList();
                int firstSeparator = captured.FindIndex(i => i.IsSeparator);
                captured.InsertRange(firstSeparator < 0 ? captured.Count : firstSeparator, modern);

                if (target is ContextMenuPreviewTarget.Desktop or ContextMenuPreviewTarget.FolderBackground)
                {
                    var viewItems = BuiltInItems(BuiltInTop[target]).ToList();
                    var viewTexts = viewItems.Where(i => !i.IsSeparator).Select(i => i.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    captured.RemoveAll(i => !i.IsSeparator && viewTexts.Contains(i.Text));
                    captured.InsertRange(0, viewItems.Append(ContextMenuPreviewItem.Separator()));
                }

                ShowPreview(WrapForMenuStyle(captured), captured: true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ContextMenuManager: menu capture failed: {ex}");
                CaptureError = ex is TimeoutException
                    ? ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_CaptureTimeout")
                    : ex.Message;
            }
            finally
            {
                IsCapturing = false;
            }
        }

        private static async Task<string> RunCaptureHelperAsync(ContextMenuPreviewTarget target)
        {
            string argument = target switch
            {
                ContextMenuPreviewTarget.Desktop => "desktop",
                ContextMenuPreviewTarget.FolderBackground => "background",
                ContextMenuPreviewTarget.Folder => "folder",
                ContextMenuPreviewTarget.File => "file",
                _ => "drive",
            };

            var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, CaptureHelperExe), $"--target {argument}")
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

        private void ShowPreview(IEnumerable<ContextMenuPreviewItem> items, bool captured)
        {
            PreviewItems.Clear();
            foreach (var item in TidySeparators(items))
            {
                PreviewItems.Add(item);
            }

            IsPreviewCaptured = captured;
        }

        private List<ContextMenuPreviewItem> BuildClassicItems(ContextMenuPreviewTarget target)
        {
            var entries = EnabledEntries(target).ToList();

            bool IsPosition(ContextMenuEntry e, string position) => string.Equals(e.Position, position, StringComparison.OrdinalIgnoreCase);

            var items = new List<ContextMenuPreviewItem>();
            items.AddRange(entries.Where(e => e.Kind == ContextMenuEntryKind.Verb && IsPosition(e, "Top")).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.AddRange(BuiltInItems(BuiltInTop[target]));
            items.Add(ContextMenuPreviewItem.Separator());

            // Explorer adds static verbs first, then packaged verbs, then shellex handlers, each in key-name order.
            items.AddRange(entries.Where(e => e.Kind == ContextMenuEntryKind.Verb && !IsPosition(e, "Top") && !IsPosition(e, "Bottom")).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.AddRange(entries.Where(e => e.Source == ContextMenuEntrySource.Modern).OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.AddRange(entries.Where(e => e.Source == ContextMenuEntrySource.Classic && e.Kind != ContextMenuEntryKind.Verb && !BuiltInHandlerDenylist.Contains(e.HandlerKeyName)).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));

            items.Add(ContextMenuPreviewItem.Separator());
            items.AddRange(BuiltInItems(BuiltInMiddle[target]));
            items.Add(ContextMenuPreviewItem.Separator());
            items.AddRange(entries.Where(e => e.Kind == ContextMenuEntryKind.Verb && IsPosition(e, "Bottom")).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.Add(ContextMenuPreviewItem.Separator());
            items.AddRange(BuiltInItems(BuiltInEnd[target]));
            return items;
        }

        // Top-level entries that are enabled and registered for the target. Submenu items are reached
        // through their parent in ToPreviewItem.
        private IEnumerable<ContextMenuEntry> EnabledEntries(ContextMenuPreviewTarget target)
        {
            var roots = TargetRoots[target];
            return Entries.Where(e => e.IsEnabled && e.Parent == null
                && (e.Roots.Any(r => roots.Contains(r, StringComparer.OrdinalIgnoreCase))
                    || e.ItemTypes.Any(t => roots.Contains(t, StringComparer.OrdinalIgnoreCase))));
        }

        private ContextMenuPreviewItem ToPreviewItem(ContextMenuEntry entry)
        {
            var item = new ContextMenuPreviewItem
            {
                Text = entry.DisplayName,
                Icon = entry.Icon,
                Shortcut = entry.Source == ContextMenuEntrySource.Classic && entry.Kind != ContextMenuEntryKind.Verb
                    ? ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_PreviewHandlerHint")
                    : null,
            };

            foreach (var child in Entries.Where(e => e.Parent == entry && e.IsEnabled).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase))
            {
                var childItem = ToPreviewItem(child);

                // Children carry "Parent > Child" in the list; the menu shows only their own label.
                childItem.Text = child.DisplayName.Substring(entry.DisplayName.Length + 3);
                item.Children.Add(childItem);
            }

            return item;
        }

        private static IEnumerable<ContextMenuPreviewItem> BuiltInItems(IEnumerable<string> keys) =>
            keys.Select(k => k == "-" ? ContextMenuPreviewItem.Separator() : BuiltIn(k));

        private static ContextMenuPreviewItem BuiltIn(string key)
        {
            bool submenu = key.EndsWith('>');
            var item = new ContextMenuPreviewItem
            {
                Text = ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_BuiltIn_" + key.TrimEnd('>')),
                IsBuiltIn = true,
            };

            if (submenu)
            {
                // Placeholder so the row shows a chevron; Explorer fills these at runtime.
                item.Children.Add(new ContextMenuPreviewItem { Text = "…", IsBuiltIn = true });
            }

            return item;
        }

        // Drops leading, trailing and doubled separators left behind by empty sections.
        private static IEnumerable<ContextMenuPreviewItem> TidySeparators(IEnumerable<ContextMenuPreviewItem> items)
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
