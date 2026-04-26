// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace PowerToys.MacroEngine;

internal sealed class MacroLoader : IDisposable
{
    private readonly string _directory;
    private readonly Dictionary<string, MacroDefinition> _macros = [];
    private FileSystemWatcher? _watcher;

    public event EventHandler? MacrosChanged;

    public MacroLoader(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory, nameof(directory));
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        _macros.Clear();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var macro = await MacroSerializer.DeserializeFileAsync(path, ct);
                _macros[macro.Id] = macro;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Skip malformed files
                System.Diagnostics.Trace.WriteLine($"[MacroEngine] Skipping malformed macro file '{path}': {ex.Message}");
            }
        }
    }

    public void StartWatching()
    {
        _watcher = new FileSystemWatcher(_directory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Created += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Deleted += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Renamed += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyDictionary<string, MacroDefinition> Macros => _macros;

    public void Dispose() => _watcher?.Dispose();
}
