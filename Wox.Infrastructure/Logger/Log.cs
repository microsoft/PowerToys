using System.Diagnostics;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;
using Wox.Infrastructure.Exception;

namespace Wox.Infrastructure.Logger
{
    public static class Log
    {
        static Log()
        {
            var directoryName = "Logs";
            var path = Path.Combine(Wox.DataPath, directoryName, Wox.Version);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var configuration = new LoggingConfiguration();
            var target = new FileTarget();
            configuration.AddTarget("file", target);
            target.FileName = "${specialfolder:folder=ApplicationData}/" + Wox.Name + "/" + directoryName + "/" + Wox.Version + "/${shortdate}.log";
            var rule = new LoggingRule("*", LogLevel.Info, target);
            configuration.LoggingRules.Add(rule);
            LogManager.Configuration = configuration;
        }
        private static string CallerType()
        {
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames().NonNull();
            var callingFrame = stackFrames[2];
            var method = callingFrame.GetMethod();
            var type = $"{method.DeclaringType.NonNull().FullName}.{method.Name}";
            return type;
        }

        public static void Error(string msg)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"ERROR: {msg}");
            logger.Error(msg);
        }

        public static void Exception(System.Exception e)
        {
#if DEBUG
            throw e;
#else
            var type = CallerType();
            var logger = LogManager.GetLogger(type);

            do
            {
                logger.Error(e.Message);
                logger.Error($"\n{e.StackTrace}");
                e = e.InnerException;
            } while (e != null);
#endif
        }

        public static void Debug(string msg)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"DEBUG: {msg}");
            logger.Debug(msg);
        }

        public static void Info(string msg)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"INFO: {msg}");
            logger.Info(msg);
        }

        public static void Warn(string msg)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"WARN: {msg}");
            logger.Warn(msg);
        }

        public static void Fatal(System.Exception e)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
#if DEBUG
            throw e;
#else
            logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
#endif
        }
    }
}
