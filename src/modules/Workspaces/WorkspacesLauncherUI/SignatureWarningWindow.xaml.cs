// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;
using ManagedCommon;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI
{
    public partial class SignatureWarningWindow : Window
    {
        private readonly SignatureWarningRequest _request;
        private bool _readyForChoice;
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
            if (!_dismissedByOwner)
            {
                _readyForChoice = true;
                RunAnywayButton.IsEnabled = true;
            }
        }

        internal void DismissWithoutResponse()
        {
            _dismissedByOwner = true;
            _readyForChoice = false;
            RunAnywayButton.IsEnabled = false;

            // Close modal children before destroying their owner so their dispatcher loops can exit.
            foreach (Window ownedWindow in OwnedWindows)
            {
                ownedWindow.Close();
            }

            Close();
        }

        internal void ShowCopyDetailsError()
        {
            if (!_dismissedByOwner)
            {
                MessageBox.Show(this, _request.CopyDetailsErrorText, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RunAnyway_Click(object sender, RoutedEventArgs e)
        {
            if (_readyForChoice && !_dismissedByOwner)
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
            catch (ExternalException exception)
            {
                Logger.LogError("Unable to copy Workspaces elevation warning details", exception);
                ShowCopyDetailsError();
            }
        }
    }
}
