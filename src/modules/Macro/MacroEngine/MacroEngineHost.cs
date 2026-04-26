// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroEngineHost : IDisposable
{
    private static readonly string MacrosDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "PowerToys", "Macros");

    private readonly HotkeyManager _hotkeyManager = new();
    private readonly MacroLoader _loader;
    private readonly MacroExecutor _executor;
    private readonly IAppScopeChecker _scopeChecker;
    private bool _hotkeysActive = true;
    private CancellationTokenSource? _currentMacro;

    public MacroEngineHost()
        : this(new SendInputHelper(), new AppScopeChecker()) { }

    internal MacroEngineHost(ISendInputHelper input, IAppScopeChecker scopeChecker)
    {
        _executor = new MacroExecutor(input);
        _scopeChecker = scopeChecker;
        _loader = new MacroLoader(MacrosDirectory);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _loader.LoadAllAsync(ct);
        _loader.MacrosChanged += async (_, _) => await ReloadAsync(ct);
        _loader.StartWatching();

        _hotkeyManager.HotkeyTriggered += OnHotkeyTriggered;
        _hotkeyManager.Start();

        RegisterAllHotkeys();
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

        _currentMacro?.Cancel();
        _currentMacro = new CancellationTokenSource();
        _ = ExecuteMacroAsync(macro, _currentMacro.Token);
    }

    private async Task ExecuteMacroAsync(MacroDefinition macro, CancellationToken ct)
    {
        try
        {
            await _executor.ExecuteAsync(macro, ct);
        }
        catch (OperationCanceledException) { }
    }

    private void RegisterAllHotkeys()
    {
        foreach (var macro in _loader.Macros.Values)
        {
            if (macro.Hotkey is not null)
            {
                try
                {
                    _hotkeyManager.RegisterHotkey(macro.Hotkey, macro.Id);
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[MacroEngine] Hotkey conflict for '{macro.Name}': {ex.Message}");
                }
            }
        }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        _hotkeyManager.UnregisterAll();
        await _loader.LoadAllAsync(ct);
        if (_hotkeysActive)
        {
            RegisterAllHotkeys();
        }
    }

    public void Dispose()
    {
        _currentMacro?.Cancel();
        _currentMacro?.Dispose();
        _hotkeyManager.Dispose();
        _loader.Dispose();
    }
}
