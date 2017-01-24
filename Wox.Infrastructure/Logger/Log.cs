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

        public static void Error(string message)
        {
            var parts = message.Split('|');
            if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var logger = LogManager.GetLogger(parts[1]);
                System.Diagnostics.Debug.WriteLine($"ERROR|{message}");
                logger.Error(parts[2]);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Exception(string message, System.Exception e)
        {
#if DEBUG
            throw e;
#else
            var parts = message.Split('|');
            if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var logger = LogManager.GetLogger(parts[1]);

                System.Diagnostics.Debug.WriteLine($"ERROR|{message}");

                logger.Error("-------------------------- Begin exception --------------------------");
                logger.Error(parts[2]);

                do
                {
                    logger.Error($"Exception message:\n <{e.Message}>");
                    logger.Error($"Exception stack trace:\n<{e.StackTrace}>");
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

        public static void Debug(string message)
        {
            var parts = message.Split('|');
            if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var logger = LogManager.GetLogger(parts[1]);
                System.Diagnostics.Debug.WriteLine($"DEBUG|{message}");
                logger.Debug(parts[2]);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        public static void Info(string message)
        {
            var parts = message.Split('|');
            if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var logger = LogManager.GetLogger(parts[1]);
                System.Diagnostics.Debug.WriteLine($"INFO|{message}");
                logger.Info(parts[2]);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        public static void Warn(string message)
        {
            var parts = message.Split('|');
            if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var logger = LogManager.GetLogger(parts[1]);
                System.Diagnostics.Debug.WriteLine($"WARN|{message}");
                logger.Warn(parts[2]);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }
    }
}