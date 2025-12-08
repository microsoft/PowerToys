// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.Extensions.Logging;

namespace Microsoft.CommandPalette.UI.Services;

// Adapter implementing Microsoft.Extensions.Logging.ILogger,
// delegating to ManagedCommon.Logger.
internal sealed partial class CmdPalLogger : ILogger
{
    private static readonly AsyncLocal<Stack<object>> _scopeStack = new();
    private readonly LogLevel _minLevel;

    public string CurrentVersionLogDirectoryPath => Logger.CurrentVersionLogDirectoryPath;

    public CmdPalLogger(LogLevel minLevel = LogLevel.Information)
    {
        _minLevel = minLevel;

        // Ensure underlying logger initialized (idempotent if already done elsewhere).
        Logger.InitializeLogger("\\CmdPal\\Logs\\");
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minLevel;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        var stack = _scopeStack.Value;
        if (stack is null)
        {
            stack = new Stack<object>();
            _scopeStack.Value = stack;
        }

        stack.Push(state);
        return new Scope(stack);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);
        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var scopeSuffix = BuildScopeSuffix();
        var eventPrefix = eventId.Id != 0 ? $"[{eventId.Id}/{eventId.Name}] " : string.Empty;
        var finalMessage = $"{eventPrefix}{message}{scopeSuffix}";

        switch (logLevel)
        {
            case LogLevel.Trace:
                // Existing stack: Trace logs an empty line; append message via Debug.
                Logger.LogTrace();

                if (!string.IsNullOrEmpty(message))
                {
                    Logger.LogDebug(finalMessage);
                }

                if (exception is not null)
                {
                    Logger.LogError(exception.Message, exception);
                }

                break;

            case LogLevel.Debug:
                Logger.LogDebug(finalMessage);

                if (exception is not null)
                {
                    Logger.LogError(exception.Message, exception);
                }

                break;

            case LogLevel.Information:
                Logger.LogInfo(finalMessage);

                if (exception is not null)
                {
                    Logger.LogError(exception.Message, exception);
                }

                break;

            case LogLevel.Warning:
                Logger.LogWarning(finalMessage);

                if (exception is not null)
                {
                    Logger.LogError(exception.Message, exception);
                }

                break;

            case LogLevel.Error:
            case LogLevel.Critical:
                if (exception is not null)
                {
                    Logger.LogError(finalMessage, exception);
                }
                else
                {
                    Logger.LogError(finalMessage);
                }

                break;

            case LogLevel.None:
            default:
                break;
        }
    }

    private static string BuildScopeSuffix()
    {
        var stack = _scopeStack.Value;
        if (stack is null || stack.Count == 0)
        {
            return string.Empty;
        }

        // Show most-recent first.
        return $" [Scopes: {string.Join(" => ", stack.ToArray())}]";
    }

    private sealed partial class Scope : IDisposable
    {
        private readonly Stack<object> _stack;
        private bool _disposed;

        public Scope(Stack<object> stack) => _stack = stack;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_stack.Count > 0)
            {
                _stack.Pop();
            }

            _disposed = true;
        }
    }
}
