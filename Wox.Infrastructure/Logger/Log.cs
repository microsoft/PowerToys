using System;
using System.Reflection;
using NLog;

namespace Wox.Infrastructure.Logger
{
    public class Log
    {
        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public static void Error(string msg)
        {
            logger.Error(msg);
        }

        public static void Error(Exception e)
        {
            logger.Error(e.Message + "\r\n" + e.StackTrace);
        }

        public static void Debug(string msg)
        {
            logger.Debug(msg);
        }

        public static void Info(string msg)
        {
            logger.Info(msg);
        }

        public static void Warn(string msg)
        {
            logger.Warn(msg);
        }

        public static void Fatal(string msg)
        {
            logger.Fatal(msg);
        }
    }
}
