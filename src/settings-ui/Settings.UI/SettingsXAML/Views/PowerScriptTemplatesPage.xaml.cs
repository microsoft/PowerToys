// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptTemplatesPage : NavigablePage
{
    private PowerScriptsViewModel ViewModel { get; set; }

    public PowerScriptTemplatesPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel ??
            throw new ArgumentException("Template navigation requires the PowerScripts view model.", nameof(e));
        TemplateBreadcrumb.ItemsSource = new[] { "PowerScripts", ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_CreateScriptTitle") };
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await LoadTemplatesAsync();

    private void BackButton_Click(object sender, RoutedEventArgs e) => NavigationService.GoBack();

    private void BackKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        args.Handled = NavigationService.GoBack();

    private void TemplateBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0)
        {
            NavigationService.GoBack();
        }
    }

    private async Task LoadTemplatesAsync()
    {
        try
        {
            TemplateCards.Children.Clear();
            foreach (var template in await ViewModel.GetTemplatesAsync())
            {
                var create = new Button { Content = ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateCustomize") };
                AutomationProperties.SetAutomationId(create, "PowerScriptsCreateTemplate_" + template.Id);
                AutomationProperties.SetName(create, template.Name);
                create.Click += async (_, _) =>
                {
                    create.IsEnabled = false;
                    try
                    {
                        var entry = await ShowTemplateSetupAsync(template);
                        if (entry is null)
                        {
                            return;
                        }

                        TemplateStatus.Severity = InfoBarSeverity.Success;
                        TemplateStatus.Message = ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateCreated") + Environment.NewLine + Path.GetDirectoryName(entry);
                        TemplateStatus.IsOpen = true;
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{entry}\"") { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Unable to create PowerScript template.", ex);
                        TemplateStatus.Severity = InfoBarSeverity.Error;
                        TemplateStatus.Message = ex.Message;
                        TemplateStatus.IsOpen = true;
                    }
                    finally
                    {
                        create.IsEnabled = true;
                    }
                };
                TemplateCards.Children.Add(new SettingsCard { Header = template.Name, Description = template.Description, Content = create });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to load PowerScript templates.", ex);
            TemplateStatus.Severity = InfoBarSeverity.Error;
            TemplateStatus.Message = ex.Message;
            TemplateStatus.IsOpen = true;
        }
    }

    private async Task<string> ShowTemplateSetupAsync(PowerScriptsViewModel.PowerScriptTemplateItem template)
    {
        var resources = ResourceLoaderInstance.ResourceLoader;
        var projectName = new TextBox
        {
            Header = resources.GetString("PowerScripts_TemplateProjectName"),
            Text = template.Name,
            MaxLength = 80,
        };
        AutomationProperties.SetAutomationId(projectName, "PowerScriptsTemplateProjectName");
        var description = new TextBox
        {
            Header = resources.GetString("PowerScripts_TemplateDescription"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = 500,
            Text = template.Description,
        };
        AutomationProperties.SetAutomationId(description, "PowerScriptsTemplateDescription");
        var extensions = new TextBox
        {
            Header = resources.GetString("PowerScripts_TemplateFileExtensions"),
            PlaceholderText = ".txt, .md, .png",
            MaxLength = 1024,
        };
        AutomationProperties.SetAutomationId(extensions, "PowerScriptsTemplateFileExtensions");
        var error = new InfoBar { IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Error };
        AutomationProperties.SetAutomationId(error, "PowerScriptsTemplateSetupError");
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(projectName);
        content.Children.Add(new TextBlock
        {
            Text = resources.GetString("PowerScripts_TemplateProjectNameHelp"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });
        content.Children.Add(description);
        if (template.AcceptsFiles)
        {
            content.Children.Add(extensions);
            content.Children.Add(new TextBlock
            {
                Text = resources.GetString("PowerScripts_TemplateFileExtensionsHelp"),
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            });
        }

        content.Children.Add(new TextBlock { Text = resources.GetString("PowerScripts_TemplateSetupHelp"), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(error);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = resources.GetString("PowerScripts_TemplateSetupTitle").Replace("{0}", template.Name, StringComparison.Ordinal),
            Content = content,
            PrimaryButtonText = resources.GetString("PowerScripts_TemplateCreate"),
            CloseButtonText = resources.GetString("PowerScripts_TemplateCancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        AutomationProperties.SetAutomationId(dialog, "PowerScriptsTemplateSetup");
        var creating = false;
        string entry = null;
        projectName.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(projectName.Text);
        dialog.Closing += (_, args) => args.Cancel = creating;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = true;
            creating = true;
            dialog.IsEnabled = false;
            dialog.IsPrimaryButtonEnabled = false;
            error.IsOpen = false;
            try
            {
                entry = await ViewModel.CreateTemplateAsync(template.Id, projectName.Text, description.Text, template.AcceptsFiles ? extensions.Text : null);
                args.Cancel = false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to create configured PowerScript template.", ex);
                error.Message = ex.Message;
                error.IsOpen = true;
            }
            finally
            {
                creating = false;
                dialog.IsEnabled = true;
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(projectName.Text);
                deferral.Complete();
            }
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? entry : null;
    }
}
