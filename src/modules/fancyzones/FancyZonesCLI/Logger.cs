// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace FancyZonesCLI;

/// <summary>
/// Simple logger for FancyZones CLI.
/// Logs to %LOCALAPPDATA%\Microsoft\PowerToys\FancyZones\CLI\Logs
/// </summary>
internal static class Logger
{
    private static readonly object LockObj = new();
    private static string _logFilePath = string.Empty;
    private static bool _isInitialized;

    /// <summary>
    /// Gets the path to the current log file.
    /// </summary>
    public static string LogFilePath => _logFilePath;

    /// <summary>
    /// Initializes the logger.
    /// </summary>
    public static void InitializeLogger()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(localAppData, "Microsoft", "PowerToys", "FancyZones", "CLI", "Logs");

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logFileName = $"FancyZonesCLI_{DateTime.Now:yyyy-MM-dd}.log";
            _logFilePath = Path.Combine(logDirectory, logFileName);
            _isInitialized = true;

            LogInfo("FancyZones CLI started");
        }
        catch
        {
            // Silently fail if logging cannot be initialized
        }
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log("ERROR", message, memberName, sourceFilePath, sourceLineNumber);
    }

    /// <summary>
    /// Logs an error message with exception details.
    /// </summary>
    public static void LogError(string message, Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        var fullMessage = ex == null
            ? message
            : $"{message} | Exception: {ex.GetType().Name}: {ex.Message}";
        Log("ERROR", fullMessage, memberName, sourceFilePath, sourceLineNumber);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log("WARN", message, memberName, sourceFilePath, sourceLineNumber);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public static void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log("INFO", message, memberName, sourceFilePath, sourceLineNumber);
    }

    /// <summary>
    /// Logs a debug message (only in DEBUG builds).
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void LogDebug(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log("DEBUG", message, memberName, sourceFilePath, sourceLineNumber);
    }

    private static void Log(string level, string message, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_logFilePath))
        {
            return;
        }

        try
        {
            var fileName = Path.GetFileName(sourceFilePath);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var logEntry = $"[{timestamp}] [{level}] [{fileName}:{sourceLineNumber}] [{memberName}] {message}{Environment.NewLine}";

            lock (LockObj)
            {
                File.AppendAllText(_logFilePath, logEntry);
            }
        }
        catch
        {
            // Silently fail if logging fails
        }
    }
}
