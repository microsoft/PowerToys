// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Wox.Plugin.Logger
{
    public static class Log
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IDirectory Directory = FileSystem.Directory;

        public const string DirectoryName = "Logs";

        public static string CurrentLogDirectory { get; }

        static Log()
        {
            CurrentLogDirectory = Path.Combine(Constant.DataDirectory, DirectoryName, Constant.Version);
            if (!Directory.Exists(CurrentLogDirectory))
            {
                Directory.CreateDirectory(CurrentLogDirectory);
            }

            var configuration = new LoggingConfiguration();
            var target = new FileTarget();
            target.Layout = NLog.Layouts.Layout.FromString("[${longdate}] [${level:uppercase=true}]${message}\n");
            configuration.AddTarget("file", target);

            // Adding CurrentCulture since this is user facing
            target.FileName = CurrentLogDirectory.Replace(@"\", "/", StringComparison.CurrentCulture) + "/${shortdate}.txt";
#if DEBUG
            var rule = new LoggingRule("*", LogLevel.Debug, target);
#else
            var rule = new LoggingRule("*", LogLevel.Info, target);
#endif
            configuration.LoggingRules.Add(rule);
            target.Dispose();
            LogManager.Configuration = configuration;
        }

        private static void LogInternalException(string message, System.Exception e, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            var logger = GetLogger(fullClassName.FullName, methodName);
            var formattedOutput = new StringBuilder();

            formattedOutput.AppendLine("-------------------------- Begin exception --------------------------");
            formattedOutput.AppendLine(CultureInfo.InvariantCulture, $"Message: {message}");

            do
            {
                formattedOutput.Append(
                    "\n" +
                    $"Exception full name  : {e.GetType().FullName}\n" +
                    $"Exception message    : {e.Message}\n" +
                    $"Exception stack trace:\n{e.StackTrace}\n" +
                    $"Exception source     : {e.Source}\n" +
                    $"Exception target site: {e.TargetSite}\n" +
                    $"Exception HResult    : {e.HResult}\n");

                e = e.InnerException;
            }
            while (e != null);

            formattedOutput.AppendLine("-------------------------- End exception --------------------------");
            LogInternal(LogLevel.Error, formattedOutput.ToString(), logger, sourceFilePath, sourceLineNumber);
        }

        public static void Info(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            ArgumentNullException.ThrowIfNull(fullClassName);

            LogInternal(LogLevel.Info, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Debug(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            ArgumentNullException.ThrowIfNull(fullClassName);

            LogInternal(LogLevel.Debug, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Warn(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            ArgumentNullException.ThrowIfNull(fullClassName);

            LogInternal(LogLevel.Warn, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Error(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            ArgumentNullException.ThrowIfNull(fullClassName);

            LogInternal(LogLevel.Error, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Exception(string message, System.Exception ex, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            ArgumentNullException.ThrowIfNull(fullClassName);

            LogInternalException(message, ex, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        private static void LogInternal(LogLevel level, string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            var logger = GetLogger(fullClassName.FullName, methodName);

            LogInternal(level, message, logger, sourceFilePath, sourceLineNumber);
        }

        private static void LogInternal(LogLevel level, string message, NLog.Logger logger, [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            var msg = $" [{sourceFilePath}::{sourceLineNumber}]" +
                      $"\n{message}";

            logger.Log(level, msg);
        }

        private static NLog.Logger GetLogger(string fullClassName, string methodName)
        {
            var classNameWithMethod = $"{fullClassName}.{methodName}";

            return LogManager.GetLogger(classNameWithMethod);
        }
    }
}
