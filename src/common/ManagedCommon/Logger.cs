﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
            string basePath;
            if (isLocalLow)
            {
                basePath = Environment.GetEnvironmentVariable("userprofile") + "\\appdata\\LocalLow\\Microsoft\\PowerToys" + applicationLogPath;
            }
            else
            {
                basePath = Constants.AppDataPath() + applicationLogPath;
            }

            string versionedPath = Path.Combine(basePath, Version);

            if (!Directory.Exists(versionedPath))
            {
                Directory.CreateDirectory(versionedPath);
            }

            var logFilePath = Path.Combine(versionedPath, "Log_" + DateTime.Now.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

            Trace.Listeners.Add(new TextWriterTraceListener(logFilePath));

            Trace.AutoFlush = true;

            // Clean up old version log folders
            Task.Run(() => DeleteOldVersionLogFolders(basePath, versionedPath));
        }

        /// <summary>
        /// Deletes old version log folders, keeping only the current version's folder.
        /// </summary>
        /// <param name="basePath">The base path to the log files folder.</param>
        /// <param name="currentVersionPath">The path to the current version's log folder.</param>
        private static void DeleteOldVersionLogFolders(string basePath, string currentVersionPath)
        {
            try
            {
                if (!Directory.Exists(basePath))
                {
                    return;
                }

                var dirs = Directory.GetDirectories(basePath)
                    .Select(d => new DirectoryInfo(d))
                    .OrderBy(d => d.CreationTime)
                    .Where(d => !string.Equals(d.FullName, currentVersionPath, StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .ToList();

                foreach (var directory in dirs)
                {
                    try
                    {
                        Directory.Delete(directory.FullName, true);
                        LogInfo($"Deleted old log directory: {directory.FullName}");
                        Task.Delay(500).Wait();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to delete old log directory: {directory.FullName}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error cleaning up old log folders", ex);
            }
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
