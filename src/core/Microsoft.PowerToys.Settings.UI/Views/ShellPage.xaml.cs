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
        /// Delcaration for the ipc callback function.
        /// </summary>
        /// <param name="msg">message.</param>
        public delegate void IPCMessageCallback(string msg);

        /// <summary>
        /// Gets or sets a shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static ShellPage ShellHandler { get; set; }

        /// <summary>
        /// Gets or sets iPC callback function for run on start up.
        /// </summary>
        public static IPCMessageCallback DefaultSndMSGCallback { get; set; }

        /// <summary>
        /// Gets view model.
        /// </summary>
        public ShellViewModel ViewModel { get; } = new ShellViewModel();

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellPage"/> class.
        /// Shell page constructor.
        /// </summary>
        public ShellPage()
        {
            this.InitializeComponent();

            this.DataContext = this.ViewModel;
            ShellHandler = this;
            this.ViewModel.Initialize(this.shellFrame, this.navigationView, this.KeyboardAccelerators);
            this.shellFrame.Navigate(typeof(GeneralPage));
        }

        /// <summary>
        /// Run on start up callback function elevated initialization.
        /// </summary>
        /// <param name="implmentation">delegate function implementation.</param>
        public void SetDefaultSndMessageCallback(IPCMessageCallback implmentation)
        {
            DefaultSndMSGCallback = implmentation;
        }
    }
}
