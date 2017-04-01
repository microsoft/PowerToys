using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Wox.Infrastructure.Logger
{
    public static class Log
    {
        public const string DirectoryName = "Logs";

        static Log()
        {
            var path = Path.Combine(Constant.DataDirectory, DirectoryName, Constant.Version);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var configuration = new LoggingConfiguration();
            var target = new FileTarget();
            configuration.AddTarget("file", target);
            target.FileName = "${specialfolder:folder=ApplicationData}/" + Constant.Wox + "/" + DirectoryName + "/" +
                              Constant.Version + "/${shortdate}.txt";
#if DEBUG
            var rule = new LoggingRule("*", LogLevel.Debug, target);
#else
            var rule = new LoggingRule("*", LogLevel.Info, target);
#endif
            configuration.LoggingRules.Add(rule);
            LogManager.Configuration = configuration;
        }

        private static void LogFaultyFormat(string message)
        {
            var logger = LogManager.GetLogger("FaultyLogger");
            message = $"Wrong logger message format <{message}>";
            System.Diagnostics.Debug.WriteLine($"FATAL|{message}");
            logger.Fatal(message);
        }

        private static bool FormatValid(string message)
        {
            var parts = message.Split('|');
            var valid = parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]);
            return valid;
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Error(string message)
        {
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                var logger = LogManager.GetLogger(prefix);

                System.Diagnostics.Debug.WriteLine($"ERROR|{message}");
                logger.Error(unprefixed);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Exception(string message, System.Exception e)
        {
#if DEBUG
            throw e;
#else
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                var logger = LogManager.GetLogger(prefix);

                System.Diagnostics.Debug.WriteLine($"ERROR|{message}");

                logger.Error("-------------------------- Begin exception --------------------------");
                logger.Error(unprefixed);

                do
                {
                    logger.Error($"Exception fulle name:\n <{e.GetType().FullName}>");
                    logger.Error($"Exception message:\n <{e.Message}>");
                    logger.Error($"Exception stack trace:\n <{e.StackTrace}>");
                    logger.Error($"Exception source:\n <{e.Source}>");
                    logger.Error($"Exception target site:\n <{e.TargetSite}>");
                    logger.Error($"Exception HResult:\n <{e.HResult}>");
                    e = e.InnerException;
                } while (e != null);

                logger.Error("-------------------------- End exception --------------------------");
            }
            else
            {
                LogFaultyFormat(message);
            }
#endif
        }
        
        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Debug(string message)
        {
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                var logger = LogManager.GetLogger(prefix);

                System.Diagnostics.Debug.WriteLine($"DEBUG|{message}");
                logger.Debug(unprefixed);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Info(string message)
        {
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                var logger = LogManager.GetLogger(prefix);

                System.Diagnostics.Debug.WriteLine($"INFO|{message}");
                logger.Info(unprefixed);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Warn(string message)
        {
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                var logger = LogManager.GetLogger(prefix);

                System.Diagnostics.Debug.WriteLine($"WARN|{message}");
                logger.Warn(unprefixed);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }
    }
}