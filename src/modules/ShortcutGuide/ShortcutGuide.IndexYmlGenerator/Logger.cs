// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace ShortcutGuide.IndexYmlGenerator
{
    /// <summary>
    /// Lightweight logger for the index generator without WinRT dependencies.
    /// Emits logs to %LOCALAPPDATA%\Microsoft\PowerToys\ShortcutGuide\IndexYmlGenerator\Logs\VersionNumber\
    /// matching the format of ManagedCommon.Logger. Also logs to Console for CLI callers.
    /// </summary>
    public static class Logger
    {
        private const string Error = "Error";
        private const string Warning = "Warning";
        private const string Info = "Info";

        private static readonly object SyncLock = new();
        private static bool _initialized;
        private static string? _applicationLogPath;

        public static bool IsPerfLoggingEnabled { get; set; }

        public static void InitializeLogger(string applicationLogPath)
        {
            _applicationLogPath = applicationLogPath;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncLock)
            {
                if (_initialized)
                {
                    return;
                }

                try
                {
                    string version = typeof(Logger).Assembly.GetName().Version?.ToString() ?? "0.0.1.0";

                    string basePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Microsoft",
                        "PowerToys" + (_applicationLogPath ?? @"\ShortcutGuide\IndexYmlGenerator\Logs"));

                    string versionedPath = Path.Combine(basePath, version);

                    if (!Directory.Exists(versionedPath))
                    {
                        Directory.CreateDirectory(versionedPath);
                    }

                    string logFile = "Log_" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";
                    string logFilePath = Path.Combine(versionedPath, logFile);

                    Trace.Listeners.Add(new TextWriterTraceListener(logFilePath));
                    Trace.AutoFlush = true;
                }
                catch
                {
                    // Logging failure should never crash the indexing utility.
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        public static void LogInfo(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Info, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogWarning(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Warning, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogError(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Error, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogError(
            string message,
            Exception? ex,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (ex == null)
            {
                Log(message, Error, memberName, sourceFilePath, sourceLineNumber);
                return;
            }

            string exMessage = $@"
{message}
{ex.GetType()} ({ex.HResult}): {ex.Message}
";

            if (ex.InnerException != null)
            {
                exMessage += $@"
Inner exception:
{ex.InnerException.GetType()} ({ex.InnerException.HResult}): {ex.InnerException.Message}
";
            }

            exMessage += $@"
Stack trace:
{ex.StackTrace}";

            Log(exMessage, Error, memberName, sourceFilePath, sourceLineNumber);
        }

        private static void Log(string message, string type, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            EnsureInitialized();
            string header = $"[{DateTime.Now.TimeOfDay}] [{type}] {GetCallerInfo(memberName, sourceFilePath, sourceLineNumber)}";
            Trace.WriteLine(header);

            if (!string.IsNullOrEmpty(message))
            {
                Trace.Indent();
                Trace.WriteLine(message);
                Trace.Unindent();
            }

            // Mirror diagnostic output to console streams for CLI callers.
            TextWriter consoleTarget = string.Equals(type, Error, StringComparison.Ordinal)
                ? Console.Error
                : Console.Out;

            if (!string.IsNullOrEmpty(message))
            {
                consoleTarget.WriteLine($"[{type}] {message}");
            }
        }

        private static string GetCallerInfo(string memberName, string sourceFilePath, int sourceLineNumber)
        {
            string callerFileName = "Unknown";
            try
            {
                string? fileName = Path.GetFileName(sourceFilePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    callerFileName = fileName;
                }
            }
            catch (Exception)
            {
                callerFileName = "Unknown";
            }

            return $"{callerFileName}::{memberName}::{sourceLineNumber}";
        }
    }
}
