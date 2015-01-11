using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wox.Core;
using Wox.Core.Exception;
using Wox.Core.UI;
using Wox.Core.UserSettings;
using Wox.Core.Version;
using Wox.Infrastructure.Http;

namespace Wox.CrashReporter
{
    internal partial class ReportWindow : IUIResource
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
            tbVersion.Text = VersionManager.Instance.CurrentVersion.ToString();
            tbDatetime.Text = DateTime.Now.ToString();
            tbStackTrace.AppendText(exception.StackTrace);
            tbSource.Text = exception.Source;
            tbType.Text = exception.GetType().ToString();
        }

        public ResourceDictionary GetResourceDictionary()
        {
            return null;
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            tbSendReport.Content = "Sending";
            btnSend.IsEnabled = false;
            string error = string.Format("{{\"data\":{0}}}", ExceptionFormatter.FormatExcpetion(exception));
            string response = HttpRequest.Post(APIServer.ErrorReportURL, error, HttpProxy.Instance);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
