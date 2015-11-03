using System;

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

            if (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }
            ReportWindow reportWindow = new ReportWindow(exception);
            reportWindow.Show();
        }
    }
}
