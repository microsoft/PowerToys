// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Wox.Plugin.Logger
{
    public static class Log
    {
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
            configuration.AddTarget("file", target);

            // Adding CurrentCulture since this is user facing
            target.FileName = CurrentLogDirectory.Replace(@"\", "/", StringComparison.CurrentCulture) + "/${shortdate}.txt";
#if DEBUG
            var rule = new LoggingRule("*", LogLevel.Debug, target);
#else
            var rule = new LoggingRule("*", LogLevel.Info, target);
#endif
            configuration.LoggingRules.Add(rule);
            LogManager.Configuration = configuration;
            target.Dispose();
        }

        private static void LogInternalException(string message, System.Exception e, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            var logger = GetLogger(fullClassName.FullName, methodName);

            LogInternal(LogLevel.Error, message, fullClassName, logger, methodName, sourceFilePath, sourceLineNumber);

            logger.Error("-------------------------- Begin exception --------------------------");
            logger.Error($"\n\tMessage:\n\t {message}");

            do
            {
                logger.Error(
                    $"\n\tException full name:\n\t <{e.GetType().FullName}>" +
                    $"\n\tException message:\n\t <{e.Message}>" +
                    $"\n\tException stack trace:\n\t <{e.StackTrace}>" +
                    $"\n\tException source:\n\t <{e.Source}>" +
                    $"\n\tException target site:\n\t <{e.TargetSite}>" +
                    $"\n\tException HResult:\n\t <{e.HResult}>");

                e = e.InnerException;
            }
            while (e != null);

            logger.Error("-------------------------- End exception --------------------------");
        }

        public static void Info(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (fullClassName == null)
            {
                throw new ArgumentNullException(nameof(fullClassName));
            }

            LogInternal(LogLevel.Info, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Debug(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (fullClassName == null)
            {
                throw new ArgumentNullException(nameof(fullClassName));
            }

            LogInternal(LogLevel.Debug, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Warn(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (fullClassName == null)
            {
                throw new ArgumentNullException(nameof(fullClassName));
            }

            LogInternal(LogLevel.Warn, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Error(string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (fullClassName == null)
            {
                throw new ArgumentNullException(nameof(fullClassName));
            }

            LogInternal(LogLevel.Error, message, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        public static void Exception(string message, System.Exception ex, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (fullClassName == null)
            {
                throw new ArgumentNullException(nameof(fullClassName));
            }

            LogInternalException(message, ex, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        private static void LogInternal(LogLevel level, string message, Type fullClassName, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            var logger = GetLogger(fullClassName.FullName, methodName);

            LogInternal(level, message, fullClassName, logger, methodName, sourceFilePath, sourceLineNumber);
        }

        private static void LogInternal(LogLevel level, string message, Type fullClassName, NLog.Logger logger, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            // System.Diagnostics.Debug.WriteLine($" {level.Name} | {message}");
            var msg = $"\n\tMessage: {message}" +
                    $"\n\tArea: {fullClassName}.{methodName}" +
                    $"\n\tSource Path: {sourceFilePath}::{sourceLineNumber}\n";

            logger.Log(level, msg);
        }

        private static NLog.Logger GetLogger(string fullClassName, string methodName)
        {
            var classNameWithMethod = $"{fullClassName}.{methodName}";

            return LogManager.GetLogger(classNameWithMethod);
        }
    }
}
