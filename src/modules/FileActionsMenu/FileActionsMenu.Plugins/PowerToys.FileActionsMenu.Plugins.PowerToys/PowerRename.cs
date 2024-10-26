// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.Common.Models;

namespace PowerToys.FileActionsMenu.Plugins.PowerToys
{
    internal sealed class PowerRename : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("PowerToys.PowerRename.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 3;

        public IconElement? Icon => IconHelper.GetIconFromModuleName("PowerRename");

        public bool IsVisible => GPOWrapperProjection.GPOWrapper.GetConfiguredPowerRenameEnabledValue() != GPOWrapperProjection.GpoRuleConfigured.Disabled && SettingsRepository<GeneralSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Enabled.PowerRename;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuPowerRenameActionInvokedEvent(), SelectedItems);
            _ = RunPowerRename(CreateShellItemArrayFromPaths(SelectedItems));
            return Task.CompletedTask;
        }

        [DllImport("\\..\\..\\WinUI3Apps\\PowerToys.PowerRenameContextMenu.dll", CharSet = CharSet.Unicode)]
        public static extern int RunPowerRename(IShellItemArray psiItemArray);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateShellItemArrayFromIDLists(uint cidl, IntPtr[] rgpidl, out IShellItemArray ppsia);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        public static IShellItemArray CreateShellItemArrayFromPaths(string[] paths)
        {
            IntPtr[] pidls = new IntPtr[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                uint psfgaoOut;
                SHParseDisplayName(paths[i], IntPtr.Zero, out pidls[i], 0, out psfgaoOut);
            }

            IShellItemArray sia;
            SHCreateShellItemArrayFromIDLists((uint)paths.Length, pidls, out sia);
            return sia;
        }
    }
}
