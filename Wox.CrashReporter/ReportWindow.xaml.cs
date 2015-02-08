using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Exceptionless;
using Wox.Core;
using Wox.Core.Exception;
using Wox.Core.i18n;
using Wox.Core.UI;
using Wox.Core.Updater;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

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
            string reproduceSteps =
                new TextRange(tbReproduceSteps.Document.ContentStart, tbReproduceSteps.Document.ContentEnd).Text;
            exception.ToExceptionless()
                .SetUserDescription(reproduceSteps)
                .Submit();
            ExceptionlessClient.Current.ProcessQueue();
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
