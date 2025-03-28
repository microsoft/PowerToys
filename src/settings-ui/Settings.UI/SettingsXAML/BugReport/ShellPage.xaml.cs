// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Microsoft.PowerToys.Settings.UI.BugReport
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        private Uri NewIssueLink => BugReportViewModel.NewIssueLink;

        public ShellPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void BugReportToolClicked(object sender, RoutedEventArgs e)
        {
            BugReportViewModel viewModel = DataContext as BugReportViewModel;
            viewModel.LaunchBugReportTool();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            BugReportViewModel viewModel = DataContext as BugReportViewModel;
            viewModel.Close();
        }
    }
}
