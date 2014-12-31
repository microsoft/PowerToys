using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using Wox.Core.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Helper.ErrorReporting
{
    public static class ErrorReporting
    {
        public static void UnhandledExceptionHandle(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached) return;

            string error = ExceptionFormatter.FormatExcpetion(e.ExceptionObject);
            //e.IsTerminating is always true in most times, so try to avoid use this property
            //http://stackoverflow.com/questions/10982443/what-causes-the-unhandledexceptioneventargs-isterminating-flag-to-be-true-or-fal
            Log.Error(error);
            TryShowErrorMessageBox(error, e.ExceptionObject);
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached) return;

            e.Handled = true;
            string error = ExceptionFormatter.FormatExcpetion(e.Exception);

            Log.Error(error);
            TryShowErrorMessageBox(error, e.Exception);
        }
        
        public static void ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            if (Debugger.IsAttached) return;

            string error = ExceptionFormatter.FormatExcpetion(e.Exception);

            Log.Fatal(error);
            TryShowErrorMessageBox(error, e.Exception);
        }

        public static bool TryShowErrorMessageBox(string error, object exceptionObject)
        {
            var title = "Wox - Unhandled Exception";

            try
            {
                ShowWPFDialog(error, title, exceptionObject);
                return true;
            }
            catch { }

            error = "Wox has occured an error that can't be handled. " + Environment.NewLine + Environment.NewLine + error;

            try
            {
                ShowWPFMessageBox(error, title);
                return true;
            }
            catch { }

            try
            {
                ShowWindowsFormsMessageBox(error, title);
                return true;
            }
            catch { }

            return true;
        }

        private static void ShowWPFDialog(string error, string title, object exceptionObject)
        {
            var dialog = new WPFErrorReportingDialog(error, title, exceptionObject);
            dialog.ShowDialog();
        }
       
        private static void ShowWPFMessageBox(string error, string title)
        {
            System.Windows.MessageBox.Show(error, title, MessageBoxButton.OK, MessageBoxImage.Error,
                        MessageBoxResult.OK, System.Windows.MessageBoxOptions.None);
        }
        
        private static void ShowWindowsFormsMessageBox(string error, string title)
        {
            System.Windows.Forms.MessageBox.Show(error, title, MessageBoxButtons.OK,
                        MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
        }
    }
}
