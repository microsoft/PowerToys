// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Serialization;
using interop;

namespace Microsoft.PowerToys.Common.Utils
{
    public static class Logger
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        private static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion;

        private static readonly string Error = "Error";
        private static readonly string Warning = "Warning";
        private static readonly string Info = "Info";
        private static readonly string Debug = "Debug";
        private static readonly string TraceFlag = "Trace";

        static Logger()
        {
            var location = Assembly.GetExecutingAssembly().Location;

            string applicationLogPath = Path.Combine(Constants.AppDataPath());

            if (location.Contains("ColorPicker"))
            {
                applicationLogPath += "\\ColorPicker\\Logs";
            }
            else
            {
                applicationLogPath += "\\FancyZones\\Logs";
            }

            applicationLogPath = Path.Combine(applicationLogPath, Version);

            if (!Directory.Exists(applicationLogPath))
            {
                Directory.CreateDirectory(applicationLogPath);
            }

            // Using InvariantCulture since this is used for a log file name
            var logFilePath = _fileSystem.Path.Combine(applicationLogPath, "Log_" + DateTime.Now.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

            Trace.Listeners.Add(new TextWriterTraceListener(logFilePath));

            Trace.AutoFlush = true;
        }

        public static void LogError(string message)
        {
            Log(message, Error);
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
                Error);
        }

        public static void LogWarning(string message)
        {
            Log(message, Warning);
        }

        public static void LogInfo(string message)
        {
            Log(message, Info);
        }

        public static void LogDebug(string message)
        {
            Log(message, Debug);
        }

        public static void LogTrace()
        {
            Log(string.Empty, TraceFlag);
        }

        private static void Log(string message, string type)
        {
            Trace.WriteLine("[" + DateTime.Now.TimeOfDay + "] [" + type + "] " + GetCallerInfo());
            Trace.Indent();
            if (message != string.Empty)
            {
                Trace.WriteLine(message);
            }

            Trace.Unindent();
        }

        private static string GetCallerInfo()
        {
            StackTrace stackTrace = new StackTrace();

            var methodName = stackTrace.GetFrame(3)?.GetMethod();
            var className = methodName?.DeclaringType.Name;
            return className + "::" + methodName?.Name;
        }
    }
}
