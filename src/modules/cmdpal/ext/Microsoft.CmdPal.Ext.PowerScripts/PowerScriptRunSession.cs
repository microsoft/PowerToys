// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>Owns a run independently of the page's navigation lifetime.</summary>
internal sealed partial class PowerScriptRunSession : IDisposable
{
    private readonly object _gate = new();
    private readonly IPowerScriptsService _service;
    private readonly TimeSpan _timeout;
    private PowerScriptInfo _script;
    private PowerScriptRunState _state;
    private PowerScriptTransformResult? _result;
    private string? _error;
    private bool _running;
    private bool _stopWasRequested;
    private bool _disposed;
    private CancellationTokenSource? _stopCancellation;
    private Task? _completion;

    internal PowerScriptRunSession(IPowerScriptsService service, PowerScriptInfo script, TimeSpan? timeout = null)
    {
        _service = service;
        _script = script;
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    internal event EventHandler? Changed;

    internal PowerScriptRunState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    internal bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    internal PowerScriptTransformResult? Result
    {
        get
        {
            lock (_gate)
            {
                return _result;
            }
        }
    }

    internal string? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    internal bool StopWasRequested
    {
        get
        {
            lock (_gate)
            {
                return _stopWasRequested;
            }
        }
    }

    internal Task? Completion
    {
        get
        {
            lock (_gate)
            {
                return _completion;
            }
        }
    }

    internal void UpdateScript(PowerScriptInfo script)
    {
        lock (_gate)
        {
            if (!_running)
            {
                _script = script;
            }
        }
    }

    internal bool TryRun(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        PowerScriptInfo script;
        lock (_gate)
        {
            script = _script;
        }

        return TryRun(script, parameters);
    }

    internal bool TryRun(PowerScriptInfo scriptSnapshot, IReadOnlyDictionary<string, JsonElement> parameters)
    {
        lock (_gate)
        {
            if (_disposed || _running)
            {
                return false;
            }

            var snapshot = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                snapshot[parameter.Key] = parameter.Value.Clone();
            }

            _state = PowerScriptRunState.Running;
            _running = true;
            _result = null;
            _error = null;
            _stopWasRequested = false;
            _stopCancellation = new CancellationTokenSource();
            var stopCancellation = _stopCancellation;

            // The async service may do synchronous preparation before its first await.
            // Keep that work, and all execution, off the palette's request thread.
            _completion = Task.Run(() => ExecuteAsync(scriptSnapshot, snapshot, stopCancellation));
        }

        RaiseChanged();
        return true;
    }

    internal void RequestStop()
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (!_running || _state is not (PowerScriptRunState.Running or PowerScriptRunState.StopRequested))
            {
                return;
            }

            _stopWasRequested = true;
            _state = PowerScriptRunState.StopRequested;
            cancellation = _stopCancellation;
        }

        Cancel(cancellation);
        RaiseChanged();
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cancellation = _stopCancellation;
        }

        Cancel(cancellation);
    }

    private async Task ExecuteAsync(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationTokenSource stopCancellation)
    {
        using var timeoutCancellation = new CancellationTokenSource(_timeout);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(stopCancellation.Token, timeoutCancellation.Token);
        try
        {
            runCancellation.Token.ThrowIfCancellationRequested();
            var result = await _service.RunAsync(script, parameters, runCancellation.Token).ConfigureAwait(false);
            lock (_gate)
            {
                _result = result;
                _state = result.ExitCode == PowerScriptsProtocol.ExitCode.Timeout || (!result.Succeeded && timeoutCancellation.IsCancellationRequested)
                    ? PowerScriptRunState.TimedOut
                    : result.Succeeded
                        ? PowerScriptRunState.Succeeded
                        : _stopWasRequested
                            ? PowerScriptRunState.Stopped
                            : PowerScriptRunState.Failed;
            }
        }
        catch (OperationCanceledException ex)
        {
            lock (_gate)
            {
                _state = _stopWasRequested || _disposed
                    ? PowerScriptRunState.Stopped
                    : timeoutCancellation.IsCancellationRequested
                        ? PowerScriptRunState.TimedOut
                        : PowerScriptRunState.Failed;
                _error = _state == PowerScriptRunState.Failed ? ex.Message : null;
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state = PowerScriptRunState.Failed;
                _error = ex.Message;
            }
        }
        finally
        {
            lock (_gate)
            {
                _stopCancellation = null;
                stopCancellation.Dispose();
                _running = false;
            }

            RaiseChanged();
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // A closed palette must not turn a completed run into an unobserved task failure.
        }
    }

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        try
        {
            // Cancellation callbacks may synchronously complete the run and update
            // its form. Do not invoke those callbacks while holding the session lock.
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run can finish and dispose its source after the caller releases the lock.
        }
    }
}
