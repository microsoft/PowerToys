using NLog;
using Wox.Infrastructure.Exception;

namespace Wox.Infrastructure.Logger
{
    public class Log
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public static void Error(System.Exception e)
        {
#if DEBUG
            throw e;
#else
            logger.Error(e.Message + "\r\n" + e.StackTrace);
#endif
        }

        public static void Debug(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: {msg}");
            logger.Debug(msg);
        }

        public static void Info(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"INFO: {msg}");
            logger.Info(msg);
        }

        public static void Warn(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"WARN: {msg}");
            logger.Warn(msg);
        }

        public static void Fatal(System.Exception e)
        {
#if DEBUG
            throw e;
#else
            logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
#endif
        }
    }
}
