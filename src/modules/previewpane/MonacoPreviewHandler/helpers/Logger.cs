// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;

namespace Microsoft.PowerToys.PreviewHandler.Monaco.Helpers
{
    public static class Logger
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();
        private static readonly string ApplicationLogPath = System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\AppData\\LocalLow\\Microsoft\\PowerToys\\logs\\FileExplorer_localLow\\Monaco";

        static Logger()
        {
            if (!_fileSystem.Directory.Exists(ApplicationLogPath))
            {
                _fileSystem.Directory.CreateDirectory(ApplicationLogPath);
            }

            // Using InvariantCulture since this is used for a log file name
            var logFilePath = _fileSystem.Path.Combine(ApplicationLogPath, "Monaco-log_" + DateTime.Now.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

            Trace.Listeners.Add(new TextWriterTraceListener(logFilePath));

            Trace.AutoFlush = true;
        }

        public static void LogError(string message)
        {
            Log(message, "ERROR");
        }

        public static void LogError(string message, Exception ex)
        {
            Log(
                message + Environment.NewLine +
                ex?.Message + Environment.NewLine +
                "Inner exception: " + Environment.NewLine +
                ex?.InnerException?.Message + Environment.NewLine +
                "Stack trace: " + Environment.NewLine +
                ex?.StackTrace,
                "ERROR");
        }

        public static void LogWarning(string message)
        {
            Log(message, "WARNING");
        }

        public static void LogInfo(string message)
        {
            Log(message, "INFO");
        }

        private static void Log(string message, string type)
        {
            Trace.WriteLine(type + ": " + DateTime.Now.TimeOfDay);
            Trace.Indent();
            Trace.WriteLine(GetCallerInfo());
            Trace.WriteLine(message);
            Trace.Unindent();
        }

        public static void LogTrace()
        {
            Log(string.Empty, "Trace");
        }

        private static string GetCallerInfo()
        {
            StackTrace stackTrace = new StackTrace();

            var methodName = stackTrace.GetFrame(3)?.GetMethod();
            var className = methodName?.DeclaringType.Name;
            return "[Method]: " + methodName?.Name + " [Class]: " + className;
        }
    }
}
