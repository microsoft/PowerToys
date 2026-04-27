// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroEngineHost : IDisposable
{
    private static readonly string MacrosDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "PowerToys",
            "Macros");

    private readonly HotkeyManager _hotkeyManager = new();
    private readonly MacroLoader _loader;
    private readonly MacroExecutor _executor;
    private readonly IAppScopeChecker _scopeChecker;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private bool _hotkeysActive = true;
    private CancellationTokenSource? _currentMacro;

    public MacroEngineHost()
        : this(new SendInputHelper(), new AppScopeChecker())
    {
    }

    internal MacroEngineHost(ISendInputHelper input, IAppScopeChecker scopeChecker)
    {
        _executor = new MacroExecutor(input);
        _scopeChecker = scopeChecker;
        _loader = new MacroLoader(MacrosDirectory);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _loader.LoadAllAsync(ct);
        _loader.MacrosChanged += OnMacrosChanged;
        _loader.StartWatching();

        _hotkeyManager.HotkeyTriggered += OnHotkeyTriggered;
        _hotkeyManager.Start();

        RegisterAllHotkeys();
    }

    private async void OnMacrosChanged(object? sender, EventArgs e)
    {
        try
        {
            await ReloadAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("MacroEngine: Macro reload failed.", ex);
        }
    }

    public async Task ExecuteMacroByIdAsync(string macroId, CancellationToken ct)
    {
        if (!_loader.Macros.TryGetValue(macroId, out var macro))
        {
            return;
        }

        await ExecuteMacroAsync(macro, ct);
    }

    public void SuspendHotkeys()
    {
        _hotkeysActive = false;
        _hotkeyManager.UnregisterAll();
    }

    public void ResumeHotkeys()
    {
        _hotkeysActive = true;
        RegisterAllHotkeys();
    }

    public IReadOnlyList<string> GetMacroIds() =>
        _loader.Macros.Keys.ToList();

    private void OnHotkeyTriggered(object? sender, string macroId)
    {
        if (!_loader.Macros.TryGetValue(macroId, out var macro))
        {
            return;
        }

        if (macro.AppScope != null && !_scopeChecker.IsForegroundAppMatch(macro.AppScope))
        {
            return;
        }

        var old = Interlocked.Exchange(ref _currentMacro, new CancellationTokenSource());
        old?.Cancel();
        old?.Dispose();

        _ = ExecuteMacroAsync(macro, _currentMacro.Token);
    }

    private async Task ExecuteMacroAsync(MacroDefinition macro, CancellationToken ct)
    {
        try
        {
            await _executor.ExecuteAsync(macro, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RegisterAllHotkeys()
    {
        foreach (var macro in _loader.Macros.Values)
        {
            if (macro.Hotkey is not null && macro.IsEnabled)
            {
                try
                {
                    _hotkeyManager.RegisterHotkey(macro.Hotkey, macro.Id);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogWarning($"MacroEngine: Hotkey conflict for '{macro.Name}': {ex.Message}");
                }
            }
        }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        if (!await _reloadLock.WaitAsync(0, ct))
        {
            return;
        }

        try
        {
            _hotkeyManager.UnregisterAll();
            await _loader.LoadAllAsync(ct);
            if (_hotkeysActive)
            {
                RegisterAllHotkeys();
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public void Dispose()
    {
        _loader.MacrosChanged -= OnMacrosChanged;
        _loader.Dispose();
        _hotkeyManager.Dispose();
        var old = Interlocked.Exchange(ref _currentMacro, null);
        old?.Cancel();
        old?.Dispose();
        _reloadLock.Dispose();
    }
}
