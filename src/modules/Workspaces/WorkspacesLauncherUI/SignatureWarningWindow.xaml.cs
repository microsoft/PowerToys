// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI
{
    public partial class SignatureWarningWindow : Window
    {
        private readonly SignatureWarningRequest _request;
        private bool _readyForChoice;
        private bool _skipRequested;
        private bool _dismissedByOwner;

        public SignatureWarningWindow(SignatureWarningRequest request)
        {
            _request = request;
            InitializeComponent();
            DataContext = request;
            MaxHeight = Math.Min(MaxHeight, SystemParameters.WorkArea.Height);
            Width = Math.Min(Width, SystemParameters.WorkArea.Width);
            Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
            FlowDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Loaded += (_, _) => SkipButton.Focus();
        }

        internal void EnableRunChoice()
        {
            _readyForChoice = true;
            if (_skipRequested)
            {
                Close();
                return;
            }

            RunAnywayButton.IsEnabled = true;
        }

        internal void DismissWithoutResponse()
        {
            _dismissedByOwner = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_readyForChoice && !_dismissedByOwner)
            {
                // Remember an early Escape/X/Skip, but do not respond before warning-shown.
                _skipRequested = true;
                e.Cancel = true;
            }
        }

        private void RunAnyway_Click(object sender, RoutedEventArgs e)
        {
            if (RunAnywayButton.IsEnabled)
            {
                DialogResult = true;
            }
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_request.DetailsText);
            }
            catch (ExternalException)
            {
                MessageBox.Show(this, _request.CopyDetailsErrorText, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
