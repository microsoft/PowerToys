using System;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using Exceptionless;
using Wox.Core.i18n;
using Wox.Core.Updater;

namespace Wox.CrashReporter
{
    internal partial class ReportWindow
    {
        private Exception exception;

        public ReportWindow(Exception exception)
        {
            this.exception = exception;
            InitializeComponent();
            SetException(exception);
        }

        private void SetException(Exception exception)
        {
            tbSummary.AppendText(exception.Message);
            tbVersion.Text = UpdaterManager.Instance.CurrentVersion.ToString();
            tbDatetime.Text = DateTime.Now.ToString();
            tbStackTrace.AppendText(exception.StackTrace);
            tbSource.Text = exception.Source;
            tbType.Text = exception.GetType().ToString();
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string sendingMsg = InternationalizationManager.Instance.GetTranslation("reportWindow_sending");
            tbSendReport.Content = sendingMsg;
            btnSend.IsEnabled = false;
            SendReport();
        }

        private void SendReport()
        {
            Hide();
            ThreadPool.QueueUserWorkItem(o =>
            {
                string reproduceSteps = new TextRange(tbReproduceSteps.Document.ContentStart, tbReproduceSteps.Document.ContentEnd).Text;
                exception.ToExceptionless()
                    .SetUserDescription(reproduceSteps)
                    .Submit();
                ExceptionlessClient.Current.ProcessQueue();
                Dispatcher.Invoke(() =>
                {
                    Close();
                });
            });
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
