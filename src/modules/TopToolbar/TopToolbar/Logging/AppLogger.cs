// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TopToolbar.Logging
{
    internal static class AppLogger
    {
        private static readonly object InitLock = new object();
        private static readonly string AssemblyVersion = typeof(AppLogger).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
        private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        private static ILogger _logger = NullLogger.Instance;
        private static bool _initialized;

        public static void Initialize(string logsRoot)
        {
            if (_initialized)
            {
                return;
            }

            lock (InitLock)
            {
                if (_initialized)
                {
                    return;
                }

                try
                {
                    string targetDirectory = PrepareLogDirectory(logsRoot);
                    var fileProvider = new FileLoggerProvider(targetDirectory);

                    _loggerFactory = LoggerFactory.Create(builder =>
                    {
                        builder.ClearProviders();
                        builder.SetMinimumLevel(LogLevel.Information);
                        builder.AddProvider(fileProvider);
                    });

                    _logger = _loggerFactory.CreateLogger("TopToolbar");
                    _initialized = true;
                }
                catch
                {
                    _loggerFactory = NullLoggerFactory.Instance;
                    _logger = NullLogger.Instance;
                }
            }
        }

        public static void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Information, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Warning, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Error, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogError(string message, Exception exception, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Error, message, exception, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogDebug(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Debug, message, null, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogTrace([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(LogLevel.Trace, string.Empty, null, memberName, sourceFilePath, sourceLineNumber);
        }

        private static void Log(LogLevel level, string message, Exception exception, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            string caller = FormatCaller(memberName, sourceFilePath, sourceLineNumber);
            string payload = string.IsNullOrEmpty(message) ? caller : string.Concat(caller, " - ", message);
            _logger.Log(level, 0, payload, exception, static (state, ex) => state);
        }

        private static string PrepareLogDirectory(string logsRoot)
        {
            if (string.IsNullOrWhiteSpace(logsRoot))
            {
                throw new ArgumentException("Log directory must be provided.", nameof(logsRoot));
            }

            Directory.CreateDirectory(logsRoot);
            string versionedDirectory = Path.Combine(logsRoot, AssemblyVersion);
            Directory.CreateDirectory(versionedDirectory);

            Task.Run(() => CleanOldVersionFolders(logsRoot, versionedDirectory));
            return versionedDirectory;
        }

        private static void CleanOldVersionFolders(string basePath, string currentVersionPath)
        {
            try
            {
                if (!Directory.Exists(basePath))
                {
                    return;
                }

                var directories = new DirectoryInfo(basePath)
                    .EnumerateDirectories()
                    .Where(d => !string.Equals(d.FullName, currentVersionPath, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d.CreationTimeUtc)
                    .Skip(3);

                foreach (var directory in directories)
                {
                    try
                    {
                        directory.Delete(true);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static string FormatCaller(string memberName, string sourceFilePath, int sourceLineNumber)
        {
            string fileName = string.Empty;
            try
            {
                fileName = Path.GetFileName(sourceFilePath);
            }
            catch
            {
                fileName = string.Empty;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                return string.Concat(memberName, ":", sourceLineNumber.ToString(CultureInfo.InvariantCulture));
            }

            return string.Concat(fileName, "::", memberName, "::", sourceLineNumber.ToString(CultureInfo.InvariantCulture));
        }

        private sealed class FileLoggerProvider : ILoggerProvider
        {
            private readonly string _directory;
            private readonly object _lock = new object();
            private bool _disposed;

            internal FileLoggerProvider(string directory)
            {
                _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            }

            public ILogger CreateLogger(string categoryName)
            {
                ObjectDisposedException.ThrowIf(_disposed, nameof(FileLoggerProvider));
                return new FileLogger(this, categoryName);
            }

            public void Dispose()
            {
                _disposed = true;
            }

            internal void WriteMessage(string categoryName, LogLevel level, string message, Exception exception)
            {
                if (_disposed)
                {
                    return;
                }

                string logFilePath = GetLogFilePath();
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string line = string.Concat("[", timestamp, "] [", level.ToString(), "] [", categoryName, "] ", message ?? string.Empty);

                if (exception != null)
                {
                    line = string.Concat(line, Environment.NewLine, exception.ToString());
                }

                lock (_lock)
                {
                    Directory.CreateDirectory(_directory);
                    File.AppendAllText(logFilePath, line + Environment.NewLine);
                }
            }

            private string GetLogFilePath()
            {
                string fileName = "Log_" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";
                return Path.Combine(_directory, fileName);
            }

            private sealed class FileLogger : ILogger
            {
                private readonly FileLoggerProvider _provider;
                private readonly string _categoryName;

                internal FileLogger(FileLoggerProvider provider, string categoryName)
                {
                    _provider = provider;
                    _categoryName = string.IsNullOrEmpty(categoryName) ? "TopToolbar" : categoryName;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    return NullScope.Instance;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return logLevel != LogLevel.None;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    if (!IsEnabled(logLevel))
                    {
                        return;
                    }

                    string message = formatter != null ? formatter(state, exception) : state != null ? state.ToString() : string.Empty;
                    _provider.WriteMessage(_categoryName, logLevel, message, exception);
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
