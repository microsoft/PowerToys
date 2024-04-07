// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;
using IShellItem = Windows.Win32.UI.Shell.IShellItem;

namespace Peek.FilePreviewer.Previewers
{
    public partial class ShellPreviewHandlerPreviewer : ObservableObject, IShellPreviewHandlerPreviewer, IDisposable
    {
        private static readonly ConcurrentDictionary<Guid, IClassFactory> HandlerFactories = new();

        [ObservableProperty]
        private IPreviewHandler? preview;

        [ObservableProperty]
        private PreviewState state;

        private Stream? fileStream;

        public ShellPreviewHandlerPreviewer(IFileSystemItem file)
        {
            FileItem = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem FileItem { get; }

        private DispatcherQueue Dispatcher { get; }

        public void Dispose()
        {
            Clear();
            GC.SuppressFinalize(this);
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await FileItem.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PreviewSize { MonitorSize = null });
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            Clear();
            State = PreviewState.Loading;

            cancellationToken.ThrowIfCancellationRequested();

            // Create the preview handler
            var previewHandler = await Task.Run(() =>
            {
                var previewHandlerGuid = GetPreviewHandlerGuid(FileItem.Extension);
                if (!string.IsNullOrEmpty(previewHandlerGuid))
                {
                    var clsid = Guid.Parse(previewHandlerGuid);

                    bool retry = false;
                    do
                    {
                        unsafe
                        {
                            // This runs the preview handler in a separate process (prevhost.exe)
                            // TODO: Figure out how to get it to run in a low integrity level
                            if (!HandlerFactories.TryGetValue(clsid, out var factory))
                            {
                                var hr = PInvoke.CoGetClassObject(clsid, CLSCTX.CLSCTX_LOCAL_SERVER, null, typeof(IClassFactory).GUID, out var pFactory);
                                Marshal.ThrowExceptionForHR(hr);

                                // Storing the factory in memory helps makes the handlers load faster
                                // TODO: Maybe free them after some inactivity or when Peek quits?
                                factory = (IClassFactory)Marshal.GetObjectForIUnknown((IntPtr)pFactory);
                                factory.LockServer(true);
                                HandlerFactories.AddOrUpdate(clsid, factory, (_, _) => factory);
                            }

                            try
                            {
                                var iid = typeof(IPreviewHandler).GUID;
                                factory.CreateInstance(null, &iid, out var instance);
                                return instance as IPreviewHandler;
                            }
                            catch
                            {
                                if (!retry)
                                {
                                    // Process is probably dead, attempt to get the factory again (once)
                                    HandlerFactories.TryRemove(new(clsid, factory));
                                    retry = true;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                    while (retry);
                }

                return null;
            });

            if (previewHandler == null)
            {
                State = PreviewState.Error;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Initialize the preview handler with the selected file
            bool success = await Task.Run(() =>
            {
                const uint STGM_READ = 0x00000000;
                if (previewHandler is IInitializeWithStream initWithStream)
                {
                    fileStream = File.OpenRead(FileItem.Path);
                    initWithStream.Initialize(new IStreamWrapper(fileStream), STGM_READ);
                }
                else if (previewHandler is IInitializeWithItem initWithItem)
                {
                    var hr = PInvoke.SHCreateItemFromParsingName(FileItem.Path, null, typeof(IShellItem).GUID, out var item);
                    Marshal.ThrowExceptionForHR(hr);

                    initWithItem.Initialize((IShellItem)item, STGM_READ);
                }
                else if (previewHandler is IInitializeWithFile initWithFile)
                {
                    unsafe
                    {
                        fixed (char* pPath = FileItem.Path)
                        {
                            initWithFile.Initialize(pPath, STGM_READ);
                        }
                    }
                }
                else
                {
                    // Handler is missing the required interfaces
                    return false;
                }

                return true;
            });

            if (!success)
            {
                State = PreviewState.Error;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Preview.SetWindow() needs to be set in the control
            Preview = previewHandler;
        }

        public void Clear()
        {
            if (Preview != null)
            {
                try
                {
                    Preview.Unload();
                    Marshal.FinalReleaseComObject(Preview);
                }
                catch
                {
                }

                Preview = null;
            }

            if (fileStream != null)
            {
                fileStream.Dispose();
                fileStream = null;
            }
        }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return !string.IsNullOrEmpty(GetPreviewHandlerGuid(item.Extension));
        }

        private static string? GetPreviewHandlerGuid(string fileExt)
        {
            const string PreviewHandlerKeyPath = "shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}";

            // Search by file extension
            using var classExtensionKey = Registry.ClassesRoot.OpenSubKey(fileExt);
            using var classExtensionPreviewHandlerKey = classExtensionKey?.OpenSubKey(PreviewHandlerKeyPath);

            if (classExtensionKey != null && classExtensionPreviewHandlerKey == null)
            {
                // Search by file class
                var className = classExtensionKey.GetValue(null) as string;
                if (!string.IsNullOrEmpty(className))
                {
                    using var classKey = Registry.ClassesRoot.OpenSubKey(className);
                    using var classPreviewHandlerKey = classKey?.OpenSubKey(PreviewHandlerKeyPath);

                    return classPreviewHandlerKey?.GetValue(null) as string;
                }
            }

            return classExtensionPreviewHandlerKey?.GetValue(null) as string;
        }
    }
}
