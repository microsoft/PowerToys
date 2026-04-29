// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Dispatching;
using PowerToys.MacroCommon.Ipc;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;
using StreamJsonRpc;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroViewModel : Observable, IDisposable
{
    private readonly string _macrosDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly DispatcherQueue? _dispatcherQueue;
    private JsonRpc? _rpc;
    private NamedPipeClientStream? _pipe;
    private bool _disposed;

    public ObservableCollection<MacroListItem> Macros { get; } = [];

    public MacroViewModel()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "PowerToys",
            "Macros"))
    {
    }

    internal MacroViewModel(string macrosDirectory)
    {
        _macrosDirectory = macrosDirectory;
        try
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }
        catch (Exception)
        {
            _dispatcherQueue = null;
        }

        Directory.CreateDirectory(_macrosDirectory);
        LoadMacros();

        _debounce = new System.Timers.Timer(300) { AutoReset = false };
        _debounce.Elapsed += (_, _) =>
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(LoadMacros);
            }
            else
            {
                Logger.LogWarning("Macro settings: FSW reload skipped — no DispatcherQueue available.");
            }
        };

        _watcher = new FileSystemWatcher(_macrosDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += (s, e) => OnFileChanged(s, e);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void LoadMacros()
    {
        string[] files = Directory.Exists(_macrosDirectory)
            ? Directory.GetFiles(_macrosDirectory, "*.json")
            : [];

        Macros.Clear();
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                MacroDefinition def = MacroSerializer.Deserialize(json);
                Macros.Add(new MacroListItem(def, file));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Macro settings: skipping malformed {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    public async Task SuspendHotkeysAsync()
    {
        await EnsureRpcAsync().ConfigureAwait(false);
        if (_rpc is null)
        {
            return;
        }

        try
        {
            await _rpc.InvokeWithCancellationAsync<object>(
                nameof(IMacroEngineRpc.SuspendHotkeysAsync),
                [],
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Macro settings: SuspendHotkeys IPC failed: {ex.Message}");
            _rpc = null;
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    /// <summary>
    /// Resumes hotkeys after a prior <see cref="SuspendHotkeysAsync"/> call.
    /// If the engine was not reachable when Suspend was called, this is a no-op.
    /// </summary>
    public async Task ResumeHotkeysAsync()
    {
        if (_rpc is null)
        {
            return;
        }

        try
        {
            await _rpc.InvokeWithCancellationAsync<object>(
                nameof(IMacroEngineRpc.ResumeHotkeysAsync),
                [],
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Macro settings: ResumeHotkeys IPC failed: {ex.Message}");
            _rpc = null;
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    public async Task SaveMacroAsync(MacroEditViewModel editVm)
    {
        MacroDefinition def = editVm.ToDefinition();
        string path = Path.Combine(_macrosDirectory, $"{def.Id}.json");
        await MacroSerializer.SerializeFileAsync(def, path).ConfigureAwait(false);
    }

    public void DeleteMacro(MacroListItem item)
    {
        try
        {
            File.Delete(item.FilePath);
            Macros.Remove(item);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Macro settings: delete failed for {item.FilePath}: {ex.Message}");
        }
    }

    private async Task EnsureRpcAsync()
    {
        if (_rpc != null)
        {
            return;
        }

        try
        {
            NamedPipeClientStream pipe = new(
                ".",
                MacroIpcConstants.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(500, CancellationToken.None).ConfigureAwait(false);
            _pipe = pipe;
            _rpc = JsonRpc.Attach(pipe);
        }
        catch
        {
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounce.Stop();
        _debounce.Dispose();
        _rpc?.Dispose();
        _pipe?.Dispose();
    }
}
