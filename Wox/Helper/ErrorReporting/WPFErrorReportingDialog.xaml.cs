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
using System.Windows.Shapes;

namespace Wox.Helper.ErrorReporting
{
    /// <summary>
    /// Interaction logic for WPFErrorReportingDialog.xaml
    /// </summary>
    public partial class WPFErrorReportingDialog : Window
    {
        private object exceptionObject;

        public WPFErrorReportingDialog(string error, string title, object exceptionObject)
        {
            InitializeComponent();

            this.tbErrorReport.Text = error;
            this.Title = title;
            this.exceptionObject = exceptionObject;
        }
    }
}
