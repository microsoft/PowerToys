// <copyright file="ShellPage.xaml.cs" company="Microsoft Corp">
// Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>

namespace Microsoft.PowerToys.Settings.UI.Views
{
    using System;
    using System.Collections.Generic;
    using Microsoft.PowerToys.Settings.UI.Activation;
    using Microsoft.PowerToys.Settings.UI.Helpers;
    using Microsoft.PowerToys.Settings.UI.Lib;
    using Microsoft.PowerToys.Settings.UI.Services;
    using Microsoft.PowerToys.Settings.UI.ViewModels;
    using Windows.UI.Xaml;
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
        public static ShellPage ShellHandler = null;

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
            Default_SndMSG_Callback = implmentation;
        }

        public void HideFeatureDetails()
        {
            this.Feature_Details_Title.Visibility = Visibility.Collapsed;
            this.Feature_Details.Visibility = Visibility.Collapsed;
        }

        public void ShowFeatureDetails()
        {
            this.Feature_Details_Title.Visibility = Visibility.Visible;
            this.Feature_Details.Visibility = Visibility.Visible;
        }

        public void SetFeatureDetails(string moduleOverviewLink,string reportBugLink)
        {
            this.Module_Overview_LinkButton.NavigateUri = new Uri(moduleOverviewLink);
            this.Module_Feedback_LinkButton.NavigateUri = new Uri(reportBugLink);
        }

        public void HideContributorsList()
        {
            this.Contributors_List_Title.Visibility = Visibility.Collapsed;
            this.Contributors_List.Visibility = Visibility.Collapsed;
        }

        public void ShowContributorsList()
        {
            this.Contributors_List_Title.Visibility = Visibility.Visible;
            this.Contributors_List.Visibility = Visibility.Visible;
        }

        public void PopulateContributorsList(List<Contributor> contributors)
        {
            this.Contributors_List.Items.Clear();

            foreach (Contributor contributor in contributors)
            {
                HyperlinkButton link = new HyperlinkButton();
                link.Content = contributor.Name;
                link.NavigateUri = new Uri(contributor.Link);
                this.Contributors_List.Items.Add(link);
            }
        }
    }
}
