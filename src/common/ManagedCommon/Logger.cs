// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using interop;

namespace Microsoft.PowerToys.Common.Utils
{
    public class Logger
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        private static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion;
        private string applicationLogPath;
        private static bool inLocalLowDirectory;

        public string LogPath
        {
            private get
            {
                return applicationLogPath;
            }

            set
            {
                applicationLogPath = !inLocalLowDirectory ?
                    Path.Combine(Constants.AppDataPath(), "\\" + value, Version) :
                    System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\AppData\\LocalLow\\Microsoft\\PowerToys" + value;
            }
        }

        private static readonly string Error = "Error";
        private static readonly string Warning = "Warning";
        private static readonly string Info = "Info";
        private static readonly string Debug = "Debug";
        private static readonly string TraceFlag = "Trace";

        public Logger(string logPath, bool localLow = false)
        {
            LogPath = logPath;
            inLocalLowDirectory = localLow;

            if (!_fileSystem.Directory.Exists(applicationLogPath))
            {
                _fileSystem.Directory.CreateDirectory(applicationLogPath);
            }

            // Using InvariantCulture since this is used for a log file name
            var logFilePath = _fileSystem.Path.Combine(applicationLogPath, "Log_" + DateTime.Now.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

            Trace.Listeners.Add(new TextWriterTraceListener(logFilePath));

            Trace.AutoFlush = true;
        }

        public void LogError(string message)
        {
            Log(message, Error);
        }

        public void LogError(string message, Exception ex)
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

        public void LogWarning(string message)
        {
            Log(message, Warning);
        }

        public void LogInfo(string message)
        {
            Log(message, Info);
        }

        public void LogDebug(string message)
        {
            Log(message, Debug);
        }

        public void LogTrace()
        {
            Log(string.Empty, TraceFlag);
        }

        private void Log(string message, string type)
        {
            Trace.WriteLine("[" + DateTime.Now.TimeOfDay + "] [" + type + "] " + GetCallerInfo());
            Trace.Indent();
            if (message != string.Empty)
            {
                Trace.WriteLine(message);
            }

            Trace.Unindent();
        }

        private string GetCallerInfo()
        {
            StackTrace stackTrace = new StackTrace();

            var methodName = stackTrace.GetFrame(3)?.GetMethod();
            var className = methodName?.DeclaringType.Name;
            return className + "::" + methodName?.Name;
        }
    }
}
