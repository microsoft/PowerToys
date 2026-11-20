// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Management.Deployment;

namespace WorkspacesCsharpLibrary.Models
{
    public partial class BaseApplication : ObservableObject, IDisposable
    {
        public string PwaAppId { get; set; }

        public string AppPath { get; set; }

        public string AppUserModelId { get; set; }

        public string Version { get; set; }

        public string PackagedId { get; set; }

        public string PackagedName { get; set; }

        public string PackagedPublisherID { get; set; }

        public string Aumid { get; set; }

        [ObservableProperty]
        [JsonIgnore]
        public partial bool IsNotFound { get; set; }

        private Icon _icon;

        public Icon Icon => _icon ??= LoadIcon(PwaHelper.GetPwaIconFilename(PwaAppId));

        internal Icon LoadIcon(string pwaIconPath)
        {
            bool executableExists = File.Exists(AppPath);
            IsNotFound = !executableExists;
            try
            {
                string imagePath = null;
                if (!string.IsNullOrEmpty(AppPath) && IsPackagedApp)
                {
                    Uri logo = GetAppLogoByPackageFamilyName();
                    if (logo != null)
                    {
                        // The installed package can outlive the versioned executable path in a snapshot.
                        IsNotFound = false;
                        imagePath = logo.LocalPath;
                    }
                }
                else if (!string.IsNullOrEmpty(PwaAppId))
                {
                    IsNotFound = !PwaHelper.HasLaunchTarget(AppPath, AppUserModelId, Version);
                    imagePath = pwaIconPath;
                }

                if (File.Exists(imagePath))
                {
                    return LoadImageIcon(imagePath);
                }

                if (executableExists && Icon.ExtractAssociatedIcon(AppPath) is Icon icon)
                {
                    return icon;
                }
            }
            catch (Exception)
            {
                // An unreadable icon does not determine whether its application is installed.
            }

            return new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "Workspaces", "DefaultIcon.ico"));
        }

        private static Icon LoadImageIcon(string path)
        {
            if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                return new Icon(path);
            }

            using var bitmap = new Bitmap(path);
            var handle = bitmap.GetHicon();
            try
            {
                // FromHandle borrows the HICON. Give the cached Icon its own copy.
                using var borrowedIcon = Icon.FromHandle(handle);
                return (Icon)borrowedIcon.Clone();
            }
            finally
            {
                _ = DestroyIcon(handle);
            }
        }

        public bool IsEdge
        {
            get => AppPath.EndsWith("edge.exe", StringComparison.InvariantCultureIgnoreCase);
        }

        public bool IsChrome
        {
            get => AppPath.EndsWith("chrome.exe", StringComparison.InvariantCultureIgnoreCase);
        }

        public Uri GetAppLogoByPackageFamilyName()
        {
            var pkgManager = new PackageManager();
            var pkg = pkgManager.FindPackagesForUser(string.Empty, PackagedId).FirstOrDefault();

            if (pkg == null)
            {
                return null;
            }

            return pkg.Logo;
        }

        private bool? _isPackagedApp;

        public bool IsPackagedApp
        {
            get
            {
                if (_isPackagedApp == null)
                {
                    if (!AppPath.StartsWith("C:\\Program Files\\WindowsApps\\", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _isPackagedApp = false;
                    }
                    else
                    {
                        string appPath = AppPath.Replace("C:\\Program Files\\WindowsApps\\", string.Empty);
                        Regex packagedAppPathRegex = new Regex(@"(?<APPID>[^_]*)_\d+.\d+.\d+.\d+_(:?x64|arm64)__(?<PublisherID>[^\\]*)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        Match match = packagedAppPathRegex.Match(appPath);
                        _isPackagedApp = match.Success;
                        if (match.Success)
                        {
                            PackagedName = match.Groups["APPID"].Value;
                            PackagedPublisherID = match.Groups["PublisherID"].Value;
                            PackagedId = $"{PackagedName}_{PackagedPublisherID}";
                            Aumid = $"{PackagedId}!App";
                        }
                    }
                }

                return _isPackagedApp.Value;
            }
        }

        public void Dispose()
        {
            _icon?.Dispose();
            _icon = null;
            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
