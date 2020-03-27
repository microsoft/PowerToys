// <copyright file="ShellPage.xaml.cs" company="Microsoft Corp">
// Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>

namespace Microsoft.PowerToys.Settings.UI.Views
{
    using System;
    using Microsoft.PowerToys.Settings.UI.Activation;
    using Microsoft.PowerToys.Settings.UI.Helpers;
    using Microsoft.PowerToys.Settings.UI.Services;
    using Microsoft.PowerToys.Settings.UI.ViewModels;
    using Windows.UI.Xaml.Controls;

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
        /// Gets view model.
        /// </summary>
        public ShellViewModel ViewModel { get; } = new ShellViewModel();

        /// <summary>
        /// A shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static Microsoft.UI.Xaml.Controls.NavigationView ShellHandler = null;

        /// <summary>
        /// IPC callback function for run on start up.
        /// </summary>
        public static IPCMessageCallback Default_SndMSG_Callback = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellPage"/> class.
        /// Shell page constructor.
        /// </summary>
        public ShellPage()
        {
            this.InitializeComponent();

            this.DataContext = this.ViewModel;
            ShellHandler = this.navigationView;
            this.ViewModel.Initialize(this.shellFrame, this.navigationView, this.KeyboardAccelerators);
            this.shellFrame.Navigate(typeof(GeneralPage));
        }

        /// <summary>
        /// Run on start up callback function elevated initialization.
        /// </summary>
        /// <param name="implmentation">delegate function implementation.</param>
        public void SetDefaultSndMessageCallback(IPCMessageCallback implmentation)
        {
            Default_SndMSG_Callback = implmentation;
        }
    }
}
