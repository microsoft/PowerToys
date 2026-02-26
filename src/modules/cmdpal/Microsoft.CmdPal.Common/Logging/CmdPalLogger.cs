// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Logging;

// Adapter implementing Microsoft.Extensions.Logging.ILogger,
// delegating to ManagedCommon.Logger.
public sealed partial class CmdPalLogger : ILogger
{
    private static readonly AsyncLocal<Stack<object>> _scopeStack = new();
    private readonly LogLevel _minLevel;
    private readonly string? _categoryName;

    public string CurrentVersionLogDirectoryPath => Logger.CurrentVersionLogDirectoryPath;

    public CmdPalLogger(LogLevel minLevel = LogLevel.Information)
        : this(null, minLevel)
    {
    }

    internal CmdPalLogger(string? categoryName, LogLevel minLevel = LogLevel.Information)
    {
        _categoryName = categoryName;
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

        var categoryPrefix = !string.IsNullOrEmpty(_categoryName) ? $"[{_categoryName}] " : string.Empty;
        var scopeSuffix = BuildScopeSuffix();
        var eventPrefix = eventId.Id != 0 ? $"[{eventId.Id}/{eventId.Name}] " : string.Empty;
        var finalMessage = $"{categoryPrefix}{eventPrefix}{message}{scopeSuffix}";

        switch (logLevel)
        {
            case LogLevel.Trace:
                // Existing stack: Trace logs an empty line; append message via Debug.
                Logger.LogTrace();

                if (!string.IsNullOrEmpty(message))
                {
                    Logger.LogDebug(finalMessage, exception);
                }

                break;

            case LogLevel.Debug:
                Logger.LogDebug(finalMessage, exception);

                break;

            case LogLevel.Information:
                Logger.LogInfo(finalMessage, exception);

                break;

            case LogLevel.Warning:
                Logger.LogWarning(finalMessage, exception);

                break;

            case LogLevel.Error:
            case LogLevel.Critical:
                Logger.LogError(finalMessage, exception);

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

// Generic logger adapter that includes the category name based on T.
public sealed partial class CmdPalLogger<T> : ILogger<T>
{
    private readonly CmdPalLogger _logger;

    public CmdPalLogger(LogLevel minLevel = LogLevel.Information)
    {
        _logger = new CmdPalLogger(typeof(T).FullName, minLevel);
    }

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => _logger.BeginScope(state);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
}
