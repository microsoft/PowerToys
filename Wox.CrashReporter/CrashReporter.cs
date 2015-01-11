using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.CrashReporter
{
    public class CrashReporter
    {
        private Exception exception;

        public CrashReporter(Exception e)
        {
            exception = e;
        }

        public void Show()
        {
            if (exception == null) return;

            ReportWindow reportWindow = new ReportWindow(exception);
            reportWindow.Show();
        }
    }
}
