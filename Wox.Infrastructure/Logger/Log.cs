using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using Wox.Infrastructure.Exception;

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

        public static string CallerType()
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Error(System.Exception e, string msg)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"ERROR: {msg}");
            logger.Error("-------------------------- Begin exception --------------------------");
            logger.Error(msg);
            do
            {
                logger.Error($"Exception message:\n <{e.Message}>");
                logger.Error($"Exception stack trace:\n<{e.StackTrace}>");
                e = e.InnerException;
            } while (e != null);
            logger.Error("-------------------------- End exception --------------------------");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Exception(System.Exception e)
        {
            var type = CallerType();
            var logger = LogManager.GetLogger(type);

            do
            {
                logger.Error($"Exception message:\n <{e.Message}>");
                logger.Error($"Exception stack trace:\n<{e.StackTrace}>");
                e = e.InnerException;
            } while (e != null);
        }

        public static void Debug(string type, string msg)
        {
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"DEBUG: {msg}");
            logger.Debug(msg);
        }

        public static void Debug(string msg)
        {
            var type = CallerType();
            Debug(type, msg);
        }

        public static void Info(string type, string msg)
        {
            var logger = LogManager.GetLogger(type);
            System.Diagnostics.Debug.WriteLine($"INFO: {msg}");
            logger.Info(msg);
        }

        public static void Info(string msg)
        {
            var type = CallerType();
            Info(type, msg);
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
#if DEBUG
            throw e;
#else
            var type = CallerType();
            var logger = LogManager.GetLogger(type);
            logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
#endif
        }
    }
}