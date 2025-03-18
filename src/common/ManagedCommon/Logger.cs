// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using PowerToys.Interop;

namespace ManagedCommon
{
    public static class Logger
    {
        private static readonly string Error = "Error";
        private static readonly string Warning = "Warning";
        private static readonly string Info = "Info";
        private static readonly string Debug = "Debug";
        private static readonly string TraceFlag = "Trace";

        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

        /*
         * Please pay more attention!
         * If you want to publish it with Native AOT enabled (or publish as a single file).
         * You need to find another way to remove Assembly.Location usage.
         */
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
        private static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file

        /// <summary>
        /// Initializes the logger and sets the path for logging.
        /// </summary>
        /// <example>InitializeLogger("\\FancyZones\\Editor\\Logs")</example>
        /// <param name="applicationLogPath">The path to the log files folder.</param>
        /// <param name="isLocalLow">If the process using Logger is a low-privilege process.</param>
        public static void InitializeLogger(string applicationLogPath, bool isLocalLow = false)
        {
            if (isLocalLow)
            {
                applicationLogPath = Environment.GetEnvironmentVariable("userprofile") + "\\appdata\\LocalLow\\Microsoft\\PowerToys" + applicationLogPath + "\\" + Version;
            }
            else
            {
                applicationLogPath = Constants.AppDataPath() + applicationLogPath + "\\" + Version;
            }

            if (!Directory.Exists(applicationLogPath))
            {
                Directory.CreateDirectory(applicationLogPath);
            }

            var logFilePath = Path.Combine(applicationLogPath, "Log_" + DateTime.Now.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

            Trace.Listeners.Add(new TextWriterTraceListener(logFilePath));

            Trace.AutoFlush = true;
        }

        public static void LogError(string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Error, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogError(string message, Exception ex, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            if (ex == null)
            {
                Log(message, Error, memberName, sourceFilePath, sourceLineNumber);
            }
            else
            {
                var exMessage =
                    message + Environment.NewLine +
                    ex.GetType() + ": " + ex.Message + Environment.NewLine;

                if (ex.InnerException != null)
                {
                    exMessage +=
                        "Inner exception: " + Environment.NewLine +
                        ex.InnerException.GetType() + ": " + ex.InnerException.Message + Environment.NewLine;
                }

                exMessage +=
                    "Stack trace: " + Environment.NewLine +
                    ex.StackTrace;

                Log(exMessage, Error, memberName, sourceFilePath, sourceLineNumber);
            }
        }

        public static void LogWarning(string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Warning, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogInfo(string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Info, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogDebug(string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(message, Debug, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void LogTrace([System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(string.Empty, TraceFlag, memberName, sourceFilePath, sourceLineNumber);
        }

        private static void Log(string message, string type, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            Trace.WriteLine("[" + DateTime.Now.TimeOfDay + "] [" + type + "] " + GetCallerInfo(memberName, sourceFilePath, sourceLineNumber));
            Trace.Indent();
            if (message != string.Empty)
            {
                Trace.WriteLine(message);
            }

            Trace.Unindent();
        }

        private static string GetCallerInfo(string memberName, string sourceFilePath, int sourceLineNumber)
        {
            string callerFileName = "Unknown";

            try
            {
                string fileName = Path.GetFileName(sourceFilePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    callerFileName = fileName;
                }
            }
            catch (Exception)
            {
                callerFileName = "Unknown";
#if DEBUG
                throw;
#endif
            }

            return $"{callerFileName}::{memberName}::{sourceLineNumber}";
        }
    }
}
