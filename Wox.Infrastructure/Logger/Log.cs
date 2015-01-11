using System;
using System.Reflection;
using log4net;

namespace Wox.Infrastructure.Logger
{
    public class Log
    {
        private static ILog fileLogger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Error(string msg)
        {
            fileLogger.Error(msg);
        }

        public static void Error(Exception e)
        {
            fileLogger.Error(e.Message + "\r\n" + e.StackTrace);
        }

        public static void Debug(string msg)
        {
            fileLogger.Debug(msg);
        }

        public static void Info(string msg)
        {
            fileLogger.Info(msg);
        }

        public static void Warn(string msg)
        {
            fileLogger.Warn(msg);
        }

        public static void Fatal(string msg)
        {
            fileLogger.Fatal(msg);
        }
    }
}
