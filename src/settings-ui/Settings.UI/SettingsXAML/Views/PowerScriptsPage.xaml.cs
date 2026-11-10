// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.UI.ViewModels;
using ResourceLoaderInstance = PowerScripts.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptsPage : NavigablePage, IRefreshablePage
{
    private PowerScriptsViewModel ViewModel { get; }

    public PowerScriptsPage()
    {
        ViewModel = new PowerScriptsViewModel(
            SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default),
            ShellPage.SendDefaultIPCMessage);
        InitializeComponent();
        Loaded += (_, _) => RefreshEnabledState();
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public async void RefreshEnabledState()
    {
        try
        {
            ViewModel.RefreshConfiguration();
            await ViewModel.RefreshConsumersAsync();
            await ViewModel.RefreshMxcPlatformSupportAsync();
            if (ViewModel.IsManagedLinuxMode)
            {
                await ViewModel.RefreshLinuxContainerAsync();
            }

            EditorError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void PowerScriptsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && ViewModel.CanChangePowerScriptsEnabled && toggle.IsOn != ViewModel.IsEnabled)
        {
            try
            {
                ViewModel.IsEnabled = toggle.IsOn;
                await ViewModel.RefreshConsumersAsync();
                EditorError.IsOpen = false;
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "PowerToys.PowerScriptsEditor.exe");
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false })
                ?? throw new InvalidOperationException("Unable to start the PowerScripts editor.");
            EditorError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void OpenScriptsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ViewModel.ScriptsFolder);
            using var process = Process.Start(new ProcessStartInfo(ViewModel.ScriptsFolder) { UseShellExecute = true })
                ?? throw new InvalidOperationException("Unable to open the scripts folder.");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void PythonEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedIndex: 2 } && ViewModel.IsMxcEnabled)
        {
            await ViewModel.RefreshLinuxContainerAsync();
        }
    }

    private async void CheckLinuxContainer_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshLinuxContainerAsync();

    private async void RecoverLinuxContainer_Click(object sender, RoutedEventArgs e)
    {
        var resources = ResourceLoaderInstance.ResourceLoader;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = resources.GetString("PowerScripts_LinuxRecoverTitle"),
            Content = resources.GetString("PowerScripts_LinuxRecoverHelp"),
            PrimaryButtonText = resources.GetString("PowerScripts_LinuxRecoverConfirm"),
            CloseButtonText = resources.GetString("PowerScripts_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RefreshLinuxContainerAsync(recover: true);
        }
    }

    private async void SetupLinuxContainer_Click(object sender, RoutedEventArgs e)
    {
        var resources = ResourceLoaderInstance.ResourceLoader;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = resources.GetString("PowerScripts_LinuxSetupTitle"),
            Content = resources.GetString("PowerScripts_LinuxSetupHelp")
                .Replace("{0}", ViewModel.LinuxContainerImage, StringComparison.Ordinal),
            PrimaryButtonText = resources.GetString("PowerScripts_LinuxSetupConfirm"),
            CloseButtonText = resources.GetString("PowerScripts_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RefreshLinuxContainerAsync(prepare: true);
        }
    }

    private async void BrowseScriptsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await PickSingleFolderDialog();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ViewModel.SetScriptsFolder(folder);
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ResetScriptsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.ResetScriptsFolder();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void BrowsePythonInterpreterButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var file = PickFileDialog(
                "Python Executable\0python.exe;python3.exe;py.exe\0All Executables\0*.exe\0",
                "Select Python interpreter");
            if (!string.IsNullOrWhiteSpace(file))
            {
                ViewModel.SetPythonInterpreterPath(file);
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void MxcEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.IsOn == ViewModel.IsMxcEnabled)
        {
            return;
        }

        try
        {
            var isConfirmed = toggle.IsOn ||
                              await ShowMxcWeakeningWarningAsync(
                                  ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_MxcDisableWarning"));
            if (!ViewModel.TrySetMxcEnabled(toggle.IsOn, isConfirmed))
            {
                toggle.IsOn = ViewModel.IsMxcEnabled;
            }
            else if (ViewModel.IsManagedLinuxMode)
            {
                await ViewModel.RefreshLinuxContainerAsync();
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void GlobalMxcPolicyToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch { Tag: string policy } toggle ||
            toggle.IsOn == ViewModel.IsGlobalMxcPolicyEnabled(policy))
        {
            return;
        }

        try
        {
            var isConfirmed = toggle.IsOn ||
                              await ShowMxcWeakeningWarningAsync(
                                  ResourceLoaderInstance.ResourceLoader
                                      .GetString("PowerScripts_MxcDisablePolicyWarning")
                                      .Replace(
                                          "{0}",
                                          PowerScriptListItem.GetMxcPolicyDisplayName(policy),
                                          StringComparison.Ordinal));
            if (!ViewModel.TrySetGlobalMxcPolicy(policy, toggle.IsOn, isConfirmed))
            {
                toggle.IsOn = ViewModel.IsGlobalMxcPolicyEnabled(policy);
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task<bool> ShowMxcWeakeningWarningAsync(string changeDescription)
    {
        var resourceLoader = ResourceLoaderInstance.ResourceLoader;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = resourceLoader.GetString("PowerScripts_MxcRiskDialogTitle"),
            Content = resourceLoader.GetString("PowerScripts_MxcRiskDialogContent")
                .Replace("{0}", changeDescription, StringComparison.Ordinal),
            PrimaryButtonText = resourceLoader.GetString("PowerScripts_MxcRiskDialogAccept"),
            CloseButtonText = resourceLoader.GetString("PowerScripts_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    // Uses the Win32 OpenFileName dialog as FileOpenPicker doesn't work when Settings runs elevated.
    private static string PickFileDialog(string filter, string title)
    {
        OpenFileName openFileName = new OpenFileName();
        openFileName.StructSize = Marshal.SizeOf(openFileName);
        openFileName.Filter = filter;

        // Make buffer double MAX_PATH since it can use 2 chars per char.
        openFileName.File = new string(new char[260 * 2]);
        openFileName.MaxFile = openFileName.File.Length;
        openFileName.FileTitle = new string(new char[260 * 2]);
        openFileName.MaxFileTitle = openFileName.FileTitle.Length;
        openFileName.Title = title;
        openFileName.DefExt = null;
        openFileName.Flags = (int)OpenFileNameFlags.OFN_NOCHANGEDIR;
        openFileName.Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());

        return NativeMethods.GetOpenFileName(openFileName) ? openFileName.File : null;
    }

    private async Task<string> PickSingleFolderDialog()
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    private void ShowError(Exception exception)
    {
        Logger.LogError("Unable to open or configure PowerScripts.", exception);
        EditorError.Message = exception.Message;
        EditorError.IsOpen = true;
    }
}
