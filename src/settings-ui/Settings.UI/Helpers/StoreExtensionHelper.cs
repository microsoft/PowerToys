// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Windows.Management.Deployment;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Helper class to manage installation status and installation command for a Microsoft Store extension.
    /// </summary>
    public class StoreExtensionHelper : INotifyPropertyChanged
    {
        private readonly string _packageFamilyName;
        private readonly string _storeUri;
        private readonly string _extensionName;
        private bool? _isInstalled;

        public event PropertyChangedEventHandler PropertyChanged;

        public StoreExtensionHelper(string packageFamilyName, string storeUri, string extensionName)
        {
            _packageFamilyName = packageFamilyName ?? throw new ArgumentNullException(nameof(packageFamilyName));
            _storeUri = storeUri ?? throw new ArgumentNullException(nameof(storeUri));
            _extensionName = extensionName ?? throw new ArgumentNullException(nameof(extensionName));
            InstallCommand = new AsyncCommand(InstallExtensionAsync);
        }

        /// <summary>
        /// Gets a value indicating whether the extension is installed.
        /// </summary>
        public bool IsInstalled
        {
            get
            {
                if (!_isInstalled.HasValue)
                {
                    _isInstalled = CheckExtensionInstalled();
                }

                return _isInstalled.Value;
            }
        }

        /// <summary>
        /// Gets the command to install the extension.
        /// </summary>
        public ICommand InstallCommand { get; }

        /// <summary>
        /// Refreshes the installation status of the extension.
        /// </summary>
        public void RefreshStatus()
        {
            _isInstalled = null;
            OnPropertyChanged(nameof(IsInstalled));
        }

        private bool CheckExtensionInstalled()
        {
            try
            {
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(string.Empty, _packageFamilyName);
                return packages.Any();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check extension installation status: {_packageFamilyName}", ex);
                return false;
            }
        }

        private async Task InstallExtensionAsync()
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri(_storeUri));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open {_extensionName} extension store page", ex);
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
