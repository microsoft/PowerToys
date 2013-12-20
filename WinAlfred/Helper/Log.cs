using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;

namespace WinAlfred.Helper
{
    public class Log
    {
        private static ILog fileLogger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Error(string msg)
        {
            fileLogger.Error(msg);
        }
    }
}
