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
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        private static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion;

        private static readonly string Error = "Error";
        private static readonly string Warning = "Warning";
        private static readonly string Info = "Info";
        private static readonly string Debug = "Debug";
        private static readonly string TraceFlag = "Trace";

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogError(string message)
        {
            Log(message, Error);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogError(string message, Exception ex)
        {
            if (ex == null)
            {
                Log(message, Error);
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

                Log(exMessage, Error);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogWarning(string message)
        {
            Log(message, Warning);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogInfo(string message)
        {
            Log(message, Info);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogDebug(string message)
        {
            Log(message, Debug);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogTrace()
        {
            Log(string.Empty, TraceFlag);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetCallerInfo()
        {
            StackTrace stackTrace = new();

            var callerMethod = GetCallerMethod(stackTrace);

            return $"{callerMethod?.DeclaringType?.Name}::{callerMethod.Name}";
        }

        private static MethodBase GetCallerMethod(StackTrace stackTrace)
        {
            const int topFrame = 3;

            var topMethod = stackTrace.GetFrame(topFrame)?.GetMethod();

            try
            {
                if (topMethod?.Name == nameof(IAsyncStateMachine.MoveNext) && typeof(IAsyncStateMachine).IsAssignableFrom(topMethod?.DeclaringType))
                {
                    // Async method; return actual method as determined by heuristic:
                    // "Nearest method on stack to async state-machine's MoveNext() in same namespace but in a different type".
                    // There are tighter ways of determining the actual method, but this is good enough and probably faster.
                    for (int deepFrame = topFrame + 1; deepFrame < stackTrace.FrameCount; deepFrame++)
                    {
                        var deepMethod = stackTrace.GetFrame(deepFrame)?.GetMethod();

                        if (deepMethod?.DeclaringType != topMethod?.DeclaringType && deepMethod?.DeclaringType?.Namespace == topMethod?.DeclaringType?.Namespace)
                        {
                            return deepMethod;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore exceptions in Release. The code above won't throw, but if it does, we don't want to crash the app.
#if DEBUG
                throw;
#endif
            }

            return topMethod;
        }
    }
}
