// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace PowerToys.MacroEngine;

internal sealed class MacroLoader : IDisposable
{
    private readonly string _directory;
    private ConcurrentDictionary<string, MacroDefinition> _macros = new();
    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _debounceTimer;

    public event EventHandler? MacrosChanged;

    public MacroLoader(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory, nameof(directory));
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        var fresh = new ConcurrentDictionary<string, MacroDefinition>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var macro = await MacroSerializer.DeserializeFileAsync(path, ct);
                fresh[macro.Id] = macro;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Trace.WriteLine($"[MacroEngine] Skipping malformed macro file '{path}': {ex.Message}");
            }
        }

        // Atomic swap: _macros is only replaced on full successful completion
        _macros = fresh;
    }

    public void StartWatching()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();

        _debounceTimer = new System.Timers.Timer(interval: 300) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);

        _watcher = new FileSystemWatcher(_directory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemEvent;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        // Restart debounce window on each event; only fires MacrosChanged after 300 ms of quiet.
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    public IReadOnlyDictionary<string, MacroDefinition> Macros => _macros;

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
