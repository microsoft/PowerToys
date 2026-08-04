// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Linq;
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

        public string PackagedId { get; set; }

        public string PackagedName { get; set; }

        public string PackagedPublisherID { get; set; }

        public string Aumid { get; set; }

        [ObservableProperty]
        [property: JsonIgnore]
        private bool _isNotFound;

        private Icon _icon;

        public Icon Icon
        {
            get
            {
                if (_icon == null)
                {
                    try
                    {
                        if (IsPackagedApp)
                        {
                            Uri uri = GetAppLogoByPackageFamilyName();
                            var bitmap = new Bitmap(uri.LocalPath);
                            var iconHandle = bitmap.GetHicon();
                            _icon = Icon.FromHandle(iconHandle);
                        }
                        else if (IsEdge || IsChrome)
                        {
                            string iconFilename = PwaHelper.GetPwaIconFilename(PwaAppId);
                            if (!string.IsNullOrEmpty(iconFilename))
                            {
                                Bitmap bitmap;
                                if (iconFilename.EndsWith("ico", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    bitmap = new Bitmap(iconFilename);
                                }
                                else
                                {
                                    bitmap = (Bitmap)Image.FromFile(iconFilename);
                                }

                                var iconHandle = bitmap.GetHicon();
                                _icon = Icon.FromHandle(iconHandle);
                            }
                        }

                        if (_icon == null)
                        {
                            _icon = Icon.ExtractAssociatedIcon(AppPath);
                        }
                    }
                    catch (Exception)
                    {
                        IsNotFound = true;
                        _icon = new Icon(@"Assets\Workspaces\DefaultIcon.ico");
                    }
                }

                return _icon;
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
            GC.SuppressFinalize(this);
        }
    }
}
