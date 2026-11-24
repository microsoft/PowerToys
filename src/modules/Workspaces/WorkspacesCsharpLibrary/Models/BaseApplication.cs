// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Management.Deployment;

namespace WorkspacesCsharpLibrary.Models
{
    public partial class BaseApplication : ObservableObject, IDisposable
    {
        public string PwaAppId { get; set; }

        public string AppPath { get; set; }

        public string AppUserModelId { get; set; }

        public string PackageFullName { get; set; }

        public string Version { get; set; }

        public string PackagedId { get; set; }

        public string PackagedName { get; set; }

        public string PackagedPublisherID { get; set; }

        public string Aumid { get; set; }

        [ObservableProperty]
        [JsonIgnore]
        public partial bool IsNotFound { get; set; }

        private Icon _icon;

        public Icon Icon => _icon ??= LoadIcon(PwaHelper.GetPwaIconFilename(PwaAppId, AppPath, AppUserModelId));

        internal Icon LoadIcon(string pwaIconPath, Func<string, (bool Installed, Uri Logo)> findPackage = null)
        {
            (bool Installed, Uri Logo) package = default;
            if (IsPackagedApp)
            {
                package = (findPackage ?? FindPackageLogo)(PackagedId);
            }

            bool isPwa = !string.IsNullOrEmpty(PwaAppId);

            // Native package activation and versioned path repair both require a captured full name.
            // A family inferred from a legacy path or AUMID can still supply the icon.
            bool launchTargetExists = (package.Installed && !string.IsNullOrEmpty(PackageFullName)) || (isPwa
                ? PwaHelper.HasLaunchTarget(AppPath, AppUserModelId, Version)
                : File.Exists(AppPath));
            IsNotFound = !launchTargetExists;

            return TryReadIcon(isPwa ? pwaIconPath : null, LoadImageIcon) ??
                   TryReadIcon(package.Logo?.LocalPath, LoadImageIcon) ??
                   TryReadIcon(AppPath, Icon.ExtractAssociatedIcon) ??
                   new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "Workspaces", "DefaultIcon.ico"));
        }

        private static Icon TryReadIcon(string path, Func<string, Icon> readIcon)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return readIcon(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                ExternalException or OutOfMemoryException or NotSupportedException or InvalidOperationException)
            {
                // GDI+ can also report an invalid image as OutOfMemoryException.
                return null;
            }
        }

        private static Icon LoadImageIcon(string path)
        {
            if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                return new Icon(path);
            }

            using var bitmap = new Bitmap(path);
            return CopyAndReleaseIconHandle(bitmap.GetHicon());
        }

        internal static Icon CopyAndReleaseIconHandle(IntPtr handle)
        {
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

        public Uri GetAppLogoByPackageFamilyName() => !string.IsNullOrEmpty(PackagedId) || IsPackagedApp ? FindPackageLogo(PackagedId).Logo : null;

        private (bool Installed, Uri Logo) FindPackageLogo(string familyName)
        {
            Windows.ApplicationModel.Package package = null;
            try
            {
                var packages = new PackageManager().FindPackagesForUser(string.Empty, familyName);
                package = packages.Where(candidate => !candidate.IsResourcePackage && !candidate.IsFramework && !candidate.IsBundle)
                    .OrderByDescending(candidate => string.Equals(candidate.Id.FullName, PackageFullName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                return (package != null, package?.Logo);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or ExternalException or InvalidOperationException)
            {
                return (package != null, null);
            }
        }

        private bool? _isPackagedApp;

        public bool IsPackagedApp
        {
            get
            {
                if (_isPackagedApp == null)
                {
                    var family = FamilyFromFullName(PackageFullName);
                    int appSeparator = AppUserModelId?.IndexOf('!') ?? -1;
                    if (family == null && appSeparator > 0 && AppUserModelId.AsSpan(0, appSeparator).Contains('_'))
                    {
                        family = AppUserModelId[..appSeparator];
                    }

                    // Snapshots without package identity can still identify the package folder on any volume.
                    if (family == null && !string.IsNullOrEmpty(AppPath))
                    {
                        const string WindowsApps = @"\WindowsApps\";
                        string path = AppPath.Replace('/', '\\');
                        int marker = path.IndexOf(WindowsApps, StringComparison.OrdinalIgnoreCase);
                        if (marker >= 0)
                        {
                            int start = marker + WindowsApps.Length;
                            int end = path.IndexOf('\\', start);
                            if (end > start)
                            {
                                family = FamilyFromFullName(path[start..end]);
                            }
                        }
                    }

                    family ??= PackagedId;
                    int publisher = family?.LastIndexOf('_') ?? -1;
                    _isPackagedApp = publisher > 0 && publisher < family.Length - 1;
                    if (_isPackagedApp.Value)
                    {
                        PackagedId = family;
                        PackagedName = family[..publisher];
                        PackagedPublisherID = family[(publisher + 1)..];
                        Aumid = string.IsNullOrEmpty(AppUserModelId) ? family + "!App" : AppUserModelId;
                    }
                }

                return _isPackagedApp.Value;
            }
        }

        private static string FamilyFromFullName(string fullName)
        {
            const int ErrorInsufficientBuffer = 122;
            uint length = 0;
            if (string.IsNullOrEmpty(fullName) || PackageFamilyNameFromFullName(fullName, ref length, null) != ErrorInsufficientBuffer)
            {
                return null;
            }

            var buffer = new char[length];
            return PackageFamilyNameFromFullName(fullName, ref length, buffer) == 0 ? new string(buffer, 0, (int)length - 1) : null;
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int PackageFamilyNameFromFullName(string fullName, ref uint length, [Out] char[] familyName);
    }
}
