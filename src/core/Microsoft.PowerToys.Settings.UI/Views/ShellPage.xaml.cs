// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Root page.
    /// </summary>
    public sealed partial class ShellPage : UserControl
    {
        /// <summary>
        /// Declaration for the ipc callback function.
        /// </summary>
        /// <param name="msg">message.</param>
        public delegate void IPCMessageCallback(string msg);

        /// <summary>
        /// Gets or sets a shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static ShellPage ShellHandler { get; set; }

        /// <summary>
        /// Gets or sets iPC default callback function.
        /// </summary>
        public static IPCMessageCallback DefaultSndMSGCallback { get; set; }

        /// <summary>
        /// Gets or sets iPC callback function for restart as admin.
        /// </summary>
        public static IPCMessageCallback SndRestartAsAdminMsgCallback { get; set; }

        /// <summary>
        /// Gets view model.
        /// </summary>
        public ShellViewModel ViewModel { get; } = new ShellViewModel();

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellPage"/> class.
        /// Shell page constructor.
        /// </summary>
        public ShellPage()
        {
            InitializeComponent();

            DataContext = ViewModel;
            ShellHandler = this;
            ViewModel.Initialize(shellFrame, navigationView, KeyboardAccelerators);
            shellFrame.Navigate(typeof(GeneralPage));
        }

        /// <summary>
        /// Set Default IPC Message callback function.
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public void SetDefaultSndMessageCallback(IPCMessageCallback implementation)
        {
            DefaultSndMSGCallback = implementation;
        }

        /// <summary>
        /// Set restart as admin IPC callback function.
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public void SetRestartAdminSndMessageCallback(IPCMessageCallback implementation)
        {
            SndRestartAsAdminMsgCallback = implementation;
        }

        public void SetElevationStatus(bool isElevated)
        {
            IsElevated = isElevated;
        }

        public void SetIsUserAnAdmin(bool isAdmin)
        {
            IsUserAnAdmin = isAdmin;
        }

        public void Refresh()
        {
            shellFrame.Navigate(typeof(GeneralPage));
        }
    }
}
